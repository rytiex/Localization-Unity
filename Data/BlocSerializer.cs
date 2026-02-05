using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PicoShot.Localization.Data
{
    /// <summary>
    /// BLOC (Binary Localization Container) format serializer.
    /// Optimized binary format for fast O(1) lookup with string deduplication.
    /// </summary>
    public static class BlocSerializer
    {
        // Header constants
        private static readonly byte[] Magic = { 0x42, 0x4C, 0x4F, 0x43 }; // "BLOC"
        private const uint Version = 1;
        private const int HeaderSize = 64;
        private const int FooterSize = 32;

        // Flags
        private const uint FlagProtected = 0x01;
        private const uint FlagHasArrays = 0x02;

        /// <summary>
        /// Serializes localization data to BLOC format.
        /// </summary>
        public static byte[] Serialize(LanguageData data, bool protect = true)
        {
            if (data?.Translations == null)
                throw new ArgumentNullException(nameof(data));

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            // Build string pool with deduplication
            var stringPool = BuildStringPool(data.Translations);
            var stringToId = BuildStringToIdMap(stringPool);

            // Calculate entry count (one entry per key per language)
            int entryCount = 0;
            int arrayCount = 0;
            foreach (var entry in data.Translations)
            {
                entryCount += entry.Value.Count;
                foreach (var langEntry in entry.Value)
                {
                    if (langEntry.Value is List<string> || langEntry.Value is string[])
                    {
                        arrayCount++;
                    }
                }
            }

            // Calculate section sizes
            // Entry format: KeyID (4) + LangID (4) + Type (1) + ValueRef (4) = 13 bytes
            int stringPoolSize = CalculateStringPoolSize(stringPool);
            int entryTableSize = entryCount * 13;
            int arrayTableSize = CalculateArrayTableSize(data.Translations, stringToId);

            uint flags = 0;
            if (protect) flags |= FlagProtected;
            if (arrayCount > 0) flags |= FlagHasArrays;

            // Calculate offsets
            long stringPoolOffset = HeaderSize;
            long entryTableOffset = stringPoolOffset + stringPoolSize;
            long arrayTableOffset = arrayCount > 0 ? entryTableOffset + entryTableSize : 0;
            long payloadSize = arrayCount > 0 
                ? arrayTableOffset + arrayTableSize - HeaderSize 
                : entryTableOffset + entryTableSize - HeaderSize;

            // Write header (will rewrite with correct hash later if protected)
            WriteHeader(writer, flags, (uint)entryCount, (uint)arrayCount, (uint)stringPool.Count,
                (ulong)stringPoolOffset, (ulong)entryTableOffset, (ulong)arrayTableOffset, (ulong)payloadSize);

            // Write string pool
            WriteStringPool(writer, stringPool);

            // Write entry table
            var arrayIndexMap = WriteEntryTable(writer, data.Translations, stringToId);

            // Write array table if present
            if (arrayCount > 0)
            {
                WriteArrayTable(writer, data.Translations, stringToId, arrayIndexMap);
            }

            // Write footer (hash placeholder)
            long payloadEnd = ms.Position;
            byte[] hash = new byte[32];
            writer.Write(hash);

            // Compute and write hash if protected
            if (protect)
            {
                ms.Position = 0;
                byte[] fileData = ms.ToArray();
                hash = ComputeHash(fileData, HeaderSize, (int)(payloadEnd - HeaderSize));
                
                ms.Position = payloadEnd;
                writer.Write(hash);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Deserializes BLOC format data.
        /// </summary>
        public static LanguageData Deserialize(byte[] data)
        {
            if (data == null || data.Length < HeaderSize + FooterSize)
                throw new ArgumentException("Data too short to be valid BLOC file", nameof(data));

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms, Encoding.UTF8);

            // Read and validate header
            var header = ReadHeader(reader);
            
            if (!header.Magic.SequenceEqual(Magic))
                throw new InvalidDataException("Invalid BLOC magic number");
            
            if (header.Version != Version)
                throw new InvalidDataException($"Unsupported BLOC version: {header.Version}");

            // Verify hash if protected
            if ((header.Flags & FlagProtected) != 0)
            {
                VerifyHash(data, header);
            }

            // Read string pool
            ms.Position = (long)header.StringPoolOffset + 4; // Skip count, we already know it from header
            var stringPool = ReadStringPool(reader, header.StringCount, header.StringPoolOffset);

            // Read arrays if present
            string[][] arrays = null;
            if ((header.Flags & FlagHasArrays) != 0 && header.ArrayCount > 0)
            {
                arrays = ReadArrays(reader, header, stringPool);
            }

            // Read entry table
            ms.Position = (long)header.EntryTableOffset;
            var translations = ReadEntryTable(reader, header.EntryCount, stringPool, arrays);

            return new LanguageData
            {
                Version = (int)header.Version,
                Translations = translations
            };
        }

        /// <summary>
        /// Deserializes BLOC data from a file.
        /// </summary>
        public static LanguageData DeserializeFromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("BLOC file not found", path);

            byte[] data = File.ReadAllBytes(path);
            return Deserialize(data);
        }

        /// <summary>
        /// Saves language data to a BLOC file.
        /// </summary>
        public static void SaveToFile(string path, LanguageData data, bool protect = true)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            byte[] bytes = Serialize(data, protect);
            File.WriteAllBytes(path, bytes);
        }

        #region Private Methods

        private static void WriteHeader(BinaryWriter writer, uint flags, uint entryCount, uint arrayCount,
            uint stringCount, ulong stringPoolOffset, ulong entryTableOffset, ulong arrayTableOffset, ulong payloadSize)
        {
            writer.Write(Magic);                          // 0x00: Magic (4 bytes)
            writer.Write(Version);                        // 0x04: Version (4 bytes)
            writer.Write(flags);                          // 0x08: Flags (4 bytes)
            writer.Write(entryCount);                     // 0x0C: EntryCount (4 bytes)
            writer.Write(arrayCount);                     // 0x10: ArrayCount (4 bytes)
            writer.Write(stringCount);                    // 0x14: StringCount (4 bytes)
            writer.Write(stringPoolOffset);               // 0x18: StringPoolOffset (8 bytes)
            writer.Write(entryTableOffset);               // 0x20: EntryTableOffset (8 bytes)
            writer.Write(arrayTableOffset);               // 0x28: ArrayTableOffset (8 bytes)
            writer.Write(payloadSize);                    // 0x30: PayloadSize (8 bytes)
            writer.Write(0UL);                            // 0x38: Reserved (8 bytes)
        }

        private static (byte[] Magic, uint Version, uint Flags, uint EntryCount, uint ArrayCount, uint StringCount,
            ulong StringPoolOffset, ulong EntryTableOffset, ulong ArrayTableOffset, ulong PayloadSize) ReadHeader(BinaryReader reader)
        {
            var magic = reader.ReadBytes(4);
            uint version = reader.ReadUInt32();
            uint flags = reader.ReadUInt32();
            uint entryCount = reader.ReadUInt32();
            uint arrayCount = reader.ReadUInt32();
            uint stringCount = reader.ReadUInt32();
            ulong stringPoolOffset = reader.ReadUInt64();
            ulong entryTableOffset = reader.ReadUInt64();
            ulong arrayTableOffset = reader.ReadUInt64();
            ulong payloadSize = reader.ReadUInt64();
            reader.ReadUInt64(); // Skip reserved

            return (magic, version, flags, entryCount, arrayCount, stringCount,
                stringPoolOffset, entryTableOffset, arrayTableOffset, payloadSize);
        }

        private static List<string> BuildStringPool(Dictionary<string, Dictionary<string, object>> translations)
        {
            var poolSet = new HashSet<string>();
            
            foreach (var entry in translations)
            {
                // Add key
                poolSet.Add(entry.Key);
                
                // Add language codes
                foreach (var langCode in entry.Value.Keys)
                {
                    poolSet.Add(langCode);
                }
                
                // Add values
                foreach (var langEntry in entry.Value)
                {
                    if (langEntry.Value is string str)
                    {
                        poolSet.Add(str);
                    }
                    else if (langEntry.Value is List<string> list)
                    {
                        foreach (var item in list)
                        {
                            poolSet.Add(item);
                        }
                    }
                    else if (langEntry.Value is string[] arr)
                    {
                        foreach (var item in arr)
                        {
                            poolSet.Add(item);
                        }
                    }
                }
            }
            
            return poolSet.ToList();
        }

        private static Dictionary<string, uint> BuildStringToIdMap(List<string> pool)
        {
            var map = new Dictionary<string, uint>(pool.Count);
            for (uint i = 0; i < pool.Count; i++)
            {
                map[pool[(int)i]] = i;
            }
            return map;
        }

        private static int CalculateStringPoolSize(List<string> pool)
        {
            int size = 4; // StringCount (uint32)
            size += pool.Count * 2; // Length table (uint16 per string)
            size += pool.Count * 4; // Offset table (uint32 per string)
            
            // Data size
            foreach (var str in pool)
            {
                size += Encoding.UTF8.GetByteCount(str);
            }
            
            return size;
        }

        private static void WriteStringPool(BinaryWriter writer, List<string> pool)
        {
            // Write count
            writer.Write((uint)pool.Count);
            
            // Calculate offsets and write length table
            var offsets = new uint[pool.Count];
            uint currentOffset = (uint)(4 + pool.Count * 2 + pool.Count * 4); // Header + length table + offset table
            
            for (int i = 0; i < pool.Count; i++)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(pool[i]);
                writer.Write((ushort)bytes.Length);
                offsets[i] = currentOffset;
                currentOffset += (uint)bytes.Length;
            }
            
            // Write offset table
            foreach (var offset in offsets)
            {
                writer.Write(offset);
            }
            
            // Write data
            foreach (var str in pool)
            {
                writer.Write(Encoding.UTF8.GetBytes(str));
            }
        }

        private static string[] ReadStringPool(BinaryReader reader, uint count, ulong stringPoolOffset)
        {
            var strings = new string[count];
            var lengths = new ushort[count];
            var offsets = new uint[count];
            
            // Note: Count was already read by the caller to get to this position
            // We are positioned right after the count field
            
            // Read length table
            for (int i = 0; i < count; i++)
            {
                lengths[i] = reader.ReadUInt16();
            }
            
            // Read offset table
            for (int i = 0; i < count; i++)
            {
                offsets[i] = reader.ReadUInt32();
            }
            
            // Read data
            // Offsets stored in file are relative to stringPoolOffset (start of string pool section)
            for (int i = 0; i < count; i++)
            {
                reader.BaseStream.Position = (long)stringPoolOffset + offsets[i];
                byte[] bytes = reader.ReadBytes(lengths[i]);
                strings[i] = Encoding.UTF8.GetString(bytes);
            }
            
            return strings;
        }

        private static int CalculateArrayTableSize(Dictionary<string, Dictionary<string, object>> translations,
            Dictionary<string, uint> stringToId)
        {
            int size = 0;
            
            foreach (var entry in translations)
            {
                foreach (var langEntry in entry.Value)
                {
                    List<string> list = null;
                    
                    if (langEntry.Value is List<string> l)
                        list = l;
                    else if (langEntry.Value is string[] a)
                        list = a.ToList();
                    
                    if (list != null && list.Count > 0)
                    {
                        size += 4; // ArrayID
                        size += 4; // Length
                        size += list.Count * 4; // StringIDs
                    }
                }
            }
            
            return size;
        }

        private static Dictionary<string, int> WriteEntryTable(BinaryWriter writer,
            Dictionary<string, Dictionary<string, object>> translations,
            Dictionary<string, uint> stringToId)
        {
            // Maps "key:lang" to array index for lookup when writing arrays
            var arrayIndexMap = new Dictionary<string, int>();
            int currentArrayIndex = 0;
            
            foreach (var entry in translations)
            {
                string key = entry.Key;
                uint keyId = stringToId[key];
                
                foreach (var langEntry in entry.Value)
                {
                    string langCode = langEntry.Key;
                    object value = langEntry.Value;
                    
                    bool isArray = value is List<string> || value is string[];
                    
                    writer.Write(keyId); // KeyID (4 bytes)
                    writer.Write(stringToId[langCode]); // Language code string ID (4 bytes) - we encode lang as part of composite key
                    writer.Write((byte)(isArray ? 0x01 : 0x00)); // Type (1 byte)
                    
                    if (isArray)
                    {
                        writer.Write((uint)currentArrayIndex); // Array index (4 bytes)
                        arrayIndexMap[$"{key}:{langCode}"] = currentArrayIndex;
                        currentArrayIndex++;
                    }
                    else
                    {
                        string str = value?.ToString() ?? "";
                        writer.Write(stringToId[str]); // String pool index (4 bytes)
                    }
                }
            }
            
            return arrayIndexMap;
        }

        private static void WriteArrayTable(BinaryWriter writer,
            Dictionary<string, Dictionary<string, object>> translations,
            Dictionary<string, uint> stringToId,
            Dictionary<string, int> arrayIndexMap)
        {
            // Write arrays in the order they were indexed
            var orderedArrays = arrayIndexMap.OrderBy(kvp => kvp.Value).ToList();
            
            foreach (var kvp in orderedArrays)
            {
                string[] parts = kvp.Key.Split(':');
                string key = parts[0];
                string langCode = parts[1];
                
                List<string> list = translations[key][langCode] as List<string>;
                if (list == null && translations[key][langCode] is string[] arr)
                {
                    list = arr.ToList();
                }
                
                writer.Write((uint)kvp.Value); // ArrayID
                writer.Write((uint)list.Count); // Length
                
                foreach (var item in list)
                {
                    writer.Write(stringToId[item]); // StringID
                }
            }
        }

        private static Dictionary<string, Dictionary<string, object>> ReadEntryTable(BinaryReader reader,
            uint entryCount, string[] stringPool, string[][] arrays)
        {
            // Group entries by key
            var groupedEntries = new Dictionary<string, Dictionary<string, object>>();
            
            for (int i = 0; i < entryCount; i++)
            {
                // Entry format: KeyID (4) + LangID (4) + Type (1) + ValueRef (4) = 13 bytes
                uint keyId = reader.ReadUInt32();
                uint langId = reader.ReadUInt32();
                byte type = reader.ReadByte();
                uint valueRef = reader.ReadUInt32();
                
                string key = stringPool[keyId];
                string langCode = stringPool[langId];
                object value;
                
                if (type == 0x01 && arrays != null)
                {
                    // Array
                    value = arrays[valueRef].ToList();
                }
                else
                {
                    // Scalar
                    value = stringPool[valueRef];
                }
                
                if (!groupedEntries.TryGetValue(key, out var langDict))
                {
                    langDict = new Dictionary<string, object>();
                    groupedEntries[key] = langDict;
                }
                
                langDict[langCode] = value;
            }
            
            return groupedEntries;
        }

        private static string[][] ReadArrays(BinaryReader reader,
            (byte[] Magic, uint Version, uint Flags, uint EntryCount, uint ArrayCount, uint StringCount,
            ulong StringPoolOffset, ulong EntryTableOffset, ulong ArrayTableOffset, ulong PayloadSize) header,
            string[] stringPool)
        {
            var arrays = new string[header.ArrayCount][];
            
            reader.BaseStream.Position = (long)header.ArrayTableOffset;
            
            for (int i = 0; i < header.ArrayCount; i++)
            {
                uint arrayId = reader.ReadUInt32();
                uint length = reader.ReadUInt32();
                
                var arr = new string[length];
                for (int j = 0; j < length; j++)
                {
                    uint stringId = reader.ReadUInt32();
                    arr[j] = stringPool[stringId];
                }
                
                arrays[arrayId] = arr;
            }
            
            return arrays;
        }

        private static byte[] ComputeHash(byte[] data, int offset, int count)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(data, offset, count);
        }

        private static void VerifyHash(byte[] data, (byte[] Magic, uint Version, uint Flags, uint EntryCount,
            uint ArrayCount, uint StringCount, ulong StringPoolOffset, ulong EntryTableOffset, ulong ArrayTableOffset,
            ulong PayloadSize) header)
        {
            int payloadStart = HeaderSize;
            int payloadEnd = data.Length - FooterSize;
            int payloadSize = payloadEnd - payloadStart;
            
            byte[] computedHash = ComputeHash(data, payloadStart, payloadSize);
            byte[] storedHash = new byte[32];
            Array.Copy(data, payloadEnd, storedHash, 0, 32);
            
            if (!computedHash.SequenceEqual(storedHash))
            {
                throw new InvalidDataException("BLOC file integrity check failed (SHA-256 mismatch)");
            }
        }

        #endregion
    }
}
