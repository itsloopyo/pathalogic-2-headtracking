#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Release workflow for Pathologic 2 Head Tracking.

.DESCRIPTION
    Resolves the version, checks the preconditions, writes the changelog,
    stamps the version into every file that carries it, builds, commits, tags
    and pushes. The push triggers .github/workflows/release.yml.

    Runs unattended end to end. `pixi run release <version>` is the consent;
    there is no second gate, and there is no stdin to read one from.

.PARAMETER Version
    major | minor | patch | nightly, or a literal X.Y.Z.

.PARAMETER Force
    Ship a release whose commits since the last tag are all noise (writes a
    maintenance changelog entry instead of aborting).

.EXAMPLE
    pixi run release 1.0.0
    pixi run release minor
#>
param(
    [Parameter(Position=0)]
    [string]$Version = "",
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$csprojPath = Join-Path $projectDir "src\Pathologic2HeadTracking\Pathologic2HeadTracking.csproj"
$pluginPath = Join-Path $projectDir "src\Pathologic2HeadTracking\Core\HeadTrackingPlugin.cs"
$installCmdPath = Join-Path $projectDir "scripts\install.cmd"
$manifestPath = Join-Path $projectDir "launcher-manifest.json"
$changelogPath = Join-Path $projectDir "CHANGELOG.md"
$pixiTomlPath = Join-Path $projectDir "pixi.toml"

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

# Mirrors New-ChangelogFromCommits' insertion so a -Force maintenance entry
# lands in the same place with the same shape.
function Add-MaintenanceChangelogEntry {
    param([string]$Path, [string]$NewVersion)
    $date = Get-Date -Format 'yyyy-MM-dd'
    $entry = "## [$NewVersion] - $date`n`n### Changed`n`n- Maintenance release (no user-facing changes).`n`n"
    $changelog = Get-Content $Path -Raw -Encoding UTF8
    if ($changelog -match '(?s)(# Changelog.*?)(## \[)') {
        $changelog = $changelog -replace '(?s)(# Changelog.*?\n\n)', "`$1$entry"
    } else {
        $changelog = $changelog -replace '(?s)(# Changelog.*?\n)', "`$1$entry"
    }
    $changelog = $changelog.TrimEnd() + "`n"
    # UTF-8 without a BOM, like every other file this script rewrites. Set-Content
    # defaults to the system ANSI codepage on Windows PowerShell 5.1, which mangles
    # any non-ASCII character a changelog entry happens to carry.
    [System.IO.File]::WriteAllText($Path, $changelog, (New-Object System.Text.UTF8Encoding $false))
}

Write-Host "=== Pathologic 2 Head Tracking Release ===" -ForegroundColor Cyan
Write-Host ""

$currentVersion = Get-CsprojVersion $csprojPath

# No argument: report and stop. Nothing is mutated, so this is safe to run
# for information.
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "Current version: $currentVersion" -ForegroundColor White
    Write-Host "Usage: pixi run release <major|minor|patch|nightly|X.Y.Z>" -ForegroundColor Yellow
    exit 0
}

if ($Version -eq 'nightly') {
    & (Join-Path $PSScriptRoot 'release-nightly.ps1')
    exit $LASTEXITCODE
}

try {
    $Version = Resolve-ReleaseVersion -Argument $Version -CurrentVersion $currentVersion
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$tagName = "v$Version"

# Preconditions are the whole safety net. Each one exits non-zero with a
# single diagnostic line; none of them asks a question.
$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main") {
    Write-Host "Error: releases run from 'main' (currently on '$currentBranch')" -ForegroundColor Red
    exit 1
}

$status = git status --porcelain
if ($status) {
    Write-Host "Error: working tree has uncommitted changes" -ForegroundColor Red
    Write-Host $status -ForegroundColor Gray
    exit 1
}

if (git tag -l $tagName) {
    Write-Host "Error: tag '$tagName' already exists" -ForegroundColor Red
    exit 1
}

# THIRD-PARTY-NOTICES.md names the cameraunlock-core commit compiled into the
# release ZIPs, and a submodule bump does not touch it. Packaging refuses to
# ship that mismatch, so catch it here rather than in CI after the tag has
# already been pushed.
& (Join-Path $projectDir 'cameraunlock-core\scripts\sync-core-notices.ps1') -Repo $projectDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: THIRD-PARTY-NOTICES.md does not record the pinned cameraunlock-core commit" -ForegroundColor Red
    exit 1
}
& git -C $projectDir diff --quiet -- THIRD-PARTY-NOTICES.md
$noticesResynced = ($LASTEXITCODE -ne 0)

Write-Host "Current version: $currentVersion" -ForegroundColor Gray
Write-Host "New version:     $Version" -ForegroundColor Green
Write-Host ""

