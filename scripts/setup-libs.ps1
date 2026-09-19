#!/usr/bin/env pwsh
# Populates src/Pathologic2HeadTracking/libs/ for a game-free build, from REPO
# FILES ONLY - no Pathologic 2 install required:
#   - BepInEx.dll      : extracted from the vendored BepInEx zip
#   - UnityEngine*.dll : compiled by the shared stub builder in
#                        cameraunlock-core/csharp/stubs

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$scriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot  = Split-Path -Parent $scriptDir
$libsPath     = Join-Path $projectRoot 'src\Pathologic2HeadTracking\libs'
$vendorZip    = Join-Path $projectRoot 'vendor\bepinex\BepInEx_win_x64.zip'
$stubBuilder  = Join-Path $projectRoot 'cameraunlock-core\csharp\stubs\build-unity-stubs.ps1'

if (-not (Test-Path $vendorZip))   { throw "Vendored BepInEx not found at $vendorZip" }
if (-not (Test-Path $stubBuilder)) { throw "Shared stub builder not found at $stubBuilder - is the cameraunlock-core submodule checked out?" }

New-Item -ItemType Directory -Path $libsPath -Force | Out-Null

Write-Host "Bootstrapping build dependencies (no game install required)..." -ForegroundColor Cyan

# Wipe libs/ so stale game DLLs from a past deploy can't mask CI parity. No
# -ErrorAction SilentlyContinue: a DLL locked by an MSBuild node or an open IDE
# would survive the wipe in silence, which is the exact drift this line exists
# to prevent. $ErrorActionPreference is Stop, so a failure stops the build.
Get-ChildItem -Path $libsPath -Force | Remove-Item -Recurse -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
$tempDir = Join-Path $env:TEMP ("p2-bep-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
try {
    [System.IO.Compression.ZipFile]::ExtractToDirectory($vendorZip, $tempDir)
    foreach ($dll in @('BepInEx.dll')) {
        $src = Join-Path $tempDir "BepInEx\core\$dll"
        if (-not (Test-Path $src)) { throw "$dll not found in vendor zip at BepInEx\core\" }
        Copy-Item $src (Join-Path $libsPath $dll) -Force
        Write-Host "  BepInEx: $dll" -ForegroundColor Gray
    }
} finally {
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

# UnityEngine.InputLegacyModule is deliberately absent from this list. Pathologic 2
# is Unity 2018.4, which predates the split: Input lives in UnityEngine.CoreModule
# and the game ships no InputLegacyModule.dll at all. Producing the stub would make
# the mod emit [UnityEngine.InputLegacyModule]Input against a game that cannot
# resolve it. The reference resolves through UnityEngine.dll instead, which this
# Unity ships as a facade that type-forwards into the modules.
& $stubBuilder -OutputPath $libsPath -TargetFramework net472 -EmptyModule UnityEngine.CoreModule,UnityEngine.IMGUIModule,UnityEngine.UIModule,UnityEngine.TextRenderingModule,UnityEngine.AnimationModule,UnityEngine.PhysicsModule
if ($LASTEXITCODE -ne 0) { throw "Shared Unity stub build failed" }

Write-Host "Build dependencies ready." -ForegroundColor Green
