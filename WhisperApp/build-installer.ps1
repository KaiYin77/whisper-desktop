Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$Project = Join-Path $ProjectRoot "WhisperApp.csproj"
$PublishDir = Join-Path $ProjectRoot "bin\Release\net8.0-windows\win-x64\publish"
$IssScript = Join-Path $ProjectRoot "installer\WhisperApp.iss"

# --- Step 1: dotnet publish (self-contained, win-x64) ---
Write-Host ">>> Building self-contained publish..." -ForegroundColor Cyan
dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=true

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
Write-Host "    Published to: $PublishDir" -ForegroundColor Green

# --- Step 2: Find Inno Setup compiler (ISCC.exe) ---
$isccOnPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
$IssccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
if ($isccOnPath) { $IssccCandidates += $isccOnPath.Source }
$IssccCandidates = $IssccCandidates | Where-Object { $_ -and (Test-Path $_) }

if (-not $IssccCandidates) {
    Write-Host ""
    Write-Host "Inno Setup not found. Install it from:" -ForegroundColor Yellow
    Write-Host "  https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "After installing, re-run this script." -ForegroundColor Yellow
    exit 1
}

$Iscc = $IssccCandidates[0]
Write-Host ">>> Found ISCC: $Iscc" -ForegroundColor Cyan

# --- Step 3: Compile installer ---
Write-Host ">>> Compiling installer..." -ForegroundColor Cyan
& $Iscc $IssScript

if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed (exit $LASTEXITCODE)" }

$Output = Join-Path $ProjectRoot "dist\WhisperApp-Setup.exe"
Write-Host ""
Write-Host "Done! Installer: $Output" -ForegroundColor Green
