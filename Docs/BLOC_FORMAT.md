<div dir="ltr" align=center>
    
 [**Usage**](Usage.md) / [**Keybinds**](Keybinds.md) / [**BLOC Format**](BLOC_FORMAT.md) / [**FAQ**](FAQ.md) / [**How It Works**](HowItWorks.md)

</div>

# BLOC (Binary Localization Container) Format

## Overview

BLOC is a compact binary format designed specifically for localization data. It optimizes for:

- **Small file size** - 50-70% smaller than JSON
- **Fast loading** - O(1) lookups, minimal parsing overhead
- **String deduplication** - Automatic elimination of duplicate text
- **Memory efficiency** - Direct binary representation
- **Optional compression** - Built-in Deflate compression support

## File Structure

### Uncompressed Format

```
┌─────────────────┐
│     Header      │ 24 bytes
├─────────────────┤
│   Entry Table   │ Variable (8+ bytes per entry)
├─────────────────┤
│   String Pool   │ Variable (deduplicated UTF-8)
├─────────────────┤
│  CRC32 Footer   │ 4 bytes
└─────────────────┘
```

### Compressed Format

```
┌─────────────────┐
│     Header      │ 24 bytes (FlagCompressed = 1)
├─────────────────┤
│ UncompressedSize│ 4 bytes
├─────────────────┤
│  CompressedData │ Variable (Deflate stream)
└─────────────────┘
```

---

## Header (24 bytes)

| Offset | Size | Field            | Description                                                     |
| ------ | ---- | ---------------- | --------------------------------------------------------------- |
| 0x00   | 4    | Magic            | "BLOC" (0x42, 0x4C, 0x4F, 0x43)                                 |
| 0x04   | 2    | Version          | Format version (currently 1)                                    |
| 0x06   | 2    | Flags            | Bit flags (see below)                                           |
| 0x08   | 4    | LanguageCode     | ISO language code (e.g., "en\0\0")                              |
| 0x0C   | 4    | EntryCount       | Number of translation entries                                   |
| 0x10   | 4    | StringCount      | Number of unique strings in pool                                |
| 0x14   | 4    | StringPoolOffset | Byte offset to string pool (or uncompressed size if compressed) |

### Flags

| Bit  | Flag       | Description                   |
| ---- | ---------- | ----------------------------- |
| 0    | Compressed | File uses Deflate compression |
| 1-15 | Reserved   | Future use                    |

---

## Compression

BLOC uses **Deflate** compression (same as ZIP/gzip) via `System.IO.Compression`.

### Smart Compression

The serializer automatically checks if compression actually helps:

- If compressed size >= uncompressed - 4 bytes → Store uncompressed
- Small files (< 1KB) often don't benefit from compression

---

## Entry Table

Contains all translation key-value pairs. Variable size depending on data.

### String Entry (8 bytes)

| Field    | Size    | Description                          |
| -------- | ------- | ------------------------------------ |
| KeyId    | 4 bytes | Index into string pool for the key   |
| StringId | 4 bytes | Index into string pool for the value |

### Array Entry (8+ bytes)

| Field       | Size      | Description                                |
| ----------- | --------- | ------------------------------------------ |
| KeyId       | 4 bytes   | Index into string pool for the key         |
| ArrayHeader | 4 bytes   | High bit set + count (0x80000000 \| count) |
| ItemIds     | 4×N bytes | Indices for each array element             |

**Array Header Format:**

- Bit 31: Always 1 (indicates array)
- Bits 0-30: Array item count (max ~2 billion items)

---

## String Pool

Deduplicated storage for all strings used in the file.

### Encoding

Each string uses **VarInt** (variable-length integer) encoding for the length prefix:

```
[VarInt: Length] [UTF-8 Data]
```

### VarInt Encoding

7 bits per byte, continuation flag:

| Value Range      | Bytes Used | Encoding                     |
| ---------------- | ---------- | ---------------------------- |
| 0-127            | 1          | `0xxxxxxx`                   |
| 128-16,383       | 2          | `1xxxxxxx 0xxxxxxx`          |
| 16,384-2,097,151 | 3          | `1xxxxxxx 1xxxxxxx 0xxxxxxx` |

### Deduplication

The string pool eliminates duplicates:

