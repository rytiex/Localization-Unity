using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor.Compilation;
using PicoShot.Localization.Data;
using PicoShot.Localization.Rtl;

namespace PicoShot.Localization
{
    public class LocalizationEditor : EditorWindow
    {
        #region Variables

        private bool _hasUnsavedChanges;
        private bool _showStatusSection = true;
        private bool _showTestingTools = true;
        private bool _showParameterList;
        private bool _showExistingComponents = true;
        private bool _showArrayKeysOnly;
        private bool _showStringKeysOnly;
        private bool _sortKeysByName;
        private bool _pendingDelete;
        private const float _keyItemHeight = 22f;
        private const string DeeplApiUrl = "https://api-free.deepl.com/v2/translate";
        private const string DeeplApiKey = "72c86981-8033-4c9e-90fb-16ab84ba0ee3:fx";
        private string _keySearchFilter = "";
        private string _newKey = "";
        private string _languageFilter = "";
        private string _testKey = "";
        private string _testRtl = "";
        private string _testKeyWithParams = "";
        private string _testResult = "";
        private string _selectedKey;
        private string _componentSearchFilter = "";
        private readonly List<string> _languageCodes = new() { "en" };
        private readonly List<string> _parameterList = new();
        private List<string> _keys = new();
        private readonly Dictionary<string, bool> _keyFoldouts = new();
        private readonly Dictionary<string, bool> _languageSelectionForCharset = new();
        private readonly Dictionary<string, string> _generatedCharsets = new();
        private Dictionary<string, Dictionary<string, object>> _languageData = new();
        private Vector2 _languageScrollPos = Vector2.zero;
        private Vector2 _componentsScrollPos = Vector2.zero;
        private Vector2 _existingComponentsScrollPos = Vector2.zero;
        private Vector2 _mainScrollPosition = Vector2.zero;
        private Vector2 _componentsScrollPosition = Vector2.zero;
        private Vector2 _toolsScrollPosition = Vector2.zero;
        private Vector2 _charsetLanguageScrollPos = Vector2.zero;
        private Vector2 _keysListScroll = Vector2.zero;
        private Vector2 _keyDetailsScroll = Vector2.zero;
        private GameObject _selectedGameObject;
        private Color _dragAreaColor;
        private readonly HttpClient _httpClient = new();

        private enum Tab
        {
            Languages,
            Keys,
            Config,
            Components,
            Debug,
            Tools
        }

        private Tab _currentTab = Tab.Languages;

        #endregion

        #region Unity Functions

        [MenuItem("Tools/Language Editor")]
        public static void OpenWindow()
        {
            GetWindow<LocalizationEditor>("Language Editor");
        }

        private void OnEnable()
        {
            // Ensure data structures are initialized (readonly fields already have default values)
            _languageData ??= new Dictionary<string, Dictionary<string, object>>();
            _keys ??= new List<string>();
            
            LoadLanguages();
            CompilationPipeline.compilationStarted += OnBeforeCompile;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            UnregisterEventHandlers();
            CompilationPipeline.compilationStarted -= OnBeforeCompile;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnBeforeCompile(object _)
        {
            PromptAutoSave("before compiling");
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                PromptAutoSave("before entering Play Mode");
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                PromptAutoSave("before exiting Play Mode");
            }
        }

        private void OnDestroy()
        {
            PromptAutoSave("before closing the editor");
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawTabs();

            EditorGUILayout.Space();

            switch (_currentTab)
            {
                case Tab.Languages:
                    DrawLanguagesTab();
                    break;
                case Tab.Keys:
                    DrawKeysTab();
                    break;
                case Tab.Config:
                    DrawConfigTab();
                    break;
                case Tab.Components:
                    DrawComponentsTab();
                    break;
                case Tab.Debug:
                    DrawDebugTab();
                    break;
                case Tab.Tools:
                    DrawToolsTab();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            EditorGUILayout.Space();
            DrawSaveButton();

            HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            if (Event.current.type != EventType.KeyDown || string.IsNullOrEmpty(_selectedKey)) return;
            
            bool ctrlPressed = (Event.current.modifiers & EventModifiers.Control) != 0;
            var index = _keys.IndexOf(_selectedKey);
            
            switch (Event.current.keyCode)
            {
                case KeyCode.UpArrow:
                    if (ctrlPressed && index > 0)
                    {
                        _keys.RemoveAt(index);
                        _keys.Insert(index - 1, _selectedKey);
                        _hasUnsavedChanges = true;
                    }
                    else if (index > 0)
                    {
                        _selectedKey = _keys[index - 1];
                    }
                    Event.current.Use();
                    Repaint();
                    break;

                case KeyCode.DownArrow:
                    if (ctrlPressed && index < _keys.Count - 1)
                    {
                        _keys.RemoveAt(index);
                        _keys.Insert(index + 1, _selectedKey);
                        _hasUnsavedChanges = true;
                    }
                    else if (index < _keys.Count - 1)
                    {
                        _selectedKey = _keys[index + 1];
                    }
                    Event.current.Use();
                    Repaint();
                    break;

                case KeyCode.Backspace:
                case KeyCode.Delete:
                    if (_pendingDelete) return;
                    _pendingDelete = true;
                    EditorApplication.delayCall += () =>
                    {
                        if (EditorUtility.DisplayDialog("Delete Key",
                                $"Are you sure you want to delete the key '{_selectedKey}'?", "Yes", "No"))
                        {
                            DeleteKey(_selectedKey);
                        }
                        _pendingDelete = false;
                    };
                    Event.current.Use();
                    Repaint();
                    break;

                case KeyCode.T:
                    if (ctrlPressed)
                    {
                        _ = TranslateAndFill(_selectedKey);
                        Event.current.Use();
                    }
                    break;

                case KeyCode.R:
                    if (ctrlPressed)
                    {
                        RenameKey(_selectedKey);
                        Event.current.Use();
                    }
                    break;

                case KeyCode.S:
                    if (ctrlPressed)
                    {
                        SaveLanguages();
                        Event.current.Use();
                    }
                    break;

                case KeyCode.Escape:
                    _selectedKey = null;
                    Event.current.Use();
                    Repaint();
                    break;
            }
        }

        #endregion

        #region Draw Functions

        private static void DrawHeader()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Language Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space();
        }

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = _currentTab == Tab.Languages ? Color.gray : Color.white;
            if (GUILayout.Button("Languages", EditorStyles.toolbarButton))
                _currentTab = Tab.Languages;

            GUI.backgroundColor = _currentTab == Tab.Keys ? Color.gray : Color.white;
            if (GUILayout.Button("Keys", EditorStyles.toolbarButton))
                _currentTab = Tab.Keys;

