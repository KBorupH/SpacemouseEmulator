# release.ps1
# Flow: tag → build → publish release
# MinVer reads the tag during dotnet publish — version is always in sync.
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root = $PSScriptRoot

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "GitHub CLI not found. Install from https://cli.github.com/"
}

# ── prompt for version ────────────────────────────────────────────────────────
$Current = (git describe --tags --abbrev=0 2>$null) -replace '^v', ''
Write-Host ""
Write-Host " Current version: $Current"
$Input   = Read-Host " New version (e.g. 1.0.1)"
$Version = $Input.Trim() -replace '^v', ''
if (-not $Version) { Write-Error "No version entered." }
$Tag       = "v$Version"
$Installer = "$Root\dist\installer\SpaceMousePilot_Setup_$Version.exe"

Write-Host ""
Write-Host " ============================================"
Write-Host "  SpaceMouse Pilot — Release $Tag"
Write-Host " ============================================"
Write-Host ""

$dirty = & git diff --quiet HEAD 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Uncommitted changes. Press Enter to continue or Ctrl+C to cancel."
    Read-Host | Out-Null
}

# ── tag ───────────────────────────────────────────────────────────────────────
Write-Host "[1/4] Tagging $Tag..."
git tag -f $Tag
git push origin $Tag --force
if ($LASTEXITCODE -ne 0) { Write-Error "git push tag failed" }

# ── build ─────────────────────────────────────────────────────────────────────
Write-Host "[2/4] Building..."
& "$Root\build.ps1"
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed" }

if (-not (Test-Path $Installer)) {
    Write-Error "Installer not found after build: $Installer"
}

# ── release notes ─────────────────────────────────────────────────────────────
Write-Host "[3/4] Preparing notes..."
$Notes      = "$Root\release_notes.md"
$PatchNotes = ""
$Changelog  = "$Root\CHANGELOG.md"

if (Test-Path $Changelog) {
    $content = (Get-Content $Changelog -Raw) -replace "`r`n", "`n"
    if ($content -match "(?ms)##\s+$([regex]::Escape($Version))\s*\n(.*?)(?=\n##\s|\z)") {
        $PatchNotes = $Matches[1].Trim()
        Write-Host "  Patch notes found for $Version"
    } else {
        Write-Host "  No entry for $Version in CHANGELOG.md"
    }
}

$ChangesSection = if ($PatchNotes) { "### Changes`n$PatchNotes`n`n" } else { "" }

@"
## SpaceMouse Pilot $Version

${ChangesSection}### Requirements
- Windows 10/11 x64
- [ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases/latest) — install before launching

### Setup
1. Install ViGEmBus
2. Run the installer
3. Launch SpaceMouse Pilot and click **Start Bridge**
4. Calibrate axes on first use
"@ | Set-Content $Notes -Encoding UTF8

# ── publish ───────────────────────────────────────────────────────────────────
Write-Host "[4/4] Publishing GitHub release..."
$exists = gh release view $Tag 2>$null
if ($LASTEXITCODE -eq 0) {
    gh release upload $Tag $Installer --clobber
    gh release edit   $Tag --title "SpaceMouse Pilot $Tag" --notes-file $Notes --latest
} else {
    gh release create $Tag $Installer --title "SpaceMouse Pilot $Tag" --notes-file $Notes --latest
}

if ($LASTEXITCODE -ne 0) { Remove-Item $Notes -Force 2>$null; Write-Error "gh release failed" }
Remove-Item $Notes -Force 2>$null

Write-Host ""
Write-Host "  Released: $Tag"
Write-Host ""
