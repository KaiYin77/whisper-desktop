Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Installing local Whisper dependencies..."

if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    throw "Python was not found on PATH. Install Python 3.10+ first."
}

python -m pip install --upgrade pip
python -m pip install --upgrade openai-whisper

if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
    Write-Host "ffmpeg was not found on PATH."
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        Write-Host "Installing ffmpeg with winget..."
        winget install --id Gyan.FFmpeg --exact --source winget --accept-package-agreements --accept-source-agreements
    }
    else {
        Write-Host "Install ffmpeg manually and make sure ffmpeg.exe is on PATH."
    }
}

Write-Host "Done. Restart WhisperApp after installation so PATH changes are loaded."