            GUI.backgroundColor = _currentTab == Tab.Components ? Color.gray : Color.white;
            if (GUILayout.Button("Components", EditorStyles.toolbarButton))
                _currentTab = Tab.Components;

            GUI.backgroundColor = _currentTab == Tab.Tools ? Color.gray : Color.white;
            if (GUILayout.Button("Tools", EditorStyles.toolbarButton))
                _currentTab = Tab.Tools;

            GUI.backgroundColor = _currentTab == Tab.Debug ? Color.gray : Color.white;
            if (GUILayout.Button("Debug", EditorStyles.toolbarButton))
                _currentTab = Tab.Debug;

            GUI.backgroundColor = _currentTab == Tab.Config ? Color.gray : Color.white;
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton))
                _currentTab = Tab.Config;

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLanguagesTab()
        {
            EditorGUILayout.LabelField("Manage Languages", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            _languageFilter = EditorGUILayout.TextField(_languageFilter);
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                _languageFilter = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Available Languages:", EditorStyles.boldLabel);

            _languageScrollPos = EditorGUILayout.BeginScrollView(_languageScrollPos);
            var filteredLanguages = LanguageDefinitions.LanguageNames
                .Where(lang => string.IsNullOrEmpty(_languageFilter) ||
                               lang.Value.ToLower().Contains(_languageFilter.ToLower()) ||
                               lang.Key.ToLower().Contains(_languageFilter.ToLower()))
                .OrderBy(lang => lang.Value);

            foreach (var lang in filteredLanguages)
            {
                EditorGUILayout.BeginHorizontal("box");

                bool isEnglish = lang.Key == "en";
                bool isSelected = _languageCodes.Contains(lang.Key);
                
                if (isEnglish)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    _ = EditorGUILayout.Toggle(true, GUILayout.Width(20));
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    bool newSelection = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                    if (newSelection != isSelected)
                    {
                        if (newSelection)
                            AddLanguage(lang.Key);
                        else
                            DeleteLanguage(lang.Key);
                    }
                }

                EditorGUILayout.LabelField(
                    isEnglish ? $"{lang.Value} ({lang.Key}) - Default" : $"{lang.Value} ({lang.Key})",
                    EditorStyles.boldLabel);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField($"Selected Languages: {_languageCodes.Count}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawKeysTab()
        {
            EditorGUILayout.BeginVertical("box");
            DrawAddKeySection();
            DrawSearchAndFilterSection();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            DrawKeysListPanel();
            DrawKeyDetailsPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawKeyDetailsPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            
            if (!string.IsNullOrEmpty(_selectedKey))
            {
                EditorGUILayout.LabelField($"Key Details: {_selectedKey}", EditorStyles.boldLabel);
                _keyDetailsScroll = EditorGUILayout.BeginScrollView(_keyDetailsScroll, GUILayout.ExpandHeight(true));

                if (_languageData.TryGetValue(_selectedKey, out var text))
                {
                    if (IsArrayKey(text))
                        DrawArrayKeyContent(_selectedKey);
                    else
                        DrawStringKeyContent(_selectedKey);

                    EditorGUILayout.Space();
                    DrawKeyActionButtons();
                }
                else
                {
                    EditorGUILayout.LabelField("Selected key not found.");
                }

                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("Select a key from the list to view and edit its details.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawKeyActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Rename", GUILayout.Width(80)))
                RenameKey(_selectedKey);

            if (GUILayout.Button("Translation Options", GUILayout.Width(120)))
                ShowTranslationOptionsMenu();

            if (GUILayout.Button("Clear", GUILayout.Width(80)))
                ClearKeyData(_selectedKey);

            if (GUILayout.Button("Delete", GUILayout.Width(80)))
                ConfirmDeleteKey(_selectedKey);

            EditorGUILayout.EndHorizontal();
        }

        private void ShowTranslationOptionsMenu()
        {
            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("Translate with DeepL"), false,
                () => { _ = TranslateAndFill(_selectedKey); });

            menu.AddItem(new GUIContent("Copy AI Translate Prompt"), false, CopyAiTranslatePrompt);

            menu.ShowAsContext();
        }

        private void CopyAiTranslatePrompt()
        {
            if (string.IsNullOrEmpty(_selectedKey) || !_languageData.TryGetValue(_selectedKey, out var keyData))
            {
                ShowNotification(new GUIContent("No key selected or data not found."));
                return;
            }

            // Build simple text representation for AI prompt
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Please translate the following language key data from English to all other languages.");
            sb.AppendLine("Only fill in the fields that are empty (leave existing translations unchanged).");
            sb.AppendLine("Return data in format: LanguageCode: Translation");
            sb.AppendLine();
            sb.AppendLine($"Key: {_selectedKey}");
            sb.AppendLine();
            
            foreach (var kvp in keyData)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
            
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            ShowNotification(new GUIContent("Translation prompt copied to clipboard!"));
        }


        private void DrawKeysListPanel()
        {
            float listPanelWidth = 180f;

            EditorGUILayout.BeginVertical("box", GUILayout.Width(listPanelWidth));
            EditorGUILayout.LabelField("Keys", EditorStyles.boldLabel, GUILayout.Width(listPanelWidth));

            var filteredKeys = FilterKeys().ToList();
            int totalKeyCount = filteredKeys.Count;
            


            float viewportHeight = position.height - 300f;
            int maxVisibleItems = Mathf.CeilToInt(viewportHeight / _keyItemHeight) + 1;

            Rect scrollViewRect = EditorGUILayout.GetControlRect(
                false,
                viewportHeight,
                GUILayout.Width(listPanelWidth),
                GUILayout.ExpandHeight(false)
            );

            float totalContentHeight = totalKeyCount * _keyItemHeight;
            _keysListScroll = GUI.BeginScrollView(
                scrollViewRect,
                _keysListScroll,
                new Rect(0, 0, listPanelWidth - 20, totalContentHeight)
            );

            if (totalKeyCount > 0)
            {
                int startIndex = Mathf.FloorToInt(_keysListScroll.y / _keyItemHeight);
                startIndex = Mathf.Max(0, startIndex);
                int endIndex = Mathf.Min(startIndex + maxVisibleItems, totalKeyCount);

                for (int i = startIndex; i < endIndex; i++)
                {
                    DrawKeyListItem(filteredKeys[i], i, listPanelWidth);
                }
            }

            GUI.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawKeyListItem(string key, int index, float width)
        {
            Rect keyRect = new Rect(4, index * _keyItemHeight, width - 8, _keyItemHeight);

            GUIStyle keyStyle = GetKeyButtonStyle(key == _selectedKey);
            
            // Check if key has any data and determine type
            string typeIndicator = "Aa"; // Default to string
            if (_languageData.TryGetValue(key, out var keyData) && keyData.Count > 0)
            {
                // Get first value to determine type
                var firstValue = keyData.Values.FirstOrDefault();
                if (firstValue is List<string> || firstValue is string[])
                    typeIndicator = "[ ]";
            }
            
            string buttonLabel = $"<color=#888888>{typeIndicator}</color> {key}";

            if (GUI.Button(keyRect, buttonLabel, keyStyle))
            {
                _selectedKey = key;
            }
        }

        private GUIStyle GetKeyButtonStyle(bool isSelected)
        {
            Texture2D transparentTexture = new Texture2D(1, 1);
            transparentTexture.SetPixel(0, 0, new Color(0, 0, 0, 0));
            transparentTexture.Apply();

            GUIStyle style = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 2, 2),
                margin = new RectOffset(0, 0, 2, 2),
                normal =
                {
                    textColor = isSelected ? Color.green : EditorStyles.label.normal.textColor,
                    background = transparentTexture
                },
                hover = { textColor = Color.green },
                active = { textColor = Color.green },
                richText = true
            };

            return style;
        }

        private void DrawComponentsTab()
        {
            _componentsScrollPosition = EditorGUILayout.BeginScrollView(_componentsScrollPosition);

            EditorGUILayout.LabelField("Component Manager", EditorStyles.boldLabel);
            DrawDragAndDropArea();

            EditorGUILayout.Space();

            if (_selectedGameObject != null)
            {
                DrawComponentsList();
            }

            EditorGUILayout.Space();
            DrawExistingComponentsSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawDebugTab()
        {
            _mainScrollPosition = EditorGUILayout.BeginScrollView(_mainScrollPosition);

            EditorGUILayout.LabelField("Language Manager Debug", EditorStyles.largeLabel);
            EditorGUILayout.Space(5);

            DrawLanguageSwitcher();
            DrawSystemStatus();
            DrawTestingTools();

            EditorGUILayout.EndScrollView();
        }

        private void DrawLanguageSwitcher()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Language Selection", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Language:", GUILayout.Width(120));

            var currentLang = LocalizationManager.CurrentLanguage;
            var content = new GUIContent(LanguageDefinitions.GetDisplayName(currentLang));
            var dropdownRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));

            if (EditorGUI.DropdownButton(dropdownRect, content, FocusType.Keyboard))
            {
                ShowLanguageDropdown(dropdownRect, currentLang);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("System Language:", GUILayout.Width(120));
            EditorGUILayout.LabelField(LanguageDefinitions.GetDisplayName(LanguageDefinitions.FromSystemLanguage(Application.systemLanguage)));
            
            if (GUILayout.Button("Use System Language", GUILayout.Width(150)))
            {
                LocalizationManager.SetLanguage(LocalizationManager.DetectSystemLanguage());
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ShowLanguageDropdown(Rect dropdownRect, string currentLang)
        {
            var menu = new GenericMenu();
            foreach (var lang in LocalizationManager.GetAvailableLanguages(false))
            {
                menu.AddItem(
                    new GUIContent(LanguageDefinitions.GetDisplayName(lang)),
                    currentLang == lang,
                    () =>
                    {
                        LocalizationManager.SetLanguage(lang);
                        GUI.FocusControl(null);
                        Repaint();
                    }
                );
            }
            menu.DropDown(dropdownRect);
        }

        private void DrawSystemStatus()
        {
            _showStatusSection = EditorGUILayout.Foldout(_showStatusSection, "System Status", true);
            if (!_showStatusSection) return;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
            DrawStatusField("Initialized", LocalizationManager.IsInitialized.ToString(), MessageType.Info);
            DrawStatusField("Current Language", LocalizationManager.CurrentLanguage, MessageType.Info);
            DrawStatusField("Default Language", LanguageDefinitions.DefaultLanguage, MessageType.Info);
            DrawStatusField("Is RTL Language", LocalizationManager.IsRightToLeft.ToString(), MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            var availableCount = LocalizationManager.GetAvailableLanguages().Count();
            DrawStatusField("Available Languages", availableCount.ToString(), MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Copy Debug Info to Clipboard"))
            {
                EditorGUIUtility.systemCopyBuffer = LocalizationManager.GetDebugInfo();
                ShowNotification(new GUIContent("Debug info copied to clipboard!"));
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTestingTools()
        {
            _showTestingTools = EditorGUILayout.Foldout(_showTestingTools, "Testing Tools", true);
            if (!_showTestingTools) return;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawTestSection("Simple Text Lookup", () =>
            {
                EditorGUILayout.BeginHorizontal();
                _testKey = EditorGUILayout.TextField(new GUIContent("Key", "Enter the language key to test"), _testKey);
                GUI.enabled = !string.IsNullOrEmpty(_testKey);
                if (GUILayout.Button("Test", GUILayout.Width(60)))
                {
                    _testResult = LocalizationManager.GetText(_testKey);
                    GUI.FocusControl(null);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            });

            EditorGUILayout.Space(5);

            DrawTestSection("RTL Test", () =>
            {
                EditorGUILayout.BeginHorizontal();
                _testRtl = EditorGUILayout.TextField(new GUIContent("Text", "Enter Arabic text to test RTL"), _testRtl);
                GUI.enabled = !string.IsNullOrEmpty(_testRtl);
                if (GUILayout.Button("Test", GUILayout.Width(60)))
                {
                    _testResult = RtlTextHandler.Fix(_testRtl);
                    GUI.FocusControl(null);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            });

            EditorGUILayout.Space(5);

            DrawTestSection("Parameterized Text", () =>
            {
                _testKeyWithParams = EditorGUILayout.TextField(
                    new GUIContent("Key", "Enter the language key with parameters"),
                    _testKeyWithParams);

                DrawParameterList();

                GUI.enabled = !string.IsNullOrEmpty(_testKeyWithParams);
                if (GUILayout.Button("Test With Parameters", GUILayout.Height(24)))
                {
                    _testResult = LocalizationManager.GetText(_testKeyWithParams, _parameterList.ToArray());
                    GUI.FocusControl(null);
                }
                GUI.enabled = true;
            });

            if (!string.IsNullOrEmpty(_testResult))
            {
                DrawTestResult();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawParameterList()
        {
            _showParameterList = EditorGUILayout.Foldout(_showParameterList, "Parameters", true);
            if (_showParameterList)
            {
                EditorGUI.indentLevel++;
                for (var i = 0; i < _parameterList.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    _parameterList[i] = EditorGUILayout.TextField($"Param {i}", _parameterList[i]);
                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        _parameterList.RemoveAt(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;

                if (GUILayout.Button("Add Parameter"))
                {
                    _parameterList.Add("");
                }
            }
        }

        private void DrawTestResult()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Result:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.SelectableLabel(_testResult, EditorStyles.wordWrappedLabel, GUILayout.Height(40));

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy", GUILayout.Width(60)))
            {
                EditorGUIUtility.systemCopyBuffer = _testResult;
                ShowNotification(new GUIContent("Copied to clipboard!"));
            }

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                _testResult = "";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private static void DrawStatusField(string label, string value, MessageType type = MessageType.None)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label + ":", GUILayout.Width(120));

            GUI.color = type switch
            {
                MessageType.Info => Color.cyan,
                MessageType.Warning => Color.yellow,
                MessageType.Error => Color.red,
                _ => GUI.color
            };

            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawTestSection(string sectionTitle, Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(sectionTitle, EditorStyles.boldLabel);
            content?.Invoke();
            EditorGUILayout.EndVertical();
        }

        private void DrawDragAndDropArea()
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Active GameObject:", EditorStyles.boldLabel, GUILayout.Width(120));
            var newSelection = (GameObject)EditorGUILayout.ObjectField(
                _selectedGameObject, typeof(GameObject), true);

            if (newSelection != _selectedGameObject)
            {
                _selectedGameObject = newSelection;
                GUI.FocusControl(null);
            }

            if (_selectedGameObject != null && GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                _selectedGameObject = null;
            }

            EditorGUILayout.EndHorizontal();

            const float dragAreaHeight = 60f;
            var rect = GUILayoutUtility.GetRect(0, dragAreaHeight, GUILayout.ExpandWidth(true));
            var dragAreaStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };

            var evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (rect.Contains(evt.mousePosition))
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        _dragAreaColor = new Color(0.1f, 1f, 0.1f);

                        if (evt.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();
                            foreach (var draggedObject in DragAndDrop.objectReferences)
                            {
                                if (draggedObject is GameObject go)
                                {
                                    _selectedGameObject = go;
                                    break;
                                }
                            }
                        }
                    }
                    evt.Use();
                    break;

                case EventType.DragExited:
                    _dragAreaColor = Color.white;
                    break;
            }

            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _dragAreaColor;
            GUI.Box(rect,
                _selectedGameObject == null
                    ? "Drag & Drop GameObjects Here\nor use the Object Field above"
                    : $"Currently Managing: {_selectedGameObject.name}",
                dragAreaStyle);
            GUI.backgroundColor = prevColor;

            EditorGUILayout.EndVertical();
        }

        private void DrawComponentsList()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Available Components", EditorStyles.boldLabel);

            var textComponents = _selectedGameObject.GetComponentsInChildren<TMP_Text>(true);
            var dropdowns = _selectedGameObject.GetComponentsInChildren<TMP_Dropdown>(true);

            if (textComponents.Length == 0 && dropdowns.Length == 0)
            {
                EditorGUILayout.HelpBox("No TextMeshPro components found in the selected GameObject hierarchy.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _componentsScrollPos = EditorGUILayout.BeginScrollView(_componentsScrollPos, GUILayout.Height(300));

            if (textComponents.Length > 0)
            {
                EditorGUILayout.LabelField("Text Components", EditorStyles.boldLabel);
                foreach (var text in textComponents)
                {
                    DrawComponentSection(text.gameObject, text);
                }
            }

            if (dropdowns.Length > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Dropdown Components", EditorStyles.boldLabel);
                foreach (var dropdown in dropdowns)
                {
                    DrawComponentSection(dropdown.gameObject, dropdown);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawExistingComponentsSection()
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            _showExistingComponents = EditorGUILayout.Foldout(_showExistingComponents, "Existing Language Components", true);

            if (_showExistingComponents)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
                _componentSearchFilter = EditorGUILayout.TextField(_componentSearchFilter, GUILayout.Width(140));
                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    _componentSearchFilter = "";
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (_showExistingComponents)
            {
                ShowExistingComponentsList();
            }

            EditorGUILayout.EndVertical();
        }

        private void ShowExistingComponentsList()
        {
            var allComponents = FindObjectsByType<LocalizationTextComponent>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (allComponents.Length == 0)
            {
                EditorGUILayout.HelpBox("No language components found in the scene.", MessageType.Info);
                return;
            }

            var filteredComponents = allComponents
                .Where(c => string.IsNullOrEmpty(_componentSearchFilter) ||
                            c.gameObject.name.ToLower().Contains(_componentSearchFilter.ToLower()) ||
                            c.TranslationKey.ToLower().Contains(_componentSearchFilter.ToLower()))
                .OrderBy(c => c.gameObject.name)
                .ToList();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Found {filteredComponents.Count} components", EditorStyles.miniBoldLabel);

            _existingComponentsScrollPos = EditorGUILayout.BeginScrollView(_existingComponentsScrollPos, GUILayout.Height(500));

            foreach (var component in filteredComponents)
            {
                DrawExistingComponentEntry(component);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawExistingComponentEntry(LocalizationTextComponent component)
        {
            if (component == null) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("☉", GUILayout.Width(23)))
                EditorGUIUtility.PingObject(component.gameObject);

            string parentName = component.gameObject.transform.parent != null
                ? component.gameObject.transform.parent.name
                : "No Parent";
            EditorGUILayout.LabelField($"{parentName}: {component.gameObject.name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Key: {component.TranslationKey}", EditorStyles.miniLabel, GUILayout.Width(200));

            var componentType = component.GetComponent<TMP_Text>() != null ? "Text" : "Dropdown";
            EditorGUILayout.LabelField(componentType, EditorStyles.miniLabel, GUILayout.Width(60));

            if (GUILayout.Button("Edit", GUILayout.Width(60)))
            {
                _selectedGameObject = component.gameObject;
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("✕", GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("Remove Language Component",
                        $"Are you sure you want to remove the language component from {component.gameObject.name}?",
                        "Yes", "No"))
                {
                    Undo.DestroyObjectImmediate(component);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawComponentSection(GameObject go, Component component)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("☉", GUILayout.Width(23)))
                EditorGUIUtility.PingObject(component.gameObject);
            
            string parentName = go.transform.parent?.name ?? "Root";
            EditorGUILayout.LabelField($"{parentName}: {go.name}", EditorStyles.boldLabel);
            var langComponent = go.GetComponent<LocalizationTextComponent>();

            if (langComponent == null)
            {
                if (GUILayout.Button("Add Language Support", GUILayout.Width(150)))
                {
                    langComponent = Undo.AddComponent<LocalizationTextComponent>(go);
                }
            }
            else
            {
                if (GUILayout.Button("Remove Language Support", GUILayout.Width(180)))
                {
                    Undo.DestroyObjectImmediate(langComponent);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (langComponent != null)
            {
                DrawComponentKeySelector(langComponent);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawComponentKeySelector(LocalizationTextComponent langComponent)
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Language Key:", GUILayout.Width(100));

            var buttonRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));
            var selectedIndex = _keys.IndexOf(langComponent.TranslationKey);
            var displayText = selectedIndex >= 0 ? _keys[selectedIndex] : "Select a key...";

            var style = new GUIStyle(EditorStyles.popup)
            {
                richText = true,
                alignment = TextAnchor.MiddleLeft
            };

            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(displayText), FocusType.Keyboard, style))
            {
                LocalizationSearchablePopup.Show(buttonRect, _keys.ToArray(), selectedIndex, (index) =>
                {
                    if (index == selectedIndex || index < 0) return;
                    Undo.RecordObject(langComponent, "Change Language Key");
                    langComponent.TranslationKey = _keys[index];
                    EditorUtility.SetDirty(langComponent);
                });
            }

            EditorGUILayout.EndHorizontal();

            DrawArrayControlsIfNeeded(langComponent);
        }

        private void DrawArrayControlsIfNeeded(LocalizationTextComponent langComponent)
        {
            if (langComponent.TranslationKey == null) return;
            if (!_languageData.TryGetValue(langComponent.TranslationKey, out var keyData)) return;
            if (!IsArrayKey(keyData)) return;
            
            // Get the array from the first available language
            var firstValue = GetFirstValue(keyData);
            if (firstValue is not List<string> array) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Array Index:", GUILayout.Width(100));

            var newArrayIndex = EditorGUILayout.IntSlider(langComponent.ArrayIndex, -1, array.Count - 1);

            EditorGUILayout.LabelField("Array Size Limit:", GUILayout.Width(100));
            var newArraySizeLimit = EditorGUILayout.IntSlider(langComponent.ArraySizeLimit, 0, array.Count);
            langComponent.ArraySizeLimit = newArraySizeLimit;

            if (newArrayIndex != langComponent.ArrayIndex)
            {
                Undo.RecordObject(langComponent, "Change Array Index");
                langComponent.ArrayIndex = newArrayIndex;
                EditorUtility.SetDirty(langComponent);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAddKeySection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Add New Key", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Key Name:", GUILayout.Width(70));
            _newKey = EditorGUILayout.TextField(_newKey);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add String Key", GUILayout.Width(120), GUILayout.Height(25)))
                AddKey(_newKey, false);
            if (GUILayout.Button("Add Array Key", GUILayout.Width(120), GUILayout.Height(25)))
                AddKey(_newKey, true);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSearchAndFilterSection()
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search Keys:", GUILayout.Width(80));
            _keySearchFilter = EditorGUILayout.TextField(_keySearchFilter);
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                _keySearchFilter = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(80));
            var newShowArrayKeys = EditorGUILayout.ToggleLeft("Array Keys", _showArrayKeysOnly, GUILayout.Width(100));
            var newShowStringKeys = EditorGUILayout.ToggleLeft("String Keys", _showStringKeysOnly, GUILayout.Width(100));

            _sortKeysByName = EditorGUILayout.ToggleLeft("Sort by Name", _sortKeysByName, GUILayout.Width(100));

            if (newShowArrayKeys != _showArrayKeysOnly)
            {
                _showArrayKeysOnly = newShowArrayKeys;
                _showStringKeysOnly = false;
            }

            if (newShowStringKeys != _showStringKeysOnly)
            {
                _showStringKeysOnly = newShowStringKeys;
                _showArrayKeysOnly = false;
            }

            EditorGUILayout.EndHorizontal();

            var totalKeys = _keys.Count;
            var filteredCount = FilterKeys().Count();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Found:", GUILayout.Width(80));
            EditorGUILayout.LabelField($"{filteredCount} / {totalKeys} keys", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawStringKeyContent(string key)
        {
            EditorGUILayout.BeginVertical("box");
            foreach (var lang in _languageCodes)
            {
                EditorGUILayout.BeginVertical();
                var langName = LanguageDefinitions.GetDisplayName(lang);
                EditorGUILayout.LabelField($"{langName}:", GUILayout.Width(120));

                var currentText = _languageData[key][lang]?.ToString() ?? "";
                Rect textRect = EditorGUILayout.GetControlRect();

                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.TextField(textRect, currentText);
                EditorGUI.EndDisabledGroup();

                if (Event.current.type == EventType.MouseDown &&
                    Event.current.clickCount == 2 &&
                    textRect.Contains(Event.current.mousePosition))
                {
                    OpenTextEditor(currentText, newText =>
                    {
                        _languageData[key][lang] = newText;
                        _hasUnsavedChanges = true;
                        Repaint();
                    });
                    Event.current.Use();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawArrayKeyContent(string key)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add New Element", GUILayout.Width(120)))
            {
                AddArrayElement(key);
            }

            if (GUILayout.Button("Clear Empty Elements", GUILayout.Width(140)))
            {
                ClearEmptyArrayElements(key);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            var firstValue = GetFirstValue(_languageData[key]);
            var array = firstValue as List<string> ?? new List<string>();
            DrawArrayElements(key, array);

            EditorGUILayout.EndVertical();
        }

        private void AddArrayElement(string key)
        {
            foreach (var lang in _languageCodes)
            {
                if (_languageData[key][lang] is List<string> langArray)
                {
                    langArray.Add("");
                }
            }
            _hasUnsavedChanges = true;
        }

        private void DrawArrayElements(string key, List<string> array)
        {
            bool elementDeleted = false;
            int deleteIndex = -1;

            for (var i = 0; i < array.Count; i++)
            {
                if (elementDeleted) break;

                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Element {i}", EditorStyles.boldLabel);

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("×", GUILayout.Width(25)) && EditorUtility.DisplayDialog("Delete Element",
                        $"Are you sure you want to delete element {i}?", "Yes", "No"))
                {
                    deleteIndex = i;
                    elementDeleted = true;
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                DrawArrayElementTranslations(key, i);

                EditorGUILayout.EndVertical();
            }

            if (elementDeleted && deleteIndex >= 0)
            {
                DeleteArrayElement(key, deleteIndex);
            }
        }

        private void DrawArrayElementTranslations(string key, int index)
        {
            foreach (var lang in _languageCodes)
            {
                var langName = LanguageDefinitions.GetDisplayName(lang);
                EditorGUILayout.LabelField($"{langName}:", GUILayout.Width(120));

                var langArray = (List<string>)_languageData[key][lang];
                var currentText = langArray[index] ?? "";

                Rect textRect = EditorGUILayout.GetControlRect();
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.TextField(textRect, currentText);
                EditorGUI.EndDisabledGroup();

                if (Event.current.type == EventType.MouseDown &&
                    Event.current.clickCount == 2 &&
                    textRect.Contains(Event.current.mousePosition))
                {
                    int capturedIndex = index;
                    OpenTextEditor(currentText, (newText) =>
                    {
                        langArray[capturedIndex] = newText;
                        _hasUnsavedChanges = true;
                        Repaint();
                    });
                    Event.current.Use();
                }
            }
        }

        private void DrawToolsTab()
        {
            _toolsScrollPosition = EditorGUILayout.BeginScrollView(_toolsScrollPosition);

            EditorGUILayout.LabelField("Charset Generation Tool", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");

            UpdateLanguageSelectionForCharset();

            EditorGUILayout.LabelField("Select Languages:", EditorStyles.boldLabel);
            _charsetLanguageScrollPos = EditorGUILayout.BeginScrollView(_charsetLanguageScrollPos, GUILayout.Height(200));
            foreach (var lang in _languageCodes)
            {
                string langName = LanguageDefinitions.GetDisplayName(lang);
                bool currentState = _languageSelectionForCharset[lang];
                bool newState = EditorGUILayout.Toggle($"{langName} ({lang})", currentState);
                if (newState != currentState)
                {
                    _languageSelectionForCharset[lang] = newState;
                    _hasUnsavedChanges = true;
                }
            }
            EditorGUILayout.EndScrollView();

            bool anySelected = _languageSelectionForCharset.Any(kvp => kvp.Value);
            EditorGUI.BeginDisabledGroup(!anySelected);
            if (GUILayout.Button("Generate Charsets", GUILayout.Height(30)))
            {
                GenerateCharsets();
            }
            EditorGUI.EndDisabledGroup();

            if (_generatedCharsets.Count > 0)
            {
                DrawGeneratedCharsets();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void UpdateLanguageSelectionForCharset()
        {
            foreach (var lang in _languageCodes.Where(lang => !_languageSelectionForCharset.ContainsKey(lang)))
            {
                _languageSelectionForCharset[lang] = false;
            }

            var currentLanguages = new HashSet<string>(_languageCodes);
            foreach (var lang in _languageSelectionForCharset.Keys.ToList().Where(lang => !currentLanguages.Contains(lang)))
            {
                _languageSelectionForCharset.Remove(lang);
            }
        }

        private void GenerateCharsets()
        {
            _generatedCharsets.Clear();
            foreach (var lang in _languageCodes.Where(l => _languageSelectionForCharset[l]))
            {
                string charset = GenerateCharsetForLanguage(lang);
                _generatedCharsets[lang] = charset;
            }
            _hasUnsavedChanges = true;
        }

        private void DrawGeneratedCharsets()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated Charsets:", EditorStyles.boldLabel);
            foreach (var kvp in _generatedCharsets)
            {
                string lang = kvp.Key;
                string charset = kvp.Value;
                string langName = LanguageDefinitions.GetDisplayName(lang);

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Language: {langName} ({lang})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Unique Characters: {charset.Length}");
                EditorGUILayout.SelectableLabel(charset, EditorStyles.textArea, GUILayout.Height(50));
                if (GUILayout.Button("Copy to Clipboard", GUILayout.Width(150)))
                {
                    EditorGUIUtility.systemCopyBuffer = charset;
                    ShowNotification(new GUIContent($"Charset for {langName} copied to clipboard!"));
                }
                EditorGUILayout.EndVertical();
            }
        }

        private string GenerateCharsetForLanguage(string language)
        {
            var charSet = new HashSet<char>();
            foreach (var key in _keys)
            {
                if (_languageData[key].TryGetValue(language, out var value))
                {
                    AddValueToCharset(value, charSet);
                }
            }

            return new string(charSet.ToArray());
        }

        private void AddValueToCharset(object value, HashSet<char> charSet)
        {
            switch (value)
            {
                case string str:
                    foreach (var c in str) charSet.Add(c);
                    break;
                case List<string> list:
                    foreach (var c in list.Where(item => item != null).SelectMany(item => item))
                        charSet.Add(c);
                    break;
            }
        }

        private void DrawConfigTab()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("DeepL API Settings", EditorStyles.boldLabel);
            EditorGUILayout.TextField("API Key:", DeeplApiKey, EditorStyles.textField);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("File Settings", EditorStyles.boldLabel);
            EditorGUILayout.TextField("File Path:", "{StreamingAssets}" + LocalizationManager.LanguagesFilePath,
                EditorStyles.textField);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("File Operations", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Delete Language Data", GUILayout.Height(25)))
            {
                PurgeAllData();
            }

            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Open Language File", GUILayout.Height(25)))
            {
                OpenLanguageFile();
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("File Path: " + LocalizationManager.FilePath, MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        private void DrawSaveButton()
        {
            EditorGUILayout.Space();
            GUI.backgroundColor = _hasUnsavedChanges ? Color.red : Color.white;
            if (GUILayout.Button("Save Changes", GUILayout.Height(30)))
            {
                SaveLanguages();
            }
            GUI.backgroundColor = Color.white;
        }

        #endregion

        #region Helper Functions

        /// <summary>
        /// Checks if a key's data represents an array type.
        /// </summary>
        private bool IsArrayKey(Dictionary<string, object> keyData)
        {
            if (keyData == null || keyData.Count == 0) return false;
            var firstValue = keyData.Values.FirstOrDefault();
            return firstValue is List<string> || firstValue is string[];
        }

        /// <summary>
        /// Gets the first available value for a key, checking multiple language fallbacks.
        /// </summary>
        private object GetFirstValue(Dictionary<string, object> keyData)
        {
            if (keyData == null || keyData.Count == 0) return null;
            
            // Try common language codes in order
            string[] tryLangs = { "en", "en-US", "en-GB" };
            foreach (var lang in tryLangs)
            {
                if (keyData.TryGetValue(lang, out var value))
                    return value;
            }
            
            // Return first available
            return keyData.Values.FirstOrDefault();
        }

        private IEnumerable<string> FilterKeys()
        {
            string lowercaseFilter = _keySearchFilter?.ToLower() ?? "";
            bool hasFilter = !string.IsNullOrEmpty(lowercaseFilter);

            var query = _keys.AsQueryable();

            if (hasFilter)
            {
                query = query.Where(key => key.ToLower().Contains(lowercaseFilter));
            }

            if (_showArrayKeysOnly)
            {
                query = query.Where(key => IsArrayKey(_languageData[key]));
            }
            else if (_showStringKeysOnly)
            {
                query = query.Where(key => !IsArrayKey(_languageData[key]));
            }

            if (_sortKeysByName)
            {
                query = query.OrderBy(key => key);
            }

            return query;
        }

        private void DeleteArrayElement(string key, int index)
        {
            foreach (var lang in _languageCodes)
            {
                if (_languageData[key][lang] is List<string> langArray)
                {
                    langArray.RemoveAt(index);
                }
            }
        }

        private void ClearEmptyArrayElements(string key)
        {
            // Get array count from first available language
            var firstValue = GetFirstValue(_languageData[key]);
            if (firstValue is not List<string> firstArray) return;
            
            for (var i = firstArray.Count - 1; i >= 0; i--)
            {
                var isEmpty = true;
                foreach (var lang in _languageCodes)
                {
                    if (_languageData[key][lang] is not List<string> langArray ||
                        string.IsNullOrWhiteSpace(langArray[i])) continue;
                    isEmpty = false;
                    break;
                }

                if (isEmpty)
                {
                    DeleteArrayElement(key, i);
                }
            }
        }

        private void PurgeAllData()
        {
            if (!EditorUtility.DisplayDialog("Purge All Data",
                    "Are you sure you want to delete all language data?\n\n" +
                    "This action cannot be undone!",
                    "Yes, Delete All", "Cancel")) return;
            
            _languageData.Clear();
            _keys.Clear();
            _languageCodes.Clear();
            _languageCodes.Add("en");
            SaveLanguages();
            Repaint();
        }

        private static void UnregisterEventHandlers()
        {
            LocalizationManager.Dispose();
        }

        private static void OpenLanguageFile()
        {
            if (!File.Exists(LocalizationManager.FilePath))
            {
                EditorUtility.DisplayDialog("Error",
                    "Language file does not exist yet.\nSave some data first to create the file.",
                    "OK");
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(LocalizationManager.FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error opening language file: {e}");
                TryOpenWithAlternativeEditors();
            }
        }

        private static void TryOpenWithAlternativeEditors()
        {
            try
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsEditor:
                        System.Diagnostics.Process.Start("notepad.exe", LocalizationManager.FilePath);
                        break;
                    case RuntimePlatform.OSXEditor:
                        System.Diagnostics.Process.Start("open", LocalizationManager.FilePath);
                        break;
                    default:
                        System.Diagnostics.Process.Start("xdg-open", LocalizationManager.FilePath);
                        break;
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Could not open the language file: {ex.Message}",
                    "OK");
                Debug.LogError($"Error opening language file: {ex}");
            }
        }

        private void AddLanguage(string language)
        {
            if (!_languageCodes.Contains(language))
            {
                _languageCodes.Add(language);
                foreach (var key in _keys)
                {
                    AddLanguageToKey(key, language);
                }

                GUI.changed = true;
                Repaint();
            }
            else
            {
                Debug.LogWarning($"Language '{language}' already exists.");
            }
        }

        private void AddLanguageToKey(string key, string language)
        {
            var firstValue = GetFirstValue(_languageData[key]);
            
            switch (firstValue)
            {
                case List<string> arr:
                    var newArray = new List<string>();
                    for (var i = 0; i < arr.Count; i++)
                    {
                        newArray.Add("");
                    }
                    _languageData[key][language] = newArray;
                    break;
                default:
                    _languageData[key][language] = "";
                    break;
            }
        }

        private void DeleteLanguage(string language)
        {
            if (_languageCodes.Contains(language))
            {
                _languageCodes.Remove(language);
                foreach (var key in _keys.Where(key => _languageData[key].ContainsKey(language)))
                {
                    _languageData[key].Remove(language);
                }

                Debug.Log($"Language '{language}' deleted.");
            }
            else
            {
                Debug.LogWarning($"Language '{language}' does not exist.");
            }
        }

        private void AddKey(string key, bool isArray)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("Key can't be empty string.");
                return;
            }

            if (_keys.Contains(key))
            {
                Debug.LogWarning($"Key '{key}' already exists.");
                return;
            }

            _keys.Add(key);
            _languageData[key] = new Dictionary<string, object>();
            foreach (var lang in _languageCodes)
            {
                _languageData[key][lang] = isArray ? new List<string>() : "";
            }

            _selectedKey = key;
            _hasUnsavedChanges = true;
            Repaint();
        }

        private void DeleteKey(string key)
        {
            if (_keys.Contains(key))
            {
                _keys.Remove(key);
                _languageData.Remove(key);
                _keyFoldouts.Remove(key);
                GUI.changed = true;
                _hasUnsavedChanges = true;
                Repaint();
            }
        }

        private void ConfirmDeleteKey(string key)
        {
            if (EditorUtility.DisplayDialog("Delete Key",
                    $"Are you sure you want to delete the key '{key}'?", "Yes", "No"))
            {
                DeleteKey(key);
                _selectedKey = "";
            }
        }

        private void ClearKeyData(string key)
        {
            if (!EditorUtility.DisplayDialog("Clear Key Data",
                    $"Are you sure you want to clear all translations for key '{key}'?\nThis cannot be undone!",
                    "Yes, Clear", "Cancel"))
                return;

            if (_languageData.TryGetValue(key, out var keyData))
            {
                foreach (var lang in keyData.Keys.ToList())
                {
                    _languageData[key][lang] = keyData[lang] switch
                    {
                        string => "",
                        List<string> list => new List<string>(new string[list.Count]),
                        _ => _languageData[key][lang]
                    };
                }

                ShowNotification(new GUIContent("All translations cleared."));
                _hasUnsavedChanges = true;
                Repaint();
            }
        }

        private void RenameKey(string key)
        {
            if (!_keys.Contains(key)) return;
            
            OpenTextEditor(key, (newKey) =>
            {
                if (string.IsNullOrEmpty(newKey))
                {
                    EditorUtility.DisplayDialog("Error", "Key name cannot be empty.", "OK");
                    return;
                }

                if (_keys.Contains(newKey))
                {
                    EditorUtility.DisplayDialog("Error", $"Key '{newKey}' already exists.", "OK");
                    return;
                }

                int index = _keys.IndexOf(key);
                _keys[index] = newKey;
                _languageData[newKey] = _languageData[key];
                _languageData.Remove(key);
                _selectedKey = newKey;
                _hasUnsavedChanges = true;
                Repaint();
            });
        }

        private void MergeImportedData(string key, Dictionary<string, object> newData, bool isArrayKey)
        {
            foreach (var lang in newData.Keys)
            {
                if (!_languageData[key].ContainsKey(lang))
                    continue;

                if (isArrayKey)
                {
                    MergeArrayData(key, lang, newData[lang]);
                }
                else
                {
                    MergeStringData(key, lang, newData[lang]);
                }
            }
        }

        private void MergeArrayData(string key, string lang, object newValue)
        {
            if (_languageData[key][lang] is not List<string> currentList) return;

            if (newValue is List<string> newList)
            {
                for (int i = 0; i < newList.Count; i++)
                {
                    if (i < currentList.Count)
                    {
                        if (string.IsNullOrWhiteSpace(currentList[i]) && !string.IsNullOrWhiteSpace(newList[i]))
                        {
                            currentList[i] = newList[i];
                        }
                    }
                    else
                    {
                        currentList.Add(newList[i]);
                    }
                }
            }
            else if (newValue is string strValue && !string.IsNullOrWhiteSpace(strValue))
            {
                if (currentList.Count > 0 && string.IsNullOrWhiteSpace(currentList[0]))
                {
                    currentList[0] = strValue;
                }
                else
                {
                    currentList.Add(strValue);
                }
            }
        }

        private void MergeStringData(string key, string lang, object newValue)
        {
            if (_languageData[key][lang] is not string currentText) return;

            string newText = newValue switch
            {
                string newStr => newStr,
                List<string> newList when newList.Count > 0 => newList[0],
                _ => newValue?.ToString() ?? ""
            };

            if (string.IsNullOrWhiteSpace(currentText) && !string.IsNullOrWhiteSpace(newText))
            {
                _languageData[key][lang] = newText;
            }
        }

        private void LoadLanguages()
        {
            try
            {
                if (File.Exists(LocalizationManager.FilePath))
                {
                    var loadedData = LocalizationManager.LoadFromFile(LocalizationManager.FilePath);
                    _languageData = loadedData.Translations;
                    _keys = new List<string>(_languageData.Keys);
                    
                    // Extract available languages from data
                    _languageCodes.Clear();
                    _languageCodes.Add("en");
                    
                    foreach (var keyData in _languageData.Values)
                    {
                        foreach (var lang in keyData.Keys)
                        {
                            if (!_languageCodes.Contains(lang))
                                _languageCodes.Add(lang);
                        }
                    }
                }
                else
                {
                    _languageData = new Dictionary<string, Dictionary<string, object>>();
                    _keys = new List<string>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading language data: {ex.Message}");
                _languageData = new Dictionary<string, Dictionary<string, object>>();
                _keys = new List<string>();
            }
        }

        private void SaveLanguages()
        {
            try
            {
                var saveData = new LanguageData { Translations = _languageData };
                LocalizationManager.SaveToFile(LocalizationManager.FilePath, saveData);
                _hasUnsavedChanges = false;
                ShowNotification(new GUIContent("Language data saved successfully!"));
                Debug.Log("[LocalizationEditor] Language data saved.");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to save language data: {ex.Message}", "OK");
                Debug.LogError($"[LocalizationEditor] Error saving language data: {ex}");
            }
        }

        private void PromptAutoSave(string context)
        {
            if (!_hasUnsavedChanges) return;
            
            if (EditorUtility.DisplayDialog("Unsaved Changes",
                    $"You have unsaved changes. Would you like to save them {context}?",
                    "Save", "Don't Save"))
            {
                SaveLanguages();
            }
            else
            {
                _hasUnsavedChanges = false;
            }
        }

        private static void OpenTextEditor(string text, Action<string> onSave)
        {
            LocalizationTextEditorPopup.Open(text, onSave);
        }

        private async Task TranslateAndFill(string key)
        {
            if (!_languageData.TryGetValue(key, out var keyData)) return;
            
            // Find a source text (prefer English, but use first available string)
            string sourceText = null;
            string sourceLang = "en";
            
            if (keyData.TryGetValue("en", out var enValue) && enValue is string enStr)
            {
                sourceText = enStr;
            }
            else
            {
                // Find first string value
                foreach (var kvp in keyData)
                {
                    if (kvp.Value is string str && !string.IsNullOrWhiteSpace(str))
                    {
                        sourceText = str;
                        sourceLang = kvp.Key;
                        break;
                    }
                }
            }
            
            if (string.IsNullOrWhiteSpace(sourceText)) return;

            foreach (var lang in _languageCodes.Where(l => l != sourceLang))
            {
                if (!string.IsNullOrWhiteSpace(keyData[lang]?.ToString())) continue;

                try
                {
                    var translated = await TranslateText(sourceText, "en", lang);
                    if (!string.IsNullOrEmpty(translated))
                    {
                        keyData[lang] = translated;
                        _hasUnsavedChanges = true;
                        Repaint();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Translation error for {lang}: {ex.Message}");
                }
            }
        }

        private async Task<string> TranslateText(string text, string sourceLang, string targetLang)
        {
            // DeepL language code mapping
            string deeplLang = targetLang switch
            {
                "zh" => "ZH",
                "zh-Hant" => "ZH",
                "pt" => "PT-PT",
                _ => targetLang.ToUpper()
            };

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("auth_key", DeeplApiKey),
                new KeyValuePair<string, string>("text", text),
                new KeyValuePair<string, string>("source_lang", sourceLang.ToUpper()),
                new KeyValuePair<string, string>("target_lang", deeplLang)
            });

            try
            {
                var response = await _httpClient.PostAsync(DeeplApiUrl, content);
                response.EnsureSuccessStatusCode();
                
                var responseJson = await response.Content.ReadAsStringAsync();
                
                // Simple JSON parsing for DeepL response: {"translations":[{"text":"..."}]}
                string translated = ParseDeepLResponse(responseJson);
                return translated ?? text;
            }
            catch (Exception ex)
            {
                Debug.LogError($"DeepL translation failed: {ex.Message}");
                return text;
            }
        }

        private string ParseDeepLResponse(string json)
        {
            // Simple parser for: {"translations":[{"detected_source_language":"EN","text":"..."}]}
            int textIndex = json.IndexOf("\"text\":");
            if (textIndex < 0) return null;
            
            int quoteStart = json.IndexOf('"', textIndex + 7);
            if (quoteStart < 0) return null;
            
            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0) return null;
            
            string escaped = json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            
            // Unescape common JSON escape sequences
            return escaped
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        #endregion
    }
}
