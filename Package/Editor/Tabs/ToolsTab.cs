using System.Linq;
using UnityEditor;
using UnityEngine;
using PicoShot.Localization.Data;
using PicoShot.Localization.Editor.Data;

namespace PicoShot.Localization.Editor.Tabs
{
    /// <summary>
    /// Tab for utility tools like charset generation.
    /// </summary>
    public sealed class ToolsTab : LocalizationEditorTabBase
    {
        public ToolsTab(LocalizationEditor editor, LanguageEditorData data) : base(editor, data) { }

        public override string TabName => "Tools";

        public override void Draw()
        {
            Data.ToolsScrollPosition = EditorGUILayout.BeginScrollView(Data.ToolsScrollPosition);

            DrawSectionHeader("Charset Generation Tool");

            using (BeginBox())
            {
                Data.SyncCharsetLanguageSelection();

                EditorGUILayout.LabelField("Select Languages:", EditorStyles.boldLabel);
                Data.CharsetLanguageScrollPos = EditorGUILayout.BeginScrollView(Data.CharsetLanguageScrollPos, GUILayout.Height(200));
                foreach (var lang in Data.LanguageCodes)
                {
                    string langName = LanguageDefinitions.GetDisplayName(lang);
                    bool currentState = Data.LanguageSelectionForCharset[lang];
                    bool newState = EditorGUILayout.Toggle($"{langName} ({lang})", currentState);
                    if (newState != currentState)
                    {
                        Data.LanguageSelectionForCharset[lang] = newState;
                        Data.HasUnsavedChanges = true;
                    }
                }
                EditorGUILayout.EndScrollView();

                bool anySelected = Data.LanguageSelectionForCharset.Any(kvp => kvp.Value);
                EditorGUI.BeginDisabledGroup(!anySelected);
                if (GUILayout.Button("Generate Charsets", GUILayout.Height(30)))
                {
                    Data.GenerateCharsets();
                }
                EditorGUI.EndDisabledGroup();

                if (Data.GeneratedCharsets.Count > 0)
                {
                    DrawGeneratedCharsets();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawGeneratedCharsets()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated Charsets:", EditorStyles.boldLabel);

            foreach (var kvp in Data.GeneratedCharsets)
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
                    Editor.ShowNotification(new GUIContent($"Charset for {langName} copied to clipboard!"));
                }

                EditorGUILayout.EndVertical();
            }
        }
    }
}
