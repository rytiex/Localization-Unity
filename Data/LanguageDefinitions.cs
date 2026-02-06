using System;
using System.Collections.Generic;

namespace PicoShot.Localization.Data
{
    /// <summary>
    /// Contains language metadata including display names, native names, and fallback mappings.
    /// Aligned with DeepL supported languages for translation compatibility.
    /// </summary>
    public static class LanguageDefinitions
    {
        /// <summary>
        /// Default language code.
        /// </summary>
        public const string DefaultLanguage = "en";

        /// <summary>
        /// Maps specific locale codes to their more general fallback versions.
        /// </summary>
        public static readonly Dictionary<string, string> FallbackLanguages = new(StringComparer.OrdinalIgnoreCase)
        {
            // Chinese variants
            { "zh-CN", "zh" },
            { "zh-SG", "zh" },
            { "zh-TW", "zh" },
            { "zh-HK", "zh" },
            { "zh-Hant", "zh" },
            // Portuguese variants
            { "pt-BR", "pt" },
            { "pt-PT", "pt" },
            // Spanish variants
            { "es-MX", "es" },
            { "es-ES", "es" },
            { "es-AR", "es" },
            { "es-CO", "es" },
            // French variants
            { "fr-CA", "fr" },
            { "fr-BE", "fr" },
            { "fr-CH", "fr" },
            // German variants
            { "de-AT", "de" },
            { "de-CH", "de" },
            // English variants
            { "en-US", "en" },
            { "en-GB", "en" },
            { "en-AU", "en" },
            { "en-CA", "en" },
            // Kurdish variants map to available codes
            { "ku", "ckb" },
            // Norwegian
            { "no", "nb" },
            // Serbian variants
            { "sr-Latn", "sr" },
            { "sr-Cyrl", "sr" },
        };

        /// <summary>
        /// English display names for languages (DeepL supported).
        /// </summary>
        public static readonly Dictionary<string, string> LanguageNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ace", "Acehnese" },
            { "af", "Afrikaans" },
            { "an", "Aragonese" },
            { "ar", "Arabic" },
            { "as", "Assamese" },
            { "ay", "Aymara" },
            { "az", "Azerbaijani" },
            { "ba", "Bashkir" },
            { "be", "Belarusian" },
            { "bg", "Bulgarian" },
            { "bho", "Bhojpuri" },
            { "bn", "Bengali" },
            { "br", "Breton" },
            { "bs", "Bosnian" },
            { "ca", "Catalan" },
            { "ceb", "Cebuano" },
            { "ckb", "Kurdish (Sorani)" },
            { "cs", "Czech" },
            { "cy", "Welsh" },
            { "da", "Danish" },
            { "de", "German" },
            { "el", "Greek" },
            { "en", "English" },
            { "eo", "Esperanto" },
            { "es", "Spanish" },
            { "et", "Estonian" },
            { "eu", "Basque" },
            { "fa", "Persian" },
            { "fi", "Finnish" },
            { "fr", "French" },
            { "ga", "Irish" },
            { "gl", "Galician" },
            { "gn", "Guarani" },
            { "gom", "Konkani" },
            { "gu", "Gujarati" },
            { "ha", "Hausa" },
            { "he", "Hebrew" },
            { "hi", "Hindi" },
            { "hr", "Croatian" },
            { "ht", "Haitian Creole" },
            { "hu", "Hungarian" },
            { "hy", "Armenian" },
            { "id", "Indonesian" },
            { "ig", "Igbo" },
            { "is", "Icelandic" },
            { "it", "Italian" },
            { "ja", "Japanese" },
            { "jv", "Javanese" },
            { "ka", "Georgian" },
            { "kk", "Kazakh" },
            { "kmr", "Kurdish (Kurmanji)" },
            { "ko", "Korean" },
            { "ky", "Kyrgyz" },
            { "la", "Latin" },
            { "lb", "Luxembourgish" },
            { "lmo", "Lombard" },
            { "ln", "Lingala" },
            { "lt", "Lithuanian" },
            { "lv", "Latvian" },
            { "mai", "Maithili" },
            { "mg", "Malagasy" },
            { "mi", "Maori" },
            { "mk", "Macedonian" },
            { "ml", "Malayalam" },
            { "mn", "Mongolian" },
            { "mr", "Marathi" },
            { "ms", "Malay" },
            { "mt", "Maltese" },
            { "my", "Burmese" },
            { "nb", "Norwegian Bokmål" },
            { "ne", "Nepali" },
            { "nl", "Dutch" },
            { "oc", "Occitan" },
            { "om", "Oromo" },
            { "pa", "Punjabi" },
            { "pag", "Pangasinan" },
            { "pam", "Kapampangan" },
            { "pl", "Polish" },
            { "prs", "Dari" },
            { "ps", "Pashto" },
            { "pt", "Portuguese" },
            { "qu", "Quechua" },
            { "ro", "Romanian" },
            { "ru", "Russian" },
            { "sa", "Sanskrit" },
            { "scn", "Sicilian" },
            { "sk", "Slovak" },
            { "sl", "Slovenian" },
            { "sq", "Albanian" },
            { "sr", "Serbian" },
            { "st", "Sesotho" },
            { "su", "Sundanese" },
            { "sv", "Swedish" },
            { "sw", "Swahili" },
            { "ta", "Tamil" },
            { "te", "Telugu" },
            { "tg", "Tajik" },
            { "th", "Thai" },
            { "tk", "Turkmen" },
            { "tl", "Tagalog" },
            { "tn", "Tswana" },
            { "tr", "Turkish" },
            { "ts", "Tsonga" },
            { "tt", "Tatar" },
            { "uk", "Ukrainian" },
            { "ur", "Urdu" },
            { "uz", "Uzbek" },
            { "vi", "Vietnamese" },
            { "wo", "Wolof" },
            { "xh", "Xhosa" },
            { "yi", "Yiddish" },
            { "yue", "Cantonese" },
            { "zh", "Chinese" },
            { "zu", "Zulu" },
        };

