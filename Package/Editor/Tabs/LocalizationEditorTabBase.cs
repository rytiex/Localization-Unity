using UnityEngine;
using UnityEditor;
using PicoShot.Localization.Editor.Data;

namespace PicoShot.Localization.Editor.Tabs
{
    /// <summary>
    /// Base class for localization editor tabs providing common functionality.
    /// </summary>
    public abstract class LocalizationEditorTabBase : ILocalizationEditorTab
    {
        protected readonly LocalizationEditor Editor;
        protected readonly LanguageEditorData Data;

        protected LocalizationEditorTabBase(LocalizationEditor editor, LanguageEditorData data)
        {
            Editor = editor;
            Data = data;
        }

        public abstract string TabName { get; }

        public virtual void OnEnter() { }

        public virtual void OnExit() { }

        public abstract void Draw();

        public virtual bool HandleKeyboardInput(Event evt) => false;

        /// <summary>
        /// Helper to draw a section header with consistent styling.
        /// </summary>
        protected static void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        /// <summary>
        /// Helper to draw a box container.
        /// </summary>
        protected static EditorGUILayout.VerticalScope BeginBox()
        {
            return new EditorGUILayout.VerticalScope("box");
        }

        /// <summary>
        /// Helper to draw a help box with a message.
        /// </summary>
        protected static void DrawHelpBox(string message, MessageType type = MessageType.Info)
        {
            EditorGUILayout.HelpBox(message, type);
        }

        /// <summary>
        /// Helper to get the window position.
        /// </summary>
        protected Rect WindowPosition => Editor.position;
    }
}
