using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PicoShot.Localization.Data;
using PicoShot.Localization.Rtl;
using UnityEngine;

namespace PicoShot.Localization
{
    /// <summary>
    /// Central manager for localization system.
    /// Handles language loading, switching, and text retrieval.
    /// </summary>
    public static class LocalizationManager
    {
        #region Events

        public static event Action OnLanguageChanged;
        public static event Action<string> OnLanguageLoadError;
        public static event Action<string> OnMissingTranslation;

        #endregion

        #region Configuration

        public static readonly string LanguagesFilePath = Path.Combine("languages", "languages.bloc");
        public static string FilePath => Path.Combine(Application.streamingAssetsPath, LanguagesFilePath);

        private const string DefaultLanguageCode = "en";

        #endregion

        #region State

        private static string _currentLanguageCode = DefaultLanguageCode;
        private static bool _isInitialized;

        private static Dictionary<string, object> _currentLanguageData;
        private static Dictionary<string, object> _fallbackLanguageData;

        private static HashSet<string> _allTranslationKeys;
        private static HashSet<string> _availableLanguages;
        private static Dictionary<string, HashSet<string>> _languageKeyMap;

        #endregion

        #region Properties

        public static string CurrentLanguage => _currentLanguageCode;
        public static string DefaultLanguage => DefaultLanguageCode;
        public static bool IsInitialized => _isInitialized;
        public static bool IsRightToLeft => LanguageDefinitions.IsRightToLeft(_currentLanguageCode);

        #endregion

        #region Initialization

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void AutoInitialize()
        {
            if (_isInitialized) return;
            Initialize();
        }

        /// <summary>
        /// Initializes the localization system.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                if (!File.Exists(FilePath))
                {
                    string error = $"[LocalizationManager] Language file not found: {FilePath}";
                    Debug.LogError(error);
                    OnLanguageLoadError?.Invoke(error);
                    return;
                }

                LoadLanguageMetadata();
                SetLanguage(DetectSystemLanguage(), useFallback: false);
                
                _isInitialized = true;
                Application.quitting += Dispose;
                
