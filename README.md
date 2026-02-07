# Localization for Unity

[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-blue)](https://unity.com)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A lightweight, zero-runtime-dependency, high-performance localization system for Unity with support for multiple languages, RTL (Right-to-Left) text, binary format (BLOC), and DeepL translation integration.

## Features

- **High Performance** - Binary BLOC format for fast loading and minimal memory footprint
- **100+ Languages** - Support for over 100 languages with native name display
- **RTL Support** - Automatic Right-to-Left text handling for Arabic, Hebrew, Persian, and more
- **Zero Runtime Dependencies** - Only requires TextMeshPro (optional for basic usage)
- **Anti-Tamper** - Optional file hash verification to protect translation files
- **Editor Tools** - Built-in Language Editor with DeepL translation integration
- **Multiple Component Support** - Works with TMP_Text, TMP_Dropdown, Legacy UI, and TextMesh
- **Format Parameters** - String formatting support with `{0}`, `{1}` placeholders

## Quick Start

### 1. Installation

Add the package via Unity Package Manager:

```
https://github.com/PicoShot/Localization-Unity.git?path=/Package
```

### 2. Create Language Files

Open the Language Editor from `Tools > Localization > Language Editor` to:

- Add supported languages
- Create translation keys
- Add translations for each language
- Click on save to export BLOC files to project

### 3. Add to Scene

Add the `Localized Text` component to any GameObject with a text component:

### Via Inspector

1. Add "Localized Text" component
2. Set Translation Key to your key

### 4. Access Translations in Code

```csharp
// Get a simple translation
string greeting = LocalizationManager.GetText("greeting");

// With format parameters
string welcome = LocalizationManager.GetText("welcome_message", "John");

// Get an array of strings
string[] options = LocalizationManager.GetArray("menu_options");
```

## Documentation

- [Usage Guide](docs/Usage.md) - Complete API documentation and usage examples
- [Keybinds](docs/Keybinds.md) - Language Editor keyboard shortcuts
- [BLOC Format](docs/BLOC_FORMAT.md) - Binary localization file format
- [FAQ](docs/FAQ.md) - Frequently asked questions
- [How It Works](docs/HowItWorks.md) - Architecture and implementation details

## Requirements

- Unity 2022.3 or higher
- TextMeshPro (included with Unity)
-

## Special Thanks

Special thanks to my friends for their hot-fixes, suggestions, and help with implementing this project:

- [Xeirel](https://github.com/Xeirel)
- [FeroKro](https://github.com/FeroKro)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---
