using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using TMPro;
using System.Collections.Concurrent;
using MessagePack;
using System.Text;

namespace DMS.Language
{
    public static class LocalizationManager
    {
        public static event Action OnLanguageChanged;
        public static event Action<string> OnLanguageLoadError;
        public static event Action<string> OnMissingTranslation;

        public static readonly string LanguagesFilePath = Path.Combine("languages", "languages.dmsl");
        private const string DefaultLanguage = "en";
        private static volatile string _currentLanguage = DefaultLanguage;
        public const string FallbackLanguage = "en";
        public static string DmsFilePath => Path.Combine(Application.streamingAssetsPath, LanguagesFilePath);

        private static Dictionary<string, object> _currentLanguageData;
        private static Dictionary<string, object> _fallbackLanguageData;

        private static HashSet<string> _allTranslationKeys;
        private static HashSet<string> _availableLanguages;
        private static Dictionary<string, HashSet<string>> _languageKeyMap;

        private static readonly Dictionary<string, string> FallbackLanguages = new(StringComparer.OrdinalIgnoreCase)
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
            { "hu", "Hungarian" },
            { "id", "Indonesian" },
            { "it", "Italian" },
            { "ja", "Japanese" },
            { "ko", "Korean" },
            { "lv", "Latvian" },
            { "lt", "Lithuanian" },
            { "nb", "Norwegian" },
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

        private static readonly Dictionary<string, string> NativeLanguageNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ar", "ﺔﻴﺑﺮﻌﻟا" },
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
            { "hu", "magyar" },
            { "id", "Bahasa Indonesia" },
            { "it", "italiano" },
            { "ja", "日本語" },
            { "ko", "한국어" },
            { "lv", "latviešu" },
            { "lt", "lietuvių" },
            { "nb", "norsk bokmål" },
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

