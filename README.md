# Localization Manager for Unity

A modern, modular localization system for Unity games. Replaces MessagePack with System.Text.Json for better compatibility and cleaner architecture.

## Features

- ✅ **No External Dependencies** - Uses System.Text.Json (built into Unity 2020.3+)
- ✅ **Modular Architecture** - Clean separation of concerns
- ✅ **RTL Support** - Full right-to-left text support for Arabic, Hebrew, Persian
- ✅ **Editor Tools** - Comprehensive editor for managing translations
- ✅ **Runtime Efficient** - Optimized for performance with object pooling
- ✅ **Backward Compatible** - Existing code continues to work with minor changes

## Project Structure

```
LocalizationManager/
├── Core/
│   └── LocalizationManager.cs      # Main API and runtime management
├── Data/
│   ├── LanguageData.cs             # Data structures
│   ├── LanguageDefinitions.cs      # Language metadata, fallbacks, names
│   └── LanguageSerializer.cs       # JSON serialization
├── Rtl/
│   ├── ArabicCharacters.cs         # Arabic/Persian letter definitions
│   ├── ArabicGlyphConnector.cs     # Letter connection logic
│   ├── LocalizationRtlManager.cs   # Backward compatibility wrapper
│   ├── RtlTextFixer.cs             # RTL text fixing logic
│   ├── RtlTextHandler.cs           # Public RTL API
│   └── TashkeelHandler.cs          # Arabic diacritics handling
├── Components/
│   └── LocalizationTextComponent.cs# Unity component for text binding
├── Utilities/
│   └── ObjectPool.cs               # StringBuilder pooling
└── Editor/
    ├── LocalizationEditor.cs       # Main editor window
    ├── LocalizationSearchablePopup.cs
    └── LocalizationTextEditorPopup.cs
```

## Quick Start

### 1. Basic Usage

```csharp
using GameDevKit.Localization;

// Get translated text
string text = LocalizationManager.GetText("ui.play_button");

// With format parameters
string welcome = LocalizationManager.GetText("ui.welcome", "Player");

// Get array text (for dropdowns, etc.)
string[] options = LocalizationManager.GetArray("ui.difficulty_options");
string option = LocalizationManager.GetArrayText("ui.difficulty_options", 0);
```

### 2. Language Switching

```csharp
// Set language
LocalizationManager.SetLanguage("ar");

// Detect from system
LocalizationManager.SetLanguage(LocalizationManager.DetectSystemLanguage());
```

### 3. Component Binding

Add `LocalizationTextComponent` to any GameObject with a TMP_Text component:

```csharp
var textComponent = GetComponent<LocalizationTextComponent>();
textComponent.TranslationKey = "ui.settings_title";
```

Or use the runtime API:

```csharp
LocalizationManager.BindText(tmpTextComponent, "ui.settings_title");
```

## File Format

The new format uses a custom binary file with JSON content:

```
Header: "LOCL" (4 bytes) - Localization file signature
Version: int (4 bytes)
Data Length: int (4 bytes)
Data: UTF-8 JSON
```

### JSON Structure

```json
{
  "version": 1,
  "timestamp": 1234567890,
  "translations": {
    "ui.play_button": {
      "en": "Play",
      "ar": "لعب",
      "fr": "Jouer"
    },
    "ui.difficulty": {
      "en": ["Easy", "Medium", "Hard"],
      "ar": ["سهل", "متوسط", "صعب"]
    }
  }
}
```

## Migration from Old System

### API Changes

The new system maintains backward compatibility. Your existing code should work with these minor considerations:

1. **MessagePack dependency removed** - Remove MessagePack from your project
2. **Namespace unchanged** - `GameDevKit.Localization` namespace remains the same
3. **Static API preserved** - `LocalizationManager.GetText()` etc. work the same

### Deprecated APIs (still work but marked obsolete)

```csharp
// These still work but are marked [Obsolete]
LocalizationManager.GetCurrentLanguage();  // Use .CurrentLanguage property
LocalizationManager.IsInitialized();       // Use .IsInitialized property
LocalizationManager.GetSystemLanguage();   // Use LanguageDefinitions.FromSystemLanguage()
```

### Editor Migration

The editor now uses the new serialization format automatically. Old `.dmsl` files in MessagePack format need to be re-exported:

1. Open Language Editor (Tools > Language Editor)
2. Use "Export JSON" to save your data
3. The new format will be used automatically on next save

## Configuration

### Adding New Languages

Edit `Data/LanguageDefinitions.cs`:

```csharp
public static readonly Dictionary<string, string> LanguageNames = new()
{
    { "en", "English" },
    { "your_code", "Your Language" },
    // ...
};
```

### RTL Languages

RTL languages are automatically detected. Add new RTL languages to:

```csharp
public static readonly HashSet<string> RightToLeftLanguages = new()
{
    "ar",   // Arabic
    "he",   // Hebrew
    "fa",   // Persian
    "your_code" // Your RTL language
};
```

## Performance Considerations

1. **Lazy Loading** - Language data is loaded only when needed
2. **Object Pooling** - StringBuilders are pooled to reduce GC
3. **Efficient Lookups** - Uses HashSet and Dictionary with proper comparers
4. **Minimal Allocations** - Cached lookups and string operations

## Events

```csharp
// Subscribe to language change
LocalizationManager.OnLanguageChanged += OnLanguageChanged;

// Missing translation callback
LocalizationManager.OnMissingTranslation += (key) => 
    Debug.LogWarning($"Missing: {key}");
```

## Debug Tools

Use the Language Editor window (Tools > Language Editor) to:
- Add/edit translation keys
- Manage languages
- Test translations
- Import/Export JSON
- Auto-translate with DeepL
- Generate font charsets

## Requirements

- Unity 2020.3 LTS or newer
- TextMeshPro package
- .NET Standard 2.1 or .NET 4.x

## License

This is your code, free to use in your projects.
