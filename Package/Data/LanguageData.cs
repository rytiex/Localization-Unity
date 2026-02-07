using System;
using System.Collections.Generic;

namespace PicoShot.Localization.Data
{
    /// <summary>
    /// Multi-language data structure for editor use.
    /// This is used by the editor to manage translations across multiple languages.
    /// At runtime, each language is stored separately in LocaleData/BLOC files.
    /// </summary>
    [Serializable]
    public sealed class LanguageData
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
        /// The translation data.
        /// Key: Translation key (e.g., "ui.play_button")
        /// Value: Dictionary of language code -> translation value
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> Translations { get; set; } = new();

        /// <summary>
        /// Converts this multi-language data to a list of single-language LocaleData objects.
        /// Used when saving to BLOC files.
        /// </summary>
        public List<LocaleData> ToLocaleDataList()
        {
            var result = new List<LocaleData>();
            var languageCodes = GetAllLanguageCodes();

            foreach (var langCode in languageCodes)
            {
                var localeData = new LocaleData
                {
                    Version = 1,
                    LanguageCode = langCode,
                    Timestamp = Timestamp
                };

                foreach (var kvp in Translations)
                {
                    string key = kvp.Key;
                    if (kvp.Value.TryGetValue(langCode, out var value))
                    {
                        localeData.Translations[key] = value;
                    }
                }

                result.Add(localeData);
            }

            return result;
        }

        /// <summary>
        /// Gets all unique language codes in this data.
        /// </summary>
        public HashSet<string> GetAllLanguageCodes()
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var translation in Translations.Values)
            {
                foreach (var langCode in translation.Keys)
                {
                    codes.Add(langCode);
                }
            }
            return codes;
        }
    }
}
