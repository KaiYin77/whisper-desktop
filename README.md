# WhisperApp

<p align="center">
  <img src="logo.png" width="80" alt="WhisperApp logo" />
</p>

<p align="center">
  A Windows desktop app for transcribing audio and video files using a <strong>local</strong> Whisper model — no cloud API, no Python, no ffmpeg, no internet required after first run.
</p>

<p align="center">
  <a href="../../releases/latest"><img src="https://img.shields.io/github/v/release/KaiYin77/whisper-app?label=download&color=0078D4" alt="Latest release" /></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-blue" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-8.0-512bd4" alt=".NET 8" />
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License" />
</p>

---

## Features

- **Drag & drop** audio or video files — or click to open a file picker
- **Batch processing** — queue multiple files and transcribe in one run
- **100% local** — Whisper runs entirely on your machine; nothing leaves your device
- **Model selector** — `tiny` → `large-v3` to trade speed for accuracy; models are downloaded once and cached
- **Language selector** — auto-detect, or pin to Chinese, English, Japanese, or Korean
- **Timecoded transcript** — each line is prefixed with `[hh:mm:ss]`
- **Traditional Chinese output** — transcript is automatically converted to Traditional Chinese (Taiwan)
- **Transcript per file** — saved next to the source as `filename-逐字稿.txt`
- **Zero external dependencies** — powered by [Whisper.net](https://github.com/sandrohanea/whisper.net) + [NAudio](https://github.com/naudio/NAudio); no Python or ffmpeg required
- **Self-contained installer** — bundles the .NET 8 runtime; no pre-install needed

---

## Requirements

| Requirement | Notes |
|-------------|-------|
| Windows 10 / 11 (64-bit) | Uses Windows Media Foundation for audio decoding |
| Internet connection (first run only) | To download the selected Whisper model (~75 MB–3 GB depending on model) |

That's it. No Python. No ffmpeg.

---

## Installation

### Option A — Download the installer (recommended)

1. Go to the [**Releases**](../../releases/latest) page and download `WhisperApp-Setup.exe`.
2. Run the installer and follow the prompts.
3. Launch **WhisperApp** from the Start Menu or desktop shortcut.

### Option B — Run from source

See [Development](#development) below.

---

## Usage

1. **Add files** — drag audio/video onto the drop zone, or click it to open a file picker.  
   Supported: `mp3` `wav` `m4a` `aac` `flac` `ogg` `wma` `mp4` `mov` `mkv` `avi` `webm`

2. **Pick a model** — left dropdown. Larger models are slower but more accurate.

   | Model | Best for |
   |-------|----------|
   | `tiny` | Quick drafts, clear speech |
   | `base` | General use (default) |
   | `small` | Better accuracy |
   | `medium` | High accuracy |
   | `large-v3` | Best quality |

   The first time you use a model, its weights are downloaded automatically and cached to `%LOCALAPPDATA%\WhisperApp\models\`. Subsequent runs use the cached file offline.

3. **Pick a language** — right dropdown, or leave on **自動偵測** to let Whisper detect it.

4. Click **開始轉錄**. A progress bar tracks each file as segments are processed.

5. When a file completes, click **下載 txt** to save the transcript anywhere, or find it automatically next to the source file:
   ```
   C:\Videos\meeting.mp4
   C:\Videos\meeting-逐字稿.txt   ← created by WhisperApp
   ```
   Each line is timecoded:
   ```
   [00:00:03] 歡迎來到本次會議。
   [00:00:07] 今天我們要討論的主題是...
   ```

6. Click **清除完成項目** to remove finished entries from the queue.

---

## Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- Any editor — [Visual Studio 2022](https://visualstudio.microsoft.com/) (Community is free), [Rider](https://www.jetbrains.com/rider/), or VS Code with the C# Dev Kit

### Clone and run

```powershell
git clone https://github.com/KaiYin77/whisper-app.git
cd whisper-app
dotnet run
```

### Common commands

```powershell
# Run in development
dotnet run

# Build only
dotnet build

# Publish self-contained binary for Windows x64
dotnet publish -c Release -r win-x64 --self-contained true
```

### Building the installer

Requires [Inno Setup 6](https://jrsoftware.org/isdl.php) (free download).

```powershell
# Publishes the app then compiles the installer
.\build-installer.ps1
# Output: dist\WhisperApp-Setup.exe
```

### Releasing a new version

Push a version tag — GitHub Actions builds and publishes the installer to GitHub Releases automatically:

```powershell
git tag v1.2.0
git push origin v1.2.0
```

The CI workflow (`.github/workflows/release.yml`) runs on `windows-latest`, installs Inno Setup via Chocolatey, runs `build-installer.ps1`, and attaches `WhisperApp-Setup.exe` to the release with auto-generated release notes.

### Project structure

```
whisper-app/
├── .github/
│   └── workflows/
│       └── release.yml         # CI: build + publish installer on tag push
├── installer/
│   └── WhisperApp.iss          # Inno Setup script
├── App.xaml / App.xaml.cs      # WPF application entry point
├── MainWindow.xaml             # Main UI layout (WPF/XAML)
├── MainWindow.xaml.cs          # UI logic + transcription pipeline
├── WhisperApp.csproj           # Project file (.NET 8, WPF, win-x64)
├── build-installer.ps1         # One-step: publish → compile installer
├── logo.png                    # App logo
└── app-icon.ico                # Window / taskbar / installer icon
```

### Tech stack

| Component | Library |
|-----------|---------|
| Whisper inference | [Whisper.net](https://github.com/sandrohanea/whisper.net) 1.9 (whisper.cpp bindings) |
| Audio decoding | [NAudio](https://github.com/naudio/NAudio) 2.2 + Windows Media Foundation |
| Chinese conversion | [OpenCCNET](https://github.com/laisuk/OpenccNET) 1.1 (Simplified → Traditional) |
| UI framework | WPF (.NET 8) |

### Contributing

Pull requests are welcome. For significant changes, please open an issue first to align on the approach before writing code.

---

## License

MIT © 2026 KaiYin Hung