# Step 1: the changelog first. It is the gate that aborts when every commit
# since the last tag is noise, and running it before anything is mutated means
# that abort leaves a clean tree rather than a half-applied version bump.
Write-Host "Generating CHANGELOG from commits..." -ForegroundColor Cyan
if (-not (git tag -l)) {
    $date = Get-Date -Format 'yyyy-MM-dd'
    [System.IO.File]::WriteAllText(
        $changelogPath,
        "# Changelog`n`n## [$Version] - $date`n`nFirst release.`n",
        (New-Object System.Text.UTF8Encoding $false))
    Write-Host "  First release - wrote initial CHANGELOG entry" -ForegroundColor Gray
} else {
    try {
        New-ChangelogFromCommits -ChangelogPath $changelogPath -Version $Version -ArtifactPaths @(
            "src/Pathologic2HeadTracking/",
            "cameraunlock-core",
            "scripts/",
            "launcher-manifest.json",
            "assets/",
            "README.md",
            "CHANGELOG.md",
            "LICENSE",
            ".github/"
        )
    } catch {
        if (-not $Force) {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "No user-facing changes to release. Re-run with -Force for a maintenance release." -ForegroundColor Yellow
            exit 1
        }
        Write-Host "No user-facing commits since last tag - writing maintenance entry (-Force)." -ForegroundColor Yellow
        Add-MaintenanceChangelogEntry -Path $changelogPath -NewVersion $Version
    }
}

# Step 2: the canonical version, in the csproj.
Write-Host "Updating version to $Version..." -ForegroundColor Cyan
Set-CsprojVersion $csprojPath $Version

# Step 3: the copies that have to agree with it. BepInPlugin's version is what
# the game's log line reports; MOD_VERSION is what install.cmd writes into the
# state file the launcher reads to spot a stale install; the manifest version
# is what the launcher shows.
$pluginContent = Get-Content $pluginPath -Raw -Encoding UTF8
if ($pluginContent -notmatch 'PluginVersion = "[^"]+"') { throw "PluginVersion constant not found in $pluginPath" }
$pluginContent = $pluginContent -replace 'PluginVersion = "[^"]+"', "PluginVersion = `"$Version`""
[System.IO.File]::WriteAllText($pluginPath, $pluginContent, (New-Object System.Text.UTF8Encoding $false))
Write-Host "  Updated HeadTrackingPlugin.cs" -ForegroundColor Gray

$installCmdContent = Get-Content $installCmdPath -Raw -Encoding UTF8
if ($installCmdContent -notmatch 'set "MOD_VERSION=[^"]*"') { throw "MOD_VERSION line not found in $installCmdPath" }
# install.cmd is CRLF and must stay CRLF - a LF-only .cmd fails silently on
# Windows - so the rewrite goes through the .NET API rather than Set-Content.
$installCmdContent = $installCmdContent -replace 'set "MOD_VERSION=[^"]*"', "set `"MOD_VERSION=$Version`""
[System.IO.File]::WriteAllText($installCmdPath, $installCmdContent, (New-Object System.Text.UTF8Encoding $false))
Write-Host "  Updated install.cmd" -ForegroundColor Gray

$manifestJson = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$manifestJson.mod_info.version = $Version
[System.IO.File]::WriteAllText(
    $manifestPath,
    ($manifestJson | ConvertTo-Json -Depth 10),
    (New-Object System.Text.UTF8Encoding $false)
)
Write-Host "  Updated launcher-manifest.json" -ForegroundColor Gray

# The workspace version. Left unstamped it would sit at the scaffolded 0.0.0 for
# the life of the mod while every other site moved, which reads as a packaging bug
# to anyone who checks the two against each other.
$pixiContent = Get-Content $pixiTomlPath -Raw -Encoding UTF8
# Exactly one, not at least one. -replace is global, and the day pixi.toml grows a
# [feature.x.dependencies] table with its own line-start version pin, a loose match
# would quietly rewrite that pin to the mod version and nothing would catch it.
$pixiVersionMatches = [regex]::Matches($pixiContent, '(?m)^version = "[^"]*"')
if ($pixiVersionMatches.Count -ne 1) {
    throw "expected exactly one workspace version line in $pixiTomlPath, found $($pixiVersionMatches.Count)"
}
$pixiContent = $pixiContent -replace '(?m)^version = "[^"]*"', "version = `"$Version`""
[System.IO.File]::WriteAllText($pixiTomlPath, $pixiContent, (New-Object System.Text.UTF8Encoding $false))
Write-Host "  Updated pixi.toml" -ForegroundColor Gray

# Step 4: build. A release that cannot compile does not get a tag.
Write-Host "Building release..." -ForegroundColor Cyan
Push-Location $projectDir
try {
    # Through pixi, not a bare dotnet build. The reference stubs and the vendored
    # BepInEx are produced by the setup-libs task the build chain depends on, and a
    # release built outside that chain is built against whatever libs/ happens to
    # hold - which on a clean clone is nothing at all.
    pixi run build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: build failed" -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

# Step 5: commit.
Write-Host "Committing changes..." -ForegroundColor Cyan
git add $csprojPath $pluginPath $installCmdPath $manifestPath $changelogPath $pixiTomlPath
if ($noticesResynced) { git add (Join-Path $projectDir 'THIRD-PARTY-NOTICES.md') }
git commit -m "Release v$Version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: commit failed" -ForegroundColor Red
    exit 1
}

# Step 6: tag.
Write-Host "Creating tag $tagName..." -ForegroundColor Cyan
git tag -a $tagName -m "Release $tagName"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: tag creation failed" -ForegroundColor Red
    exit 1
}

# Step 7: push. This is what triggers .github/workflows/release.yml.
Write-Host "Pushing to GitHub..." -ForegroundColor Cyan
git push origin main
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: push failed - the tag exists locally, push it once the branch is pushed" -ForegroundColor Red
    exit 1
}
git push origin $tagName
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: tag push failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Release $tagName initiated." -ForegroundColor Green
Write-Host "  https://github.com/itsloopyo/pathalogic-2-headtracking/actions" -ForegroundColor Cyan
