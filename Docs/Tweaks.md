# Tweaks

`Tweaks` restores small quality-of-life controls known from newer TABS versions while using Closed Alpha APIs.

- Hold left mouse button: simulation `0.1x`.
- Hold `G`: simulation `0.01x`, using `Time.timeScale` only. `fixedDeltaTime` is deliberately kept at the Alpha's original physics step because scaling it down makes the 2016 ragdoll solver unstable and can kill every unit.
- `F`: hold/release to select the unit nearest to the center of the camera view and enter possession; pressing `F` again exits it. This mirrors the Steam version's hold-to-highlight flow. `C` toggles first/third person. The isolated camera supports full mouse pitch/yaw and follows the unit with smoothing.
- With `FISICA AVANCADA` enabled, movement is fed through the Alpha's existing `PhysicsAnimation`/`Rigidbody` path by overriding its private `forwardDir` and `turnMultiPlier` fields through reflection. The hidden target is not used for movement, so backward/sideways input does not force the Alpha's AI turn-then-walk behavior. The mod never writes the unit's `Transform.position`.
- The Steam build was inspected locally for the movement comparison. Its normal `MovementHandler.BatchedFixedUpdate` returns while `DataHandler.isGrounded` is false, then applies the state-specific `MovementInstance.force` as `VelocityChange` to the unit's rigidbodies. Its `Balance` system separately applies corrective forces at knee/foot positions. The Alpha does not contain those Steam classes; its closest verified equivalent is `PhysicsAnimation.grounded` plus its existing `Walk()` leg/torso solver. Possession now respects that grounded flag: airborne units receive no walk intent and are allowed to finish their native fall/landing simulation.
- Look and movement are independent: the torso's yaw follows the camera with `Rigidbody.MoveRotation`, while `forwardDir` receives the WASD direction. Holding `S` therefore moves backward while the unit continues facing the camera direction, matching the modern possession model.
- While possessed, LMB/RMB call the unit's two real attack entry points once per click and respect the Alpha's actual `attackCounter`/`rate` fields; holding a mouse button cannot create a machine-gun loop. `SPACE` uses the secondary attack as the Alpha-compatible special-ability fallback.
- A center reticle is displayed while possessing. Projectile attacks temporarily aim their real `ProjectileAttack.shooter` transforms along that reticle ray, so ranged units such as the Musketeer fire where the camera is pointing instead of at an unrelated nearest unit.
- Slow motion is disabled while possessing because the same mouse buttons are attack inputs in the Steam control scheme; the slow-motion hint is therefore not shown as a possession action.
- `FISICA AVANCADA DURING UNIT POSSESSION` is optional. When enabled, it applies the Steam-style rigidbody interpolation and continuous collision treatment to the possessed unit, restoring every body setting on exit.
- In possession mode, `SPACE` invokes the selected unit's real `AttackHandler.Attack2()` method (when present). The bottom HUD reports the actual `rate2`/`attackCounter2` cooldown fields; it does not invent a modern ability system that this Alpha does not contain.
- While possessed, the selected `UnitHandler` AI update is suspended. Its attack components, cooldowns and Unity physics remain active, so the unit can still fight instead of being frozen completely.
- Battle-finish handling leaves the Alpha's native `WhilePlaying`/`GameOverScreen` flow intact. Tweaks no longer freezes time or draws its own incorrect `PRESS [TAB] TO CONTINUE` overlay.
- Optional edge camera: moves the active camera while the cursor is inside a configurable edge margin.

Settings are registered in `Mods -> Tweaks -> Settings` through SoftUI and saved to `Mods/Tweaks/config.cfg`.

The feature mapping follows the modern TABS control reference: mouse slow motion, `G` super slow motion, `F` unit possession and `T` pause are documented controls. The Closed Alpha itself contains `FirstPersonCameraHandler`, `FollowUnit`, `UIManager`, `UnitHandler` and `AttackHandler`, but no native possession/ability HUD controller. Tweaks therefore bridges those confirmed systems without modifying `Assembly-CSharp.dll`.
