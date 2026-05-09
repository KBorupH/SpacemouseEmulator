# build.ps1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root    = $PSScriptRoot

# MinVer sets version from the latest git tag (e.g. v1.0.1 → 1.0.1)
# Read it the same way so the installer filename matches
$Version = (git describe --tags --abbrev=0 2>$null) -replace '^v', ''
if (-not $Version) {
    Write-Warning "No git tag found — defaulting to 0.0.0. Tag a version first: git tag v1.0.0"
    $Version = "0.0.0"
}

$env:SPACEMOUSE_VERSION = $Version

Write-Host ""
Write-Host " ============================================"
Write-Host "  SpaceMouse Pilot — Build  v$Version"
Write-Host " ============================================"
Write-Host ""

# ── publish ───────────────────────────────────────────────────────────────────
Write-Host "[1/2] Publishing..."
dotnet publish "$Root\SpaceMousePilot.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    --output "$Root\dist\app" `
    --nologo

if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed"; exit 1 }

# ── installer ─────────────────────────────────────────────────────────────────
Write-Host "[2/2] Compiling installer..."
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning "Inno Setup not found — skipping. Download: https://jrsoftware.org/isinfo.php"
} else {
    New-Item -ItemType Directory -Force "$Root\dist\installer" | Out-Null
    & $iscc "$Root\installer\spacemouse_pilot.iss"
    if ($LASTEXITCODE -ne 0) { Write-Error "Inno Setup failed"; exit 1 }
}

Write-Host ""
Write-Host " Build complete!"
Write-Host "  EXE:       $Root\dist\app\SpaceMousePilot.exe"
if (Test-Path "$Root\dist\installer") {
    Write-Host "  Installer: $Root\dist\installer\SpaceMousePilot_Setup_$Version.exe"
}
Write-Host ""
