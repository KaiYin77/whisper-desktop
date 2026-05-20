# Whisper Local 逐字稿

Windows WPF desktop app for creating transcripts from local video or audio files with a local Whisper model.

## Run

```powershell
dotnet run
```

Drag a media file into the drop zone, choose a local model variant, then click `開始轉錄`.

The app writes the final transcript next to the source file:

```text
video-name.逐字稿.txt
```

## Local Whisper Setup

The app runs a local Whisper CLI process. It does not call a cloud API.

Install dependencies:

```powershell
.\scripts\install-whisper.ps1
```

Whisper model variants in the UI:

- `tiny`
- `base`
- `small`
- `medium`
- `large`
- `large-v2`
- `large-v3`

The first run for a selected variant may download that model's weights to the local Whisper cache. Later runs reuse the local cached model.

## Custom Command

The `Whisper 指令` box defaults to `auto`, which tries:

- `whisper.exe`
- `whisper`
- `python -m whisper`
- `py -m whisper`

You can also enter a custom command prefix, for example:

```text
C:\Path\To\python.exe -m whisper
```

The app appends file path, model, language, output format, and output directory arguments automatically.
