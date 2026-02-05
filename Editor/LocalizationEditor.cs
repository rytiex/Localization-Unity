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
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.Compilation;

namespace DMS.Language
{
    public class LocalizationEditor : EditorWindow
    {
        #region Variables

        private const bool _compact = true;
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
        private static readonly MessagePackSerializerOptions MsgpackOptions =
            MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

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

        [Obsolete("Obsolete")]
        private void OnEnable()
        {
            LoadLanguages();
            CompilationPipeline.assemblyCompilationStarted += OnBeforeCompile;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [Obsolete("Obsolete")]
        private void OnDisable()
        {
            UnregisterEventHandlers();
            CompilationPipeline.assemblyCompilationStarted -= OnBeforeCompile;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnBeforeCompile(string assembly)
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

            if (Event.current.type != EventType.KeyDown || string.IsNullOrEmpty(_selectedKey)) return;
            bool ctrlPressed = (Event.current.modifiers & EventModifiers.Control) != 0;
            var index = _keys.IndexOf(_selectedKey);
            switch (Event.current.keyCode)
            {
                case KeyCode.UpArrow:
                    if (ctrlPressed)
                    {
                        if (index > 0)
                        {
                            _keys.RemoveAt(index);
                            _keys.Insert(index - 1, _selectedKey);
                            _hasUnsavedChanges = true;
                        }
                    }
                    else
                    {
                        if (index > 0)
                        {
                            _selectedKey = _keys[index - 1];
                        }
                    }

                    Event.current.Use();
                    Repaint();
                    break;

                case KeyCode.DownArrow:
                    if (ctrlPressed)
                    {
                        if (index < _keys.Count - 1)
                        {
                            _keys.RemoveAt(index);
                            _keys.Insert(index + 1, _selectedKey);
                            _hasUnsavedChanges = true;
                        }
                    }
                    else
                    {
                        if (index < _keys.Count - 1)
                        {
                            _selectedKey = _keys[index + 1];
                        }
                    }

                    Event.current.Use();
                    Repaint();
                    break;

                case KeyCode.Backspace:
                case KeyCode.Delete:
                    if (_pendingDelete)
                        return;
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
                    if (!ctrlPressed) return;
                    _ = TranslateAndFill(_selectedKey);
                    Event.current.Use();
                    break;

                case KeyCode.R:
                    if (!ctrlPressed) return;
                    RenameKey(_selectedKey);
                    Event.current.Use();
                    break;

                case KeyCode.S:
                    if (!ctrlPressed) return;
                    SaveLanguages();
                    Event.current.Use();
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
            var filteredLanguages = LocalizationManager.LanguageNames
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
                        {
                            AddLanguage(lang.Key);
                        }
                        else
                        {
                            DeleteLanguage(lang.Key);
                        }
                    }
                }

                EditorGUILayout.LabelField(
                    isEnglish ? $"{lang.Value} ({lang.Key}) - Default" : $"{lang.Value} ({lang.Key})",
                    EditorStyles.boldLabel);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
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
                    if (text["en"] is List<string>)
                    {
                        DrawArrayKeyContent(_selectedKey);
                    }
                    else
                    {
                        DrawStringKeyContent(_selectedKey);
                    }

                    EditorGUILayout.Space();

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Rename", GUILayout.Width(80)))
                    {
                        RenameKey(_selectedKey);
                    }

                    if (GUILayout.Button("Translation Options", GUILayout.Width(120)))
                    {
                        GenericMenu menu = new GenericMenu();

                        menu.AddItem(new GUIContent("Translate with DeepL"), false,
                            () => { _ = TranslateAndFill(_selectedKey); });

                        menu.AddItem(new GUIContent("Copy AI Translate Prompt"), false, () =>
                        {
                            if (!string.IsNullOrEmpty(_selectedKey) &&
                                _languageData.TryGetValue(_selectedKey, out var keyData))
                            {
                                string jsonData = JsonConvert.SerializeObject(keyData, Formatting.Indented);
                                string prompt =
                                    "Please translate the following language key data from English to all other languages.\n" +
                                    "Only fill in the fields that are empty (leave existing translations unchanged).\n" +
                                    "Return only the translated JSON data without any additional text.\n\n" +
                                    jsonData;
                                EditorGUIUtility.systemCopyBuffer = prompt;
                                ShowNotification(new GUIContent("Translation prompt copied to clipboard!"));
                            }
                            else
                            {
                                ShowNotification(new GUIContent("No key selected or data not found."));
                            }
                        });

                        menu.AddItem(new GUIContent("Import JSON Data from Clipboard"), false, () =>
                        {
                            if (!string.IsNullOrEmpty(_selectedKey) && _languageData.ContainsKey(_selectedKey))
                            {
                                string pastedText = EditorGUIUtility.systemCopyBuffer;
                                try
                                {
                                    bool isSelectedKeyArray = _languageData[_selectedKey]["en"] is List<string>;

                                    JObject jsonObj = JObject.Parse(pastedText);
                                    if (jsonObj == null)
                                    {
                                        ShowNotification(new GUIContent("Pasted data is not valid JSON."));
                                        return;
                                    }

                                    var newData = new Dictionary<string, object>();
                                    foreach (var prop in jsonObj.Properties())
                                    {
                                        if (prop.Value is JArray jArray && isSelectedKeyArray)
                                        {
                                            newData[prop.Name] = jArray.ToObject<List<string>>();
                                        }
                                        else if (prop.Value.Type == JTokenType.String ||
                                                 prop.Value.Type == JTokenType.Integer ||
                                                 prop.Value.Type == JTokenType.Float)
                                        {
                                            newData[prop.Name] = prop.Value.ToString();
                                        }
                                        else
                                        {
                                            if (isSelectedKeyArray)
                                            {
                                                try
                                                {
                                                    if (prop.Value.ToString().StartsWith("[") && prop.Value.ToString().EndsWith("]"))
                                                    {
                                                        var arrayValues = JArray.Parse(prop.Value.ToString());
                                                        newData[prop.Name] = arrayValues.ToObject<List<string>>();
                                                    }
                                                    else
                                                    {
                                                        EditorUtility.DisplayDialog("Type Mismatch",
                                                            $"The value for '{prop.Name}' is not an array but the key is an array type.", "OK");
                                                        continue;
                                                    }
                                                }
                                                catch
                                                {
                                                    EditorUtility.DisplayDialog("Type Mismatch",
                                                        $"Failed to convert '{prop.Name}' to an array. Skipping this language.", "OK");
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                newData[prop.Name] = prop.Value.ToString();
                                            }
                                        }
                                    }

                                    int userChoice = EditorUtility.DisplayDialogComplex(
                                        "Import Data",
                                        "Do you want to merge the new data with the existing one, or replace it completely?",
                                        "Merge",
                                        "Cancel",
                                        "Replace"
                                    );

                                    switch (userChoice)
                                    {
                                        case 0: // Merge
                                            MergeImportedData(_selectedKey, newData, isSelectedKeyArray);
                                            ShowNotification(new GUIContent("Data merged successfully."));
                                            break;

                                        case 2: // Replace
                                            if (isSelectedKeyArray)
                                            {
                                                foreach (var lang in newData.Keys)
                                                {
                                                    if (!(newData[lang] is List<string>))
                                                    {
                                                        if (newData[lang] is string strValue)
                                                        {
                                                            newData[lang] = new List<string> { strValue };
                                                        }
                                                        else
                                                        {
                                                            try
                                                            {
                                                                var jToken = JToken.FromObject(newData[lang]);
                                                                newData[lang] = jToken.ToObject<List<string>>();
                                                            }
                                                            catch
                                                            {
                                                                newData[lang] = new List<string>();
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                foreach (var lang in newData.Keys.ToList())
                                                {
                                                    if (!(newData[lang] is string))
                                                    {
                                                        if (newData[lang] is List<string> listValue && listValue.Count > 0)
                                                        {
                                                            newData[lang] = string.Join(", ", listValue);
                                                        }
                                                        else
                                                        {
                                                            newData[lang] = newData[lang]?.ToString() ?? "";
                                                        }
                                                    }
                                                }
                                            }

                                            var originalKeys = new HashSet<string>(_languageData[_selectedKey].Keys);
                                            foreach (var lang in originalKeys)
                                            {
                                                if (!newData.ContainsKey(lang))
                                                {
                                                    if (isSelectedKeyArray)
                                                    {
                                                        newData[lang] = new List<string>();
                                                    }
                                                    else
                                                    {
                                                        newData[lang] = "";
                                                    }
                                                }
                                            }

                                            _languageData[_selectedKey] = newData;
                                            ShowNotification(new GUIContent("Existing data replaced with new data."));
                                            break;

                                        default: // Cancel
                                            ShowNotification(new GUIContent("Import cancelled."));
                                            break;
                                    }

                                    _hasUnsavedChanges = true;
                                    Repaint();
                                }
                                catch (Exception ex)
                                {
                                    EditorUtility.DisplayDialog("Paste Data Error",
                                        "Failed to parse pasted data: " + ex.Message, "OK");
                                }
                            }
                            else
                            {
                                ShowNotification(new GUIContent("No key selected."));
                            }
                        });

                        menu.ShowAsContext();
                    }

                    if (GUILayout.Button("Clear", GUILayout.Width(80)))
                    {
                        if (EditorUtility.DisplayDialog("Clear Key Data",
                                $"Are you sure you want to clear all translations for key '{_selectedKey}'?\nThis cannot be undone!",
                                "Yes, Clear", "Cancel"))
                        {
                            if (_languageData.TryGetValue(_selectedKey, out var keyData))
                            {
                                foreach (var lang in keyData.Keys.ToList())
                                {
                                    _languageData[_selectedKey][lang] = keyData[lang] switch
                                    {
                                        string => "",
                                        List<string> list =>
                                            new List<string>(new string[list.Count]),
                                        _ => _languageData[_selectedKey][lang]
                                    };
                                }

                                ShowNotification(new GUIContent("All translations cleared."));
                                _hasUnsavedChanges = true;
                                Repaint();
                            }
                        }
                    }


                    if (GUILayout.Button("Delete", GUILayout.Width(80)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Key",
                                $"Are you sure you want to delete the key '{_selectedKey}'?", "Yes", "No"))
                        {
                            DeleteKey(_selectedKey);
                            _selectedKey = "";
                        }
                    }

                    EditorGUILayout.EndHorizontal();
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

        private void MergeImportedData(string key, Dictionary<string, object> newData, bool isArrayKey)
        {
            foreach (var lang in newData.Keys)
            {
                if (!_languageData[key].ContainsKey(lang))
                    continue;

                if (isArrayKey)
                {
                    if (_languageData[key][lang] is List<string> currentList)
                    {
                        if (newData[lang] is List<string> newList)
                        {
                            for (int i = 0; i < newList.Count; i++)
                            {
                                if (i < currentList.Count)
                                {
                                    if (string.IsNullOrWhiteSpace(currentList[i]) &&
                                        !string.IsNullOrWhiteSpace(newList[i]))
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
                        else if (newData[lang] is string strValue && !string.IsNullOrWhiteSpace(strValue))
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
                }
                else
                {
                    if (_languageData[key][lang] is string currentText)
                    {
                        string newText;

                        if (newData[lang] is string newStr)
                        {
                            newText = newStr;
                        }
                        else if (newData[lang] is List<string> newList && newList.Count > 0)
                        {
                            newText = newList[0];
                        }
                        else
                        {
                            newText = newData[lang]?.ToString() ?? "";
                        }

                        if (string.IsNullOrWhiteSpace(currentText) && !string.IsNullOrWhiteSpace(newText))
                        {
                            _languageData[key][lang] = newText;
                        }
                    }
                }
            }
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

            int startIndex = Mathf.FloorToInt(_keysListScroll.y / _keyItemHeight);
            startIndex = Mathf.Max(0, startIndex);
            int endIndex = Mathf.Min(startIndex + maxVisibleItems, totalKeyCount);

            for (int i = startIndex; i < endIndex; i++)
            {
                string key = filteredKeys[i];
                Rect keyRect = new Rect(4, i * _keyItemHeight, listPanelWidth - 8, _keyItemHeight);

                GUIStyle keyStyle = GetKeyButtonStyle(key == _selectedKey);
                string typeIndicator = _languageData[key]["en"] is List<string> ? "[ ]" : "Aa";
                string buttonLabel = $"<color=#888888>{typeIndicator}</color> {key}";

                if (GUI.Button(keyRect, buttonLabel, keyStyle))
                {
                    _selectedKey = key;
                }
            }

            GUI.EndScrollView();
            EditorGUILayout.EndVertical();
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
                hover =
                {
                    textColor = Color.green
                },
                active =
                {
                    textColor = Color.green
                },
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

            var currentLang = LocalizationManager.GetCurrentLanguage();
            var content = new GUIContent(LocalizationManager.GetLanguageDisplayName(currentLang));

            var dropdownRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));

            if (EditorGUI.DropdownButton(dropdownRect, content, FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                foreach (var lang in LocalizationManager.GetAvailableLanguages(false))
                {
                    menu.AddItem(
                        new GUIContent(LocalizationManager.GetLanguageDisplayName(lang)),
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

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("System Language:", GUILayout.Width(120));
            EditorGUILayout.LabelField(LocalizationManager.GetLanguageDisplayName(LocalizationManager.GetSystemLanguage()));
            if (GUILayout.Button("Use System Language", GUILayout.Width(150)))
            {
                LocalizationManager.SetLanguage(LocalizationManager.DetectLanguageFromSystemLocale());
                Repaint();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSystemStatus()
        {
            _showStatusSection = EditorGUILayout.Foldout(_showStatusSection, "System Status", true);
            if (!_showStatusSection) return;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
            DrawStatusField("Initialized", LocalizationManager.IsInitialized().ToString(), MessageType.Info);
            DrawStatusField("Current Language", LocalizationManager.GetCurrentLanguage(), MessageType.Info);
            DrawStatusField("Fallback Language", LocalizationManager.FallbackLanguage, MessageType.Info);
            DrawStatusField("Is RTL Language", LocalizationManager.IsRightToLeft().ToString(), MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            DrawStatusField("Available Languages", LocalizationManager.GetAvailableLanguages(true).Count().ToString(),
                MessageType.Info);
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
                _testKey = EditorGUILayout.TextField(new GUIContent("Key", "Enter the language key to test"),
                    _testKey);
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
                _testRtl = EditorGUILayout.TextField(new GUIContent("Text", "Enter the language key to rtl"),
                    _testRtl);
                GUI.enabled = !string.IsNullOrEmpty(_testRtl);
                if (GUILayout.Button("Test", GUILayout.Width(60)))
                {
                    _testResult = LocalizationRtlManager.Fix(_testRtl);
                    GUI.FocusControl(null);
                }

                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            });

            EditorGUILayout.Space(5);

            DrawTestSection("Parameterized Text", () =>
            {
                _testKeyWithParams =
                    EditorGUILayout.TextField(new GUIContent("Key", "Enter the language key with parameters"),
                        _testKeyWithParams);

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

            EditorGUILayout.EndVertical();
        }

        private void DrawStatusField(string label, string value, MessageType type = MessageType.None)
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
                    if (!rect.Contains(evt.mousePosition))
                        break;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    _dragAreaColor = new Color(0.1f, 1f, 0.1f);

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is not GameObject go) continue;
                            _selectedGameObject = go;
                            break;
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
            _showExistingComponents =
                EditorGUILayout.Foldout(_showExistingComponents, "Existing Language Components", true);

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
                var allComponents =
                    FindObjectsByType<LocalizationTextComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (allComponents.Length == 0)
                {
                    EditorGUILayout.HelpBox("No language components found in the scene.", MessageType.Info);
                }
                else
                {
                    var filteredComponents = allComponents
                        .Where(c => string.IsNullOrEmpty(_componentSearchFilter) ||
                                    c.gameObject.name.ToLower().Contains(_componentSearchFilter.ToLower()) ||
                                    c.languageKey.ToLower().Contains(_componentSearchFilter.ToLower()))
                        .OrderBy(c => c.gameObject.name)
                        .ToList();

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField($"Found {filteredComponents.Count} components",
                        EditorStyles.miniBoldLabel);

                    _existingComponentsScrollPos =
                        EditorGUILayout.BeginScrollView(_existingComponentsScrollPos, GUILayout.Height(500));

                    foreach (var component in filteredComponents)
                    {
                        DrawExistingComponentEntry(component);
                    }

                    EditorGUILayout.EndScrollView();
                }
            }

            EditorGUILayout.EndVertical();
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
            EditorGUILayout.LabelField($"Key: {component.languageKey}", EditorStyles.miniLabel, GUILayout.Width(200));

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
            string parentName = go.transform.parent.name;
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
                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Language Key:", GUILayout.Width(100));

                var buttonRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));
                var selectedIndex = _keys.IndexOf(langComponent.languageKey);
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
                        langComponent.languageKey = _keys[index];
                        EditorUtility.SetDirty(langComponent);
                    });
                }

                EditorGUILayout.EndHorizontal();

                if (langComponent.languageKey != null &&
                    _languageData.ContainsKey(langComponent.languageKey) &&
                    _languageData[langComponent.languageKey]["en"] is List<string> array)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Array Index:", GUILayout.Width(100));

                    var newArrayIndex = EditorGUILayout.IntSlider(
                        langComponent.arrayIndex,
                        -1,
                        array.Count - 1);

                    EditorGUILayout.LabelField("Array Size Limit:", GUILayout.Width(100));

                    var newArraySizeLimit = EditorGUILayout.IntSlider(
                        langComponent.arraySizeLimit,
                        0,
                        array.Count);

                    langComponent.arraySizeLimit = newArraySizeLimit;

                    if (newArrayIndex != langComponent.arrayIndex)
                    {
                        Undo.RecordObject(langComponent, "Change Array Index");
                        langComponent.arrayIndex = newArrayIndex;
                        EditorUtility.SetDirty(langComponent);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space();
                EditorGUI.BeginDisabledGroup(true);
                switch (component)
                {
                    case TMP_Text text:
                        EditorGUILayout.TextField("Current Text:", text.text);
                        break;
                    case TMP_Dropdown dropdown:
                        EditorGUILayout.TextField("Options Count:", dropdown.options.Count.ToString());
                        break;
                }

                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical();
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
            var newShowStringKeys =
                EditorGUILayout.ToggleLeft("String Keys", _showStringKeysOnly, GUILayout.Width(100));

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
                var langName = LocalizationManager.LanguageNames.GetValueOrDefault(lang, lang);
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
                var enArray = (List<string>)_languageData[key]["en"];
                enArray.Add("");
                foreach (var lang in _languageCodes.Where(l => l != "en"))
                {
                    if (_languageData[key][lang] is List<string> otherArray)
                    {
                        otherArray.Add("");
                    }
                }
            }

            if (GUILayout.Button("Clear Empty Elements", GUILayout.Width(140)))
            {
                ClearEmptyArrayElements(key);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            var array = (List<string>)_languageData[key]["en"];
            var elementDeleted = false;
            var deleteIndex = -1;

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

                foreach (var lang in _languageCodes)
                {
                    var langName = LocalizationManager.LanguageNames.GetValueOrDefault(lang, lang);
                    EditorGUILayout.LabelField($"{langName}:", GUILayout.Width(120));

                    var langArray = (List<string>)_languageData[key][lang];
                    var currentText = langArray[i] ?? "";

                    Rect textRect = EditorGUILayout.GetControlRect();
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUI.TextField(textRect, currentText);
                    EditorGUI.EndDisabledGroup();

                    if (Event.current.type == EventType.MouseDown &&
                        Event.current.clickCount == 2 &&
                        textRect.Contains(Event.current.mousePosition))
                    {
                        int capturedIndex = i;
                        OpenTextEditor(currentText, (newText) =>
                        {
                            langArray[capturedIndex] = newText;
                            _hasUnsavedChanges = true;
                            Repaint();
                        });
                        Event.current.Use();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();

            if (elementDeleted && deleteIndex >= 0)
            {
                DeleteArrayElement(key, deleteIndex);
            }
        }

        private void DrawToolsTab()
        {
            _toolsScrollPosition = EditorGUILayout.BeginScrollView(_toolsScrollPosition);

            EditorGUILayout.LabelField("Charset Generation Tool", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");

            foreach (var lang in _languageCodes.Where(lang => !_languageSelectionForCharset.ContainsKey(lang)))
            {
                _languageSelectionForCharset[lang] = false;
            }

            var currentLanguages = new HashSet<string>(_languageCodes);
            foreach (var lang in _languageSelectionForCharset.Keys.ToList()
                         .Where(lang => !currentLanguages.Contains(lang)))
            {
                _languageSelectionForCharset.Remove(lang);
            }

            EditorGUILayout.LabelField("Select Languages:", EditorStyles.boldLabel);
            _charsetLanguageScrollPos =
                EditorGUILayout.BeginScrollView(_charsetLanguageScrollPos, GUILayout.Height(200));
            foreach (var lang in _languageCodes)
            {
                string langName = LocalizationManager.LanguageNames.GetValueOrDefault(lang, lang);
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
            GUI.enabled = anySelected;
            if (GUILayout.Button("Generate Charsets", GUILayout.Height(30)))
            {
                _generatedCharsets.Clear();
                foreach (var lang in _languageCodes.Where(l => _languageSelectionForCharset[l]))
                {
                    string charset = GenerateCharsetForLanguage(lang);
                    _generatedCharsets[lang] = charset;
                }

                _hasUnsavedChanges = true;
            }

            GUI.enabled = true;

            if (_generatedCharsets.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Generated Charsets:", EditorStyles.boldLabel);
                foreach (var kvp in _generatedCharsets)
                {
                    string lang = kvp.Key;
                    string charset = kvp.Value;
                    string langName = LocalizationManager.LanguageNames.GetValueOrDefault(lang, lang);

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

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private string GenerateCharsetForLanguage(string language)
        {
            var charSet = new HashSet<char>();
            foreach (var key in _keys)
            {
                if (_languageData[key].TryGetValue(language, out var value))
                {
                    switch (value)
                    {
                        case string str:
                            {
                                foreach (var c in str)
                                {
                                    charSet.Add(c);
                                }

                                break;
                            }
                        case List<string> list:
                            {
                                foreach (var c in list.Where(item => item != null).SelectMany(item => item))
                                {
                                    charSet.Add(c);
                                }

                                break;
                            }
                    }
                }
            }

            return new string(charSet.ToArray());
        }

        private void DrawConfigTab()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("DeepL API Settings", EditorStyles.boldLabel);
            //EditorGUILayout.TextField("API URL:", DeeplApiUrl, EditorStyles.textField);
            EditorGUILayout.TextField("API Key:", DeeplApiKey, EditorStyles.textField);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("DMSL Settings", EditorStyles.boldLabel);
            EditorGUILayout.TextField("File Path:", "{StreamingAssets}" + LocalizationManager.LanguagesFilePath,
                EditorStyles.textField);
            EditorGUILayout.Toggle("LZ4 Compression", _compact);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Import/Export As Json", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Export JSON", GUILayout.Height(25)))
            {
                ExportLanguages();
            }

            if (GUILayout.Button("Import JSON", GUILayout.Height(25)))
            {
                ImportLanguages();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("File Operations", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Delete DMSL Data", GUILayout.Height(25)))
            {
                PurgeAllData();
            }

            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Open DMSL File", GUILayout.Height(25)))
            {
                OpenLanguageFile();
            }


            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("File Path: " + LocalizationManager.DmsFilePath, MessageType.Info);

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
                query = query.Where(key => _languageData[key]["en"] is List<string>);
            }
            else if (_showStringKeysOnly)
            {
                query = query.Where(key => _languageData[key]["en"] is string);
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
            var enArray = (List<string>)_languageData[key]["en"];
            for (var i = enArray.Count - 1; i >= 0; i--)
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
            LocalizationManager.Cleanup();
        }

        private static void OpenLanguageFile()
        {
            if (!File.Exists(LocalizationManager.DmsFilePath))
            {
                EditorUtility.DisplayDialog("Error",
                    "Language file does not exist yet.\nSave some data first to create the file.",
                    "OK");
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(LocalizationManager.DmsFilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error opening language file: {e} trying to open with other editors.");
                try
                {
                    switch (Application.platform)
                    {
                        case RuntimePlatform.WindowsEditor:
                            System.Diagnostics.Process.Start("notepad.exe", LocalizationManager.DmsFilePath);
                            break;
                        case RuntimePlatform.OSXEditor:
                            System.Diagnostics.Process.Start("open", LocalizationManager.DmsFilePath);
                            break;
                        default:
                            System.Diagnostics.Process.Start("xdg-open", LocalizationManager.DmsFilePath);
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
        }

        private void AddLanguage(string language)
        {
            if (!_languageCodes.Contains(language))
            {
                _languageCodes.Add(language);
                foreach (var key in _keys)
                {
                    switch (_languageData[key]["en"])
                    {
                        case string:
                            _languageData[key][language] = "";
                            break;
                        case List<string> enArray:
                            {
                                var newArray = new List<string>();
                                for (var i = 0; i < enArray.Count; i++)
                                {
                                    newArray.Add("");
                                }

                                _languageData[key][language] = newArray;
                                break;
                            }
                    }
                }

                GUI.changed = true;
                Repaint();
            }
            else
            {
                Debug.LogWarning($"Language '{language}' already exists.");
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
            if (!string.IsNullOrEmpty(key))
            {
                if (!_keys.Contains(key))
                {
                    _keys.Add(key);
                    _languageData[key] = new Dictionary<string, object>();
                    foreach (var lang in _languageCodes)
                    {
                        _languageData[key][lang] = isArray ? new List<string>() : "";
                    }

                    _selectedKey = key;
                }
                else
                {
                    Debug.LogWarning($"Key '{key}' already exists.");
                }

                _hasUnsavedChanges = true;
                Repaint();
            }
            else
            {
                Debug.LogWarning("Key can't be empty string.");
            }
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

        private void RenameKey(string key)
        {
            if (_keys.Contains(key))
            {
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

                    var translations = _languageData[key];
                    var currentIndex = _keys.IndexOf(key);

                    _keys.RemoveAt(currentIndex);
                    _languageData.Remove(key);

                    _keys.Insert(currentIndex, newKey);
                    _languageData[newKey] = translations;

                    if (_keyFoldouts.ContainsKey(key))
                    {
                        var foldoutState = _keyFoldouts[key];
                        _keyFoldouts.Remove(key);
                        _keyFoldouts[newKey] = foldoutState;
                    }

                    Debug.Log($"Key renamed from '{key}' to '{newKey}'");
                    GUI.changed = true;
                    _hasUnsavedChanges = true;
                    Repaint();
                });
            }
        }

        private static void OpenTextEditor(string initialText, Action<string> onSave)
        {
            LocalizationTextEditorPopup.Open(initialText, onSave);
        }

        #endregion

        #region File Management Functions

        private void PromptAutoSave(string reason)
        {
            if (_hasUnsavedChanges)
            {
                bool save = EditorUtility.DisplayDialog(
                    "Auto-Save Confirmation",
                    $"You have unsaved changes. Do you want to save {reason}?",
                    "Save", "Discard"
                );

                if (save)
                {
                    SaveWork();
                }
                else
                {
                    _hasUnsavedChanges = false;
                    Repaint();
                }
            }
        }

        private void SaveWork()
        {
            SaveLanguages();
            _hasUnsavedChanges = false;
        }

        private void ExportLanguages()
        {
            var path = EditorUtility.SaveFilePanel(
                "Export Languages",
                Application.dataPath,
                "languages.json",
                "json"
            );

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var jsonString = JsonConvert.SerializeObject(_languageData, Formatting.Indented);

                    File.WriteAllText(path, jsonString);

                    Debug.Log($"Languages exported successfully to: {path}");
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Export Error",
                        $"Failed to export languages: {e.Message}", "OK");
                    Debug.LogError($"Export error: {e}");
                }
            }
        }


        private void ImportLanguages()
        {
            var path = EditorUtility.OpenFilePanel(
                "Import Languages",
                Application.dataPath,
                "json"
            );

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var jsonToMessagePack = MessagePackSerializer.ConvertFromJson(json);
                    var importedData =
                        MessagePackSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(
                            jsonToMessagePack, options: MsgpackOptions);

                    if (importedData == null)
                    {
                        throw new Exception("Invalid JSON format");
                    }

                    foreach (var key in importedData.Keys.ToList())
                    {
                        foreach (var lang in importedData[key].Keys.ToList())
                        {
                            if (importedData[key][lang] is object[] objArray)
                            {
                                importedData[key][lang] = objArray.Select(x => x?.ToString()).ToList();
                            }
                        }
                    }

                    if (EditorUtility.DisplayDialog("Import Languages",
                            "Do you want to merge with existing data or replace it completely?\n\n" +
                            "- Click 'Merge' to add new entries and update existing ones\n" +
                            "- Click 'Replace' to remove all existing data and import new data",
                            "Merge", "Replace"))
                    {
                        foreach (var entry in importedData)
                        {
                            if (!_languageData.ContainsKey(entry.Key))
                            {
                                _keys.Add(entry.Key);
                            }

                            _languageData[entry.Key] = entry.Value;

                            foreach (var lang in entry.Value.Keys.Where(lang => !_languageCodes.Contains(lang)))
                            {
                                _languageCodes.Add(lang);
                            }
                        }
                    }
                    else
                    {
                        _languageData = importedData;
                        _keys = new List<string>(importedData.Keys);
                        _languageCodes.Clear();
                        foreach (var lang in from entry in importedData
                                             from lang in entry.Value.Keys
                                             where !_languageCodes.Contains(lang)
                                             select lang)
                        {
                            _languageCodes.Add(lang);
                        }
                    }

                    Debug.Log($"Languages imported successfully from: {path}");
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Import Error",
                        $"Failed to import languages: {e.Message}", "OK");
                    Debug.LogError($"Import error: {e}");
                }
            }
        }

        private void LoadLanguages()
        {
            if (File.Exists(LocalizationManager.DmsFilePath))
            {
                var loadedData =
                    LocalizationManager.LoadDmsl<Dictionary<string, Dictionary<string, object>>>(
                        LocalizationManager.DmsFilePath);

                foreach (var key in loadedData.Keys.ToList())
                {
                    foreach (var lang in loadedData[key].Keys.ToList())
                    {
                        if (loadedData[key][lang] is object[] objArray)
                        {
                            loadedData[key][lang] = objArray.Select(x => x?.ToString()).ToList();
                        }
                    }
                }

                foreach (var lang in from entry in loadedData
                                     from lang in entry.Value.Keys
                                     where !_languageCodes.Contains(lang)
                                     select lang)
                {
                    _languageCodes.Add(lang);
                }

                _languageData = loadedData;
                _keys = new List<string>(_languageData.Keys);
            }
            else
            {
                Debug.LogWarning("Language file not found. A new one will be created on save.");
            }
        }

        private void SaveLanguages()
        {
            foreach (var key in _languageData.Keys.ToList())
            {
                foreach (var lang in _languageData[key].Keys.ToList())
                {
                    if (_languageData[key][lang] is object[] objArray)
                    {
                        _languageData[key][lang] = objArray.Select(x => x?.ToString()).ToList();
                    }
                }
            }

            var orderedData = new Dictionary<string, Dictionary<string, object>>();
            foreach (var key in _keys)
            {
                if (_languageData.TryGetValue(key, out var text))
                {
                    orderedData.Add(key, text);
                }
            }

            LocalizationManager.SaveDmsl(LocalizationManager.DmsFilePath, orderedData);
            AssetDatabase.Refresh();
            Debug.Log("Languages saved.");
            _hasUnsavedChanges = false;
            Repaint();
        }

        #endregion

        #region Translation Functions

        private string ProtectPlaceholders(string text, out Dictionary<string, string> placeholderMap)
        {
            placeholderMap = new Dictionary<string, string>();
            var regex = new Regex(@"\{(\d+)\}");
            var map = placeholderMap;
            var protectedText = regex.Replace(text, match =>
            {
                var token = $"<x>{match.Value}</x>";
                map[token] = match.Value;
                return token;
            });
            return protectedText;
        }

        private string RestorePlaceholders(string text, Dictionary<string, string> placeholderMap)
        {
            return placeholderMap.Keys.Aggregate(text,
                (current, token) => current.Replace(token, placeholderMap[token]));
        }

        private async Task<string> TranslateText(string text, string targetLanguageCode)
        {
            var protectedText = ProtectPlaceholders(text, out var placeholderMap);

            var requestData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("auth_key", DeeplApiKey),
                new KeyValuePair<string, string>("text", protectedText),
                new KeyValuePair<string, string>("source_lang", "EN"),
                new KeyValuePair<string, string>("target_lang", targetLanguageCode),
                new KeyValuePair<string, string>("tag_handling", "xml")
            });

            try
            {
                var response = await _httpClient.PostAsync(DeeplApiUrl, requestData);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);

                var translation = json["translations"]?[0]?["text"]?.ToString();

                return translation != null
                    ? RestorePlaceholders(translation, placeholderMap)
                    : "Translation not available";
            }
            catch (HttpRequestException ex)
            {
                Debug.LogError($"Request error: {ex.Message}");
                return "";
            }
        }

        private async Task TranslateAndFill(string key)
        {
            if (!_languageData.ContainsKey(key)) return;

            foreach (var lang in _languageCodes.Where(lang => lang != "en"))
            {
                switch (_languageData[key]["en"])
                {
                    case string defaultText when !string.IsNullOrWhiteSpace(defaultText):
                        {
                            if (_languageData[key][lang] is string currentText && string.IsNullOrWhiteSpace(currentText))
                            {
                                var translatedText = await TranslateText(defaultText, lang);
                                _languageData[key][lang] = translatedText;
                            }

                            break;
                        }
                    case List<string> defaultArray when _languageData[key][lang] is List<string> currentArray:
                        {
                            for (var i = 0; i < defaultArray.Count; i++)
                            {
                                var defaultItem = defaultArray[i] ?? "";

                                if (i >= currentArray.Count)
                                {
                                    currentArray.Add("");
                                }

                                if (!string.IsNullOrWhiteSpace(currentArray[i])) continue;

                                var translatedText = await TranslateText(defaultItem, lang);
                                currentArray[i] = translatedText;
                                Task.Delay(400).Wait();
                            }

                            break;
                        }
                }

                Task.Delay(400).Wait();
                Repaint();
            }

            Debug.Log($"Translations updated for key: {key}");
        }

        #endregion
    }
}
