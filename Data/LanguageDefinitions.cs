using System;
using System.Collections.Generic;

namespace PicoShot.Localization.Data
{
    /// <summary>
    /// Contains language metadata including display names, native names, and fallback mappings.
    /// </summary>
    public static class LanguageDefinitions
    {
        public const string DefaultLanguage = "en";
        public const string FallbackLanguage = "en";

        /// <summary>
        /// Maps specific locale codes to their more general fallback versions.
        /// </summary>
        public static readonly Dictionary<string, string> FallbackLanguages = new(StringComparer.OrdinalIgnoreCase)
        {
            { "zh-CN", "zh" },
            { "zh-SG", "zh" },
            { "zh-TW", "zh-Hant" },
            { "zh-HK", "zh-Hant" },
            { "pt-BR", "pt" },
            { "pt-PT", "pt" },
            { "es-MX", "es" },
            { "es-ES", "es" },
            { "es-AR", "es" },
            { "es-CO", "es" },
            { "fr-CA", "fr" },
            { "fr-BE", "fr" },
            { "fr-CH", "fr" },
            { "de-AT", "de" },
            { "de-CH", "de" },
            { "en-US", "en" },
            { "en-GB", "en" },
            { "en-AU", "en" },
            { "en-CA", "en" }
        };

        /// <summary>
        /// English display names for languages.
        /// </summary>
        public static readonly Dictionary<string, string> LanguageNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ar", "Arabic" },
            { "bg", "Bulgarian" },
            { "zh", "Chinese Simplified" },
            { "zh-Hant", "Chinese Traditional" },
            { "cs", "Czech" },
            { "da", "Danish" },
            { "nl", "Dutch" },
            { "en", "English" },
            { "et", "Estonian" },
            { "fi", "Finnish" },
            { "fr", "French" },
            { "de", "German" },
            { "el", "Greek" },
            { "he", "Hebrew" },
            { "hu", "Hungarian" },
            { "id", "Indonesian" },
            { "it", "Italian" },
            { "ja", "Japanese" },
            { "ko", "Korean" },
            { "lv", "Latvian" },
            { "lt", "Lithuanian" },
            { "nb", "Norwegian" },
            { "fa", "Persian" },
            { "pl", "Polish" },
            { "pt", "Portuguese" },
            { "ro", "Romanian" },
            { "ru", "Russian" },
            { "sk", "Slovak" },
            { "sl", "Slovenian" },
            { "es", "Spanish" },
            { "sv", "Swedish" },
            { "tr", "Turkish" },
            { "uk", "Ukrainian" }
        };

