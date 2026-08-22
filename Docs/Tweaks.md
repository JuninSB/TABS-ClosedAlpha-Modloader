# Tweaks

`Tweaks` restores small quality-of-life controls known from newer TABS versions while using Closed Alpha APIs.

- Hold left mouse button: simulation `0.1x`.
- Hold `G`: simulation `0.01x`.
- `F`: selects the unit nearest to the center of the camera view by assigning the real `FirstPersonCameraHandler.mCurrentTargetAssigned` field.
- Optional battle-finish pause: when the real `StartManager.Playing` transition ends, time is set to zero and `PRESS [TAB] TO CONTINUE` is shown.
- Optional edge camera: moves the active camera while the cursor is inside a configurable edge margin.

Settings are registered in `Mods -> Tweaks -> Settings` through SoftUI and saved to `Mods/Tweaks/config.cfg`.

The feature mapping follows the modern TABS control reference: mouse slow motion, `G` super slow motion, `F` unit possession and `T` pause are
documented controls. This Closed Alpha mod uses `Time.timeScale` and confirmed Closed Alpha types rather than assuming modern TABS classes.
