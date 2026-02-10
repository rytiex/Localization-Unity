using System.Linq;
using PicoShot.Localization.Rtl;
using UnityEditor;
using UnityEngine;
using PicoShot.Localization.Data;
using PicoShot.Localization.Editor.Data;

namespace PicoShot.Localization.Editor.Tabs
{
    /// <summary>
    /// Tab for debugging and testing localization.
    /// </summary>
    public sealed class DebugTab : LocalizationEditorTabBase
    {
        public DebugTab(LocalizationEditor editor, LanguageEditorData data) : base(editor, data) { }
        
        public override string TabName => "Debug";
        
        public override void Draw()
        {
            Data.MainScrollPosition = EditorGUILayout.BeginScrollView(Data.MainScrollPosition);
            
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
                Editor.Repaint();
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
                        Editor.Repaint();
                    }
                );
            }
            menu.DropDown(dropdownRect);
        }
        
        private void DrawSystemStatus()
        {
            Data.ShowStatusSection = EditorGUILayout.Foldout(Data.ShowStatusSection, "System Status", true);
            if (!Data.ShowStatusSection) return;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.BeginVertical(GUILayout.Width(WindowPosition.width / 2 - 10));
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
            Data.ShowTestingTools = EditorGUILayout.Foldout(Data.ShowTestingTools, "Testing Tools", true);
            if (!Data.ShowTestingTools) return;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            DrawTestSection("Simple Text Lookup", () =>
            {
                EditorGUILayout.BeginHorizontal();
                Data.TestKey = EditorGUILayout.TextField(new GUIContent("Key", "Enter the language key to test"), Data.TestKey);
                GUI.enabled = !string.IsNullOrEmpty(Data.TestKey);
                if (GUILayout.Button("Test", GUILayout.Width(60)))
                {
                    Data.TestResult = LocalizationManager.GetText(Data.TestKey);
                    GUI.FocusControl(null);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            });
            
            EditorGUILayout.Space(5);
            
            DrawTestSection("RTL Test", () =>
            {
                EditorGUILayout.BeginHorizontal();
                Data.TestRtl = EditorGUILayout.TextField(new GUIContent("Text", "Enter Arabic text to test RTL"), Data.TestRtl);
                GUI.enabled = !string.IsNullOrEmpty(Data.TestRtl);
                if (GUILayout.Button("Test", GUILayout.Width(60)))
                {
                    Data.TestResult = RtlTextHandler.Fix(Data.TestRtl);
                    GUI.FocusControl(null);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            });
            
            EditorGUILayout.Space(5);
            
            DrawTestSection("Parameterized Text", () =>
            {
                Data.TestKeyWithParams = EditorGUILayout.TextField(
                    new GUIContent("Key", "Enter the language key with parameters"),
                    Data.TestKeyWithParams);
                
                DrawParameterList();
                
                GUI.enabled = !string.IsNullOrEmpty(Data.TestKeyWithParams);
                if (GUILayout.Button("Test With Parameters", GUILayout.Height(24)))
                {
                    Data.TestResult = LocalizationManager.GetText(Data.TestKeyWithParams, Data.ParameterList.ToArray());
                    GUI.FocusControl(null);
                }
                GUI.enabled = true;
            });
            
            if (!string.IsNullOrEmpty(Data.TestResult))
            {
                DrawTestResult();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawParameterList()
        {
            Data.ShowParameterList = EditorGUILayout.Foldout(Data.ShowParameterList, "Parameters", true);
            if (Data.ShowParameterList)
            {
                EditorGUI.indentLevel++;
                for (var i = 0; i < Data.ParameterList.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    Data.ParameterList[i] = EditorGUILayout.TextField($"Param {i}", Data.ParameterList[i]);
                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        Data.ParameterList.RemoveAt(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
                
                if (GUILayout.Button("Add Parameter"))
                {
                    Data.ParameterList.Add("");
                }
            }
        }
        
        private void DrawTestResult()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Result:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.SelectableLabel(Data.TestResult, EditorStyles.wordWrappedLabel, GUILayout.Height(40));
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy", GUILayout.Width(60)))
            {
                EditorGUIUtility.systemCopyBuffer = Data.TestResult;
                Editor.ShowNotification(new GUIContent("Copied to clipboard!"));
            }
            
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                Data.TestResult = "";
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
        
        private static void DrawTestSection(string sectionTitle, System.Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(sectionTitle, EditorStyles.boldLabel);
            content?.Invoke();
            EditorGUILayout.EndVertical();
        }
    }
}