        /// <summary>
        /// Native display names for languages (as they appear in their own language).
        /// </summary>
        public static readonly Dictionary<string, string> NativeLanguageNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ar", "العربية" },
            { "bg", "български" },
            { "zh", "简体中文" },
            { "zh-Hant", "正體中文" },
            { "cs", "čeština" },
            { "da", "dansk" },
            { "nl", "Nederlands" },
            { "en", "English" },
            { "et", "eesti" },
            { "fi", "suomi" },
            { "fr", "français" },
            { "de", "Deutsch" },
            { "el", "Ελληνικά" },
            { "he", "עברית" },
            { "hu", "magyar" },
            { "id", "Bahasa Indonesia" },
            { "it", "italiano" },
            { "ja", "日本語" },
            { "ko", "한국어" },
            { "lv", "latviešu" },
            { "lt", "lietuvių" },
            { "nb", "norsk bokmål" },
            { "fa", "فارسی" },
            { "pl", "polski" },
            { "pt", "português" },
            { "ro", "română" },
            { "ru", "русский" },
            { "sk", "slovenčina" },
            { "sl", "slovenščina" },
            { "es", "español" },
            { "sv", "svenska" },
            { "tr", "Türkçe" },
            { "uk", "українська" }
        };

        /// <summary>
        /// Languages that are written right-to-left.
        /// </summary>
        public static readonly HashSet<string> RightToLeftLanguages = new(StringComparer.OrdinalIgnoreCase)
        {
            "ar",   // Arabic
            "he",   // Hebrew
            "fa"    // Persian/Farsi
        };

        /// <summary>
        /// Gets the display name for a language code.
        /// </summary>
        public static string GetDisplayName(string languageCode, bool native = false)
        {
            var dictionary = native ? NativeLanguageNames : LanguageNames;
            return dictionary.TryGetValue(languageCode, out var name) ? name : languageCode;
        }

        /// <summary>
        /// Gets the fallback language code for a given language.
        /// </summary>
        public static string GetFallbackLanguage(string languageCode)
        {
            return FallbackLanguages.TryGetValue(languageCode, out var fallback) 
                ? fallback 
                : languageCode;
        }

        /// <summary>
        /// Checks if a language is written right-to-left.
        /// </summary>
        public static bool IsRightToLeft(string languageCode)
        {
            return RightToLeftLanguages.Contains(languageCode);
        }

        /// <summary>
        /// Maps Unity's SystemLanguage to ISO language codes.
        /// </summary>
        public static string FromSystemLanguage(UnityEngine.SystemLanguage systemLanguage)
        {
            return systemLanguage switch
            {
                UnityEngine.SystemLanguage.Arabic => "ar",
                UnityEngine.SystemLanguage.Bulgarian => "bg",
                UnityEngine.SystemLanguage.ChineseSimplified => "zh",
                UnityEngine.SystemLanguage.ChineseTraditional => "zh-Hant",
                UnityEngine.SystemLanguage.Czech => "cs",
                UnityEngine.SystemLanguage.Danish => "da",
                UnityEngine.SystemLanguage.Dutch => "nl",
                UnityEngine.SystemLanguage.English => "en",
                UnityEngine.SystemLanguage.Estonian => "et",
                UnityEngine.SystemLanguage.Finnish => "fi",
                UnityEngine.SystemLanguage.French => "fr",
                UnityEngine.SystemLanguage.German => "de",
                UnityEngine.SystemLanguage.Greek => "el",
                UnityEngine.SystemLanguage.Hebrew => "he",
                UnityEngine.SystemLanguage.Hungarian => "hu",
                UnityEngine.SystemLanguage.Indonesian => "id",
                UnityEngine.SystemLanguage.Italian => "it",
                UnityEngine.SystemLanguage.Japanese => "ja",
                UnityEngine.SystemLanguage.Korean => "ko",
                UnityEngine.SystemLanguage.Latvian => "lv",
                UnityEngine.SystemLanguage.Lithuanian => "lt",
                UnityEngine.SystemLanguage.Norwegian => "nb",
                UnityEngine.SystemLanguage.Polish => "pl",
                UnityEngine.SystemLanguage.Portuguese => "pt",
                UnityEngine.SystemLanguage.Romanian => "ro",
                UnityEngine.SystemLanguage.Russian => "ru",
                UnityEngine.SystemLanguage.Slovak => "sk",
                UnityEngine.SystemLanguage.Slovenian => "sl",
                UnityEngine.SystemLanguage.Spanish => "es",
                UnityEngine.SystemLanguage.Swedish => "sv",
                UnityEngine.SystemLanguage.Turkish => "tr",
                UnityEngine.SystemLanguage.Ukrainian => "uk",
                _ => "en"
            };
        }

        /// <summary>
        /// Gets the language code from a display name (English or native).
        /// </summary>
        public static string GetLanguageCode(string languageName, bool nativeName = false)
        {
            if (string.IsNullOrEmpty(languageName))
                return null;

            var dictionary = nativeName ? NativeLanguageNames : LanguageNames;

            foreach (var kvp in dictionary)
            {
                if (string.Equals(kvp.Value, languageName, StringComparison.OrdinalIgnoreCase))
                    return kvp.Key;
            }

            return null;
        }
    }
}
