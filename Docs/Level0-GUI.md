# Native Options UI discovered in level0

The serialized Unity scene `level0` contains the original TABS settings system. The confirmed objects and scripts are `OptionsHandler`,
`OptionsUI`, `GoToMenuScript`, and `SubmitOptions`. The category roots are named `Video`, `AUDIO`, and `game` (Gameplay). Their controls are
native uGUI objects: `Button`, `Toggle`, `Slider`, `Dropdown`, `ScrollRect`, `Panel`, `Image`, and `Text`.

The serialized labels include `FOV`, `LANGUAGE`, `MASTER`, `MUSIC`, `EFFECTS`, `SSAO`, `ANTI ALIASING`, `DEPTH OF FIELD`, `BLOOM`,
`INVERTED X`, `INVERTED Y`, and `sensitivity`. These are the real settings, not invented loader controls. `Options.Instance` exposes the matching
setters and `SubmitPrefs()`.

`MainMenuHandler.GetCurrentMenu(MenuState.Options)` is private, so Tabium resolves it with reflection. `MainMenuHandler.Instance`,
`CurrentMenuState`, and `L_ShowOptionsMenu` are public. The loader does not patch or rewrite `Assembly-CSharp.dll`.

SoftUI now attaches to the native Options canvas, clones a native button style for the category selectors, preserves the original Video/Audio/
Gameplay objects, and adds Tabium as a fourth category. The Sandbox map selector is intentionally left untouched.
