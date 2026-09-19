#!/usr/bin/env pwsh
#Requires -Version 5.1
# Bump vendored BepInEx x64 to the latest upstream within the pinned range and
# rewrite vendor/bepinex/{LICENSE,README.md}. Manual: dev runs this when they
# want a fresh upstream bump, then commits the result. CI never refreshes.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

$module = Join-Path $projectDir 'cameraunlock-core/powershell/ModLoaderSetup.psm1'
if (-not (Test-Path $module)) {
    throw "ModLoaderSetup.psm1 not found at $module. Run 'pixi run sync' to update the cameraunlock-core submodule."
}
Import-Module $module -Force

$out = Join-Path $projectDir 'vendor/bepinex'
Update-VendoredLoader `
    -Name 'bepinex' `
    -OutputDir $out `
    -OutputFileName 'BepInEx_win_x64.zip' `
    -Owner 'BepInEx' -Repo 'BepInEx' `
    -VersionPrefix 'v5.4.' `
    -AssetPattern '^BepInEx_win_x64_.*\.zip$' `
    -LicenseUrl 'https://raw.githubusercontent.com/BepInEx/BepInEx/master/LICENSE' | Out-Null

Write-Host ""
Write-Host "vendor/bepinex refreshed. Review and commit." -ForegroundColor Green
