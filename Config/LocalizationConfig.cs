using System.Collections.Generic;
using UnityEngine;

namespace PicoShot.Localization.Config
{
    /// <summary>
    /// Configuration asset for localization system.
    /// </summary>
    public class LocalizationConfig : ScriptableObject
    {
        [Tooltip("Default language code to use when system language is not available")]
        [SerializeField]
        private string _defaultLanguage = "en";

        [Tooltip("Enable protection against file tampering. Only loads selected languages.")]
        [SerializeField]
        private bool _protectionEnabled = false;

        [Tooltip("Languages allowed to load when protection is enabled")]
        [SerializeField]
        private List<string> _selectedLanguages = new() { "en" };

        /// <summary>
        /// Default language code.
        /// </summary>
        public string DefaultLanguage => _defaultLanguage;

        /// <summary>
        /// Whether protection against tampering is enabled.
        /// </summary>
        public bool ProtectionEnabled => _protectionEnabled;

        /// <summary>
        /// Languages allowed to load when protection is enabled.
        /// </summary>
        public IReadOnlyList<string> SelectedLanguages => _selectedLanguages;

        #region Editor Only

#if UNITY_EDITOR

        /// <summary>
        /// Sets the default language (Editor only).
        /// </summary>
        public void SetDefaultLanguage(string languageCode)
        {
            _defaultLanguage = languageCode;
        }

        /// <summary>
        /// Sets protection enabled state (Editor only).
        /// </summary>
        public void SetProtectionEnabled(bool enabled)
        {
            _protectionEnabled = enabled;
        }

        /// <summary>
        /// Sets the selected languages list (Editor only).
        /// </summary>
        public void SetSelectedLanguages(List<string> languages)
        {
            _selectedLanguages = new List<string>(languages);
        }

        /// <summary>
        /// Adds a language to selected languages (Editor only).
        /// </summary>
        public void AddSelectedLanguage(string languageCode)
        {
            if (!_selectedLanguages.Contains(languageCode))
            {
                _selectedLanguages.Add(languageCode);
            }
        }

        /// <summary>
        /// Removes a language from selected languages (Editor only).
        /// </summary>
        public void RemoveSelectedLanguage(string languageCode)
        {
            _selectedLanguages.Remove(languageCode);
        }

#endif

        #endregion
    }
}
