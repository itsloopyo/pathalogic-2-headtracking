#!/usr/bin/env pwsh
#Requires -Version 5.1
# Packaging for Pathologic 2 Head Tracking. Produces two ZIPs:
#   - Pathologic2HeadTracking-v{version}-installer.zip (GitHub Release: launcher
#     manifest + install.cmd + plugins/ + vendored BepInEx + docs)
#   - Pathologic2HeadTracking-v{version}-nexus.zip (laid out as the game folder:
#     BepInEx/plugins/*.dll plus the licence notices, no loader)
#
# The Nexus ZIP has a manager route because of the community Pathologic 2 Vortex
# extension (ChemBoy1, nexusmods.com/site/mods/1631). Its queryModPath is
# "Mods", but it registers a "Root Folder" mod type targeting the game folder,
# and its fallback installer copies any archive it has no other installer for
# into that mod type unchanged. This ZIP matches none of the other installers
# (no modinfo.ltx, no P2ModLoader.exe, no root proxy DLL), so it deploys as laid
# out: BepInEx/plugins/ lands under the game folder. BepInEx itself is a
# separate download on the Nexus page and is not bundled here.
#
# The vendored loader under vendor/bepinex is consumed exactly as committed.
# Refreshing it is `pixi run update-deps`, a deliberate act with a commit
# attached, never a side effect of packaging.
#
# No prompts: `pixi run package` allocates no TTY, so any stdin read aborts
# the task.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

$csprojPath = Join-Path $projectDir "src\Pathologic2HeadTracking\Pathologic2HeadTracking.csproj"
$version = Get-CsprojVersion $csprojPath

$buildOutputDir = Join-Path $projectDir "src\Pathologic2HeadTracking\bin\Release\net472"
$scriptsDir = Join-Path $projectDir "scripts"
$releaseDir = Join-Path $projectDir "release"

$modDlls = @("Pathologic2HeadTracking.dll", "CameraUnlock.Core.dll", "CameraUnlock.Core.Unity.dll")
$vendorZipName = "BepInEx_win_x64.zip"

Write-Host "=== Pathologic 2 Head Tracking - Package Release ===" -ForegroundColor Magenta
Write-Host ""
Write-Host "Version: $version" -ForegroundColor Cyan
Write-Host ""

# CameraUnlock.Core is compiled from the submodule working tree, so uncommitted
# edits there go straight into the shipped DLLs. CI checks out the pinned commit
# and would build something else from the same tag, and nothing about the two
# ZIPs would look different. A warning rather than a failure: packaging a local
# build to test an unlanded core change is a normal thing to do.
# git submodule status prefixes a moved pointer with '+' and a dirty tree with '-'
# or 'U'; a clean, correctly-pinned submodule gets a leading space. That catches
# the checked-out-at-another-commit case as well as the uncommitted-edits one,
# which produce the same unreproducible ZIP. Guarded on git being present, since
# this is only a diagnostic and StrictMode makes a missing command terminating.
$coreDrifted = $false
if (Get-Command git -ErrorAction SilentlyContinue) {
    $corePath = Join-Path $projectDir 'cameraunlock-core'
    # Two different drifts, and the prefix only reports one of them. A submodule
    # checked out at another commit gets a '+' from submodule status; one with a
    # dirty working tree keeps its leading space and is only visible to status
    # --porcelain inside it. Both compile something the pinned commit does not.
    $corePointer = & git -C $projectDir submodule status -- cameraunlock-core
    $coreEdits = & git -C $corePath status --porcelain
    $coreDrifted = (($corePointer -join [Environment]::NewLine) -notmatch '^[ ]') -or [bool]$coreEdits
}
if ($coreDrifted) {
    Write-Host "WARNING: cameraunlock-core is not at its pinned commit, or has" -ForegroundColor Yellow
    Write-Host "         uncommitted changes. These ZIPs are NOT" -ForegroundColor Yellow
    Write-Host "         reproducible from the pinned submodule commit, and CI will build" -ForegroundColor Yellow
    Write-Host "         different binaries from the same tag. Commit and push core, then" -ForegroundColor Yellow
    Write-Host "         bump the submodule pointer, before cutting a release." -ForegroundColor Yellow
    Write-Host ""
}

foreach ($dll in $modDlls) {
    $dllPath = Join-Path $buildOutputDir $dll
    if (-not (Test-Path $dllPath)) {
        throw "Required DLL not found: $dllPath"
    }
}

foreach ($script in @("install.cmd", "uninstall.cmd")) {
    $scriptPath = Join-Path $scriptsDir $script
    if (-not (Test-Path $scriptPath)) {
        throw "Required script not found: $scriptPath"
    }
}

if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

# --- GitHub Release ZIP (with installer) ---

Write-Host "--- GitHub Release ZIP ---" -ForegroundColor Yellow
Write-Host ""

$ghStagingDir = Join-Path $releaseDir "staging-github"
if (Test-Path $ghStagingDir) { Remove-Item -Recurse -Force $ghStagingDir }
New-Item -ItemType Directory -Path $ghStagingDir -Force | Out-Null

