# Análise da Closed Alpha

- Executável: x64/PE32+; scripting backend Mono (`mono.dll` presente).
- Unity: `5.5.0x1-CollabPreview (09b457573f85)`, confirmado em `output_log.txt`.
- Assembly principal: `Assembly-CSharp, Version=0.3.6642.30052`; `ImageRuntimeVersion=v2.0.50727`; referências a `mscorlib 2.0`, `System 2.0` e `System.Core 3.5`. O teste real do BepInEx identificou o CLR Mono como `3.0.40818.0`.
- Não há ofuscação aparente: 526 tipos com nomes e assinaturas legíveis.

## Inicialização e cenas

As cenas incluídas são `MainScene`, `BattleScene` e `Environments/{Denmark,Japan,Neon,Sahara,Scotland}`. `LevelLoaderHandler.LoadWorld(string)` usa `SceneManager.LoadSceneAsync(nome, LoadSceneMode.Additive)`; `LevelLoaderHandler.Awake()` assina `SceneManager.sceneLoaded`. O loader assina os mesmos eventos, sem patch permanente.

`GameMode`, `StartManager`, `LevelLoaderHandler` e `UnitLoaderHandler` têm instâncias estáticas. `StartManager` mantém as listas de unidades dos dois times e inicia uma batalha com `StartLevelWithLayout`.

## Unidades, mapas e recursos

`UnitHandler` é o componente de unidade ativa: inclui time, vida privada `m_health`, custo, alvo, animação `PhysicsAnimation`, juntas e métodos `TakeDamage`, `ForcedDie` e `SetIdle`. `UnitUIInfo` contém nome, descrição, vida, velocidade, ataque e custo. `UnitDatabase` é singleton e retorna definições `UnitHandler`.

`UnitLoaderHandler.LoadUnitByPath(path)` chama exatamente `Resources.Load("Units/" + path)`. Formações vêm de `Resources` sob `Levels/Formations/`. Os recursos empacotados contêm unidades sob caminhos como `units/misc/peasant`, e os mapas são cenas Unity. Há AssetBundles padrão do Unity 5.5; mods podem carregar os seus por `AssetBundle.LoadFromFile`.

`Projectile`, `ProjectileAttack`, `PlayAnimation` e `PhysicsAnimation` são os componentes identificados para projéteis e animações. A API não encapsula artificialmente esses componentes: mods podem referenciar `Assembly-CSharp.dll` e Unity diretamente.

## Teste de integracao

O executavel foi iniciado com o bootstrap instalado. O log confirmou: BepInEx 5.4.23.5, Unity `5.5.0.635991`, CLR `3.0.40818.0`, carregamento de `TABSClosedAlpha.ModLoader.dll`, `ExampleMod.dll`, `ExampleMod initialized`, `Loaded Example Mod 1.0.0` e o evento `Scene loaded: MainScene`.

## Pontos de extensão confirmados

- Eventos Unity `SceneManager.sceneLoaded` e `sceneUnloaded`.
- Ciclos `Update` e `FixedUpdate` via um `MonoBehaviour` persistente do loader.
- Harmony para métodos reais, por exemplo `UnitHandler.TakeDamage(float, Vector3, string, Vector3)`.
- Reflection para campos privados, quando necessário (o exemplo valida `UnitHandler.m_health` antes de alterá-lo).

Não foi identificado um sistema interno de modding ou carregamento de conteúdo externo. O sistema de recursos interno é `Resources`, portanto novos prefabs/mapas devem vir de AssetBundles do mod ou ser construídos por código; não há suporte seguro para inserir novos assets diretamente no `resources.assets` original em runtime.
