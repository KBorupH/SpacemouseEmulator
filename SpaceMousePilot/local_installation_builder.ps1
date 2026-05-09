# build.ps1 — local build and installer for testing
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root    = $PSScriptRoot
$Version = (git describe --tags --abbrev=0 2>$null) -replace '^v', ''
if (-not $Version) { $Version = "0.0.0-local" }

$env:SPACEMOUSE_VERSION = $Version

Write-Host ""
Write-Host " SpaceMouse Pilot — Local Build  v$Version"
Write-Host ""

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
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "[2/2] Building installer..."
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning "Inno Setup not found — EXE only. Download: https://jrsoftware.org/isinfo.php"
} else {
    New-Item -ItemType Directory -Force "$Root\dist\installer" | Out-Null
    & $iscc "$Root\installer\spacemouse_pilot.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }
    Write-Host ""
    Write-Host " Installer: $Root\dist\installer\SpaceMousePilot_Setup_$Version.exe"
}

Write-Host " EXE:       $Root\dist\app\SpaceMousePilot.exe"
Write-Host ""