```json
{
  "greeting": "Hello",
  "farewell": "Hello",
  "welcome": "Hello World"
}
```

String pool contents:

1. "greeting"
2. "farewell"
3. "welcome"
4. "Hello" ← Stored once, referenced 3 times
5. "Hello World"

**Savings:** "Hello" (5 bytes) stored once instead of 3 times = 10 bytes saved.

---

## Complete Example

### Input Data (JSON representation)

```json
{
  "ui.play": "Play",
  "ui.quit": "Quit",
  "difficulty.options": ["Easy", "Medium", "Hard"],
  "ui.back": "Back"
}
```

### Uncompressed BLOC (112 bytes)

```
[Header: 24 bytes]
  Magic: "BLOC"
  Version: 1
  Flags: 0 (uncompressed)
  LanguageCode: "en\0\0"
  EntryCount: 4
  StringCount: 9
  StringPoolOffset: 56

[Entry Table: 40 bytes]
  ui.play → "Play"
  ui.quit → "Quit"
  difficulty.options → ["Easy", "Medium", "Hard"]
  ui.back → "Back"

[String Pool: 48 bytes]
  [0-8]: 9 unique strings

[Footer: 4 bytes]
  CRC32
```

### Compressed BLOC (~65 bytes)

```
[Header: 24 bytes]
  Magic: "BLOC"
  Version: 1
  Flags: 1 (compressed)
  ...

[UncompressedSize: 4 bytes]
  112

[CompressedData: ~37 bytes]
  Deflate stream of the 112 bytes
```

---

## Size Comparison

### Without Compression

| Format           | Size       | Notes              |
| ---------------- | ---------- | ------------------ |
| JSON (formatted) | 100%       | Human-readable     |
| JSON (minified)  | 70%        | No whitespace      |
| **BLOC**         | **40-50%** | **Binary + dedup** |
| MessagePack      | 55-65%     | General binary     |

### With Compression

| Format             | Size       | Notes                       |
| ------------------ | ---------- | --------------------------- |
| JSON + gzip        | 25-35%     | Requires decompression      |
| **BLOC + Deflate** | **15-25%** | **Built-in, auto-detected** |
| ZIP archive        | 20-30%     | Multiple files              |

**Real-world example:**

- 280KB JSON
- 170KB BLOC (uncompressed)
- **85KB BLOC (compressed)** ← 70% smaller than JSON!

---

## Technical Specifications

### Limits

| Limit               | Value                 |
| ------------------- | --------------------- |
| Max file size       | 4 GB (32-bit offsets) |
| Max entries         | 4,294,967,295         |
| Max strings in pool | 4,294,967,295         |
| Max string length   | 4,294,967,295 bytes   |
| Max array items     | 2,147,483,647         |

### Compression

- **Algorithm:** Deflate
- **Implementation:** `System.IO.Compression.DeflateStream`
- **Checksum:** CRC32 stored separately (verifies uncompressed data)

### Endianness

Little-endian (standard for Unity/.NET platforms).

---

## Performance

### File Sizes

| Content Type         | JSON  | BLOC  | BLOC+Compress | Savings |
| -------------------- | ----- | ----- | ------------- | ------- |
| UI text (repetitive) | 100KB | 45KB  | **18KB**      | **82%** |
| Long descriptions    | 500KB | 300KB | **120KB**     | **76%** |
| Mixed game content   | 1MB   | 550KB | **220KB**     | **78%** |

### Loading Speed

| Operation     | Time Complexity | Notes              |
| ------------- | --------------- | ------------------ |
| Decompression | O(N)            | Only if compressed |
| Deserialize   | O(N)            | Single pass        |
| Key lookup    | O(1)            | Hash table         |

### Memory Usage

| Phase           | Memory                    |
| --------------- | ------------------------- |
| Serialization   | ~2x file size (temporary) |
| Deserialization | ~1.5x file size (runtime) |
| Runtime lookup  | O(1) additional           |

## Example: Real Game Data

### Input (500 keys, 3 languages)

```
JSON files: 2.8 MB total
BLOC uncompressed: 1.6 MB
BLOC compressed:   0.7 MB
```

### Load Time

```
JSON parse:    ~154ms
BLOC uncompressed: ~12ms
BLOC compressed:   ~18ms  ← Still 2.5x faster than JSON!
```
