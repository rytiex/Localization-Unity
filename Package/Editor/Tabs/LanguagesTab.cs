using System.Linq;
using UnityEditor;
using UnityEngine;
using PicoShot.Localization.Config;
using PicoShot.Localization.Data;
using PicoShot.Localization.Editor.Data;

namespace PicoShot.Localization.Editor.Tabs
{
    /// <summary>
    /// Tab for managing available languages.
    /// </summary>
    public sealed class LanguagesTab : LocalizationEditorTabBase
    {
        private Vector2 _scrollPos;

        public LanguagesTab(LocalizationEditor editor, LanguageEditorData data) : base(editor, data) { }

        public override string TabName => "Languages";

        public override void Draw()
        {
            DrawSectionHeader("Manage Languages");

            using (BeginBox())
            {
                DrawSearchField();
                EditorGUILayout.Space();
                DrawLanguageList();
                DrawSummary();
            }
        }

        private void DrawSearchField()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            Data.LanguageFilter = EditorGUILayout.TextField(Data.LanguageFilter);

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                Data.LanguageFilter = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLanguageList()
        {
            EditorGUILayout.LabelField("Available Languages:", EditorStyles.boldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            var filteredLanguages = LanguageDefinitions.LanguageNames
                .Where(lang => string.IsNullOrEmpty(Data.LanguageFilter) ||
                               lang.Value.ToLower().Contains(Data.LanguageFilter.ToLower()) ||
                               lang.Key.ToLower().Contains(Data.LanguageFilter.ToLower()))
                .OrderBy(lang => lang.Value);

            string defaultLang = LocalizationConfigProvider.Config.DefaultLanguage;

            foreach (var lang in filteredLanguages)
            {
                DrawLanguageItem(lang.Key, lang.Value, defaultLang);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawLanguageItem(string code, string name, string defaultLang)
        {
            EditorGUILayout.BeginHorizontal("box");

            bool isDefault = code == defaultLang;
            bool isSelected = Data.LanguageCodes.Contains(code);

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
                        OnLanguageAdded(code);
                    else
                        OnLanguageRemoved(code);
                }
            }

            string label = isDefault
                ? $"{name} ({code}) - Default"
                : $"{name} ({code})";

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            EditorGUILayout.LabelField($"Selected Languages: {Data.LanguageCodes.Count}", EditorStyles.boldLabel);
        }

        private void OnLanguageAdded(string language)
        {
            if (Data.AddLanguage(language))
            {
                var config = LocalizationConfigProvider.Config;
                config.AddSelectedLanguage(language);
                LocalizationConfigProvider.SaveConfig();
                Editor.Repaint();
            }
        }

        private void OnLanguageRemoved(string language)
        {
            if (Data.RemoveLanguage(language))
            {
                string filePath = LocalizationManager.GetLanguageFilePath(language);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                var config = LocalizationConfigProvider.Config;
                config.RemoveSelectedLanguage(language);
                LocalizationConfigProvider.SaveConfig();

                Editor.Repaint();
            }
        }
    }
}
