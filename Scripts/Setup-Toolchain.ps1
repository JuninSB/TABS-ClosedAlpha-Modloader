$root = Split-Path -Parent $PSScriptRoot
$toolchain = Join-Path $root '.toolchain'
$dotnet = Join-Path $toolchain 'dotnet\dotnet.exe'
New-Item -ItemType Directory -Force -Path $toolchain | Out-Null
if (-not (Test-Path $dotnet)) {
    $installer = Join-Path $toolchain 'dotnet-install.ps1'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    & $installer -Channel 8.0 -InstallDir (Join-Path $toolchain 'dotnet') -NoPath
}
$bepinexDir = Join-Path $toolchain 'bepinex'
if (-not (Test-Path (Join-Path $bepinexDir 'winhttp.dll'))) {
    $zip = Join-Path $toolchain 'BepInEx_win_x64_5.4.23.5.zip'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip' -OutFile $zip
    Expand-Archive -LiteralPath $zip -DestinationPath $bepinexDir -Force
}
Write-Host 'Development toolchain ready.'
