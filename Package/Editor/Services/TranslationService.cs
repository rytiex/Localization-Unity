using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using PicoShot.Localization.Config;
using PicoShot.Localization.Editor.Data;

namespace PicoShot.Localization.Editor.Services
{
    /// <summary>
    /// Service for translating text using DeepL API.
    /// </summary>
    public sealed class TranslationService
    {
        private readonly LanguageEditorData _data;
        private readonly HttpClient _httpClient;
        
        public TranslationService(LanguageEditorData data)
        {
            _data = data;
            _httpClient = new HttpClient();
        }
        
        /// <summary>
        /// Translates a key into all missing target languages.
        /// </summary>
        public async Task TranslateAndFill(string key)
        {
            if (!_data.LanguageData.TryGetValue(key, out var keyData)) return;
            
            string defaultLang = LocalizationConfigProvider.Config.DefaultLanguage;
            
            if (LanguageEditorData.IsArrayKey(keyData))
            {
                await TranslateAndFillArray(key, keyData, defaultLang);
            }
            else
            {
                await TranslateAndFillString(key, keyData, defaultLang);
            }
        }
        
        /// <summary>
        /// Translates a string value for the given key.
        /// </summary>
        private async Task TranslateAndFillString(string key, Dictionary<string, object> keyData, string defaultLang)
        {
            string sourceText = null;
            string sourceLang = defaultLang;
            
            if (keyData.TryGetValue(defaultLang, out var defaultValue) && defaultValue is string defaultStr)
            {
                sourceText = defaultStr;
            }
            else
            {
                foreach (var kvp in keyData)
                {
                    if (kvp.Value is string str && !string.IsNullOrWhiteSpace(str))
                    {
                        sourceText = str;
                        sourceLang = kvp.Key;
                        break;
                    }
                }
            }
            
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                Debug.LogWarning($"The source text is empty for key '{key}', source text must be set to translate.");
                return;
            }
            
            string keyHint = _data.SelectedKey == key ? _data.CurrentKeyHint : "";
            
