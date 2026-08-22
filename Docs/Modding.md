# Criando mods

Compile para .NET Framework 3.5 e referencie `app/Loader/TABSClosedAlpha.ModLoader.dll`, as DLLs Unity em `app/*_Data/Managed/` e, para APIs do jogo, `Assembly-CSharp.dll`. Não copie as DLLs do jogo junto ao mod.

## Manifesto

```json
{
  "id": "meu.mod",
  "name": "Meu Mod",
  "version": "1.0.0",
  "apiVersion": "1.0",
  "author": "Você",
  "description": "Descrição",
  "main": "MeuMod.dll",
  "mainType": "MeuMod.Main",
  "dependencies": ["outro.mod", { "id": "api.extra", "version": "1.2.0" }],
  "conflicts": ["mod.incompativel"]
}
```

`id`, `name`, `version` e `main` são obrigatórios. `apiVersion` é validada por versão maior da API. Dependências carregam antes do mod, por `id` em ordem determinística; se informar `version`, a versão deve coincidir exatamente. IDs duplicados, ciclos, conflitos e dependências ausentes são registrados e aquele mod é ignorado sem impedir os demais.

## Entrada

```csharp
using TABSClosedAlpha;
using UnityEngine;

public sealed class Main : IMod {
  ModContext context;
  public void Initialize(ModContext c) {
    context = c;
    c.Log.Info("Olá");
    c.Events.SceneLoaded += (scene, mode) => c.Log.Info(scene.name);
    c.Keys.Register(KeyCode.F8, () => Debug.Log("F8"));
    new GameObject("Meu objeto").AddComponent<MeuComponente>();
  }
  public void Shutdown() { }
}
public sealed class MeuComponente : MonoBehaviour { void Update() { } }
```

## API do contexto

- `Log`: log identificado pelo mod; exceções de inicialização e callbacks são isoladas e registradas.
- `Events`: `SceneLoaded`, `SceneUnloaded`, `Update`, `FixedUpdate`.
- `Keys`: `Register(KeyCode, Action)` para teclas Unity.
- `Settings`: arquivo `config.cfg` na pasta do mod; `Get` e `GetBool` criam o valor padrão se ausente.
- `Assets`: `PathFor`, `LoadTexture` e `LoadBundle`; use `Assets/` do próprio mod. AssetBundles devem ser gerados com Unity 5.5 para compatibilidade com esta build.
- `Services`: comunicação entre mods por `Register<T>(id, service)` e `Get<T>(id)`; declare a dependência no manifesto para garantir ordem.
- `Commands`: registro e execução programática de comandos (`Register`, `Execute`), preparado para consoles/UI de mods.
- `Patches`: `Prefix`, `Postfix` e `Patch` sobre um `MethodBase`, usando Harmony incluído pelo BepInEx.
- `Game`: objetos e APIs confirmados desta build: `Units`, `GetUnitDefinition`, `LoadBuiltinUnit`, `LoadWorld`, `Battle`, `Mode`, `Find<T>` e `PrivateField`.

## SoftUI

`SoftUI` é um mod-library carregado antes dos mods que dependem dele. Declare `"softui"` no manifesto e referencie `Examples/SoftUI/bin/.../SoftUI.dll` ao compilar. Obtenha o serviço com `context.Services.Get<SoftUiService>("softui")`.

```csharp
var ui = context.Services.Get<SoftUiService>("softui");
var window = ui.CreateWindow("my-mod", "Meu Mod")
    .BindTo(() => MainMenuHandler.Instance != null &&
        MainMenuHandler.Instance.CurrentMenuState.ToString() == "Options");
var tab = window.AddTab("general", "General");
tab.AddToggle("enabled", "Ativo", true, value => { });
tab.AddSlider("scale", "Scale", 1f, 0.5f, 2f, value => { });
tab.AddDropdown("mode", "Mode", new[] { "Low", "High" }, 0, value => { });
tab.AddButton("Apply", () => { });
```

O `BindTo` é importante: a janela é criada no canvas próprio da SoftUI, mas só fica ativa quando a tela correspondente está aberta. Isso evita sobrepor a UI de outras telas. A SoftUI usa `CanvasScaler` com resolução de referência 1280x720 e layouts Unity (`HorizontalLayoutGroup`, `VerticalLayoutGroup`, `LayoutElement`) para adaptar controles.

## TABS e patches

Os tipos de `Assembly-CSharp.dll` podem ser usados diretamente. Exemplos confirmados: `UnitHandler`, `UnitDatabase`, `UnitLoaderHandler`, `LevelLoaderHandler`, `StartManager`, `GameMode`, `Projectile`, `ProjectileAttack`, `PhysicsAnimation` e `UnitUIInfo`.

Para um prefixo Harmony, a assinatura deve casar com o método real. Exemplo para o método analisado:

```csharp
var method = typeof(UnitHandler).GetMethod("TakeDamage");
context.Patches.Prefix(method, typeof(Main).GetMethod("BeforeDamage",
    BindingFlags.Static | BindingFlags.NonPublic));

static void BeforeDamage(UnitHandler __instance, float damage, string damager) {
  // executar antes do dano
}
```

Sempre trate a ausência de singleton/campo em tempo de execução: unidades e carregadores só existem nas cenas apropriadas. Para campos privados, obtenha o `FieldInfo` por `context.Game.PrivateField` e valide nulo antes de usar.
