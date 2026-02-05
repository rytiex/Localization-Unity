using System;
using System.Collections.Generic;

namespace PicoShot.Localization.Data
{
    /// <summary>
    /// Represents a single locale file containing translations for one language.
    /// Key: Translation key (e.g., "ui.play_button")
    /// Value: String or string array
    /// </summary>
    [Serializable]
    public class LocaleData
    {
        /// <summary>
        /// Version of the locale file format.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// ISO language code (e.g., "en", "ar", "fr")
        /// </summary>
        public string LanguageCode { get; set; }

        /// <summary>
        /// Timestamp when the file was generated.
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// The translation data for this locale.
        /// Key: Translation key
        /// Value: String or string array
        /// </summary>
        public Dictionary<string, object> Translations { get; set; } = new();
    }
}
