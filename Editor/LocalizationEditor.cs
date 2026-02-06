using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using TMPro;
using UnityEditor.Compilation;
using PicoShot.Localization.Config;
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
        private const string DefaultDeeplApiUrl = "https://api-free.deepl.com/v2/translate";
        private const string DeeplApiUrlPref = "PicoShot_Localization_DeepLApiUrl";
        private const string DeeplApiKeyPref = "PicoShot_Localization_DeepLApiKey";
        private const string DeeplContextPref = "PicoShot_Localization_DeepLContext";
        private const int DeeplRequestDelayMs = 350;
        private const string DefaultDeepLContext = "This is a game localization text. The translation should be concise and suitable for game UI.";

        /// <summary>
        /// Temporary translation hint for the currently selected key.
        /// </summary>
        private string _currentKeyHint = "";

        /// <summary>
        /// Tracks the last selected key
        /// </summary>
        private string _lastSelectedKey;

        /// <summary>
        /// Gets or sets the DeepL API URL from PlayerPrefs
        /// </summary>
        private string DeeplApiUrl
        {
            get => PlayerPrefs.GetString(DeeplApiUrlPref, DefaultDeeplApiUrl);
            set => PlayerPrefs.SetString(DeeplApiUrlPref, value);
        }

        /// <summary>
        /// Gets or sets the DeepL API key from EditorPrefs
        /// </summary>
        private string DeeplApiKey
        {
            get => EditorPrefs.GetString(DeeplApiKeyPref, "");
            set => EditorPrefs.SetString(DeeplApiKeyPref, value);
        }

        /// <summary>
        /// Gets or sets the DeepL context for improving translation quality
        /// </summary>
        private string DeeplContext
        {
            get => PlayerPrefs.GetString(DeeplContextPref, DefaultDeepLContext);
            set => PlayerPrefs.SetString(DeeplContextPref, value);
        }

        /// <summary>
        /// Clears the temporary translation hint when switching keys.
        /// Also clears keyboard focus
        /// </summary>
        private void ClearKeyHint()
        {
            _currentKeyHint = "";
            GUIUtility.keyboardControl = 0;
            Repaint();
        }
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

        [MenuItem("Tools/Localization/Language Editor")]
        public static void OpenWindow()
        {
            GetWindow<LocalizationEditor>("Language Editor");
        }

        private void OnEnable()
        {
            _languageData ??= new Dictionary<string, Dictionary<string, object>>();
            _keys ??= new List<string>();
            _currentKeyHint = "";
            _lastSelectedKey = null;

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

                case KeyCode.C:
                    if (ctrlPressed)
                    {
                        CopyKeyNameToClipboard();
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

            string defaultLang = LocalizationConfigProvider.Config.DefaultLanguage;

            foreach (var lang in filteredLanguages)
            {
                EditorGUILayout.BeginHorizontal("box");

                bool isDefault = lang.Key == defaultLang;
                bool isSelected = _languageCodes.Contains(lang.Key);

                if (isDefault)
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

                string label = isDefault
                    ? $"{lang.Value} ({lang.Key}) - Default"
                    : $"{lang.Value} ({lang.Key})";

                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

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

        /// <summary>
        /// Draws the translation hint field for DeepL context.
        /// </summary>
        private void DrawTranslationHintField(string key)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Translation Hint (for DeepL):", EditorStyles.miniBoldLabel);

            GUI.SetNextControlName($"TranslationHint_{key}");
            string newHint = EditorGUILayout.TextArea(_currentKeyHint, GUILayout.MinHeight(40));
            _currentKeyHint = newHint;

            if (string.IsNullOrEmpty(_currentKeyHint))
            {
                EditorGUILayout.LabelField($"Example: '{key}' should be translated as verb not noun",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawKeyDetailsPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            if (_lastSelectedKey != _selectedKey)
            {
                ClearKeyHint();
                _lastSelectedKey = _selectedKey;
            }

            if (!string.IsNullOrEmpty(_selectedKey))
            {
                EditorGUILayout.LabelField($"Key Details: {_selectedKey}", EditorStyles.boldLabel);
                _keyDetailsScroll = EditorGUILayout.BeginScrollView(_keyDetailsScroll, GUILayout.ExpandHeight(true));

                DrawTranslationHintField(_selectedKey);

                EditorGUILayout.Space(5);

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

            if (GUILayout.Button("Copy Key", GUILayout.Width(80)))
                ShowCopyKeyMenu();

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

            menu.AddItem(new GUIContent("Translate with Gemini (soon)"), false, null);

            menu.ShowAsContext();
        }

        /// <summary>
        /// Shows the copy key menu with options to copy key name or code snippets.
        /// </summary>
        private void ShowCopyKeyMenu()
        {
            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("Copy Key Name"), false, () =>
            {
                CopyKeyNameToClipboard();
            });

            menu.AddItem(new GUIContent("Copy with GetText()"), false, () =>
            {
                CopyWithGetText();
            });

            menu.AddItem(new GUIContent("Copy with GetArray()"), false, () =>
            {
                CopyWithGetArray();
            });

            menu.ShowAsContext();
        }

        /// <summary>
        /// Copies the selected key name to the clipboard.
        /// </summary>
        private void CopyKeyNameToClipboard()
        {
            if (string.IsNullOrEmpty(_selectedKey)) return;
            EditorGUIUtility.systemCopyBuffer = _selectedKey;
            ShowNotification(new GUIContent($"Key '{_selectedKey}' copied to clipboard!"));
        }

        /// <summary>
        /// Copies the GetText code snippet for the selected key to the clipboard.
        /// </summary>
        private void CopyWithGetText()
        {
            if (string.IsNullOrEmpty(_selectedKey)) return;
            string code = $"LocalizationManager.GetText(\"{_selectedKey}\")";
            EditorGUIUtility.systemCopyBuffer = code;
            ShowNotification(new GUIContent("GetText() snippet copied!"));
        }

        /// <summary>
        /// Copies the GetArray code snippet for the selected key to the clipboard.
        /// </summary>
        private void CopyWithGetArray()
        {
            if (string.IsNullOrEmpty(_selectedKey)) return;
            string code = $"LocalizationManager.GetArray(\"{_selectedKey}\")";
            EditorGUIUtility.systemCopyBuffer = code;
            ShowNotification(new GUIContent("GetArray() snippet copied!"));
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

            string typeIndicator = "Aa";
            if (_languageData.TryGetValue(key, out var keyData) && keyData.Count > 0)
            {
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
            var content = new GUIContent(LanguageDefinitions.GetDisplayName(currentLang ?? string.Empty));
            var dropdownRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));

            if (EditorGUI.DropdownButton(dropdownRect, content, FocusType.Keyboard))
            {
                ShowLanguageDropdown(dropdownRect, currentLang);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("System Language:", GUILayout.Width(120));
            var systemLangCode = LanguageDefinitions.FromSystemLanguage(Application.systemLanguage);
            EditorGUILayout.LabelField(LanguageDefinitions.GetDisplayName(systemLangCode ?? string.Empty));

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
            foreach (var lang in LocalizationManager.GetAvailableLanguageCodes())
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
            DrawStatusField("Default Language", LocalizationManager.DefaultLanguage, MessageType.Info);
            DrawStatusField("Is RTL Language", LocalizationManager.IsRightToLeft.ToString(), MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            var availableCount = LocalizationManager.GetAvailableLanguages().Count();
            DrawStatusField("Available Languages", availableCount.ToString(), MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

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
                    string suggestedKey = GetBestMatchingKey(component);
                    langComponent = Undo.AddComponent<LocalizationTextComponent>(go);
                    if (!string.IsNullOrEmpty(suggestedKey))
                    {
                        langComponent.TranslationKey = suggestedKey;
                        EditorUtility.SetDirty(langComponent);
                    }
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

            if (langComponent == null)
            {
                DrawSuggestedKeysForComponent(go, component);
            }
            else
            {
                DrawComponentKeySelector(langComponent);

                if (string.IsNullOrEmpty(langComponent.TranslationKey))
                {
                    DrawSuggestedKeys(langComponent, component);
                }
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Gets the single best matching key for a component, or null if no good match.
        /// </summary>
        private string GetBestMatchingKey(Component component)
        {
            string currentText = GetComponentText(component);
            if (string.IsNullOrWhiteSpace(currentText)) return null;

            var suggestions = FindBestMatchingKeys(currentText, 1);
            return suggestions.Count > 0 && suggestions[0].score >= 70 ? suggestions[0].key : null;
        }

        /// <summary>
        /// Shows suggested keys for a component that doesn't have Language Support yet.
        /// </summary>
        private void DrawSuggestedKeysForComponent(GameObject go, Component component)
        {
            string currentText = GetComponentText(component);
            if (string.IsNullOrWhiteSpace(currentText)) return;

            var suggestions = FindBestMatchingKeys(currentText, 5);
            if (suggestions.Count == 0) return;

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Suggested Keys:", EditorStyles.miniBoldLabel);

            foreach (var (key, score) in suggestions)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  {key}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField($"{score:F0}% match", EditorStyles.miniLabel, GUILayout.Width(70));

                if (GUILayout.Button("Assign", GUILayout.Width(60)))
                {
                    var langComponent = Undo.AddComponent<LocalizationTextComponent>(go);
                    langComponent.TranslationKey = key;
                    EditorUtility.SetDirty(langComponent);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Shows suggested keys based on similarity between current text and key values.
        /// </summary>
        private void DrawSuggestedKeys(LocalizationTextComponent langComponent, Component component)
        {
            string currentText = GetComponentText(component);
            if (string.IsNullOrWhiteSpace(currentText)) return;

            var suggestions = FindBestMatchingKeys(currentText, 5);
            if (suggestions.Count == 0) return;

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Suggested Keys (based on current text):", EditorStyles.miniBoldLabel);

            foreach (var (key, score) in suggestions)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{key}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField($"{score:F0}% match", EditorStyles.miniLabel, GUILayout.Width(60));

                if (GUILayout.Button("Assign", GUILayout.Width(60)))
                {
                    Undo.RecordObject(langComponent, "Assign Translation Key");
                    langComponent.TranslationKey = key;
                    EditorUtility.SetDirty(langComponent);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Gets text from a component (TMP_Text or string property).
        /// </summary>
        private string GetComponentText(Component component)
        {
            if (component is TMP_Text tmpText)
                return tmpText.text;

            var textProperty = component.GetType().GetProperty("text");
            if (textProperty != null)
                return textProperty.GetValue(component)?.ToString() ?? "";

            return "";
        }

        /// <summary>
        /// Finds best matching keys using advanced fuzzy string comparison.
        /// Matches against both key names and translation values.
        /// </summary>
        private List<(string key, float score)> FindBestMatchingKeys(string text, int maxResults)
        {
            var results = new List<(string key, float score)>();
            if (string.IsNullOrWhiteSpace(text)) return results;

            string normalizedText = text.ToLowerInvariant().Trim();
            var queryTokens = Tokenize(text);

            foreach (var key in _keys)
            {
                float bestScore = 0;
                string normalizedKey = key.ToLowerInvariant();

                float keyNameScore = CalculateFuzzyScore(normalizedText, normalizedKey, queryTokens, true);
                bestScore = Math.Max(bestScore, keyNameScore);

                if (_languageData.TryGetValue(key, out var keyData))
                {
                    foreach (var langValue in keyData.Values)
                    {
                        string valueStr = langValue?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(valueStr)) continue;

                        float valueScore = CalculateFuzzyScore(normalizedText, valueStr.ToLowerInvariant(), queryTokens, false);
                        bestScore = Math.Max(bestScore, valueScore * 0.9f);
                    }
                }

                if (bestScore > 0.2f)
                {
                    results.Add((key, bestScore * 100));
                }
            }

            return results.OrderByDescending(r => r.score).Take(maxResults).ToList();
        }

        /// <summary>
        /// Calculates a comprehensive fuzzy matching score (0-1 scale).
        /// Combines multiple algorithms for robust matching.
        /// </summary>
        private float CalculateFuzzyScore(string query, string target, string[] queryTokens, bool isKeyName = false)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target)) return 0;

            string normalizedQuery = query.ToLowerInvariant().Trim();
            string normalizedTarget = target.ToLowerInvariant().Trim();

            if (normalizedQuery == normalizedTarget) return 1f;

            var targetTokens = Tokenize(target);

            float tokenSetScore = CalculateTokenSetRatio(queryTokens, targetTokens);
            float partialScore = CalculatePartialRatio(normalizedQuery, normalizedTarget);
            float ngramScore = CalculateNgramSimilarity(normalizedQuery, normalizedTarget, 2);
            float prefixBonus = CalculatePrefixBonus(normalizedQuery, normalizedTarget);
            float acronymScore = CalculateAcronymMatch(queryTokens, targetTokens);

            if (isKeyName)
            {
                float keyNameScore = CalculateKeyNameMatchScore(normalizedQuery, normalizedTarget);

                float weightedScore = keyNameScore * 0.50f +
                                      tokenSetScore * 0.20f +
                                      partialScore * 0.15f +
                                      prefixBonus * 0.10f +
                                      ngramScore * 0.05f;

                return Math.Min(1f, weightedScore);
            }

            float valueScore = tokenSetScore * 0.35f +
                               partialScore * 0.30f +
                               ngramScore * 0.20f +
                               prefixBonus * 0.10f +
                               acronymScore * 0.05f;

            return Math.Min(1f, valueScore);
        }

        /// <summary>
        /// Specialized scoring for key name matching.
        /// Prioritizes exact, prefix, and substring matches.
        /// </summary>
        private float CalculateKeyNameMatchScore(string query, string keyName)
        {
            if (query == keyName) return 1f;

            if (keyName.StartsWith(query)) return 0.95f;

            if (keyName.Contains(query)) return 0.85f;

            var queryParts = query.Split(new[] { '_', '.', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var keyParts = keyName.Split(new[] { '_', '.', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);

            int matchingParts = 0;
            foreach (var qPart in queryParts)
            {
                foreach (var kPart in keyParts)
                {
                    if (kPart == qPart || kPart.StartsWith(qPart))
                    {
                        matchingParts++;
                        break;
                    }
                }
            }

            if (matchingParts == queryParts.Length)
            {
                return 0.75f + (0.15f * (matchingParts / (float)keyParts.Length));
            }

            if (matchingParts > 0)
            {
                return 0.50f + (0.20f * (matchingParts / (float)queryParts.Length));
            }

            return 0;
        }

        /// <summary>
        /// Tokenizes text into words, handling camelCase and snake_case.
        /// </summary>
        private string[] Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

            string normalized = text.ToLowerInvariant();

            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, "([a-z])([A-Z])", "$1 $2");
            normalized = normalized.Replace('_', ' ').Replace('-', ' ');

            return normalized.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Calculates token set ratio - matches words regardless of order.
        /// Based on FuzzyWuzzy's token_set_ratio algorithm.
        /// </summary>
        private float CalculateTokenSetRatio(string[] queryTokens, string[] targetTokens)
        {
            if (queryTokens.Length == 0 || targetTokens.Length == 0) return 0;

            var querySet = new HashSet<string>(queryTokens);
            var targetSet = new HashSet<string>(targetTokens);

            var intersection = querySet.Intersect(targetSet).ToList();
            var queryDiff = querySet.Except(targetSet).ToList();
            var targetDiff = targetSet.Except(querySet).ToList();

            string intersectionStr = string.Join(" ", intersection);
            string queryCombined = intersectionStr + " " + string.Join(" ", queryDiff);
            string targetCombined = intersectionStr + " " + string.Join(" ", targetDiff);

            float score1 = CalculateLevenshteinRatio(queryCombined.Trim(), targetCombined.Trim());

            string querySorted = string.Join(" ", queryTokens.OrderBy(t => t));
            string targetSorted = string.Join(" ", targetTokens.OrderBy(t => t));
            float score2 = CalculateLevenshteinRatio(querySorted, targetSorted);

            return Math.Max(score1, score2);
        }

        /// <summary>
        /// Calculates partial ratio - best matching substring.
        /// Finds the best alignment of shorter string within longer string.
        /// </summary>
        private float CalculatePartialRatio(string shorter, string longer)
        {
            if (shorter.Length > longer.Length)
            {
                (shorter, longer) = (longer, shorter);
            }

            if (longer.Contains(shorter)) return 1f;

            int sLen = shorter.Length;
            int lLen = longer.Length;

            if (sLen == 0 || lLen == 0) return 0;

            float bestScore = 0;

            for (int i = 0; i <= lLen - sLen; i++)
            {
                string substring = longer.Substring(i, sLen);
                float score = CalculateLevenshteinRatio(shorter, substring);
                bestScore = Math.Max(bestScore, score);
            }

            for (int windowSize = sLen - 1; windowSize >= Math.Max(1, sLen / 2); windowSize--)
            {
                for (int i = 0; i <= lLen - windowSize; i++)
                {
                    string substring = longer.Substring(i, windowSize);
                    float score = CalculateLevenshteinRatio(shorter, substring) * ((float)windowSize / sLen);
                    bestScore = Math.Max(bestScore, score);
                }
            }

            return bestScore;
        }

        /// <summary>
        /// Calculates n-gram similarity for typo tolerance.
        /// </summary>
        private float CalculateNgramSimilarity(string a, string b, int n)
        {
            if (a.Length < n || b.Length < n) return CalculateLevenshteinRatio(a, b);

            var aNgrams = GetNgrams(a, n);
            var bNgrams = GetNgrams(b, n);

            if (aNgrams.Count == 0 || bNgrams.Count == 0) return 0;

            int matches = aNgrams.Intersect(bNgrams).Count();
            return (2f * matches) / (aNgrams.Count + bNgrams.Count);
        }

        /// <summary>
        /// Extracts n-grams from a string.
        /// </summary>
        private HashSet<string> GetNgrams(string text, int n)
        {
            var ngrams = new HashSet<string>();
            for (int i = 0; i <= text.Length - n; i++)
            {
                ngrams.Add(text.Substring(i, n));
            }
            return ngrams;
        }

        /// <summary>
        /// Gives bonus for prefix/suffix matches for better UX.
        /// </summary>
        private float CalculatePrefixBonus(string query, string target)
        {
            float bonus = 0;

            if (target.StartsWith(query))
            {
                bonus += 0.5f + (0.5f * query.Length / target.Length);
            }
            else if (target.Contains(" " + query) || target.Contains("_" + query))
            {
                bonus += 0.3f;
            }

            var queryWords = query.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var targetWords = target.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);

            int matchingStartWords = 0;
            for (int i = 0; i < Math.Min(queryWords.Length, targetWords.Length); i++)
            {
                if (targetWords[i].StartsWith(queryWords[i]) || queryWords[i].StartsWith(targetWords[i]))
                {
                    matchingStartWords++;
                }
                else
                {
                    break;
                }
            }

            if (matchingStartWords > 0)
            {
                bonus += 0.2f * (matchingStartWords / (float)queryWords.Length);
            }

            return Math.Min(1f, bonus);
        }

        /// <summary>
        /// Calculates acronym match score (e.g., "hp" matches "health points").
        /// </summary>
        private float CalculateAcronymMatch(string[] queryTokens, string[] targetTokens)
        {
            if (queryTokens.Length == 0 || targetTokens.Length == 0) return 0;
            if (queryTokens.Length > targetTokens.Length) return 0;

            string acronym = string.Concat(queryTokens.Select(t => t[0]));
            string targetAcronym = string.Concat(targetTokens.Take(queryTokens.Length).Select(t => t[0]));

            if (acronym == targetAcronym) return 1f;

            if (queryTokens.Length == 1 && queryTokens[0].Length <= 3)
            {
                int matches = targetTokens.Count(t => t.StartsWith(queryTokens[0]));
                return matches > 0 ? 0.8f * (matches / (float)targetTokens.Length) : 0;
            }

            return 0;
        }

        /// <summary>
        /// Calculates normalized Levenshtein ratio (0-1 scale).
        /// </summary>
        private float CalculateLevenshteinRatio(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1f;
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0f;

            int distance = LevenshteinDistance(a, b);
            int maxLen = Math.Max(a.Length, b.Length);

            return 1f - (float)distance / maxLen;
        }

        /// <summary>
        /// Calculates Levenshtein distance between two strings.
        /// </summary>
        private int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            int[,] matrix = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++) matrix[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) matrix[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                    int deletion = matrix[i - 1, j] + 1;
                    int insertion = matrix[i, j - 1] + 1;
                    int substitution = matrix[i - 1, j - 1] + cost;
                    matrix[i, j] = Math.Min(Math.Min(deletion, insertion), substitution);
                }
            }

            return matrix[a.Length, b.Length];
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

            var firstValue = GetFirstValue(_languageData[key]);
            var array = firstValue as List<string> ?? new List<string>();
            DrawArrayElements(key, array);

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add New Element", GUILayout.Width(120), GUILayout.Height(25)))
            {
                AddArrayElement(key);
            }

            if (GUILayout.Button("Clear Empty Elements", GUILayout.Width(140), GUILayout.Height(25)))
            {
                ClearEmptyArrayElements(key);
            }
            EditorGUILayout.EndHorizontal();

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

            var config = LocalizationConfigProvider.Config;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Default Language", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Default:", GUILayout.Width(80));

            var currentDefault = config.DefaultLanguage;
            var content = new GUIContent(LanguageDefinitions.GetDisplayName(currentDefault));
            var dropdownRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));

            if (EditorGUI.DropdownButton(dropdownRect, content, FocusType.Keyboard))
            {
                ShowDefaultLanguageDropdown(dropdownRect, config, currentDefault);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Protection Settings (experimental)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Protection Mode:", GUILayout.Width(120));
            var newMode = (ProtectionMode)EditorGUILayout.EnumPopup(config.ProtectionMode);
            if (newMode != config.ProtectionMode)
            {
                config.SetProtectionMode(newMode);
                LocalizationConfigProvider.SaveConfig();
                GUI.changed = true;
            }
            EditorGUILayout.EndHorizontal();

            if (config.IsAntiTamperEnabled)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("", GUILayout.Width(120));
                if (GUILayout.Button("Sync File Hashes", GUILayout.Height(25)))
                {
                    SyncFileHashes(config);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox(
                    "Anti-Tamper: File hashes are verified at runtime. " +
                    "Click 'Sync File Hashes' after modifying language files.",
                    MessageType.Info);
            }

            string protectionHelp = config.ProtectionMode switch
            {
                ProtectionMode.Disabled => "Protection is disabled. All language files can be loaded.",
                ProtectionMode.SelectionOnly => "Only selected languages can be loaded at runtime. This prevents loading unauthorized language files.",
                ProtectionMode.AntiTamper => "Anti-tamper protection with hash verification. File hashes are verified at runtime to detect modifications.",
                ProtectionMode.Both => "Full protection: Only selected languages can be loaded AND file hashes are verified to detect any modifications.",
                _ => ""
            };
            EditorGUILayout.HelpBox(protectionHelp, MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("DeepL API Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("API URL:", GUILayout.Width(70));
            string newApiUrl = EditorGUILayout.TextField(DeeplApiUrl);
            if (newApiUrl != DeeplApiUrl)
            {
                DeeplApiUrl = newApiUrl;
            }

            GUI.enabled = DeeplApiUrl != DefaultDeeplApiUrl;
            if (GUILayout.Button("Reset", GUILayout.Width(60)))
            {
                DeeplApiUrl = DefaultDeeplApiUrl;
                GUI.FocusControl(null);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("API Key:", GUILayout.Width(70));
            string newApiKey = EditorGUILayout.TextField(DeeplApiKey);
            if (newApiKey != DeeplApiKey)
            {
                DeeplApiKey = newApiKey;
            }

            GUI.enabled = !string.IsNullOrEmpty(DeeplApiKey);
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                DeeplApiKey = "";
                GUI.FocusControl(null);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Translation Context:", GUILayout.Width(150));

            GUI.enabled = DeeplContext != DefaultDeepLContext;
            if (GUILayout.Button("Reset to Default", GUILayout.Width(120)))
            {
                DeeplContext = DefaultDeepLContext;
                GUI.FocusControl(null);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            string newContext = EditorGUILayout.TextArea(DeeplContext, GUILayout.MinHeight(60));
            if (newContext != DeeplContext)
            {
                DeeplContext = newContext;
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("File Operations", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Delete All Language Data", GUILayout.Height(25)))
            {
                PurgeAllData();
            }

            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Open Languages Folder", GUILayout.Height(25)))
            {
                OpenLanguagesFolder();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Export to JSON", GUILayout.Height(25)))
            {
                ExportToJson();
            }

            if (GUILayout.Button("Import from JSON", GUILayout.Height(25)))
            {
                ImportFromJson();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox($"Languages Path: {LocalizationManager.LanguagesPath}", MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        private void SyncProtectionWithLanguages(LocalizationConfig config)
        {
            config.SetSelectedLanguages(new List<string>(_languageCodes));
            LocalizationConfigProvider.SaveConfig();
        }

        /// <summary>
        /// Calculates and stores SHA256 hashes for all language files.
        /// </summary>
        private void SyncFileHashes(LocalizationConfig config)
        {
            try
            {
                if (!Directory.Exists(LocalizationManager.LanguagesPath))
                {
                    EditorUtility.DisplayDialog("Error", "Languages directory not found.", "OK");
                    return;
                }

                var files = Directory.GetFiles(LocalizationManager.LanguagesPath, "*.bloc");
                int syncedCount = 0;
                int removedCount = 0;

                var existingHashes = new HashSet<string>(config.GetFileHashes().Select(h => h.fileName));

                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string hash = LocalizationManager.CalculateFileHash(file);
                    config.SetFileHash(fileName, hash);
                    syncedCount++;
                    existingHashes.Remove(fileName);
                }

                foreach (var oldFile in existingHashes)
                {
                    config.RemoveFileHash(oldFile);
                    removedCount++;
                }

                LocalizationConfigProvider.SaveConfig();

                EditorUtility.DisplayDialog("Hashes Synced",
                    $"Successfully synced {syncedCount} file hashes.\n" +
                    $"Removed {removedCount} outdated hashes.", "OK");
                Debug.Log($"[LocalizationEditor] Synced {syncedCount} file hashes, removed {removedCount} outdated.");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to sync hashes: {ex.Message}", "OK");
                Debug.LogError($"[LocalizationEditor] Hash sync failed: {ex}");
            }
        }

        private void ShowDefaultLanguageDropdown(Rect dropdownRect, LocalizationConfig config, string currentDefault)
        {
            var menu = new GenericMenu();

            foreach (var lang in _languageCodes)
            {
                menu.AddItem(
                    new GUIContent(LanguageDefinitions.GetDisplayName(lang)),
                    currentDefault == lang,
                    () =>
                    {
                        config.SetDefaultLanguage(lang);
                        LocalizationConfigProvider.SaveConfig();
                        GUI.changed = true;
                        Repaint();
                    }
                );
            }

            menu.DropDown(dropdownRect);
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
        /// Gets the first available value for a key, checking default language first.
        /// </summary>
        private object GetFirstValue(Dictionary<string, object> keyData)
        {
            if (keyData == null || keyData.Count == 0) return null;

            string defaultLang = LocalizationConfigProvider.Config.DefaultLanguage;
            if (keyData.TryGetValue(defaultLang, out var value))
                return value;

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

            var config = LocalizationConfigProvider.Config;
            string defaultLang = config.DefaultLanguage;

            _languageData.Clear();
            _keys.Clear();
            _languageCodes.Clear();
            _languageCodes.Add(defaultLang);

            if (Directory.Exists(LocalizationManager.LanguagesPath))
            {
                var files = Directory.GetFiles(LocalizationManager.LanguagesPath, "*.bloc");
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }

            config.SetSelectedLanguages(new List<string> { defaultLang });
            LocalizationConfigProvider.SaveConfig();

            SaveLanguages();
            Repaint();
        }

        private static void UnregisterEventHandlers()
        {
            LocalizationManager.Dispose();
        }

        private static void OpenLanguagesFolder()
        {
            string path = LocalizationManager.LanguagesPath;

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error opening languages folder: {e}");
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

                var config = LocalizationConfigProvider.Config;
                config.AddSelectedLanguage(language);
                LocalizationConfigProvider.SaveConfig();

                _hasUnsavedChanges = true;
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

                string filePath = LocalizationManager.GetLanguageFilePath(language);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                var config = LocalizationConfigProvider.Config;
                config.RemoveSelectedLanguage(language);
                LocalizationConfigProvider.SaveConfig();

                _hasUnsavedChanges = true;
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
                string defaultLang = LocalizationConfigProvider.Config.DefaultLanguage;

                _languageData = new Dictionary<string, Dictionary<string, object>>();
                _keys = new List<string>();
                _languageCodes.Clear();
                _languageCodes.Add(defaultLang);

                if (!Directory.Exists(LocalizationManager.LanguagesPath))
                {
                    Debug.Log($"[LocalizationEditor] Languages directory not found. Creating new data.");
                    return;
                }

                var blocFiles = Directory.GetFiles(LocalizationManager.LanguagesPath, "*.bloc", SearchOption.TopDirectoryOnly);

                foreach (var file in blocFiles)
                {
                    try
                    {
                        var localeData = LocalizationManager.LoadLocaleFromFile(file);
                        string langCode = Path.GetFileNameWithoutExtension(file);

                        if (!_languageCodes.Contains(langCode))
                        {
                            _languageCodes.Add(langCode);
                        }

                        foreach (var entry in localeData.Translations)
                        {
                            string key = entry.Key;
                            object value = entry.Value;

                            if (!_keys.Contains(key))
                            {
                                _keys.Add(key);
                                _languageData[key] = new Dictionary<string, object>();
                            }

                            _languageData[key][langCode] = value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[LocalizationEditor] Error loading file '{file}': {ex.Message}");
                    }
                }

                foreach (var key in _keys)
                {
                    foreach (var lang in _languageCodes)
                    {
                        if (!_languageData[key].ContainsKey(lang))
                        {
                            var firstValue = _languageData[key].Values.FirstOrDefault();
                            if (firstValue is List<string> list)
                            {
                                _languageData[key][lang] = new List<string>(new string[list.Count]);
                            }
                            else
                            {
                                _languageData[key][lang] = "";
                            }
                        }
                    }
                }

                SyncProtectionOnLoad();

                Debug.Log($"[LocalizationEditor] Loaded {_keys.Count} keys across {_languageCodes.Count} languages.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalizationEditor] Error loading language data: {ex.Message}");
                _languageData = new Dictionary<string, Dictionary<string, object>>();
                _keys = new List<string>();
            }
        }

        private void SyncProtectionOnLoad()
        {
            var config = LocalizationConfigProvider.Config;

            bool changed = false;
            foreach (var lang in _languageCodes)
            {
                if (!config.SelectedLanguages.Contains(lang))
                {
                    config.AddSelectedLanguage(lang);
                    changed = true;
                }
            }

            if (changed)
            {
                LocalizationConfigProvider.SaveConfig();
            }

            if (config.IsAntiTamperEnabled)
            {
                SyncFileHashes(config);
            }
        }

        private void SaveLanguages()
        {
            try
            {
                if (!Directory.Exists(LocalizationManager.LanguagesPath))
                {
                    Directory.CreateDirectory(LocalizationManager.LanguagesPath);
                }

                foreach (var lang in _languageCodes)
                {
                    var localeData = new LocaleData
                    {
                        LanguageCode = lang,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Translations = new Dictionary<string, object>()
                    };

                    foreach (var key in _keys)
                    {
                        if (_languageData[key].TryGetValue(lang, out var value))
                        {
                            localeData.Translations[key] = value;
                        }
                    }

                    string filePath = LocalizationManager.GetLanguageFilePath(lang);
                    LocalizationManager.SaveLocaleToFile(filePath, localeData);
                }

                var config = LocalizationConfigProvider.Config;
                SyncProtectionWithLanguages(config);

                _hasUnsavedChanges = false;
                ShowNotification(new GUIContent("Language data saved successfully!"));
                Debug.Log("[LocalizationEditor] Language data saved.");

                if (LocalizationManager.IsInitialized)
                {
                    LocalizationManager.Dispose();
                    LocalizationManager.Initialize();
                }
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

            string defaultLang = LocalizationConfigProvider.Config.DefaultLanguage;
            string sourceText = null;
            string sourceLang = defaultLang;

            if (keyData.TryGetValue(defaultLang, out var defaultValue) && defaultValue is string defaultStr)
            {
                sourceText = defaultStr;
            }
            else
            {
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

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                Debug.LogWarning($"The source text is empty for key '{key}', source text must be set to translate.");
                return;
            }

            string keyHint = _selectedKey == key ? _currentKeyHint : "";

            foreach (var lang in _languageCodes.Where(l => l != sourceLang))
            {
                if (!string.IsNullOrWhiteSpace(keyData[lang]?.ToString())) continue;

                try
                {
                    var translated = await TranslateText(sourceText, sourceLang, lang, keyHint);
                    if (!string.IsNullOrEmpty(translated))
                    {
                        keyData[lang] = translated;
                        _hasUnsavedChanges = true;
                        Repaint();
                    }

                    await Task.Delay(DeeplRequestDelayMs);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Translation error for {lang}: {ex.Message}");
                }
            }
        }

        private async Task<string> TranslateText(string text, string sourceLang, string targetLang, string keyHint = "")
        {
            string deeplSourceLang = sourceLang.ToUpperInvariant();
            string deeplTargetLang = targetLang.ToUpperInvariant();

            string context = DeeplContext;
            if (!string.IsNullOrWhiteSpace(keyHint))
            {
                context += $"\n\nSpecific instruction for this text: {keyHint}";
            }

            var requestBody = new DeepLTranslateRequest
            {
                text = new[] { text },
                source_lang = deeplSourceLang,
                target_lang = deeplTargetLang,
                context = context
            };

            string jsonBody = JsonUtility.ToJson(requestBody);
            var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, DeeplApiUrl);
            request.Content = content;
            request.Headers.Add("Authorization", $"DeepL-Auth-Key {DeeplApiKey}");

            try
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();

                string translated = ParseDeepLResponse(responseJson);
                return translated ?? string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogError($"DeepL translation failed: {ex.Message}");
                Debug.LogError($"Request Body: {jsonBody}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Parses DeepL JSON response to extract translated text.
        /// </summary>
        private string ParseDeepLResponse(string json)
        {
            try
            {
                var wrapper = JsonUtility.FromJson<DeepLResponseWrapper>(json);
                if (wrapper?.translations != null && wrapper.translations.Length > 0)
                {
                    return wrapper.translations[0].text;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse DeepL response: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Serializable wrapper for DeepL API response.
        /// </summary>
        [Serializable]
        private class DeepLResponseWrapper
        {
            public DeepLTranslation[] translations;
        }

        /// <summary>
        /// Single translation entry in DeepL response.
        /// </summary>
        [Serializable]
        private class DeepLTranslation
        {
            public string detected_source_language;
            public string text;
        }

        /// <summary>
        /// Request body for DeepL translate API.
        /// </summary>
        [Serializable]
        private class DeepLTranslateRequest
        {
            public string[] text;
            public string source_lang;
            public string target_lang;
            public string context;
        }

        #endregion

        #region JSON Import/Export

        /// <summary>
        /// Escapes a string for JSON output.
        /// </summary>
        private static string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Exports all localization data to a JSON file.
        /// Arrays are exported as ["value1", "value2"], strings as "value".
        /// </summary>
        private void ExportToJson()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Localization Data to JSON",
                "",
                $"Localization_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                "json");

            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("{");

                for (int i = 0; i < _keys.Count; i++)
                {
                    string key = _keys[i];
                    sb.Append($"  \"{EscapeJsonString(key)}\": {{\n");

                    if (_languageData.TryGetValue(key, out var keyData))
                    {
                        var langs = keyData.Keys.ToList();
                        for (int j = 0; j < langs.Count; j++)
                        {
                            string lang = langs[j];
                            var value = keyData[lang];

                            sb.Append($"    \"{lang}\": ");

                            if (value is List<string> arr)
                            {
                                sb.Append("[");
                                for (int k = 0; k < arr.Count; k++)
                                {
                                    sb.Append($"\"{EscapeJsonString(arr[k])}\"");
                                    if (k < arr.Count - 1) sb.Append(", ");
                                }
                                sb.Append("]");
                            }
                            else
                            {
                                sb.Append($"\"{EscapeJsonString(value?.ToString() ?? "")}\"");
                            }

                            if (j < langs.Count - 1) sb.Append(",");
                            sb.Append("\n");
                        }
                    }

                    sb.Append("  }");
                    if (i < _keys.Count - 1) sb.Append(",");
                    sb.Append("\n");
                }

                sb.AppendLine("}");
                File.WriteAllText(path, sb.ToString());

                EditorUtility.DisplayDialog("Export Successful",
                    $"Exported {_keys.Count} keys across {_languageCodes.Count} languages to:\n{path}", "OK");
                Debug.Log($"[LocalizationEditor] Exported to JSON: {path}");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Export Failed", $"Failed to export data: {ex.Message}", "OK");
                Debug.LogError($"[LocalizationEditor] JSON export failed: {ex}");
            }
        }

        /// <summary>
        /// Imports localization data from a JSON file.
        /// Supports both string and array values.
        /// </summary>
        private void ImportFromJson()
        {
            string path = EditorUtility.OpenFilePanel(
                "Import Localization Data from JSON",
                "",
                "json");

            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                var importData = ParseLocalizationJson(json);

                if (importData.Count == 0)
                {
                    EditorUtility.DisplayDialog("Import Failed", "Invalid or empty JSON file.", "OK");
                    return;
                }

                int totalKeys = importData.Count;
                int totalLangs = importData.Values.SelectMany(d => d.Keys).Distinct().Count();

                bool merge = EditorUtility.DisplayDialog("Import JSON Data",
                    $"Found {totalKeys} keys across {totalLangs} languages.\n\n" +
                    "Do you want to merge with existing data?\n\n" +
                    "Yes = Merge (keep existing keys, add/update imported)\n" +
                    "No = Replace (clear all existing data)",
                    "Merge", "Replace");

                if (!merge)
                {
                    if (!EditorUtility.DisplayDialog("Confirm Replace",
                            "This will DELETE all existing language data. Are you sure?", "Yes, Replace All", "Cancel"))
                    {
                        return;
                    }

                    _languageData.Clear();
                    _keys.Clear();
                    _languageCodes.Clear();
                }

                int importedKeys = 0;
                int importedLangs = 0;

                foreach (var kvp in importData)
                {
                    string key = kvp.Key;
                    var langData = kvp.Value;

                    if (!_languageData.TryGetValue(key, out var keyData))
                    {
                        keyData = new Dictionary<string, object>();
                        _languageData[key] = keyData;
                        _keys.Add(key);
                        importedKeys++;
                    }

                    foreach (var langKvp in langData)
                    {
                        string lang = langKvp.Key;
                        object value = langKvp.Value;

                        if (!_languageCodes.Contains(lang))
                        {
                            _languageCodes.Add(lang);
                            importedLangs++;
                        }

                        if (value is List<string> list)
                        {
                            keyData[lang] = new List<string>(list);
                        }
                        else
                        {
                            keyData[lang] = value?.ToString() ?? "";
                        }
                    }
                }

                _keys = _keys.Distinct().ToList();
                _keys.Sort();
                _languageCodes.Sort();

                _hasUnsavedChanges = true;

                EditorUtility.DisplayDialog("Import Successful",
                    $"Imported {importedKeys} new keys and {importedLangs} new languages.\n" +
                    $"Total: {_keys.Count} keys across {_languageCodes.Count} languages.", "OK");
                Debug.Log($"[LocalizationEditor] Imported from JSON: {path}");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import Failed", $"Failed to import data: {ex.Message}", "OK");
                Debug.LogError($"[LocalizationEditor] JSON import failed: {ex}");
            }
        }

        /// <summary>
        /// Parses JSON supporting both string and array values.
        /// Returns object which can be string or List<string>.
        /// </summary>
        private Dictionary<string, Dictionary<string, object>> ParseLocalizationJson(string json)
        {
            var result = new Dictionary<string, Dictionary<string, object>>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            int pos = 0;
            SkipWhitespace(json, ref pos);

            if (pos >= json.Length || json[pos] != '{')
                throw new Exception("JSON must start with {");

            pos++;
            SkipWhitespace(json, ref pos);

            if (pos < json.Length && json[pos] == '}')
                return result;

            while (pos < json.Length)
            {
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length) break;

                if (json[pos] == '}')
                {
                    pos++;
                    break;
                }

                string key = ParseJsonString(json, ref pos);
                SkipWhitespace(json, ref pos);

                if (pos >= json.Length || json[pos] != ':')
                    throw new Exception("Expected ':' after key");
                pos++;

                SkipWhitespace(json, ref pos);

                if (pos >= json.Length || json[pos] != '{')
                    throw new Exception("Expected '{' for language object");
                pos++;

                var langDict = new Dictionary<string, object>();
                SkipWhitespace(json, ref pos);

                if (pos < json.Length && json[pos] != '}')
                {
                    while (pos < json.Length)
                    {
                        SkipWhitespace(json, ref pos);
                        if (pos >= json.Length) break;

                        if (json[pos] == '}')
                        {
                            pos++;
                            break;
                        }

                        string lang = ParseJsonString(json, ref pos);
                        SkipWhitespace(json, ref pos);

                        if (pos >= json.Length || json[pos] != ':')
                            throw new Exception("Expected ':' after language code");
                        pos++;

                        SkipWhitespace(json, ref pos);

                        object value;
                        if (pos < json.Length && json[pos] == '[')
                        {
                            value = ParseJsonArray(json, ref pos);
                        }
                        else
                        {
                            value = ParseJsonString(json, ref pos);
                        }
                        langDict[lang] = value;

                        SkipWhitespace(json, ref pos);
                        if (pos < json.Length && json[pos] == ',')
                        {
                            pos++;
                            continue;
                        }
                        else if (pos < json.Length && json[pos] == '}')
                        {
                            pos++;
                            break;
                        }
                    }
                }
                else if (pos < json.Length && json[pos] == '}')
                {
                    pos++;
                }

                result[key] = langDict;

                SkipWhitespace(json, ref pos);
                if (pos < json.Length && json[pos] == ',')
                {
                    pos++;
                    continue;
                }
                else if (pos < json.Length && json[pos] == '}')
                {
                    pos++;
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Parses a JSON array of strings.
        /// </summary>
        private List<string> ParseJsonArray(string json, ref int pos)
        {
            var result = new List<string>();

            if (pos >= json.Length || json[pos] != '[')
                throw new Exception("Expected '[' to start array");
            pos++;

            SkipWhitespace(json, ref pos);

            if (pos < json.Length && json[pos] == ']')
            {
                pos++;
                return result;
            }

            while (pos < json.Length)
            {
                SkipWhitespace(json, ref pos);

                if (pos >= json.Length)
                    throw new Exception("Unexpected end of array");

                if (json[pos] == ']')
                {
                    pos++;
                    return result;
                }

                string value = ParseJsonString(json, ref pos);
                result.Add(value);

                SkipWhitespace(json, ref pos);

                if (pos < json.Length && json[pos] == ',')
                {
                    pos++;
                    continue;
                }
                else if (pos < json.Length && json[pos] == ']')
                {
                    pos++;
                    return result;
                }
            }

            throw new Exception("Unterminated array");
        }

        /// <summary>
        /// Skips whitespace characters in JSON.
        /// </summary>
        private void SkipWhitespace(string json, ref int pos)
        {
            while (pos < json.Length && char.IsWhiteSpace(json[pos]))
                pos++;
        }

        /// <summary>
        /// Parses a JSON string value (with quotes and escape sequences).
        /// </summary>
        private string ParseJsonString(string json, ref int pos)
        {
            SkipWhitespace(json, ref pos);

            if (pos >= json.Length || json[pos] != '"')
                throw new Exception("Expected string to start with quote");

            pos++;
            var sb = new System.Text.StringBuilder();

            while (pos < json.Length)
            {
                char c = json[pos];

                if (c == '"')
                {
                    pos++;
                    return sb.ToString();
                }

                if (c == '\\' && pos + 1 < json.Length)
                {
                    pos++;
                    char next = json[pos];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(next); break;
                    }
                    pos++;
                }
                else
                {
                    sb.Append(c);
                    pos++;
                }
            }

            throw new Exception("Unterminated string");
        }

        #endregion
    }
}
