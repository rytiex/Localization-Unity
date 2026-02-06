using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PicoShot.Localization.Config;
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

        public const string LanguagesDirectory = "Locales";
        public const string FileExtension = ".bloc";

        /// <summary>
        /// Gets the path to the locales directory.
        /// </summary>
        public static string LanguagesPath
        {
            get
            {
#if UNITY_STANDALONE || UNITY_EDITOR
                return Path.Combine(
                    Path.GetDirectoryName(Application.dataPath),
                    LanguagesDirectory);
#else
                return Path.Combine(Application.streamingAssetsPath, LanguagesDirectory);
#endif
            }
        }

        /// <summary>
        /// Gets the default language from config.
        /// </summary>
        public static string DefaultLanguage => LocalizationConfigProvider.Config.DefaultLanguage;

        /// <summary>
        /// Gets whether anti-tamper mode is enabled.
        /// </summary>
        public static bool IsAntiTamperEnabled => LocalizationConfigProvider.Config.IsAntiTamperEnabled;

        /// <summary>
        /// Gets the selected languages from config (used when protection is enabled).
        /// </summary>
        public static IReadOnlyList<string> SelectedLanguages => LocalizationConfigProvider.Config.SelectedLanguages;

        #endregion

        #region State

        private static string _currentLanguageCode;
        private static bool _isInitialized;

        private static Dictionary<string, object> _currentLanguageData;
        private static Dictionary<string, object> _fallbackLanguageData;

        private static HashSet<string> _allTranslationKeys;
        private static HashSet<string> _availableLanguages;

        // Cache for string arrays to avoid repeated conversions
        private static readonly Dictionary<string, string[]> _arrayCache = new(StringComparer.Ordinal);

        #endregion

        #region Properties

        public static string CurrentLanguage => _currentLanguageCode;
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

            _isInitialized = true;

            try
            {
                ScanAvailableLanguages();

                if (_availableLanguages.Count == 0)
                {
                    string error = $"[LocalizationManager] No language files found in: {LanguagesPath}";
                    Debug.LogError(error);
                    OnLanguageLoadError?.Invoke(error);
                    _isInitialized = false;
                    return;
                }

                SetLanguage(DetectSystemLanguage(), useFallback: false);

                Application.quitting += Dispose;
                OnLanguageChanged?.Invoke();
            }
            catch (Exception ex)
            {
                string error = $"[LocalizationManager] Initialization failed: {ex.Message}";
                Debug.LogError(error);
                OnLanguageLoadError?.Invoke(error);
                _isInitialized = false;
            }
        }

        private static void ScanAvailableLanguages()
        {
            _availableLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _allTranslationKeys = new HashSet<string>(StringComparer.Ordinal);

            if (!Directory.Exists(LanguagesPath))
            {
                Debug.LogWarning($"[LocalizationManager] Languages directory not found: {LanguagesPath}");
                return;
            }

            var blocFiles = Directory.GetFiles(LanguagesPath, $"*{FileExtension}", SearchOption.TopDirectoryOnly);
            var config = LocalizationConfigProvider.Config;

            foreach (var file in blocFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);

                if (config.ProtectionMode == ProtectionMode.SelectionOnly ||
                    config.ProtectionMode == ProtectionMode.Both)
                {
                    if (!config.SelectedLanguages.Contains(fileName))
                        continue;
                }

                if (IsAntiTamperEnabled)
                {
                    string fileNameWithExt = Path.GetFileName(file);
                    if (!VerifyFileHash(file, fileNameWithExt, config))
                    {
                        Debug.LogError($"[LocalizationManager] Hash verification failed for: {fileName}");
                        OnLanguageLoadError?.Invoke($"File tampering detected: {fileName}");
                        continue;
                    }
                }

                _availableLanguages.Add(fileName);
            }

            if (config.IsProtectionEnabled && !_availableLanguages.Contains(config.DefaultLanguage))
            {
                if (File.Exists(GetLocaleFilePath(config.DefaultLanguage)))
                {
                    _availableLanguages.Add(config.DefaultLanguage);
                }
            }

            if (_availableLanguages.Contains(config.DefaultLanguage))
            {
                try
                {
                    var defaultData = LoadLocaleFile(config.DefaultLanguage);
                    foreach (var key in defaultData.Keys)
                    {
                        _allTranslationKeys.Add(key);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LocalizationManager] Failed to load default language keys: {ex.Message}");
                }
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
                return;
            }

            string targetLanguage = ResolveTargetLanguage(languageCode, useFallback);

            if (_currentLanguageCode == targetLanguage && _currentLanguageData != null)
                return;

            try
            {
                LoadLanguageData(targetLanguage);
                _currentLanguageCode = targetLanguage;
                _arrayCache.Clear();
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

            return DefaultLanguage;
        }

        private static void LoadLanguageData(string languageCode)
        {
            _currentLanguageData = LoadLocaleFile(languageCode);

            if (languageCode != DefaultLanguage)
            {
                _fallbackLanguageData = LoadLocaleFile(DefaultLanguage);
            }
            else
            {
                _fallbackLanguageData = _currentLanguageData;
            }
        }

        private static Dictionary<string, object> LoadLocaleFile(string languageCode)
        {
            string filePath = GetLocaleFilePath(languageCode);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Locale file not found for language '{languageCode}'", filePath);
            }

            var localeData = LocaleBlocSerializer.DeserializeFromFile(filePath);

            if (localeData?.Translations == null)
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }

            var result = new Dictionary<string, object>(localeData.Translations.Count, StringComparer.Ordinal);
            foreach (var entry in localeData.Translations)
            {
                result[entry.Key] = NormalizeValue(entry.Value);
            }

            return result;
        }

        private static string GetLocaleFilePath(string languageCode)
        {
            return Path.Combine(LanguagesPath, $"{languageCode}{FileExtension}");
        }

        private static object NormalizeValue(object value)
        {
            return value switch
            {
                null => null,
                List<string> list => list,
                string[] arr => new List<string>(arr),
                _ => value?.ToString()
            };
        }

        private static bool VerifyFileHash(string filePath, string fileName, LocalizationConfig config)
        {
            if (!config.TryGetFileHash(fileName, out string expectedHash))
            {
                Debug.LogWarning($"[LocalizationManager] No hash stored for file: {fileName}");
                return false;
            }

            string actualHash = CalculateFileHash(filePath);
            return string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Calculates SHA256 hash of a file.
        /// </summary>
        public static string CalculateFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
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
            if (_currentLanguageData != null && _currentLanguageData.TryGetValue(key, out var value))
            {
                return value switch
                {
                    List<string> list => list.Count > 0 ? list[0] : key,
                    _ => value?.ToString() ?? key
                };
            }

            if (_fallbackLanguageData != null && _fallbackLanguageData.TryGetValue(key, out var fallbackValue))
            {
                OnMissingTranslation?.Invoke($"Using fallback for key '{key}' in '{_currentLanguageCode}'");
                return fallbackValue switch
                {
                    List<string> list => list.Count > 0 ? list[0] : key,
                    _ => fallbackValue?.ToString() ?? key
                };
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

            if (_arrayCache.TryGetValue(key, out var cached))
                return cached;

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

            _arrayCache[key] = array;
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
            object value = null;
            bool found = false;

            if (_currentLanguageData != null)
            {
                found = _currentLanguageData.TryGetValue(key, out value);
            }

            if (!found && _fallbackLanguageData != null)
            {
                found = _fallbackLanguageData.TryGetValue(key, out value);
                if (found)
                {
                    OnMissingTranslation?.Invoke($"Using fallback for array key '{key}' in '{_currentLanguageCode}'");
                }
            }

            if (!found)
            {
                OnMissingTranslation?.Invoke($"Missing array translation for key '{key}' in '{_currentLanguageCode}'");
                return null;
            }

            return ConvertToStringArray(value);
        }

        private static string[] ConvertToStringArray(object value)
        {
            return value switch
            {
                List<string> list => list.ToArray(),
                string single => new[] { single },
                _ => null
            };
        }

        #endregion

        #region Language Information

        /// <summary>
        /// Gets available languages.
        /// </summary>
        public static IEnumerable<string> GetAvailableLanguages(bool withNativeNames = false)
        {
            if (_availableLanguages == null || _availableLanguages.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            return _availableLanguages.Select(code =>
                LanguageDefinitions.GetDisplayName(code, withNativeNames));
        }

        /// <summary>
        /// Gets available language codes.
        /// </summary>
        public static IEnumerable<string> GetAvailableLanguageCodes()
        {
            return _availableLanguages ?? Enumerable.Empty<string>();
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
        /// Checks if a key exists in the default language.
        /// </summary>
        public static bool HasKeyInDefault(string key)
        {
            return _fallbackLanguageData?.ContainsKey(key) ?? false;
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

#if UNITY_EDITOR

        /// <summary>
        /// Saves locale data to file (for editor use).
        /// </summary>
        public static void SaveLocaleToFile(string path, LocaleData data, bool compress = true)
        {
            LocaleBlocSerializer.SaveToFile(path, data, compress);
        }

        /// <summary>
        /// Loads locale data from file (for editor use).
        /// </summary>
        public static LocaleData LoadLocaleFromFile(string path)
        {
            return LocaleBlocSerializer.DeserializeFromFile(path);
        }

        /// <summary>
        /// Gets the file path for a language code (for editor use).
        /// </summary>
        public static string GetLanguageFilePath(string languageCode)
        {
            return GetLocaleFilePath(languageCode);
        }

        /// <summary>
        /// Refreshes available languages (for editor use).
        /// </summary>
        public static void RefreshAvailableLanguages()
        {
            ScanAvailableLanguages();
        }

#endif

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

            _availableLanguages?.Clear();
            _availableLanguages = null;

            _arrayCache?.Clear();

            _isInitialized = false;
            _currentLanguageCode = DefaultLanguage;
        }

        #endregion
    }
}
