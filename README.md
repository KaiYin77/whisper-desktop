# WhisperDesktop

<p align="center">
  <img src="logo.png" width="80" alt="WhisperDesktop logo" />
</p>

<p align="center">
  本地端音訊／影片逐字稿工具 — 無需雲端 API、無需 Python，首次執行後完全離線。
</p>

<p align="center">
  <a href="../../releases/latest"><img src="https://img.shields.io/github/v/release/KaiYin77/whisper-desktop?label=download&color=0078D4" alt="Latest release" /></a>
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20macOS-blue" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-8.0-512bd4" alt=".NET 8" />
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License" />
</p>

---

## 下載安裝

**Windows** — 至 [Releases](../../releases/latest) 下載 `WhisperDesktop-Setup.exe` 並執行。

**macOS** — 先安裝 ffmpeg（僅需一次），再下載對應 zip：

```bash
brew install ffmpeg
```

- Apple Silicon (M1/M2/M3)：下載 `WhisperDesktop-macOS-Apple-Silicon.zip`
- Intel Mac：下載 `WhisperDesktop-macOS-Intel.zip`

---

## 使用方式

1. 將音訊或影片拖曳至視窗，或點擊選擇檔案。
2. 選擇模型與語言，點擊 **開始轉錄**。
3. 完成後點擊 **開啟 txt** 以預設編輯器開啟逐字稿，檔案存於原始媒體檔旁的 `檔名-逐字稿.txt`。

> 首次使用某模型時會自動下載權重（75 MB–3 GB），之後離線可用。  
> 輸出格式為繁體中文帶時間碼：`[00:00:03] 今天的主題是...`

---

## 開發

```bash
git clone https://github.com/KaiYin77/whisper-desktop.git
cd whisper-desktop
dotnet run
```

**發布新版本：**

```bash
git tag v2.1.0
git push origin v2.1.0
```

推送 tag 後 GitHub Actions 自動建置 Windows 安裝檔與 macOS zip 並發布至 Releases。

---

## Tech Stack

| | |
|---|---|
| Whisper 推理 | [Whisper.net](https://github.com/sandrohanea/whisper.net) 1.9 |
| 音訊解碼 | NAudio (Windows) / FFMpegCore (macOS) |
| 中文轉換 | [OpenCCNET](https://github.com/laisuk/OpenccNET) 1.1 |
| UI | [Avalonia](https://avaloniaui.net/) 11.1 |

---

## License

MIT © 2026 KaiYin Hung
