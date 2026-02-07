using System;
using System.Collections.Generic;
using System.Linq;

namespace PicoShot.Localization.Data
{
    /// <summary>
    /// Represents a single locale file containing translations for one language.
    /// This is the runtime format used by BLOC files.
    /// 
    /// Key: Translation key (e.g., "ui.play_button")
    /// Value: String or List&lt;string&gt; for arrays
    /// </summary>
    [Serializable]
    public sealed class LocaleData
    {
        /// <summary>
        /// Version of the locale file format.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// ISO language code (e.g., "en", "ar", "fr")
        /// </summary>
        public string LanguageCode { get; set; } = "en";

        /// <summary>
        /// Timestamp when the file was generated (Unix seconds).
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// The translation data for this locale.
        /// Key: Translation key
        /// Value: String or List&lt;string&gt; for arrays
        /// </summary>
        public Dictionary<string, object> Translations { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets the number of translations.
        /// </summary>
        public int Count => Translations?.Count ?? 0;

        /// <summary>
        /// Checks if a key exists.
        /// </summary>
        public bool ContainsKey(string key) => Translations?.ContainsKey(key) ?? false;

        /// <summary>
        /// Gets a translation value by key.
        /// </summary>
        public object GetValue(string key) => 
            Translations?.TryGetValue(key, out var value) ?? false ? value : null;

        /// <summary>
        /// Gets a string translation by key.
        /// Returns first element if value is an array.
        /// </summary>
        public string GetString(string key)
        {
            var value = GetValue(key);
            return value switch
            {
                string s => s,
                List<string> list => list.Count > 0 ? list[0] : null,
                string[] arr => arr.Length > 0 ? arr[0] : null,
                _ => null
            };
        }

        /// <summary>
        /// Gets an array translation by key.
        /// </summary>
        public List<string> GetArray(string key)
        {
            var value = GetValue(key);
            return value switch
            {
                List<string> list => list,
                string[] arr => new List<string>(arr),
                string s => new List<string> { s },
                _ => null
            };
        }

        /// <summary>
        /// Sets a string translation.
        /// </summary>
        public void SetString(string key, string value)
        {
            Translations ??= new Dictionary<string, object>(StringComparer.Ordinal);
            Translations[key] = value ?? string.Empty;
        }

        /// <summary>
        /// Sets an array translation.
        /// </summary>
        public void SetArray(string key, IEnumerable<string> values)
        {
            Translations ??= new Dictionary<string, object>(StringComparer.Ordinal);
            Translations[key] = values?.ToList() ?? new List<string>();
        }

        /// <summary>
        /// Removes a translation key.
        /// </summary>
        public bool Remove(string key) => Translations?.Remove(key) ?? false;

        /// <summary>
        /// Clears all translations.
        /// </summary>
        public void Clear() => Translations?.Clear();
    }
}
