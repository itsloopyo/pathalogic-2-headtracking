#!/usr/bin/env pwsh
#Requires -Version 5.1
# Thin wrapper - dev-deploy orchestration lives in
# cameraunlock-core/powershell/DevDeploy.psm1.

param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory=$false, Position=1)]
    [string]$GivenPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\DevDeploy.psm1") -Force
Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\ModDeployment.psm1") -Force

$buildOutput = Join-Path $projectRoot "src\Pathologic2HeadTracking\bin\$Configuration\net472"
$vendorZip = Join-Path $projectRoot "vendor\bepinex\BepInEx_win_x64.zip"

$result = Invoke-DevDeployBepInEx `
    -GameId 'pathologic-2' `
    -GameDisplayName 'Pathologic 2' `
    -BuildOutputPath $buildOutput `
    -ModDllName 'Pathologic2HeadTracking.dll' `
    -ExtraDlls @('CameraUnlock.Core.dll', 'CameraUnlock.Core.Unity.dll') `
    -GivenPath $GivenPath `
    -EnsureLoader `
    -VendorZip $vendorZip

Write-DeploymentSuccess `
    -ModName "Head Tracking mod" `
    -DeployPath $result.DeployedDllPath `
    -Controls @(
        "End       - Toggle head tracking on/off",
        "Page Up   - Cycle tracking mode (full / rotation-only / position-only)",
        "Page Down - Toggle yaw mode (world-locked / camera-local)",
        "Insert    - Toggle the aim dot on/off",
        "",
        "No nav cluster? Chords: Ctrl+Shift+ Y=Toggle G=Mode H=Yaw U=Aim dot"
    )