        private static volatile bool _isInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                if (File.Exists(DmsFilePath))
                {
                    InitializeLanguageMetadata();
                    LoadCurrentLanguage(_currentLanguage);
                    _isInitialized = true;
                    OnLanguageChanged?.Invoke();
                    Application.quitting += Dispose;
                }
                else
                {
                    string error = $"Language file not found at path: {DmsFilePath}";
                    Debug.LogError(error);
                    OnLanguageLoadError?.Invoke(error);
                }
            }
            catch (Exception ex)
            {
                string error = $"Failed to initialize LanguageManager: {ex.Message}";
                Debug.LogError(error);
                OnLanguageLoadError?.Invoke(error);
            }
        }

        private static void InitializeLanguageMetadata()
        {
            _allTranslationKeys = new HashSet<string>(StringComparer.Ordinal);
            _languageKeyMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _availableLanguages = new HashSet<string>(LanguageNames.Keys, StringComparer.OrdinalIgnoreCase);

            using var stream = File.OpenRead(DmsFilePath);
            try
            {
                var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
                var data = MessagePackSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(stream, options);

                foreach (var entry in data)
                {
                    _allTranslationKeys.Add(entry.Key);
                    foreach (var langCode in entry.Value.Keys)
                    {
                        if (!_languageKeyMap.TryGetValue(langCode, out var keys))
                        {
                            keys = new HashSet<string>(StringComparer.Ordinal);
                            _languageKeyMap[langCode] = keys;
                        }
                        keys.Add(entry.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error extracting language metadata: {ex.Message}");
                throw;
            }
        }

        private static void LoadCurrentLanguage(string language)
        {
            _currentLanguageData = new Dictionary<string, object>(StringComparer.Ordinal);

            if (language != FallbackLanguage)
            {
                _fallbackLanguageData = new Dictionary<string, object>(StringComparer.Ordinal);
            }

            using (var stream = File.OpenRead(DmsFilePath))
            {
                try
                {
                    var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
                    var data = MessagePackSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(stream, options);

                    foreach (var entry in data)
                    {
                        string key = entry.Key;
                        var translations = entry.Value;

                        if (translations.TryGetValue(language, out var value))
                        {
                            if (value is object[] objArray)
                            {
                                _currentLanguageData[key] = objArray.Select(x => x?.ToString()).ToList();
                            }
                            else
                            {
                                _currentLanguageData[key] = value;
                            }
                        }

                        if (language == FallbackLanguage ||
                            !translations.TryGetValue(FallbackLanguage, out var fallbackValue)) continue;
                        {
                            if (fallbackValue is object[] objArray)
                            {
                                _fallbackLanguageData[key] = objArray.Select(x => x?.ToString()).ToList();
                            }
                            else
                            {
                                _fallbackLanguageData[key] = fallbackValue;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading language data: {ex.Message}");
                    throw;
                }
            }

            if (language == FallbackLanguage)
            {
                _fallbackLanguageData = _currentLanguageData;
            }
        }

        private static void OnLanguageChange()
        {
            OnLanguageChanged?.Invoke();
        }

        public static string GetLanguageDisplayName(string languageCode)
        {
            return LanguageNames.GetValueOrDefault(languageCode, languageCode);
        }

        public static void SetLanguage(string language, bool useFallback = true)
        {
            if (string.IsNullOrEmpty(language))
            {
                Debug.LogError("SetLanguage called with null or empty language code");
                return;
            }

            if (!_isInitialized)
            {
                Initialize();
            }

            var targetLanguage = language;
            if (useFallback && !_availableLanguages.Contains(language) &&
                FallbackLanguages.TryGetValue(language, out var fallbackLanguage))
            {
                targetLanguage = fallbackLanguage;
            }

            if (_currentLanguage == targetLanguage && _isInitialized) return;

            _currentLanguage = targetLanguage;
            LoadCurrentLanguage(targetLanguage);
            OnLanguageChange();
        }

        public static string GetText(string key, params string[] args)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("GetText called with null or empty key");
                return string.Empty;
            }

            if (!_isInitialized)
            {
                Initialize();
            }

            string baseText = GetBaseText(key);

            if (args != null && args.Length > 0)
            {
                baseText = string.Format(baseText, args);
            }

            return IsRightToLeft(_currentLanguage) ? LocalizationRtlManager.Fix(baseText) : baseText;
        }

        private static string GetBaseText(string key)
        {
            if (_currentLanguageData.TryGetValue(key, out var value))
                return value?.ToString() ?? key;

            if (_fallbackLanguageData.TryGetValue(key, out var fallbackValue))
            {
                OnMissingTranslation?.Invoke($"Using fallback for key '{key}' in language '{_currentLanguage}'");
                return fallbackValue?.ToString() ?? key;
            }

            OnMissingTranslation?.Invoke($"Missing translation for key '{key}' in language '{_currentLanguage}'");
            return key;
        }

        public static void BindText(TMP_Text textComponent, string key, int arrayIndex = -1, Func<string, string> textProcessor = null, params object[] args)
        {
            if (!textComponent.TryGetComponent<LocalizationTextComponent>(out var langComp))
            {
                langComp = textComponent.gameObject.AddComponent<LocalizationTextComponent>();
            }

            langComp.languageKey = key;
            langComp.arrayIndex = arrayIndex;
            langComp.formatParameters = args.Select(arg => arg?.ToString()).ToArray();

            if (textProcessor != null)
                langComp.AddTextProcessor(textProcessor);

            langComp.UpdateText();
        }

        public static void BindText(TMP_Dropdown textComponent, string key, int arrayMaxSize = 0, Func<string, string> textProcessor = null, params object[] args)
        {
            if (!textComponent.TryGetComponent<LocalizationTextComponent>(out var langComp))
            {
                langComp = textComponent.gameObject.AddComponent<LocalizationTextComponent>();
            }

            langComp.languageKey = key;
            langComp.formatParameters = args.Select(arg => arg?.ToString()).ToArray();
            langComp.arraySizeLimit = arrayMaxSize;

            if (textProcessor != null)
                langComp.AddTextProcessor(textProcessor);

            langComp.UpdateText();
        }

        public static string GetArrayText(string key, int index)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            if (!_isInitialized)
            {
                Initialize();
            }

            var array = GetArray(key);
            if (array == null || array.Length == 0)
            {
                return $"[Invalid:{key}:NotArray]";
            }

            if (index >= 0 && index < array.Length) return array[index] ?? string.Empty;
            Debug.LogWarning($"Array index {index} out of range for key '{key}'");
            return $"[Invalid:{key}:{index}]";
        }

        public static string[] GetArray(string key)
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            if (_currentLanguageData.TryGetValue(key, out var value))
            {
                return ConvertToStringArray(value);
            }

            if (_fallbackLanguageData.TryGetValue(key, out var fallbackValue))
            {
                OnMissingTranslation?.Invoke($"Using fallback for key '{key}' in language '{_currentLanguage}'");
                return ConvertToStringArray(fallbackValue);
            }

            OnMissingTranslation?.Invoke($"Missing translation for key '{key}' in language '{_currentLanguage}'");
            return null;
        }

        private static string[] ConvertToStringArray(object value)
        {
            switch (value)
            {
                case List<string> stringList when IsRightToLeft(_currentLanguage):
                    return stringList.Select(LocalizationRtlManager.Fix).ToArray();
                case List<string> stringList:
                    return stringList.ToArray();
                case string singleString when IsRightToLeft(_currentLanguage):
                    return new[] { LocalizationRtlManager.Fix(singleString) };
                case string singleString:
                    return new[] { singleString };
                default:
                    Debug.LogError($"Invalid data type for array key: expected List<string>, got {value?.GetType().ToString() ?? "null"}");
                    return null;
            }
        }

        public static IEnumerable<string> GetAvailableLanguages(bool withNativeNames)
        {
            if (_languageKeyMap == null || _languageKeyMap.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            return _languageKeyMap.Keys.Select(code =>
                withNativeNames
                    ? NativeLanguageNames.GetValueOrDefault(code, code)
                    : LanguageNames.GetValueOrDefault(code, code)
            ).ToList();
        }

        public static string GetLanguageCode(string languageName, bool nativeName = false)
        {
            if (string.IsNullOrEmpty(languageName))
            {
                Debug.LogWarning("GetLanguageCode called with null or empty language name");
                return null;
            }

            var dictionary = nativeName ? NativeLanguageNames : LanguageNames;

            foreach (var kvp in dictionary)
            {
                if (string.Equals(kvp.Value, languageName, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            Debug.LogWarning(
                $"Language name '{languageName}' not found in {(nativeName ? "native" : "English")} language names.");
            return null;
        }

        public static string GetCurrentLanguage()
        {
            return _currentLanguage;
        }

        public static bool IsInitialized()
        {
            return _isInitialized;
        }

        public static void Subscribe(Action callback)
        {
            if (callback != null)
            {
                OnLanguageChanged += callback;
            }
        }

        public static void Unsubscribe(Action callback)
        {
            if (callback != null)
            {
                OnLanguageChanged -= callback;
            }
        }

        public static bool IsRightToLeft(string language = null)
        {
            var lang = language ?? _currentLanguage;
            return lang is "ar" or "he" or "fa";
        }

        public static string GetSystemLanguage()
        {
            var unityLanguage = Application.systemLanguage;
            return unityLanguage switch
            {
                SystemLanguage.Arabic => "ar",
                SystemLanguage.Bulgarian => "bg",
                SystemLanguage.ChineseSimplified => "zh",
                SystemLanguage.ChineseTraditional => "zh-Hant",
                SystemLanguage.Czech => "cs",
                SystemLanguage.Danish => "da",
                SystemLanguage.Dutch => "nl",
                SystemLanguage.English => "en",
                SystemLanguage.Estonian => "et",
                SystemLanguage.Finnish => "fi",
                SystemLanguage.French => "fr",
                SystemLanguage.German => "de",
                SystemLanguage.Greek => "el",
                SystemLanguage.Hungarian => "hu",
                SystemLanguage.Indonesian => "id",
                SystemLanguage.Italian => "it",
                SystemLanguage.Japanese => "ja",
                SystemLanguage.Korean => "ko",
                SystemLanguage.Latvian => "lv",
                SystemLanguage.Lithuanian => "lt",
                SystemLanguage.Norwegian => "nb",
                SystemLanguage.Polish => "pl",
                SystemLanguage.Portuguese => "pt",
                SystemLanguage.Romanian => "ro",
                SystemLanguage.Russian => "ru",
                SystemLanguage.Slovak => "sk",
                SystemLanguage.Slovenian => "sl",
                SystemLanguage.Spanish => "es",
                SystemLanguage.Swedish => "sv",
                SystemLanguage.Turkish => "tr",
                SystemLanguage.Ukrainian => "uk",
                _ => "en"
            };
        }

        public static void Cleanup()
        {
            OnLanguageChanged = null;
            OnLanguageLoadError = null;
            OnMissingTranslation = null;
            _isInitialized = false;
        }

        private static void Dispose()
        {
            OnLanguageChanged = null;
            OnLanguageLoadError = null;
            OnMissingTranslation = null;

            _currentLanguageData?.Clear();
            _currentLanguageData = null;

            _fallbackLanguageData?.Clear();
            _fallbackLanguageData = null;

            _allTranslationKeys?.Clear();
            _allTranslationKeys = null;

            _languageKeyMap?.Clear();
            _languageKeyMap = null;

            _availableLanguages?.Clear();
            _availableLanguages = null;

            _isInitialized = false;
        }

        public static string DetectLanguageFromSystemLocale()
        {
            string systemLanguage = GetSystemLanguage();

            return _availableLanguages.Contains(systemLanguage)
                ? systemLanguage
                : FallbackLanguages.GetValueOrDefault(systemLanguage, FallbackLanguage);
        }

        public static string GetDebugInfo()
        {
            return $"LanguageManager Status:\n" +
                   $"Initialized: {_isInitialized}\n" +
                   $"Current Language: {_currentLanguage}\n" +
                   $"Fallback Language: {FallbackLanguage}\n" +
                   $"Available Languages: {string.Join(", ", _availableLanguages)}\n" +
                   $"Current Language Keys: {_currentLanguageData?.Count ?? 0}\n" +
                   $"DMSL Path: {DmsFilePath}";
        }

        public static void SaveDmsl(string path, object data)
        {
            var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
            byte[] bytes = MessagePackSerializer.Serialize(data, options);
            File.WriteAllBytes(path, bytes);
        }

        public static T LoadDmsl<T>(string path)
        {
            var data = File.ReadAllBytes(path);
            return MessagePackSerializer.Deserialize<T>(data, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block));
        }
    }

    public static class StringBuilderPool
    {
        private static readonly ObjectPool<StringBuilder> Pool = new(() => new StringBuilder(256), sb => sb.Clear());

        public static StringBuilder Get() => Pool.Get();

        public static void Return(StringBuilder sb) => Pool.Return(sb);

        private class ObjectPool<T> where T : class
        {
            private readonly Func<T> _createFunc;
            private readonly Action<T> _resetAction;
            private readonly ConcurrentBag<T> _objects = new();

            public ObjectPool(Func<T> createFunc, Action<T> resetAction = null)
            {
                _createFunc = createFunc;
                _resetAction = resetAction;
            }

            public T Get()
            {
                if (_objects.TryTake(out T item))
                    return item;

                return _createFunc();
            }

            public void Return(T item)
            {
                _resetAction?.Invoke(item);
                _objects.Add(item);
            }
        }
    }
}