                OnLanguageChanged?.Invoke();
            }
            catch (Exception ex)
            {
                string error = $"[LocalizationManager] Initialization failed: {ex.Message}";
                Debug.LogError(error);
                OnLanguageLoadError?.Invoke(error);
            }
        }

        private static void LoadLanguageMetadata()
        {
            _allTranslationKeys = new HashSet<string>(StringComparer.Ordinal);
            _languageKeyMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var data = BlocSerializer.DeserializeFromFile(FilePath);
                _availableLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in data.Translations)
                {
                    string key = entry.Key;
                    _allTranslationKeys.Add(key);

                    foreach (string langCode in entry.Value.Keys)
                    {
                        _availableLanguages.Add(langCode);
                        
                        if (!_languageKeyMap.TryGetValue(langCode, out var keys))
                        {
                            keys = new HashSet<string>(StringComparer.Ordinal);
                            _languageKeyMap[langCode] = keys;
                        }
                        keys.Add(key);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalizationManager] Failed to load metadata: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Language Management

        /// <summary>
        /// Sets the current language.
        /// </summary>
        public static void SetLanguage(string languageCode, bool useFallback = true)
        {
            if (string.IsNullOrEmpty(languageCode))
            {
                Debug.LogError("[LocalizationManager] SetLanguage called with null or empty code");
                return;
            }

            if (!_isInitialized)
            {
                Initialize();
            }

            string targetLanguage = ResolveTargetLanguage(languageCode, useFallback);

            if (_currentLanguageCode == targetLanguage && _currentLanguageData != null)
                return;

            try
            {
                LoadLanguageData(targetLanguage);
                _currentLanguageCode = targetLanguage;
                OnLanguageChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalizationManager] Failed to set language to '{targetLanguage}': {ex.Message}");
                OnLanguageLoadError?.Invoke($"Failed to load language '{targetLanguage}'");
            }
        }

        private static string ResolveTargetLanguage(string requestedCode, bool useFallback)
        {
            if (_availableLanguages.Contains(requestedCode))
                return requestedCode;

            if (useFallback)
            {
                string fallback = LanguageDefinitions.GetFallbackLanguage(requestedCode);
                if (_availableLanguages.Contains(fallback))
                    return fallback;
            }

            return DefaultLanguageCode;
        }

        private static void LoadLanguageData(string languageCode)
        {
            var data = BlocSerializer.DeserializeFromFile(FilePath);
            
            _currentLanguageData = ExtractLanguageDictionary(data, languageCode, DefaultLanguageCode);
            
            if (languageCode != DefaultLanguageCode)
            {
                _fallbackLanguageData = ExtractLanguageDictionary(data, DefaultLanguageCode, null);
            }
            else
            {
                _fallbackLanguageData = _currentLanguageData;
            }
        }
        
        private static Dictionary<string, object> ExtractLanguageDictionary(LanguageData data, string languageCode, string fallbackCode)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);

            if (data?.Translations == null)
                return result;

            foreach (var entry in data.Translations)
            {
                string key = entry.Key;
                var translations = entry.Value;

                // Try to get translation in requested language
                if (translations.TryGetValue(languageCode, out var value))
                {
                    result[key] = NormalizeValue(value);
                }
                // Fall back if needed
                else if (languageCode != fallbackCode && fallbackCode != null && translations.TryGetValue(fallbackCode, out var fallbackValue))
                {
                    result[key] = NormalizeValue(fallbackValue);
                }
            }

            return result;
        }
        
        private static object NormalizeValue(object value)
        {
            if (value == null)
                return null;

            if (value is List<string>)
                return value;

            if (value is string[] stringArray)
                return new List<string>(stringArray);

            return value?.ToString();
        }

        /// <summary>
        /// Detects system language from Unity settings.
        /// </summary>
        public static string DetectSystemLanguage()
        {
            string systemLanguage = LanguageDefinitions.FromSystemLanguage(Application.systemLanguage);
            return ResolveTargetLanguage(systemLanguage, useFallback: true);
        }

        #endregion

        #region Text Retrieval

        /// <summary>
        /// Gets a translated string by key.
        /// </summary>
        public static string GetText(string key, params string[] args)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[LocalizationManager] GetText called with null or empty key");
                return string.Empty;
            }

            if (!_isInitialized)
            {
                Initialize();
            }

            string text = GetRawText(key);

            if (args != null && args.Length > 0)
            {
                text = string.Format(text, args);
            }

            if (IsRightToLeft)
            {
                text = RtlTextHandler.Fix(text);
            }

            return text;
        }

        private static string GetRawText(string key)
        {
            if (_currentLanguageData.TryGetValue(key, out var value))
            {
                if (value is List<string> list)
                    return list.FirstOrDefault() ?? key;
                return value?.ToString() ?? key;
            }

            if (_fallbackLanguageData.TryGetValue(key, out var fallbackValue))
            {
                OnMissingTranslation?.Invoke($"Using fallback for key '{key}' in '{_currentLanguageCode}'");
                if (fallbackValue is List<string> list)
                    return list.FirstOrDefault() ?? key;
                return fallbackValue?.ToString() ?? key;
            }

            OnMissingTranslation?.Invoke($"Missing translation for key '{key}' in '{_currentLanguageCode}'");
            return key;
        }

        /// <summary>
        /// Gets an array of strings by key.
        /// </summary>
        public static string[] GetArray(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[LocalizationManager] GetArray called with null or empty key");
                return null;
            }

            if (!_isInitialized)
            {
                Initialize();
            }

            var array = GetArrayInternal(key);
            
            if (array == null)
                return null;

            if (IsRightToLeft)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = RtlTextHandler.Fix(array[i]);
                }
            }

            return array;
        }

        /// <summary>
        /// Gets a single element from an array by key and index.
        /// </summary>
        public static string GetArrayText(string key, int index)
        {
            var array = GetArray(key);
            
            if (array == null || array.Length == 0)
            {
                Debug.LogWarning($"[LocalizationManager] Key '{key}' is not an array or is empty");
                return $"[{key}]";
            }

            if (index >= 0 && index < array.Length)
            {
                return array[index] ?? string.Empty;
            }

            Debug.LogWarning($"[LocalizationManager] Array index {index} out of range for key '{key}'");
            return $"[{key}:{index}]";
        }

        private static string[] GetArrayInternal(string key)
        {
            if (_currentLanguageData.TryGetValue(key, out var value))
            {
                return ConvertToStringArray(value);
            }

            if (_fallbackLanguageData.TryGetValue(key, out var fallbackValue))
            {
                OnMissingTranslation?.Invoke($"Using fallback for array key '{key}' in '{_currentLanguageCode}'");
                return ConvertToStringArray(fallbackValue);
            }

            OnMissingTranslation?.Invoke($"Missing array translation for key '{key}' in '{_currentLanguageCode}'");
            return null;
        }

        private static string[] ConvertToStringArray(object value)
        {
            switch (value)
            {
                case List<string> list:
                    return list.ToArray();
                case string single:
                    return new[] { single };
                default:
                    Debug.LogWarning($"[LocalizationManager] Expected array but got {value?.GetType().Name ?? "null"}");
                    return null;
            }
        }

        #endregion

        #region Language Information

        /// <summary>
        /// Gets available languages.
        /// </summary>
        public static IEnumerable<string> GetAvailableLanguages(bool withNativeNames = false)
        {
            if (_languageKeyMap == null || _languageKeyMap.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            return _languageKeyMap.Keys.Select(code => 
                LanguageDefinitions.GetDisplayName(code, withNativeNames));
        }

        /// <summary>
        /// Checks if a language is available.
        /// </summary>
        public static bool IsLanguageAvailable(string languageCode)
        {
            return _availableLanguages?.Contains(languageCode) ?? false;
        }

        /// <summary>
        /// Gets the display name for a language code.
        /// </summary>
        public static string GetLanguageDisplayName(string languageCode, bool native = false)
        {
            return LanguageDefinitions.GetDisplayName(languageCode, native);
        }

        /// <summary>
        /// Gets the language code from a display name.
        /// </summary>
        public static string GetLanguageCode(string displayName, bool nativeName = false)
        {
            return LanguageDefinitions.GetLanguageCode(displayName, nativeName);
        }

        /// <summary>
        /// Checks if a key exists in the current language.
        /// </summary>
        public static bool HasKey(string key)
        {
            return _currentLanguageData?.ContainsKey(key) ?? false;
        }

        /// <summary>
        /// Gets all available translation keys.
        /// </summary>
        public static IEnumerable<string> GetAllKeys()
        {
            return _allTranslationKeys ?? Enumerable.Empty<string>();
        }

        #endregion

        #region Editor Support

        /// <summary>
        /// Saves language data to file (for editor use).
        /// </summary>
        public static void SaveToFile(string path, LanguageData data)
        {
            BlocSerializer.SaveToFile(path, data);
        }

        /// <summary>
        /// Loads language data from file (for editor use).
        /// </summary>
        public static LanguageData LoadFromFile(string path)
        {
            return BlocSerializer.DeserializeFromFile(path);
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Disposes resources and unsubscribes from events.
        /// </summary>
        public static void Dispose()
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
            _currentLanguageCode = DefaultLanguageCode;
        }

        /// <summary>
        /// Gets debug information about the localization system.
        /// </summary>
        public static string GetDebugInfo()
        {
            return $"[LocalizationManager]\n" +
                   $"  Initialized: {_isInitialized}\n" +
                   $"  Current Language: {_currentLanguageCode}\n" +
                   $"  Available Languages: {_availableLanguages?.Count ?? 0}\n" +
                   $"  Loaded Keys: {_currentLanguageData?.Count ?? 0}\n" +
                   $"  File Path: {FilePath}\n" +
                   $"  File Exists: {File.Exists(FilePath)}";
        }

        #endregion
    }
}
