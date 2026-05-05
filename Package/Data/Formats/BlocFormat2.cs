using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;
using PicoShot.Localization.Utils;
using static PicoShot.Localization.Bloc.BlocFormat;

namespace PicoShot.Localization.Bloc
{
    public unsafe static class BlocFormat2
    {
        public const int VERSION = 2;
        public const int HEADER_SIZE = 34;
        public const int FOOTER_SIZE = 4;

        public const int CONTENT_START = MAGIC_AND_VERSION_SIZE + HEADER_SIZE;

        public const int LANGUAGE_CODE_SIZE = 12;

        public static BlocFormatLayout FormatLayout = new(VERSION, Validate, Serialize, Deserialize);
        public static bool Validate(BinaryReader reader, out string languageCode)
        {
            languageCode = null;

            if (reader.BaseStream.Length < (FILE_MIN_SIZE + HEADER_SIZE + FOOTER_SIZE))
                return false;

            var header = ReadHeader(reader);
            languageCode = Marshal.PtrToStringAnsi((nint)header.languageCode);

            if (header.contentSize < 0 || header.contentSize > 100_000_000) // 100MB sanity check
                return false;

            Crc32 computedCrc = new Crc32();
            computedCrc.Reset();

            var contentData = ArrayPool<byte>.Shared.Rent((int)header.contentSize);
            var contentSpan = contentData.AsSpan(0, (int)header.contentSize);
            try
            {
                if ((header.flags & BlocFlags.IsCompressed) != 0)
                {
                    using var contentSection = new SectionStream(reader.BaseStream, CONTENT_START, reader.BaseStream.Length - (CONTENT_START + FOOTER_SIZE));
                    using var deflateStream = new DeflateStream(contentSection, CompressionMode.Decompress);

                    int totalRead = 0;
                    while (totalRead < header.contentSize)
                    {
                        int r = deflateStream.Read(contentData, totalRead, (int)header.contentSize - totalRead);

                        if (r == 0)
                            break;

                        totalRead += r;
                    }
                }
                else
                    reader.Read(contentSpan);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(contentData);
            }

            uint storedCrc = reader.ReadUInt32();
            computedCrc.Append(contentSpan);

            return storedCrc == computedCrc.GetCurrentHashAsUInt32();
        }
        public static void Serialize(BinaryWriter writer, in IBlocEntry[] entries, string languageCode, CompressionLevel compressionLevel)
        {
            var header = new Header()
            {
                flags = compressionLevel != CompressionLevel.NoCompression ? BlocFlags.IsCompressed : 0,
            };

            Encoding.ASCII.GetBytes(languageCode, new Span<byte>(header.languageCode, LANGUAGE_CODE_SIZE));

            Crc32 contentCrc = new Crc32();
            using (var contentStream = new MemoryStream())
            {
                using var contentWriter = new BinaryWriter(contentStream);

                contentStream.Position = MAGIC_AND_VERSION_SIZE + HEADER_SIZE;

                var stringPool = new Dictionary<string, int>();

                //Write entries
                header.entries.offset = (uint)contentStream.Position; //This offset start from after header
                header.entries.count = (uint)entries.Length;
                foreach (var kvp in entries)
                {
                    if (stringPool.TryGetValue(kvp.Key, out var fi))
                        contentWriter.Write((uint)fi);
                    else
                    {
                        contentWriter.Write((uint)stringPool.Count);
                        stringPool.Add(kvp.Key, stringPool.Count);
                    }

                    switch (kvp)
                    {
                        case StringEntry se:
                            if (stringPool.TryGetValue(se.Value, out var fi1))
                                contentWriter.Write((uint)fi1);
                            else
                            {
                                contentWriter.Write((uint)stringPool.Count);
                                stringPool.Add(se.Value, stringPool.Count);
                            }

                            break;
                        case ArrayEntry ae:
                            contentWriter.Write((uint)(0x80000000 | ae.Values.Length));
                            foreach (var val in ae.Values)
                            {
                                if (stringPool.TryGetValue(val, out var fi2))
                                    contentWriter.Write((uint)fi2);
                                else
                                {
                                    contentWriter.Write((uint)stringPool.Count);
                                    stringPool.Add(val, stringPool.Count);
                                }
                            }
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }

                //Write strings
                header.strings.offset = (uint)contentStream.Position; //This offset start from after header
                header.strings.count = (uint)stringPool.Count;
                foreach (var str in stringPool)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(str.Key);
                    WriteVarInt(contentWriter, (uint)bytes.Length);
                    contentWriter.Write(bytes);
                }

                header.contentSize = (uint)contentStream.Position;

                //Write to main writer
                const int TEMPBUFFER_SIZE = 512;

                writer.BaseStream.Position = MAGIC_AND_VERSION_SIZE + HEADER_SIZE;
                contentStream.Position = 0;

                contentCrc.Reset();

                var tempBuffer = ArrayPool<byte>.Shared.Rent(TEMPBUFFER_SIZE);
                try
                {
                    if (compressionLevel != CompressionLevel.NoCompression)
                    {
                        using (var deflateStream = new DeflateStream(writer.BaseStream, compressionLevel, true))
                        {
                            int totalRead = 0;
                            while (totalRead < contentStream.Length)
                            {
                                int r = contentStream.Read(tempBuffer, 0, TEMPBUFFER_SIZE);
                                if (r == 0)
                                    break;

                                deflateStream.Write(tempBuffer, 0, r);
                                contentCrc.Append(tempBuffer.AsSpan(0, r));

                                totalRead += r;
                            }
                        }
                    }
                    else
                    {
                        int totalRead = 0;
                        while (totalRead < contentStream.Length)
                        {
                            int r = contentStream.Read(tempBuffer, 0, TEMPBUFFER_SIZE);
                            if (r == 0)
                                break;

                            writer.Write(tempBuffer, 0, r);
                            contentCrc.Append(tempBuffer.AsSpan(0, r));

                            totalRead += r;
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tempBuffer);
                }
            }

            //Write footer
            WriteFooter(writer, new Footer()
            {
                contentCRC = contentCrc.GetCurrentHashAsUInt32()
            });

            //Write header, the reason I put the header last is because some values ??are not predictable.
            writer.BaseStream.Position = MAGIC_AND_VERSION_SIZE;
            WriteHeader(writer, header);
        }
        public static void Deserialize(BinaryReader reader, out IBlocEntry[] entries, out BlocInfo info)
        {
            info = new BlocInfo()
            {
                Version = VERSION
            };

            var header = ReadHeader(reader);
            info.LanguageCode = Marshal.PtrToStringAnsi((nint)header.languageCode);

            if (header.contentSize < 0 || header.contentSize > 100_000_000) // 100MB sanity check
                throw new InvalidDataException("Invalid content size");

            int dataSize = (int)reader.BaseStream.Length - CONTENT_START - FOOTER_SIZE;

            var contentData = ArrayPool<byte>.Shared.Rent((int)header.contentSize);
            var contentSpan = contentData.AsSpan(0, (int)header.contentSize);
            try
            {
                if ((header.flags & BlocFlags.IsCompressed) != 0)
                {
                    using var contentSection = new SectionStream(reader.BaseStream, CONTENT_START, dataSize);
                    using var deflateStream = new DeflateStream(contentSection, CompressionMode.Decompress);

                    int totalRead = 0;
                    while (totalRead < header.contentSize)
                    {
                        int r = deflateStream.Read(contentData, totalRead, (int)header.contentSize - totalRead);

                        if (r == 0)
                            break;

                        totalRead += r;
                    }
                }
                else
                    reader.Read(contentSpan);

                Crc32 computedCrc = new Crc32();
                computedCrc.Reset();

                var footer = ReadFooter(reader);
                computedCrc.Append(contentSpan);

                if (footer.contentCRC != computedCrc.GetCurrentHashAsUInt32())
                    throw new FileLoadException("File damaged (CRC mismatch)");

                using var contentStream = new MemoryStream(contentData, 0, contentSpan.Length);
                using var contentReader = new BinaryReader(contentStream);

                contentStream.Position = header.strings.offset;
                var stringPool = new string[header.strings.count];
                for (int i = 0; i < header.strings.count; i++)
                {
                    uint length = ReadVarInt(contentReader);
                    if (length > 100000) // Sanity check
                        throw new InvalidDataException($"Invalid string length: {length}");

                    byte[] bytes = contentReader.ReadBytes((int)length);
                    stringPool[i] = Encoding.UTF8.GetString(bytes);
                }

                info.EntryCount = header.entries.count;

                contentStream.Position = header.entries.offset;
                entries = new IBlocEntry[header.entries.count];
                for (int i = 0; i < header.entries.count; i++)
                {
                    uint keyId = contentReader.ReadUInt32();
                    uint valueRef = contentReader.ReadUInt32();

                    string key = stringPool[keyId];

                    if ((valueRef & 0x80000000) != 0)
                    {
                        int count = (int)(valueRef & 0x7FFFFFFF);
                        var values = new string[count];

                        for (int j = 0; j < count; j++)
                        {
                            uint itemId = contentReader.ReadUInt32();
                            values[j] = stringPool[itemId];
                        }

                        entries[i] = new ArrayEntry()
                        {
                            Key = key,
                            Values = values,
                        };
                    }
                    else
                    {
                        entries[i] = new StringEntry()
                        {
                            Key = key,
                            Value = stringPool[valueRef],
                        };
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(contentData);
            }
        }
        public static Header ReadHeader(BinaryReader reader)
        {
            if (!EnsureLength(reader, HEADER_SIZE))
                throw new InvalidDataException("Size too low");

            var header = new Header();

            header.flags = (BlocFlags)reader.ReadUInt16();
            reader.Read(new Span<byte>(header.languageCode, LANGUAGE_CODE_SIZE));
            header.entries = new(reader.ReadUInt32(), reader.ReadUInt32());
            header.strings = new(reader.ReadUInt32(), reader.ReadUInt32());
            header.contentSize = reader.ReadUInt32();

            return header;
        }
        public static Footer ReadFooter(BinaryReader reader)
        {
            if (!EnsureLength(reader, FOOTER_SIZE))
                throw new InvalidDataException("Size too low");

            return new Footer()
            {
                contentCRC = reader.ReadUInt32()
            };
        }
        public static void WriteHeader(BinaryWriter writer, Header header)
        {
            writer.Write((ushort)header.flags);
            writer.Write(new Span<byte>(header.languageCode, LANGUAGE_CODE_SIZE));

            writer.Write(header.entries.offset);
            writer.Write(header.entries.count);

            writer.Write(header.strings.offset);
            writer.Write(header.strings.count);

            writer.Write(header.contentSize);
        }
        public static void WriteFooter(BinaryWriter writer, Footer footer)
        {
            writer.Write(footer.contentCRC);
        }

        public struct Header//34 byte
        {
            public BlocFlags flags;//Bit flags
            public fixed byte languageCode[LANGUAGE_CODE_SIZE];//ASCII encoded string
            public DataSpan entries;
            public DataSpan strings;
            public uint contentSize;//uncompressed content size
        }

        public struct Footer//4 byte
        {
            public uint contentCRC;//content checksum
        }

        public struct Entry//8* byte
        {
            public uint keyIndex;//key string index

            /// <summary>
            /// isArray: 0x80000000 != 0
            /// 
            /// String Entry:
            /// -value string index: 0xFFFFFFFF
            /// Array Entry:
            /// -value count: 0x7FFFFFFF
            /// </summary>
            public uint stringIndex;
            //uint[] stringIndices;//values string indices
        }

        [Flags]
        public enum BlocFlags : ushort
        {
            None = 0,
            IsCompressed = 1 << 0,
        }
    }
}