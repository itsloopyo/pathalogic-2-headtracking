[CmdletBinding()]
param(
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

Import-Module (Join-Path $ProjectRoot 'cameraunlock-core\powershell\NightlyRelease.psm1') -Force

$csprojFile = Join-Path $ProjectRoot 'src\Pathologic2HeadTracking\Pathologic2HeadTracking.csproj'
$versionMatch = Select-String -Path $csprojFile -Pattern '<Version>([^<]+)</Version>'
if (-not $versionMatch) {
    throw "Could not extract version from $csprojFile"
}
$version = $versionMatch.Matches[0].Groups[1].Value

Publish-NightlyBuild `
    -ModId 'pathologic-2' `
    -ModName 'Pathologic2HeadTracking' `
    -Version $version `
    -ProjectRoot $ProjectRoot `
    -BuildCommand 'pixi run build' `
    -AllowDirty:$AllowDirty