            foreach (var lang in _data.LanguageCodes.Where(l => l != sourceLang))
            {
                if (!string.IsNullOrWhiteSpace(keyData[lang]?.ToString())) continue;
                
                try
                {
                    var translated = await TranslateText(sourceText, sourceLang, lang, keyHint);
                    if (!string.IsNullOrEmpty(translated))
                    {
                        keyData[lang] = translated;
                        _data.HasUnsavedChanges = true;
                    }
                    
                    await Task.Delay(LanguageEditorData.DeeplRequestDelayMs);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Translation error for {lang}: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Translates array elements for the given key.
        /// Translates element by element, language by language to avoid API rate limits.
        /// </summary>
        private async Task TranslateAndFillArray(string key, Dictionary<string, object> keyData, string defaultLang)
        {
            List<string> sourceArray = null;
            string sourceLang = defaultLang;
            
            if (keyData.TryGetValue(defaultLang, out var defaultValue))
            {
                sourceArray = LanguageEditorData.ConvertToList(defaultValue);
            }
            
            if (sourceArray == null || sourceArray.Count == 0 || sourceArray.All(string.IsNullOrWhiteSpace))
            {
                foreach (var kvp in keyData)
                {
                    var list = LanguageEditorData.ConvertToList(kvp.Value);
                    if (list != null && list.Count > 0 && list.Any(s => !string.IsNullOrWhiteSpace(s)))
                    {
                        sourceArray = list;
                        sourceLang = kvp.Key;
                        break;
                    }
                }
            }
            
            if (sourceArray == null || sourceArray.Count == 0 || sourceArray.All(string.IsNullOrWhiteSpace))
            {
                Debug.LogWarning($"The source array is empty for key '{key}', source text must be set to translate.");
                return;
            }
            
            string keyHint = _data.SelectedKey == key ? _data.CurrentKeyHint : "";
            var targetLanguages = _data.LanguageCodes.Where(l => l != sourceLang).ToList();
            
            // Initialize target arrays if needed
            foreach (var lang in targetLanguages)
            {
                var existingArray = LanguageEditorData.ConvertToList(keyData[lang]);
                if (existingArray != null && existingArray.Count > 0 && existingArray.Any(s => !string.IsNullOrWhiteSpace(s)))
                    continue;
                
                if (existingArray == null)
                {
                    existingArray = new List<string>(new string[sourceArray.Count]);
                    keyData[lang] = existingArray;
                }
            }
            
            // Translate element by element
            for (int i = 0; i < sourceArray.Count; i++)
            {
                string sourceText = sourceArray[i];
                
                if (string.IsNullOrWhiteSpace(sourceText))
                {
                    // Copy empty strings directly
                    foreach (var lang in targetLanguages)
                    {
                        var targetArray = LanguageEditorData.ConvertToList(keyData[lang]);
                        if (targetArray != null && targetArray.Count > i)
                        {
                            targetArray[i] = sourceText;
                            _data.HasUnsavedChanges = true;
                        }
                    }
                    continue;
                }
                
                foreach (var lang in targetLanguages)
                {
                    var targetArray = LanguageEditorData.ConvertToList(keyData[lang]);
                    if (targetArray == null || targetArray.Count <= i)
                        continue;
                    
                    if (!string.IsNullOrWhiteSpace(targetArray[i]))
                        continue;
                    
                    try
                    {
                        var translated = await TranslateText(sourceText, sourceLang, lang, keyHint);
                        targetArray[i] = !string.IsNullOrEmpty(translated) ? translated : sourceText;
                        _data.HasUnsavedChanges = true;
                        await Task.Delay(LanguageEditorData.DeeplRequestDelayMs);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Translation error for {lang}, element {i}: {ex.Message}");
                        targetArray[i] = sourceText;
                        _data.HasUnsavedChanges = true;
                    }
                }
            }
        }
        
        /// <summary>
        /// Translates text using DeepL API.
        /// </summary>
        private async Task<string> TranslateText(string text, string sourceLang, string targetLang, string keyHint = "")
        {
            string deeplSourceLang = sourceLang.ToUpperInvariant();
            string deeplTargetLang = targetLang.ToUpperInvariant();
            
            string context = _data.DeeplContext;
            if (!string.IsNullOrWhiteSpace(keyHint))
            {
                context += $"\n\nSpecific instruction for this text: {keyHint}";
            }
            
            var requestBody = new DeepLTranslateRequest
            {
                text = new[] { text },
                source_lang = deeplSourceLang,
                target_lang = deeplTargetLang,
                context = context
            };
            
            string jsonBody = JsonUtility.ToJson(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Post, _data.DeeplApiUrl);
            request.Content = content;
            request.Headers.Add("Authorization", $"DeepL-Auth-Key {_data.DeeplApiKey}");
            
            try
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var responseJson = await response.Content.ReadAsStringAsync();
                return ParseDeepLResponse(responseJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"DeepL translation failed: {ex.Message}");
                Debug.LogError($"Request Body: {jsonBody}");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Parses DeepL JSON response to extract translated text.
        /// </summary>
        private static string ParseDeepLResponse(string json)
        {
            try
            {
                var wrapper = JsonUtility.FromJson<DeepLResponseWrapper>(json);
                if (wrapper?.translations != null && wrapper.translations.Length > 0)
                {
                    return wrapper.translations[0].text;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse DeepL response: {ex.Message}");
            }
            return null;
        }
        
        [Serializable]
        private class DeepLResponseWrapper
        {
            public DeepLTranslation[] translations;
        }
        
        [Serializable]
        private class DeepLTranslation
        {
            public string detected_source_language;
            public string text;
        }
        
        [Serializable]
        private class DeepLTranslateRequest
        {
            public string[] text;
            public string source_lang;
            public string target_lang;
            public string context;
        }
    }
}
