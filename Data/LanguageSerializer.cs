using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;

namespace PicoShot.Localization.Data
{
    /// <summary>
    /// Custom JSON serializer for language data that handles both strings and string arrays.
    /// Replaces the previous MessagePack-based serialization.
    /// </summary>
    public static class LanguageSerializer
    {
        // Custom JSON options optimized for language data
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private const int CurrentVersion = 1;
        private const string FileHeader = "LOCL"; // Localization file signature

        /// <summary>
        /// Serializes language data to a binary format (JSON + optional compression).
        /// </summary>
        public static byte[] Serialize(LanguageData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            data.Version = CurrentVersion;
            data.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            string json = JsonSerializer.Serialize(data, JsonOptions);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            // Simple format: Header (4) + Version (4) + Length (4) + Data
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(FileHeader);
            writer.Write(CurrentVersion);
            writer.Write(jsonBytes.Length);
            writer.Write(jsonBytes);

            return ms.ToArray();
        }

        /// <summary>
        /// Deserializes language data from binary format.
        /// </summary>
        public static LanguageData Deserialize(byte[] data)
        {
            if (data == null || data.Length < 12)
                throw new ArgumentException("Invalid data: too short", nameof(data));

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            // Read and verify header
            string header = new string(reader.ReadChars(4));
            if (header != FileHeader)
                throw new InvalidDataException($"Invalid file header. Expected '{FileHeader}', got '{header}'");

            int version = reader.ReadInt32();
            if (version > CurrentVersion)
                Debug.LogWarning($"[LanguageSerializer] File version {version} is newer than supported version {CurrentVersion}");

            int length = reader.ReadInt32();
            byte[] jsonBytes = reader.ReadBytes(length);

            string json = Encoding.UTF8.GetString(jsonBytes);
            return JsonSerializer.Deserialize<LanguageData>(json, JsonOptions);
        }

        /// <summary>
        /// Deserializes language data from a file path.
        /// </summary>
        public static LanguageData DeserializeFromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Language file not found", path);

            byte[] data = File.ReadAllBytes(path);
            return Deserialize(data);
        }

        /// <summary>
        /// Saves language data to a file.
        /// </summary>
        public static void SaveToFile(string path, LanguageData data)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            byte[] bytes = Serialize(data);
            File.WriteAllBytes(path, bytes);
        }

        /// <summary>
        /// Extracts a flattened dictionary for a specific language from the language data.
        /// </summary>
        public static Dictionary<string, object> ExtractLanguageDictionary(
            LanguageData data, 
            string languageCode, 
            string fallbackCode)
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
                else if (languageCode != fallbackCode && translations.TryGetValue(fallbackCode, out var fallbackValue))
                {
                    result[key] = NormalizeValue(fallbackValue);
                }
            }

            return result;
        }

        /// <summary>
        /// Normalizes a value to either string or List<string>.
        /// </summary>
        private static object NormalizeValue(object value)
        {
            if (value == null)
                return null;

            // Already a string list
            if (value is List<string>)
                return value;

            // String array -> List<string>
            if (value is string[] stringArray)
                return new List<string>(stringArray);

            // Object array -> List<string> (from JSON deserialization)
            if (value is object[] objArray)
            {
                var list = new List<string>(objArray.Length);
                foreach (var item in objArray)
                {
                    list.Add(item?.ToString());
                }
                return list;
            }

            // JsonElement array (from System.Text.Json)
            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>(jsonElement.GetArrayLength());
                    foreach (var element in jsonElement.EnumerateArray())
                    {
                        list.Add(element.GetString());
                    }
                    return list;
                }
                return jsonElement.GetString();
            }

            // Default: convert to string
            return value.ToString();
        }

        /// <summary>
        /// Gets all available language codes from the data.
        /// </summary>
        public static HashSet<string> GetAvailableLanguages(LanguageData data)
        {
            var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (data?.Translations == null)
                return languages;

            foreach (var entry in data.Translations)
            {
                foreach (var langCode in entry.Value.Keys)
                {
                    languages.Add(langCode);
                }
            }

            return languages;
        }

        /// <summary>
        /// Gets all translation keys from the data.
        /// </summary>
        public static HashSet<string> GetAllKeys(LanguageData data)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            if (data?.Translations == null)
                return keys;

            foreach (var key in data.Translations.Keys)
            {
                keys.Add(key);
            }

            return keys;
        }
    }
}