        /// <summary>
        /// Native display names for languages (as they appear in their own language).
        /// </summary>
        public static readonly Dictionary<string, string> NativeLanguageNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ace", "Basa Acèh" },
            { "af", "Afrikaans" },
            { "an", "Aragonés" },
            { "ar", "العربية" },
            { "as", "অসমীয়া" },
            { "ay", "Aymar aru" },
            { "az", "Azərbaycan dili" },
            { "ba", "Башҡорт теле" },
            { "be", "Беларуская" },
            { "bg", "български" },
            { "bho", "भोजपुरी" },
            { "bn", "বাংলা" },
            { "br", "Brezhoneg" },
            { "bs", "Bosanski" },
            { "ca", "Català" },
            { "ceb", "Cebuano" },
            { "ckb", "کوردیی ناوەندی" },
            { "cs", "čeština" },
            { "cy", "Cymraeg" },
            { "da", "dansk" },
            { "de", "Deutsch" },
            { "el", "Ελληνικά" },
            { "en", "English" },
            { "eo", "Esperanto" },
            { "es", "español" },
            { "et", "eesti" },
            { "eu", "euskara" },
            { "fa", "فارسی" },
            { "fi", "suomi" },
            { "fr", "français" },
            { "ga", "Gaeilge" },
            { "gl", "galego" },
            { "gn", "Avañe'ẽ" },
            { "gom", "कोंकणी" },
            { "gu", "ગુજરાતી" },
            { "ha", "Hausa" },
            { "he", "עברית" },
            { "hi", "हिन्दी" },
            { "hr", "hrvatski" },
            { "ht", "Kreyòl ayisyen" },
            { "hu", "magyar" },
            { "hy", "Հայերեն" },
            { "id", "Bahasa Indonesia" },
            { "ig", "Igbo" },
            { "is", "íslenska" },
            { "it", "italiano" },
            { "ja", "日本語" },
            { "jv", "Basa Jawa" },
            { "ka", "ქართული" },
            { "kk", "Қазақ тілі" },
            { "kmr", "Kurmancî" },
            { "ko", "한국어" },
            { "ky", "Кыргызча" },
            { "la", "Latina" },
            { "lb", "Lëtzebuergesch" },
            { "lmo", "Lombard" },
            { "ln", "Lingála" },
            { "lt", "lietuvių" },
            { "lv", "latviešu" },
            { "mai", "मैथिली" },
            { "mg", "Malagasy" },
            { "mi", "te reo Māori" },
            { "mk", "македонски" },
            { "ml", "മലയാളം" },
            { "mn", "Монгол" },
            { "mr", "मराठी" },
            { "ms", "Bahasa Melayu" },
            { "mt", "Malti" },
            { "my", "မြန်မာ" },
            { "nb", "norsk bokmål" },
            { "ne", "नेपाली" },
            { "nl", "Nederlands" },
            { "oc", "Occitan" },
            { "om", "Afaan Oromoo" },
            { "pa", "ਪੰਜਾਬੀ" },
            { "pag", "Pangasinan" },
            { "pam", "Kapampangan" },
            { "pl", "polski" },
            { "prs", "دری" },
            { "ps", "پښتو" },
            { "pt", "português" },
            { "qu", "Runa Simi" },
            { "ro", "română" },
            { "ru", "русский" },
            { "sa", "संस्कृतम्" },
            { "scn", "Sicilianu" },
            { "sk", "slovenčina" },
            { "sl", "slovenščina" },
            { "sq", "shqip" },
            { "sr", "српски" },
            { "st", "Sesotho" },
            { "su", "Basa Sunda" },
            { "sv", "svenska" },
            { "sw", "Kiswahili" },
            { "ta", "தமிழ்" },
            { "te", "తెలుగు" },
            { "tg", "тоҷикӣ" },
            { "th", "ไทย" },
            { "tk", "Türkmen" },
            { "tl", "Tagalog" },
            { "tn", "Setswana" },
            { "tr", "Türkçe" },
            { "ts", "Xitsonga" },
            { "tt", "татар" },
            { "uk", "українська" },
            { "ur", "اردو" },
            { "uz", "Oʻzbek" },
            { "vi", "Tiếng Việt" },
            { "wo", "Wolof" },
            { "xh", "isiXhosa" },
            { "yi", "ייִדיש" },
            { "yue", "粵語" },
            { "zh", "中文" },
            { "zu", "isiZulu" },
        };

        /// <summary>
        /// Languages that are written right-to-left.
        /// </summary>
        public static readonly HashSet<string> RightToLeftLanguages = new(StringComparer.OrdinalIgnoreCase)
        {
            "ar",   // Arabic
            "he",   // Hebrew
            "fa",   // Persian/Farsi
            "ps",   // Pashto
            "ur",   // Urdu
            "yi",   // Yiddish
            "prs",  // Dari
            "ckb",  // Kurdish (Sorani)
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
        /// Maps Unity's SystemLanguage to ISO language codes (DeepL compatible).
        /// </summary>
        public static string FromSystemLanguage(UnityEngine.SystemLanguage systemLanguage)
        {
            return systemLanguage switch
            {
                UnityEngine.SystemLanguage.Arabic => "ar",
                UnityEngine.SystemLanguage.Bulgarian => "bg",
                UnityEngine.SystemLanguage.Catalan => "ca",
                UnityEngine.SystemLanguage.ChineseSimplified => "zh",
                UnityEngine.SystemLanguage.ChineseTraditional => "zh",
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
                UnityEngine.SystemLanguage.SerboCroatian => "sr",
                UnityEngine.SystemLanguage.Slovak => "sk",
                UnityEngine.SystemLanguage.Slovenian => "sl",
                UnityEngine.SystemLanguage.Spanish => "es",
                UnityEngine.SystemLanguage.Swedish => "sv",
                UnityEngine.SystemLanguage.Thai => "th",
                UnityEngine.SystemLanguage.Turkish => "tr",
                UnityEngine.SystemLanguage.Ukrainian => "uk",
                UnityEngine.SystemLanguage.Vietnamese => "vi",
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

        /// <summary>
        /// Gets the DeepL-compatible language code for translation.
        /// DeepL uses specific codes (e.g., "PT-PT" for European Portuguese).
        /// </summary>
        public static string ToDeepLCode(string languageCode)
        {
            return languageCode.ToLowerInvariant() switch
            {
                "pt" => "PT-PT",
                "zh" => "ZH",
                "nb" => "NB",  // Norwegian Bokmål
                "ckb" => "KU", // Kurdish (Sorani)
                "kmr" => "KU", // Kurdish (Kurmanji)
                _ => languageCode.ToUpperInvariant()
            };
        }
    }
}
