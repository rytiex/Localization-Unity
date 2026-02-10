using UnityEngine;

namespace PicoShot.Localization.Editor.Tabs
{
    /// <summary>
    /// Interface for all localization editor tabs.
    /// Implement this to create a new tab in the editor.
    /// </summary>
    public interface ILocalizationEditorTab
    {
        /// <summary>
        /// The display name of the tab shown in the toolbar.
        /// </summary>
        string TabName { get; }

        /// <summary>
        /// Called when the tab becomes active.
        /// </summary>
        void OnEnter();

        /// <summary>
        /// Called when the tab becomes inactive.
        /// </summary>
        void OnExit();

        /// <summary>
        /// Draw the tab content.
        /// </summary>
        void Draw();

        /// <summary>
        /// Handle keyboard shortcuts specific to this tab.
        /// Return true if the event was consumed.
        /// </summary>
        bool HandleKeyboardInput(Event evt);
    }
}
