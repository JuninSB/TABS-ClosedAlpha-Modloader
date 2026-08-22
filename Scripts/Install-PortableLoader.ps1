param(
    [Parameter(Mandatory = $true)]
    [string]$GameDirectory
)

$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path -Parent $PSScriptRoot
$gameDirectory = (Resolve-Path -LiteralPath $GameDirectory).Path
$gameExe = Join-Path $gameDirectory 'TotallyAccurateBattleSimulatorClosedAlpha.exe'
if (-not (Test-Path -LiteralPath $gameExe)) {
    throw "Closed Alpha executable was not found in $gameDirectory"
}

foreach ($name in @('BepInEx', 'Loader', 'Mods')) {
    $source = Join-Path $packageRoot $name
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination $gameDirectory -Recurse -Force
    }
}
foreach ($name in @('winhttp.dll', 'doorstop_config.ini')) {
    $source = Join-Path $packageRoot $name
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $gameDirectory $name) -Force
    }
}

Write-Host "TABS Closed Alpha Modloader installed into $gameDirectory"
Write-Host 'Original Assembly-CSharp.dll was not modified.'
