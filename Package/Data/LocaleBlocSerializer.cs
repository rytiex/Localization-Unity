using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace PicoShot.Localization.Data
{
    /// <summary>
    /// BLOC (Binary Localization Container) format serializer.
    /// Optimized binary format with optional compression and string deduplication.
    /// </summary>
    public static class LocaleBlocSerializer
    {
        private static readonly byte[] Magic = { 0x42, 0x4C, 0x4F, 0x43 }; // "BLOC"
        private const int Version = 1;

        // Flags
        private const ushort FlagCompressed = 0x01;

        /// <summary>
        /// Compression level for BLOC files. Set to Optimal for best compression,
        /// Fastest for quicker saves, or NoCompression to disable.
        /// </summary>
        public static CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

        /// <summary>
        /// Serializes locale data to BLOC format with optional compression.
        /// </summary>
        public static byte[] Serialize(LocaleData data, bool compress = true)
        {
            if (data?.Translations == null)
                throw new ArgumentNullException(nameof(data));

            // Build string pool (deduplication)
            var stringPool = BuildStringPool(data.Translations);
            var stringToId = BuildStringToIdMap(stringPool);

            // Calculate sizes
            int entryTableSize = CalculateEntryTableSize(data.Translations);
            int stringPoolSize = CalculateStringPoolSize(stringPool);
            int uncompressedSize = 24 + entryTableSize + stringPoolSize + 4; // header + table + pool + footer

            byte[] uncompressedData;
            using (var ms = new MemoryStream(uncompressedSize))
            using (var writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                int stringPoolOffset = 24 + entryTableSize;

                WriteHeader(writer, data.LanguageCode, (uint)data.Translations.Count, (uint)stringPool.Count,
                    (uint)stringPoolOffset, false);

                WriteEntryTable(writer, data.Translations, stringToId);

                // Write string pool
                WriteStringPool(writer, stringPool);

                uncompressedData = ms.ToArray();
                uint crc = ComputeCrc32(uncompressedData, 0, uncompressedData.Length);
                
                ms.Position = 0;
                writer.Write(uncompressedData, 0, uncompressedData.Length);
                writer.Write(crc);
                
                uncompressedData = ms.ToArray();
            }

            if (compress && CompressionLevel != CompressionLevel.NoCompression)
            {
                byte[] compressed = CompressData(uncompressedData);
                
                if (compressed.Length < uncompressedData.Length - 4)
                {
                    using var resultMs = new MemoryStream(8 + compressed.Length);
                    using var resultWriter = new BinaryWriter(resultMs, Encoding.UTF8);
                    
                    WriteHeader(resultWriter, data.LanguageCode, (uint)data.Translations.Count, (uint)stringPool.Count,
                        (uint)uncompressedData.Length, true);
                    
                    resultWriter.Write(uncompressedData.Length);
                    
                    resultWriter.Write(compressed);
                    
                    return resultMs.ToArray();
                }
            }

            return uncompressedData;
        }

        /// <summary>
        /// Deserializes BLOC format data.
        /// </summary>
        public static LocaleData Deserialize(byte[] data)
        {
            if (data == null || data.Length < 28)
                throw new ArgumentException("Data too short", nameof(data));

            // Verify magic
            if (data[0] != Magic[0] || data[1] != Magic[1] ||
                data[2] != Magic[2] || data[3] != Magic[3])
                throw new InvalidDataException("Invalid BLOC magic");

            // Check compression flag
            ushort flags = BitConverter.ToUInt16(data, 6);
            bool isCompressed = (flags & FlagCompressed) != 0;

            byte[] uncompressedData;
            
            if (isCompressed)
            {
                if (data.Length < 32)
                    throw new InvalidDataException("Compressed data too short");
                
                int uncompressedSize = BitConverter.ToInt32(data, 24);
                if (uncompressedSize < 0 || uncompressedSize > 100_000_000) // 100MB sanity check
                    throw new InvalidDataException("Invalid uncompressed size");
                
                uncompressedData = DecompressData(data, 28, data.Length - 28, uncompressedSize);
            }
            else
            {
                uncompressedData = data;
            }

            using var ms = new MemoryStream(uncompressedData);
            using var reader = new BinaryReader(ms, Encoding.UTF8);

            // Read header
            var header = ReadHeader(reader);

            if (header.Version != Version)
                throw new InvalidDataException($"Version {header.Version} not supported");

            // Read entry table
            ms.Position = 24; // After header
            var translations = ReadEntryTable(reader, header.EntryCount, header.StringPoolOffset, header.StringCount);

            return new LocaleData
            {
                Version = (int)header.Version,
                LanguageCode = header.LanguageCode,
                Translations = translations
            };
        }

        /// <summary>
        /// Deserializes BLOC data from a file.
        /// </summary>
        public static LocaleData DeserializeFromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("BLOC file not found", path);

            byte[] data = File.ReadAllBytes(path);
            return Deserialize(data);
        }

        /// <summary>
        /// Saves locale data to a BLOC file.
        /// </summary>
        public static void SaveToFile(string path, LocaleData data, bool compress = true)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            byte[] bytes = Serialize(data, compress);
            File.WriteAllBytes(path, bytes);
        }

        #region Compression

        private static byte[] CompressData(byte[] data)
        {
            using var outputMs = new MemoryStream();
            using (var deflateStream = new DeflateStream(outputMs, CompressionLevel, true))
            {
                deflateStream.Write(data, 0, data.Length);
            }
            return outputMs.ToArray();
        }

        private static byte[] DecompressData(byte[] compressedData, int offset, int count, int uncompressedSize)
        {
            byte[] result = new byte[uncompressedSize];
            
            using var inputMs = new MemoryStream(compressedData, offset, count);
            using var deflateStream = new DeflateStream(inputMs, CompressionMode.Decompress);
            
            int totalRead = 0;
            while (totalRead < uncompressedSize)
            {
                int read = deflateStream.Read(result, totalRead, uncompressedSize - totalRead);
                if (read == 0)
                    throw new InvalidDataException("Decompression incomplete");
                totalRead += read;
            }
            
            return result;
        }

        #endregion

        #region Header (24 bytes)

        private static void WriteHeader(BinaryWriter writer, string languageCode,
            uint entryCount, uint stringCount, uint stringPoolOffset, bool compressed)
        {
            ushort flags = compressed ? FlagCompressed : (ushort)0;
            
            writer.Write(Magic);                          // 0-3: Magic
            writer.Write((ushort)Version);                // 4-5: Version
            writer.Write(flags);                          // 6-7: Flags

            // Language code (4 bytes, null-padded ASCII)
            byte[] langBytes = new byte[4];
            byte[] inputBytes = Encoding.ASCII.GetBytes(languageCode ?? "en");
            int copyLen = Math.Min(inputBytes.Length, 4);
            Array.Copy(inputBytes, langBytes, copyLen);
            writer.Write(langBytes);                      // 8-11: Language code

            writer.Write(entryCount);                     // 12-15: Entry count
            writer.Write(stringCount);                    // 16-19: String count
            writer.Write(stringPoolOffset);               // 20-23: String pool offset (or uncompressed size if compressed)
        }

        private static (ushort Version, string LanguageCode, uint EntryCount, 
            uint StringCount, uint StringPoolOffset) ReadHeader(BinaryReader reader)
        {
            reader.ReadBytes(4); // Skip magic
            ushort version = reader.ReadUInt16();
            reader.ReadBytes(2); // Skip flags

            byte[] langBytes = reader.ReadBytes(4);
            int len = 0;
            while (len < 4 && langBytes[len] != 0) len++;
            string languageCode = Encoding.ASCII.GetString(langBytes, 0, len);

            uint entryCount = reader.ReadUInt32();
            uint stringCount = reader.ReadUInt32();
            uint stringPoolOffset = reader.ReadUInt32();

            return (version, languageCode, entryCount, stringCount, stringPoolOffset);
        }

        #endregion

        #region Entry Table

        private static int CalculateEntryTableSize(Dictionary<string, object> translations)
        {
            int size = 0;
            foreach (var kvp in translations)
            {
                size += 4; // Key ID
                
                if (kvp.Value is List<string> list)
                {
                    size += 4; // Array header
                    size += list.Count * 4; // Item IDs
                }
                else if (kvp.Value is string[] arr)
                {
                    size += 4; // Array header
                    size += arr.Length * 4; // Item IDs
                }
                else
                {
                    size += 4; // String ID
                }
            }
            return size;
        }

        private static void WriteEntryTable(BinaryWriter writer,
            Dictionary<string, object> translations, Dictionary<string, uint> stringToId)
        {
            foreach (var kvp in translations)
            {
                // Key ID
                writer.Write(stringToId[kvp.Key]);

                // Value
                if (kvp.Value is List<string> list)
                {
                    writer.Write((uint)(0x80000000 | list.Count));
                    foreach (var item in list)
                        writer.Write(stringToId[item ?? ""]);
                }
                else if (kvp.Value is string[] arr)
                {
                    writer.Write((uint)(0x80000000 | arr.Length));
                    foreach (var item in arr)
                        writer.Write(stringToId[item ?? ""]);
                }
                else
                {
                    writer.Write(stringToId[kvp.Value?.ToString() ?? ""]);
                }
            }
        }

        private static Dictionary<string, object> ReadEntryTable(BinaryReader reader,
            uint entryCount, uint stringPoolOffset, uint stringCount)
        {
            var translations = new Dictionary<string, object>((int)entryCount, StringComparer.Ordinal);

            // First, read string pool
            long savedPosition = reader.BaseStream.Position;
            reader.BaseStream.Position = stringPoolOffset;
            var stringPool = ReadStringPool(reader, stringCount);
            reader.BaseStream.Position = savedPosition;

            for (int i = 0; i < entryCount; i++)
            {
                uint keyId = reader.ReadUInt32();
                uint valueRef = reader.ReadUInt32();

                string key = stringPool[keyId];

                if ((valueRef & 0x80000000) != 0)
                {
                    int count = (int)(valueRef & 0x7FFFFFFF);
                    var list = new List<string>(count);
                    for (int j = 0; j < count; j++)
                    {
                        uint itemId = reader.ReadUInt32();
                        list.Add(stringPool[itemId]);
                    }
                    translations[key] = list;
                }
                else
                {
                    translations[key] = stringPool[valueRef];
                }
            }

            return translations;
        }

        #endregion

        #region String Pool

        private static List<string> BuildStringPool(Dictionary<string, object> translations)
        {
            var pool = new HashSet<string>(StringComparer.Ordinal);

            foreach (var kvp in translations)
            {
                pool.Add(kvp.Key);

                if (kvp.Value is List<string> list)
                {
                    foreach (var item in list)
                        if (item != null) pool.Add(item);
                }
                else if (kvp.Value is string[] arr)
                {
                    foreach (var item in arr)
                        if (item != null) pool.Add(item);
                }
                else if (kvp.Value is string str)
                {
                    pool.Add(str);
                }
            }

            return pool.ToList();
        }

        private static Dictionary<string, uint> BuildStringToIdMap(List<string> pool)
        {
            var map = new Dictionary<string, uint>(pool.Count, StringComparer.Ordinal);
            for (uint i = 0; i < pool.Count; i++)
                map[pool[(int)i]] = i;
            return map;
        }

        private static int CalculateStringPoolSize(List<string> pool)
        {
            int size = 0;
            foreach (var str in pool)
            {
                int byteCount = Encoding.UTF8.GetByteCount(str);
                size += GetVarIntSize((uint)byteCount);
                size += byteCount;
            }
            return size;
        }

        private static void WriteStringPool(BinaryWriter writer, List<string> pool)
        {
            foreach (var str in pool)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(str);
                WriteVarInt(writer, (uint)bytes.Length);
                writer.Write(bytes);
            }
        }

        private static string[] ReadStringPool(BinaryReader reader, uint count)
        {
            var pool = new string[count];

            for (int i = 0; i < count; i++)
            {
                uint length = ReadVarInt(reader);
                if (length > 100000) // Sanity check
                    throw new InvalidDataException($"Invalid string length: {length}");
                byte[] bytes = reader.ReadBytes((int)length);
                pool[i] = Encoding.UTF8.GetString(bytes);
            }

            return pool;
        }

        #endregion

        #region Variable-Length Integer Encoding

        private static int GetVarIntSize(uint value)
        {
            if (value <= 0x7F) return 1;
            if (value <= 0x3FFF) return 2;
            if (value <= 0x1FFFFF) return 3;
            if (value <= 0xFFFFFFF) return 4;
            return 5;
        }

        private static void WriteVarInt(BinaryWriter writer, uint value)
        {
            while (value >= 0x80)
            {
                writer.Write((byte)(value | 0x80));
                value >>= 7;
            }
            writer.Write((byte)value);
        }

        private static uint ReadVarInt(BinaryReader reader)
        {
            uint result = 0;
            int shift = 0;
            byte b;
            do
            {
                b = reader.ReadByte();
                result |= (uint)(b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return result;
        }

        #endregion

        #region Checksum

        private static uint ComputeCrc32(byte[] data, int offset, int count)
        {
            const uint polynomial = 0xEDB88320;
            uint crc = 0xFFFFFFFF;

            for (int i = offset; i < offset + count; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                    crc = (crc >> 1) ^ (polynomial & ~(crc & 1));
            }

            return ~crc;
        }

        #endregion
    }
}
