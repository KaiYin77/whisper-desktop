# WhisperApp

<p align="center">
  <img src="logo.png" width="80" alt="WhisperApp logo" />
</p>

<p align="center">
  A Windows desktop app for transcribing audio and video files using a <strong>local</strong> OpenAI Whisper model — no cloud API, no internet required after setup.
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
- **100% local** — runs `openai-whisper` on your own machine; nothing leaves your device
- **Model selector** — `tiny` → `large-v3` to trade speed for accuracy
- **Language selector** — auto-detect, or pin to Chinese, English, Japanese, or Korean
- **Transcript per file** — saved next to the source as `filename.逐字稿.txt`
- **Self-contained installer** — bundles the .NET 8 runtime; no pre-install needed

---

## Requirements

| Requirement | Version | Notes |
|-------------|---------|-------|
| Windows | 10 / 11 (64-bit) | |
| Python | 3.10 or later | [python.org](https://www.python.org/downloads/) — add to PATH during install |
| openai-whisper | latest | `pip install openai-whisper` |
| ffmpeg | any | `winget install Gyan.FFmpeg` or [ffmpeg.org](https://ffmpeg.org/download.html) |

> The installer can handle the Whisper + ffmpeg step automatically — see [Installation](#installation).

---

## Installation

### Option A — Download the installer (recommended)

1. Go to the [**Releases**](../../releases/latest) page and download `WhisperApp-Setup.exe`.
2. Run the installer. On the **Select Additional Tasks** screen:
   - Tick **"建立桌面捷徑"** if you want a desktop shortcut.
   - Tick **"安裝 Whisper 相依套件"** to automatically install `openai-whisper` and `ffmpeg` (Python must already be on PATH).
3. Click **Install**, then launch WhisperApp from the Start Menu or desktop shortcut.

> If you skipped step 2, install the dependencies manually:
> ```powershell
> # In a terminal (Python must be on PATH)
> pip install openai-whisper
> winget install Gyan.FFmpeg
> ```

### Option B — Run from source

See [Development](#development) below.

---

## Usage

1. **Add files** — drag audio/video onto the drop zone, or click it to open a file picker.  
   Supported: `mp3` `wav` `m4a` `aac` `flac` `ogg` `wma` `mp4` `mov` `mkv` `avi` `webm`

2. **Pick a model** — left dropdown. Larger models are slower but more accurate.

   | Model | Speed | Best for |
   |-------|-------|----------|
   | `tiny` | ~10× realtime | Quick drafts, clear speech |
   | `base` | ~7× realtime | General use |
   | `small` | ~4× realtime | Better accuracy |
   | `medium` | ~2× realtime | High accuracy |
   | `large-v3` | ~1× realtime | Best quality |

   The first run for a new model downloads its weights to `%USERPROFILE%\.cache\whisper`. Later runs reuse the cached weights offline.

3. **Pick a language** — right dropdown, or leave on **自動偵測** to let Whisper detect it.

4. Click **開始轉錄**. Progress and live log output appear as each file is processed.

5. When a file completes, click **下載 txt** to save the transcript anywhere, or find it automatically next to the source file:
   ```
   C:\Videos\meeting.mp4
   C:\Videos\meeting.逐字稿.txt   ← created by WhisperApp
   ```

6. Click **清除完成項目** to remove finished entries from the queue.

---

## Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) (includes the `dotnet` CLI)
- Any editor — [Visual Studio 2022](https://visualstudio.microsoft.com/) (Community is free), [Rider](https://www.jetbrains.com/rider/), or VS Code with the C# Dev Kit

### Clone and run

```powershell
git clone https://github.com/KaiYin77/whisper-app.git
cd whisper-app/WhisperApp
dotnet run
```

### Common commands

```powershell
# Run in development (hot-reload not supported for WPF, just restart)
dotnet run

# Build only (no launch)
dotnet build

# Run tests (none yet — add under WhisperApp.Tests/)
dotnet test

# Publish self-contained binary for Windows x64
dotnet publish -c Release -r win-x64 --self-contained true
```

### Building the installer

Requires [Inno Setup 6](https://jrsoftware.org/isdl.php) (free download).

```powershell
# From the WhisperApp/ directory — publishes the app then compiles the installer
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
│       └── release.yml          # CI: build + publish installer on tag push
└── WhisperApp/
    ├── App.xaml / App.xaml.cs   # WPF application entry point
    ├── MainWindow.xaml          # Main UI layout (WPF/XAML)
    ├── MainWindow.xaml.cs       # UI logic, Whisper process management
    ├── WhisperApp.csproj        # Project file (.NET 8, WPF, win-x64)
    ├── scripts/
    │   └── install-whisper.ps1  # Installs openai-whisper + ffmpeg
    ├── installer/
    │   └── WhisperApp.iss       # Inno Setup script
    ├── build-installer.ps1      # One-step: publish → compile installer
    ├── logo.png                 # App logo (used in UI + converted to .ico)
    └── app-icon.ico             # Window / taskbar / installer icon
```

### How Whisper is invoked

The app auto-discovers the Whisper CLI — no configuration needed. It checks in this order:

1. `whisper.exe`
2. `whisper`
3. `py -3.12 -m whisper`
4. `python -m whisper`

The resolved command is called with the selected file path, model, language, output format (`txt`), and output directory. stdout and stderr are streamed live to the log panel.

### Contributing

Pull requests are welcome. For significant changes, please open an issue first to align on the approach before writing code.

---

## License

MIT © 2026 KaiYin Hung