foreach ($script in @("install.cmd", "uninstall.cmd")) {
    Copy-Item (Join-Path $scriptsDir $script) -Destination $ghStagingDir -Force
    Write-Host "  $script" -ForegroundColor Green
}

# The launcher reads launcher-manifest.json (never mod.json) to deploy the
# package natively. The committed copy is authoritative; only mod_info.version
# is stamped here, and Assert-ManifestSeedsMatchShipped fails the build when a
# seed blob has drifted from the file it seeds rather than quietly refreshing
# it into the ZIP over a stale committed manifest.
$manifestSource = Join-Path $projectDir "launcher-manifest.json"
if (-not (Test-Path $manifestSource)) {
    throw "launcher-manifest.json not found at repo root: $manifestSource"
}
Assert-ManifestSeedsMatchShipped -ManifestPath $manifestSource -ProjectRoot $projectDir
$manifestJson = Get-Content $manifestSource -Raw | ConvertFrom-Json
$manifestJson.mod_info.version = $version
# Set-Content -Encoding UTF8 on Windows PowerShell 5.1 writes a BOM, which
# serde_json rejects. Write through the .NET API with a no-BOM encoder.
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText(
    (Join-Path $ghStagingDir "launcher-manifest.json"),
    ($manifestJson | ConvertTo-Json -Depth 10),
    $utf8NoBom
)
Write-Host "  launcher-manifest.json (v$version)" -ForegroundColor Green

$pluginsDir = Join-Path $ghStagingDir "plugins"
New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutputDir $dll) -Destination $pluginsDir -Force
    Write-Host "  plugins/$dll" -ForegroundColor Green
}

# Vendored BepInEx, as committed. install.cmd extracts this copy and never
# reaches the network at install time.
$vendorSrc = Join-Path $projectDir "vendor\bepinex"
$vendorDest = Join-Path $ghStagingDir "vendor\bepinex"
New-Item -ItemType Directory -Path $vendorDest -Force | Out-Null

# The LGPL-2.1 text has to travel with the archive we redistribute, so a
# missing LICENSE is a compliance failure rather than a skippable step.
foreach ($vendorFile in @($vendorZipName, "LICENSE", "README.md")) {
    $src = Join-Path $vendorSrc $vendorFile
    if (-not (Test-Path $src)) {
        throw "Required vendor file not found: $src. Run 'pixi run update-deps' to refresh, then commit."
    }
    Copy-Item $src -Destination $vendorDest -Force
    Write-Host "  vendor/bepinex/$vendorFile" -ForegroundColor Green
}

# install.cmd / uninstall.cmd resolve the game through shared/find-game.ps1 and
# run the shared per-strategy body, so the ZIP is self-contained without them.
Copy-SharedBundle -StagingDir $ghStagingDir -CoreRoot (Join-Path $projectDir 'cameraunlock-core')

# LICENSE and THIRD-PARTY-NOTICES.md carry the notices every licence here
# requires to accompany the binaries, so a missing one fails the build.
Copy-LicenceNotices -StagingDir $ghStagingDir -ProjectRoot $projectDir -Additional @("README.md", "CHANGELOG.md")

$ghZipName = "Pathologic2HeadTracking-v$version-installer.zip"
$ghZipPath = Join-Path $releaseDir $ghZipName
if (Test-Path $ghZipPath) { Remove-Item $ghZipPath -Force }

Write-Host ""
Write-Host "Creating GitHub ZIP..." -ForegroundColor Cyan

Push-Location $ghStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $ghZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $ghStagingDir

$ghZipSize = (Get-Item $ghZipPath).Length / 1KB
Write-Host ("  $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green

# --- Nexus ZIP (extract into the game folder) ---

Write-Host ""
Write-Host "--- Nexus ZIP ---" -ForegroundColor Yellow
Write-Host ""

$nexusStagingDir = Join-Path $releaseDir "staging-nexus"
if (Test-Path $nexusStagingDir) { Remove-Item -Recurse -Force $nexusStagingDir }
$nexusPluginsDir = Join-Path $nexusStagingDir "BepInEx\plugins"
New-Item -ItemType Directory -Path $nexusPluginsDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutputDir $dll) -Destination $nexusPluginsDir -Force
    Write-Host "  BepInEx/plugins/$dll" -ForegroundColor Green
}

Copy-LicenceNotices -StagingDir $nexusStagingDir -ProjectRoot $projectDir -Additional @("README.md")

$nexusZipName = "Pathologic2HeadTracking-v$version-nexus.zip"
$nexusZipPath = Join-Path $releaseDir $nexusZipName
if (Test-Path $nexusZipPath) { Remove-Item $nexusZipPath -Force }

Write-Host ""
Write-Host "Creating Nexus ZIP..." -ForegroundColor Cyan

Push-Location $nexusStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $nexusZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $nexusStagingDir

$nexusZipSize = (Get-Item $nexusZipPath).Length / 1KB
Write-Host ("  $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# --- Summary ---

Write-Host ""
Write-Host "=== Package Complete ===" -ForegroundColor Magenta
Write-Host ""
Write-Host ("GitHub Release:  $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green
Write-Host ("Nexus:           $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# One path per line for CI capture.
Write-Output $ghZipPath
Write-Output $nexusZipPath
