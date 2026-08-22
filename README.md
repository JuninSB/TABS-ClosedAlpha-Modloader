# TABS ClosedAlpha Modloader

Mod loader para a Closed Alpha local do Totally Accurate Battle Simulator, especificamente a build com `Assembly-CSharp` versão `0.3.6642.30052`, Unity `5.5.0x1-CollabPreview` e Mono/.NET 2.0/3.5.

Ele não altera `Assembly-CSharp.dll`. O bootstrap usa BepInEx 5 (Unity Mono x64) e a API carrega DLLs descritas por `Mods/*/mod.json`.

## Instalação de desenvolvimento

1. Put the extracted game copy in `app/` (it is not versioned or distributed by this repository).
2. Run `Scripts\Setup-Toolchain.ps1`, then `Scripts\Install-Development.ps1 -Build`.
3. Start `app\TotallyAccurateBattleSimulatorClosedAlpha.exe`.
4. Inspect `app\TotallyAccurateBattleSimulatorClosedAlpha_Data\output_log.txt` and `app\BepInEx\config\BepInEx.cfg`.

O script adiciona somente `app\winhttp.dll`, `app\doorstop_config.ini`, `app\BepInEx\`, `app\Loader\` e `app\Mods\`; os binários originais do jogo permanecem intactos. Para reverter, remova somente esses itens adicionados.

## Estrutura

```text
app/
  Mods/ExampleMod/{ExampleMod.dll,mod.json,Assets/}
  Mods/Tabium/{Tabium.dll,mod.json}
  Mods/SoftUI/{SoftUI.dll,mod.json}
  Loader/{TABSClosedAlpha.ModLoader.dll,disabled-mods.txt}
  BepInEx/plugins/TABSClosedAlpha.ModLoader.dll
```

Para desabilitar um mod, acrescente o `id` dele em `app/Loader/disabled-mods.txt`.

Leia [a documentação de desenvolvimento](Docs/Modding.md) e [a análise da build](Docs/Game-Analysis.md).

`SoftUI` e `Tabium` são projetos separados. `SoftUI` é somente uma library/framework: fornece janelas, sidebar, tabs, labels, buttons, toggles, sliders e selects para qualquer mod, sem alterar performance ou regras do jogo. `Tabium` é o mod de otimização inspirado no Sodium; ele depende de `SoftUI` apenas para exibir suas configurações. O Tabium adiciona seu painel somente quando `MainMenuHandler.CurrentMenuState` é o estado real `Options`; ele não aparece na tela inicial. As escolhas ficam em `app/Mods/Tabium/config.cfg`.
