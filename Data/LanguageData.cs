using System;
using System.Collections.Generic;

namespace PicoShot.Localization.Data
{
    /// <summary>
    /// Represents a single translation entry with all language variants.
    /// Key: Language code (e.g., "en", "ar")
    /// Value: String or string array
    /// </summary>
    [Serializable]
    public class TranslationEntry
    {
        public string Key { get; set; }
        public Dictionary<string, object> Translations { get; set; } = new();
    }

    /// <summary>
    /// Root data structure for the language file.
    /// Format: Dictionary<TranslationKey, Dictionary<LanguageCode, Value>>
    /// </summary>
    [Serializable]
    public class LanguageData
    {
        /// <summary>
        /// Version of the language file format.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Timestamp when the file was generated.
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// The actual translation data.
        /// Key: Translation key (e.g., "ui.play_button")
        /// Value: Dictionary of language code -> translation value
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> Translations { get; set; } = new();
    }
}
