# SoftUI Mods menu

Mods should not edit the Closed Alpha `OptionsObject` hierarchy. SoftUI exposes a separate Mods button in the native main menu and a page
registration API:

```csharp
SoftUiService softUi = context.Services.Get<SoftUiService>("softui");
softUi.ModMenu.Register("my-mod", "My Mod", BuildSettings);

void BuildSettings(SoftTab tab)
{
    tab.AddToggle("enabled", "Enabled", true, value => { });
    tab.AddButton("Apply", Apply);
}
```

The flow is `Main menu -> Mods -> My Mod -> Settings`. SoftUI owns the navigation and Back buttons. Each page is an independent window, so mods do
not overlap the game's Options scene or inherit its scene-transition events. Controls use the SoftUI theme and are safe to register from any mod
that declares a dependency on `softui`.
