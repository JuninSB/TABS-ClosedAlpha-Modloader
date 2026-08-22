# Portable release

The public release contains the loader, BepInEx bootstrap files, mods, metadata and installation script. It intentionally does not contain the TABS executable or proprietary game data.

1. Extract the release beside your existing Closed Alpha copy.
2. Run `Scripts\\Install-PortableLoader.ps1 -GameDirectory "C:\\path\\to\\your\\TABS"`.
3. Start `TotallyAccurateBattleSimulatorClosedAlpha.exe` from that existing game directory.

The script copies only `BepInEx/`, `Loader/`, `Mods/`, `winhttp.dll` and `doorstop_config.ini`; it does not replace `Assembly-CSharp.dll`.
