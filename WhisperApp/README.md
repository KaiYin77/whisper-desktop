# WhisperApp

<p align="center">
  <img src="logo.png" width="80" alt="WhisperApp logo" />
</p>

<p align="center">
  A Windows desktop app for transcribing audio and video files using a <strong>local</strong> OpenAI Whisper model — no cloud API, no internet required.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-blue" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-8.0-512bd4" alt=".NET 8" />
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License" />
</p>

---

## Features

- **Drag & drop** audio or video files — or click to open a file picker
- **Batch processing** — queue multiple files in one run
- **Local inference** — runs `openai-whisper` on your own machine; nothing leaves your device
- **Model selector** — choose from `tiny` → `large-v3` to trade speed for accuracy
- **Language selector** — auto-detect or pin to Chinese, English, Japanese, or Korean
- **Transcript per file** — output saved next to the source as `filename.逐字稿.txt`
- **One-click installer** — self-contained `.exe` bundles the .NET 8 runtime; no pre-install needed

---

## Requirements

| Requirement | Notes |
|-------------|-------|
| Windows 10 / 11 (64-bit) | |
| Python 3.10 or later | [python.org](https://www.python.org/downloads/) |
| openai-whisper | `pip install openai-whisper` |
| ffmpeg | Install via `winget install Gyan.FFmpeg` or [ffmpeg.org](https://ffmpeg.org/download.html) |

> **Note:** The installer can run these steps automatically — see [Installation](#installation) below.

---

## Installation

### Option A — Installer (recommended)

1. Download `WhisperApp-Setup.exe` from the [Releases](../../releases/latest) page.
2. Run the installer. During setup you can tick **"Install Whisper dependencies"** to let the installer run `scripts/install-whisper.ps1` automatically (requires Python already on PATH).
3. Launch **WhisperApp** from the Start Menu or Desktop shortcut.

### Option B — Build from source

```powershell
git clone https://github.com/KaiYin77/whisper-app.git
cd whisper-app/WhisperApp
dotnet run
```

---

## Usage

1. **Drop files** onto the drop zone, or click it to open a file picker.  
   Supported formats: `mp3` `wav` `m4a` `aac` `flac` `ogg` `wma` `mp4` `mov` `mkv` `avi` `webm`

2. **Select a model** from the left dropdown.

   | Model | Speed | Accuracy |
   |-------|-------|----------|
   | `tiny` | fastest | lowest |
   | `base` | fast | good for clear speech |
   | `small` | moderate | better accuracy |
   | `medium` | slower | high accuracy |
   | `large` / `large-v2` / `large-v3` | slowest | best accuracy |

   The first run for a new model downloads its weights to the local Whisper cache (`~/.cache/whisper`). Subsequent runs reuse the cached weights.

3. **Select a language** from the right dropdown, or leave on **自動偵測** to let Whisper infer it.

4. Click **開始轉錄**. Progress and log output appear in real time.

5. When a file completes, click **下載 txt** to save the transcript anywhere, or find it automatically next to the source file:
   ```
   /your/folder/recording.mp4
   /your/folder/recording.逐字稿.txt   ← created by WhisperApp
   ```

6. Click **清除完成項目** to remove finished entries from the list.

---

## Building the Installer

Requires [Inno Setup 6](https://jrsoftware.org/isdl.php) (free).

```powershell
# From the WhisperApp directory
.\build-installer.ps1
```

This script:
1. Runs `dotnet publish` (self-contained, `win-x64`, Release)
2. Compiles `installer/WhisperApp.iss` with Inno Setup
3. Outputs `dist/WhisperApp-Setup.exe`

---

## Project Structure

```
WhisperApp/
├── MainWindow.xaml          # Main UI layout
├── MainWindow.xaml.cs       # UI logic, Whisper process management
├── App.xaml / App.xaml.cs   # WPF app entry point
├── scripts/
│   └── install-whisper.ps1  # Installs openai-whisper + ffmpeg via winget
├── installer/
│   └── WhisperApp.iss       # Inno Setup installer script
├── build-installer.ps1      # One-step build → installer script
├── logo.png                 # App logo
└── app-icon.ico             # Window / taskbar icon
```

---

## How Whisper Is Invoked

The app discovers the Whisper CLI automatically — no configuration needed. It checks in this order:

1. `whisper.exe`
2. `whisper`
3. `py -3.12 -m whisper`
4. `python -m whisper`

The resolved command is called with the selected file, model, language, and output flags. Standard output and stderr are streamed live to the log panel.

---

## Contributing

Pull requests are welcome. For significant changes, please open an issue first to discuss what you'd like to change.

```powershell
# Run the app in development
dotnet run

# Build (no run)
dotnet build
```

---

## License

[MIT](LICENSE)
