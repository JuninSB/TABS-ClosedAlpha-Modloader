param([switch]$Build)

$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root '.toolchain\dotnet\dotnet.exe'
$game = Join-Path $root 'app'
$bepinex = Join-Path $root '.toolchain\bepinex'
if ($Build) {
    & $dotnet build (Join-Path $root 'Loader\TABSClosedAlpha.ModLoader\TABSClosedAlpha.ModLoader.csproj') -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Loader build failed.' }
    & $dotnet build (Join-Path $root 'Examples\ExampleMod\ExampleMod.csproj') -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'ExampleMod build failed.' }
    & $dotnet build (Join-Path $root 'Examples\SoftUI\SoftUI.csproj') -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'SoftUI build failed.' }
    & $dotnet build (Join-Path $root 'Examples\Tabium\Tabium.csproj') -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Tabium build failed.' }
}
Copy-Item -LiteralPath (Join-Path $bepinex 'BepInEx') -Destination $game -Recurse -Force
Copy-Item -LiteralPath (Join-Path $bepinex 'winhttp.dll') -Destination $game -Force
Copy-Item -LiteralPath (Join-Path $bepinex 'doorstop_config.ini') -Destination $game -Force
$bepConfig = Join-Path $game 'BepInEx\config\BepInEx.cfg'
if (-not (Test-Path $bepConfig)) {
    New-Item -ItemType Directory -Force -Path (Split-Path $bepConfig) | Out-Null
    @"
[Preloader.Entrypoint]
Assembly = Assembly-CSharp.dll
Type = GameMode
Method = Awake
"@ | Set-Content -LiteralPath $bepConfig -Encoding ASCII
}
$pluginDir = Join-Path $game 'BepInEx\plugins'
$loaderDir = Join-Path $game 'Loader'
$modDir = Join-Path $game 'Mods\ExampleMod'
$tabiumDir = Join-Path $game 'Mods\Tabium'
$softUiDir = Join-Path $game 'Mods\SoftUI'
New-Item -ItemType Directory -Force -Path $pluginDir, $loaderDir, $modDir, $tabiumDir, $softUiDir | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'Loader\TABSClosedAlpha.ModLoader\bin\Debug\net35\TABSClosedAlpha.ModLoader.dll') -Destination (Join-Path $pluginDir 'TABSClosedAlpha.ModLoader.dll') -Force
Copy-Item -LiteralPath (Join-Path $root 'Loader\TABSClosedAlpha.ModLoader\bin\Debug\net35\TABSClosedAlpha.ModLoader.dll') -Destination (Join-Path $loaderDir 'TABSClosedAlpha.ModLoader.dll') -Force
Copy-Item -LiteralPath (Join-Path $root 'Examples\ExampleMod\bin\Debug\net35\ExampleMod.dll') -Destination (Join-Path $modDir 'ExampleMod.dll') -Force
Copy-Item -LiteralPath (Join-Path $root 'Mods\ExampleMod\mod.json') -Destination (Join-Path $modDir 'mod.json') -Force
Copy-Item -LiteralPath (Join-Path $root 'Examples\Tabium\bin\Debug\net35\Tabium.dll') -Destination (Join-Path $tabiumDir 'Tabium.dll') -Force
Copy-Item -LiteralPath (Join-Path $root 'Mods\Tabium\mod.json') -Destination (Join-Path $tabiumDir 'mod.json') -Force
Copy-Item -LiteralPath (Join-Path $root 'Examples\SoftUI\bin\Debug\net35\SoftUI.dll') -Destination (Join-Path $softUiDir 'SoftUI.dll') -Force
Copy-Item -LiteralPath (Join-Path $root 'Mods\SoftUI\mod.json') -Destination (Join-Path $softUiDir 'mod.json') -Force
if (-not (Test-Path (Join-Path $loaderDir 'disabled-mods.txt'))) { New-Item -ItemType File -Path (Join-Path $loaderDir 'disabled-mods.txt') | Out-Null }
Write-Host 'Installed TABS ClosedAlpha Modloader into app/. No original managed assembly was modified.'
