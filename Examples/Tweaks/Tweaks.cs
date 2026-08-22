using System;
using System.Collections.Generic;
using System.Reflection;
using TABSClosedAlpha;
using SoftUI;
using UnityEngine;
using UnityEngine.UI;

namespace Tweaks
{
    public sealed class Main : IMod
    {
        static Main activeInstance; static readonly FieldInfo ForwardDirField = typeof(PhysicsAnimation).GetField("forwardDir", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); static readonly FieldInfo GroundedField = typeof(PhysicsAnimation).GetField("grounded", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); static readonly FieldInfo SpeedField = typeof(PhysicsAnimation).GetField("speed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); static readonly FieldInfo TurnField = typeof(PhysicsAnimation).GetField("turnMultiPlier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        sealed class FallenWalkState { public float speed; public float turn; }
        ModContext context; ModSettings settings; GameObject prompt; GameObject controlHud; Text controlHudText; Text crosshairText; GameObject aimTargetObject; bool wasPlaying; bool pausedAfterFinish; float baseFixedDelta;
        UnitHandler controlledUnit; UnitHandler originalTarget; UnitHandler pendingPossessionUnit; Camera originalCamera; Camera controlCamera; GameObject possessionCameraObject; GameObject movementTargetObject; Vector3 cameraOffset; Vector3 controlMoveVelocity; Vector3 possessionMoveDirection; float cameraYaw; float cameraPitch; bool originalCameraWasEnabled; AudioListener[] originalListeners; bool[] originalListenerStates; bool controlling; bool advancedPhysicsActive; bool firstPerson; bool holdingPossess; bool originalIdle; bool originalAttacking;
        Behaviour[] disabledCameraScripts; bool[] disabledCameraStates; Canvas[] hiddenCanvases; bool[] hiddenCanvasStates; Rigidbody[] advancedBodies; RigidbodyInterpolation[] advancedInterpolation; CollisionDetectionMode[] advancedCollision; bool controlledUnitWasEnabled; bool cursorWasVisible; CursorLockMode cursorLockState; float legStillTime; float stepReleaseTime;
        public void Initialize(ModContext context)
        {
            this.context = context; activeInstance = this; settings = context.Settings; baseFixedDelta = Time.fixedDeltaTime;
            SoftUiService softUi = context.Services.Get<SoftUiService>("softui");
            if (softUi == null) { context.Log.Error("SoftUI dependency was not loaded."); return; }
            softUi.ModMenu.Register("tweaks", "Tweaks", BuildSettings);
            BindingFlags patchFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic; context.Patches.Patch(typeof(PhysicsAnimation).GetMethod("Walk", patchFlags), typeof(Main).GetMethod("BeforePhysicsWalk", BindingFlags.Static | BindingFlags.NonPublic), typeof(Main).GetMethod("AfterPhysicsWalk", BindingFlags.Static | BindingFlags.NonPublic));
            context.Events.Update += Update;
            context.Events.FixedUpdate += FixedControlPhysics;
            context.Events.SceneLoaded += (scene, mode) => { ExitControl(); wasPlaying = false; pausedAfterFinish = false; HidePrompt(); RestoreTime(); };
            context.Log.Info("Tweaks initialized: mouse slow motion, G super slow motion, battle-finish pause, edge camera and F unit control.");
        }
        public void Shutdown() { context.Events.FixedUpdate -= FixedControlPhysics; context.Patches.UnpatchAll(); activeInstance = null; ExitControl(); RestoreTime(); HidePrompt(); }
        void BuildSettings(SoftTab tab)
        {
            tab.AddLabel("TWEAKS", 20);
            tab.AddToggle("pause-finish", "PAUSE SIMULATION WHEN BATTLE FINISHES", settings.GetBool("pauseWhenFinished", true), value => Set("pauseWhenFinished", value));
            tab.AddToggle("edge-camera", "MOVE CAMERA WHEN MOUSE TOUCHES SCREEN EDGE", settings.GetBool("edgeCamera", false), value => Set("edgeCamera", value));
            tab.AddSlider("edge-margin", "EDGE MARGIN", ParseFloat(settings.Get("edgeMargin", "28"), 28f), 4f, 120f, value => Set("edgeMargin", value.ToString("0")));
            tab.AddSlider("edge-speed", "EDGE CAMERA SPEED", ParseFloat(settings.Get("edgeSpeed", "8"), 8f), 1f, 30f, value => Set("edgeSpeed", value.ToString("0.0")));
            tab.AddToggle("nearest-unit", "F CONTROLS UNIT NEAREST TO SCREEN CENTER", settings.GetBool("nearestUnit", true), value => Set("nearestUnit", value));
            tab.AddToggle("advanced-physics", "FISICA AVANCADA NA POSSE DA UNIDADE", settings.GetBool("advancedPhysics", true), value => Set("advancedPhysics", value));
            tab.AddToggle("velocity-assist", "IMPULSO DISTRIBUIDO NOS RIGIDBODIES", settings.GetBool("velocityAssist", false), value => Set("velocityAssist", value));
            tab.AddToggle("balance-assist", "CORRECAO LEVE DE EQUILIBRIO", settings.GetBool("balanceAssist", true), value => Set("balanceAssist", value));
            tab.AddToggle("step-gate", "ANTI-DESLIZAMENTO: EXIGIR PASSO ATIVO", settings.GetBool("stepGate", true), value => Set("stepGate", value));
            tab.AddToggle("fallen-lock", "BLOQUEAR MOVIMENTO QUANDO CAIDO", settings.GetBool("fallenMovementLock", true), value => Set("fallenMovementLock", value));
            tab.AddLabel("HOLD LEFT MOUSE: 0.1x   |   HOLD G: 0.01x   |   F: CONTROL UNIT", 11);
        }
        void Update()
        {
            StartManager start = StartManager.Instance; bool playing = start != null && start.Playing;
            if (!playing && controlling) ExitControl();
            if (playing && !wasPlaying) { pausedAfterFinish = false; HidePrompt(); }
            if (wasPlaying && !playing && settings.GetBool("pauseWhenFinished", true)) HandleNativeBattleFinish();
            wasPlaying = playing;
            if (pausedAfterFinish) { Time.timeScale = 0f; if (Input.GetKeyDown(KeyCode.Tab)) { pausedAfterFinish = false; HidePrompt(); RestoreTime(); } return; }
            if (playing) ApplySlowMotion(); else RestoreTime();
            if (playing && !controlling && settings.GetBool("edgeCamera", false)) EdgeCamera();
            if (playing && settings.GetBool("nearestUnit", true)) HandlePossessInput();
            if (controlling) UpdateControl();
        }
        void ApplySlowMotion()
        {
            if (controlling) { Time.timeScale = 1f; Time.fixedDeltaTime = baseFixedDelta; return; }
            float scale = Input.GetKey(KeyCode.G) ? .01f : (Input.GetMouseButton(0) ? .1f : 1f);
            // This Alpha's physics is driven by old Rigidbody/FixedUpdate code. Scaling
            // fixedDeltaTime to 0.01 creates thousands of tiny integration steps and
            // causes the ragdolls to jitter or freeze. The native-style slow motion is
            // achieved with timeScale while the physics step remains at its calibrated value.
            Time.timeScale = scale; Time.fixedDeltaTime = baseFixedDelta;
        }
        void RestoreTime() { Time.timeScale = 1f; Time.fixedDeltaTime = baseFixedDelta; }
        void HandleNativeBattleFinish() { pausedAfterFinish = false; HidePrompt(); RestoreTime(); context.Log.Info("Battle finished; using the native GameOverScreen flow."); }
        void EnableAdvancedPhysics(UnitHandler unit)
        {
            advancedBodies = unit.GetComponentsInChildren<Rigidbody>(); advancedInterpolation = new RigidbodyInterpolation[advancedBodies.Length]; advancedCollision = new CollisionDetectionMode[advancedBodies.Length]; for (int i = 0; i < advancedBodies.Length; i++) { Rigidbody body = advancedBodies[i]; if (body == null) continue; advancedInterpolation[i] = body.interpolation; advancedCollision[i] = body.collisionDetectionMode; body.interpolation = RigidbodyInterpolation.Interpolate; if (!body.isKinematic) body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; }
            context.Log.Info("Advanced possession physics enabled: Rigidbody interpolation and continuous collision detection.");
        }
        void DisableAdvancedPhysics()
        {
            if (advancedBodies == null) return; for (int i = 0; i < advancedBodies.Length; i++) if (advancedBodies[i] != null) { advancedBodies[i].interpolation = advancedInterpolation[i]; advancedBodies[i].collisionDetectionMode = advancedCollision[i]; } advancedBodies = null; advancedInterpolation = null; advancedCollision = null;
        }
        void EdgeCamera()
        {
            Camera camera = Camera.main; if (camera == null) return; float margin = ParseFloat(settings.Get("edgeMargin", "28"), 28f); float speed = ParseFloat(settings.Get("edgeSpeed", "8"), 8f); Vector3 direction = Vector3.zero; Vector3 mouse = Input.mousePosition; if (mouse.x <= margin) direction -= camera.transform.right; else if (mouse.x >= Screen.width - margin) direction += camera.transform.right; if (mouse.y <= margin) direction -= Vector3.ProjectOnPlane(camera.transform.up, Vector3.up).normalized; else if (mouse.y >= Screen.height - margin) direction += Vector3.ProjectOnPlane(camera.transform.up, Vector3.up).normalized; direction.y = 0f; if (direction.sqrMagnitude > .001f) camera.transform.position += direction.normalized * speed * Time.unscaledDeltaTime;
        }
        void HandlePossessInput()
        {
            if (controlling) { if (Input.GetKeyDown(KeyCode.F)) ExitControl(); return; }
            if (Input.GetKeyDown(KeyCode.F)) { holdingPossess = true; pendingPossessionUnit = FindNearestUnit(); }
            if (holdingPossess) pendingPossessionUnit = FindNearestUnit();
            if (holdingPossess && Input.GetKeyUp(KeyCode.F)) { holdingPossess = false; EnterControl(pendingPossessionUnit); pendingPossessionUnit = null; }
        }
        UnitHandler FindNearestUnit()
        {
            Camera camera = Camera.main; if (camera == null) return null; UnitHandler[] units = context.Game.Units; UnitHandler best = null; float bestDistance = Single.MaxValue; Vector2 center = new Vector2(Screen.width * .5f, Screen.height * .5f); for (int i = 0; i < units.Length; i++) { UnitHandler unit = units[i]; if (unit == null || !unit.Alive) continue; Vector3 screen = camera.WorldToScreenPoint(unit.transform.position); if (screen.z <= 0f) continue; float distance = (new Vector2(screen.x, screen.y) - center).sqrMagnitude; if (distance < bestDistance) { bestDistance = distance; best = unit; } } return best;
        }
        void EnterControl(UnitHandler unit)
        {
            if (unit == null || Camera.main == null) return; controlledUnit = unit; advancedPhysicsActive = settings.GetBool("advancedPhysics", true); originalTarget = unit.CurrentTarget; originalIdle = unit.Isidling; originalAttacking = unit.attacking; movementTargetObject = new GameObject("Tweaks Possession Direction"); UnityEngine.Object.DontDestroyOnLoad(movementTargetObject); controlledUnit.target = movementTargetObject.transform; controlledUnit.SetIdle(true); controlledUnit.attacking = false; controlledUnitWasEnabled = unit.enabled; unit.enabled = false; if (advancedPhysicsActive) EnableAdvancedPhysics(unit); originalCamera = Camera.main; originalCameraWasEnabled = originalCamera.enabled; originalCamera.enabled = false; originalListeners = originalCamera.GetComponents<AudioListener>(); originalListenerStates = new bool[originalListeners.Length]; for (int i = 0; i < originalListeners.Length; i++) { originalListenerStates[i] = originalListeners[i].enabled; originalListeners[i].enabled = false; }
            possessionCameraObject = new GameObject("Tweaks Third Person Camera"); UnityEngine.Object.DontDestroyOnLoad(possessionCameraObject); controlCamera = possessionCameraObject.AddComponent<Camera>(); controlCamera.CopyFrom(originalCamera); controlCamera.enabled = true; controlCamera.tag = "MainCamera"; cameraOffset = new Vector3(0f, 2.0f, -4.5f); cameraYaw = originalCamera.transform.eulerAngles.y; cameraPitch = 12f; controlMoveVelocity = Vector3.zero; controlling = true;
            cursorWasVisible = Cursor.visible; cursorLockState = Cursor.lockState; Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked;
            hiddenCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>(); hiddenCanvasStates = new bool[hiddenCanvases.Length]; for (int i = 0; i < hiddenCanvases.Length; i++) { hiddenCanvasStates[i] = hiddenCanvases[i].enabled; hiddenCanvases[i].enabled = false; }
            UIManager ui = UnityEngine.Object.FindObjectOfType<UIManager>(); if (ui != null) ui.CloseAll();
            disabledCameraScripts = new Behaviour[] { GetBehaviour(controlCamera, "FirstPersonCameraHandler"), GetBehaviour(controlCamera, "FollowUnit"), GetBehaviour(controlCamera, "MouseLookCustom"), GetBehaviour(controlCamera, "PlaybackViewCam"), GetBehaviour(controlCamera, "CameraBuildingMovement") }; disabledCameraStates = new bool[disabledCameraScripts.Length]; for (int i = 0; i < disabledCameraScripts.Length; i++) if (disabledCameraScripts[i] != null) { disabledCameraStates[i] = disabledCameraScripts[i].enabled; disabledCameraScripts[i].enabled = false; }
            PrimeControlCooldowns(); BuildControlHud(); context.Log.Info("Possession enabled for " + unit.name + ". F exits; SPACE uses the unit's secondary attack.");
        }
        Behaviour GetBehaviour(Camera camera, string typeName) { if (camera == null) return null; Component component = camera.GetComponent(typeName); return component as Behaviour; }
        void ExitControl()
        {
            if (!controlling) return; controlling = false; advancedPhysicsActive = false; Cursor.visible = cursorWasVisible; Cursor.lockState = cursorLockState; if (controlledUnit != null) DisableAdvancedPhysics(); if (movementTargetObject != null) UnityEngine.Object.Destroy(movementTargetObject); movementTargetObject = null; if (aimTargetObject != null) UnityEngine.Object.Destroy(aimTargetObject); aimTargetObject = null; if (possessionCameraObject != null) UnityEngine.Object.Destroy(possessionCameraObject); possessionCameraObject = null; controlCamera = null; if (originalCamera != null) originalCamera.enabled = originalCameraWasEnabled; if (originalListeners != null) for (int i = 0; i < originalListeners.Length; i++) if (originalListeners[i] != null) originalListeners[i].enabled = originalListenerStates[i]; if (controlledUnit != null) { controlledUnit.target = originalTarget == null ? null : originalTarget.transform; controlledUnit.SetIdle(originalIdle); controlledUnit.attacking = originalAttacking; controlledUnit.enabled = controlledUnitWasEnabled; } if (disabledCameraScripts != null) for (int i = 0; i < disabledCameraScripts.Length; i++) if (disabledCameraScripts[i] != null) disabledCameraScripts[i].enabled = disabledCameraStates[i]; if (hiddenCanvases != null) for (int i = 0; i < hiddenCanvases.Length; i++) if (hiddenCanvases[i] != null) hiddenCanvases[i].enabled = hiddenCanvasStates[i]; if (controlHud != null) controlHud.SetActive(false); controlledUnit = null; originalTarget = null; context.Log.Info("Possession disabled.");
        }
        void UpdateControl()
        {
            if (controlledUnit == null || !controlledUnit.Alive || controlCamera == null) { ExitControl(); return; }
            Vector3 unitPosition = GetControlAnchorPosition();
            AdvanceControlCooldowns();
            float mx = Input.GetAxis("Mouse X"); float my = Input.GetAxis("Mouse Y"); cameraYaw += mx * 3.5f; cameraPitch = Mathf.Clamp(cameraPitch - my * 2.5f, -80f, 80f); Quaternion rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f); Vector3 fullForward = rotation * Vector3.forward; Vector3 forward = new Vector3(fullForward.x, 0f, fullForward.z); Vector3 right = new Vector3(fullForward.z, 0f, -fullForward.x); if (forward.sqrMagnitude > .01f) forward.Normalize(); if (right.sqrMagnitude > .01f) right.Normalize(); float horizontal = Input.GetAxisRaw("Horizontal"); float vertical = Input.GetAxisRaw("Vertical"); Vector3 move = horizontal * right + vertical * forward; if (move.sqrMagnitude > 1f) move.Normalize(); if (move.sqrMagnitude < .04f) { move = Vector3.zero; controlMoveVelocity = Vector3.zero; controlledUnit.SetIdle(true); } else { controlledUnit.SetIdle(false); } possessionMoveDirection = move; if (!advancedPhysicsActive && movementTargetObject != null) { controlledUnit.target = movementTargetObject.transform; movementTargetObject.transform.position = unitPosition + move * 4f; }
            Vector3 target = unitPosition + Vector3.up * 1.2f; float distance = Mathf.Clamp(cameraOffset.magnitude, 3f, 6f); Vector3 desiredCameraPosition = firstPerson ? target + Vector3.up * .35f : target - fullForward * distance + Vector3.up * cameraOffset.y; float cameraLerp = Mathf.Clamp01(Time.unscaledDeltaTime * 20f); controlCamera.transform.position = Vector3.Lerp(controlCamera.transform.position, desiredCameraPosition, cameraLerp); controlCamera.transform.rotation = Quaternion.Slerp(controlCamera.transform.rotation, rotation, cameraLerp); if (Input.GetMouseButtonDown(0)) UseAttack(false); if (Input.GetMouseButtonDown(1)) UseAttack(true); if (Input.GetKeyDown(KeyCode.Space)) UseSpecial(); if (Input.GetKeyDown(KeyCode.C)) { firstPerson = !firstPerson; controlMoveVelocity = Vector3.zero; } UpdateControlHud();
        }
        Vector3 GetControlAnchorPosition()
        {
            if (controlledUnit != null && controlledUnit.anim != null && controlledUnit.anim.torso != null) return controlledUnit.anim.torso.worldCenterOfMass;
            if (controlledUnit != null && controlledUnit.ownBox != null) return controlledUnit.ownBox.position;
            return controlledUnit == null ? Vector3.zero : controlledUnit.transform.position;
        }
        static bool IsFallen(PhysicsAnimation anim)
        {
            if (activeInstance == null || anim == null || !activeInstance.settings.GetBool("fallenMovementLock", true) || anim.torso == null) return false;
            bool grounded = GroundedField == null || (bool)GroundedField.GetValue(anim); if (!grounded) return true;
            float fallAngle = ParseFloat(activeInstance.settings.Get("fallAngle", "55"), 55f); return Vector3.Angle(anim.torso.transform.up, Vector3.up) >= fallAngle;
        }
        static void BeforePhysicsWalk(PhysicsAnimation __instance, ref FallenWalkState __state)
        {
            __state = null; if (!IsFallen(__instance)) return; __state = new FallenWalkState(); __state.speed = SpeedField == null ? 0f : (float)SpeedField.GetValue(__instance); __state.turn = TurnField == null ? 1f : (float)TurnField.GetValue(__instance); if (SpeedField != null) SpeedField.SetValue(__instance, 0f); if (TurnField != null) TurnField.SetValue(__instance, 0f); if (ForwardDirField != null) ForwardDirField.SetValue(__instance, Vector3.zero);
        }
        static void AfterPhysicsWalk(PhysicsAnimation __instance, FallenWalkState __state)
        {
            if (__state == null) return; if (SpeedField != null) SpeedField.SetValue(__instance, __state.speed); if (TurnField != null) TurnField.SetValue(__instance, __state.turn);
        }
        void FixedControlPhysics()
        {
            if (!advancedPhysicsActive || !controlling || controlledUnit == null || controlledUnit.anim == null) return;
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic; FieldInfo forwardField = typeof(PhysicsAnimation).GetField("forwardDir", flags); FieldInfo turnField = typeof(PhysicsAnimation).GetField("turnMultiPlier", flags);
            // The modern game's MovementHandler has an explicit grounded gate. The
            // Closed Alpha has the same physical state in PhysicsAnimation.grounded;
            // do not feed a walking intent while the unit is airborne. This keeps
            // the existing leg/torso simulation responsible for landing instead of
            // fighting it with an artificial transform or target teleport.
            FieldInfo groundedField = typeof(PhysicsAnimation).GetField("grounded", flags); bool grounded = groundedField == null || (bool)groundedField.GetValue(controlledUnit.anim); if (!grounded) { controlledUnit.SetIdle(true); if (forwardField != null) forwardField.SetValue(controlledUnit.anim, Vector3.zero); return; }
            Rigidbody torso = controlledUnit.anim.torso;
            controlledUnit.target = null;
            if (ShouldGateStationaryLegs(controlledUnit.anim, torso, flags)) { controlledUnit.SetIdle(true); if (forwardField != null) forwardField.SetValue(controlledUnit.anim, Vector3.zero); return; }
            if (torso != null)
            {
                // Keep the Alpha's torso upright while allowing the native leg
                // solver to handle the step cycle. Looking and walking remain
                // separate, like the modern possession controller.
                Quaternion lookRotation = Quaternion.Euler(0f, cameraYaw, 0f); torso.MoveRotation(Quaternion.Slerp(torso.rotation, lookRotation, .14f));
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(torso.velocity, Vector3.up);
                if (possessionMoveDirection.sqrMagnitude < .001f) torso.AddForce(-horizontalVelocity * .32f, ForceMode.VelocityChange);
                else { Vector3 direction = possessionMoveDirection.normalized; Vector3 sidewaysVelocity = horizontalVelocity - Vector3.Project(horizontalVelocity, direction); torso.AddForce(-sidewaysVelocity * .20f, ForceMode.VelocityChange); }
            }
            if (possessionMoveDirection.sqrMagnitude < .001f) { controlledUnit.SetIdle(true); if (forwardField != null) forwardField.SetValue(controlledUnit.anim, Vector3.zero); return; }
            controlledUnit.SetIdle(false);
            if (forwardField != null) forwardField.SetValue(controlledUnit.anim, possessionMoveDirection);
            if (turnField != null) turnField.SetValue(controlledUnit.anim, 1f);
            ApplyModernPhysicsAssist(controlledUnit.anim, possessionMoveDirection, torso, flags);
        }
        bool ShouldGateStationaryLegs(PhysicsAnimation anim, Rigidbody torso, BindingFlags flags)
        {
            if (!settings.GetBool("stepGate", true) || anim == null || torso == null || possessionMoveDirection.sqrMagnitude < .001f) { legStillTime = 0f; stepReleaseTime = 0f; return false; }
            FieldInfo leftField = typeof(PhysicsAnimation).GetField("leftLeg", flags); FieldInfo rightField = typeof(PhysicsAnimation).GetField("rightLeg", flags);
            Rigidbody left = leftField == null ? null : leftField.GetValue(anim) as Rigidbody; Rigidbody right = rightField == null ? null : rightField.GetValue(anim) as Rigidbody;
            if (left == null || right == null) { legStillTime = 0f; stepReleaseTime = 0f; return false; }
            float threshold = ParseFloat(settings.Get("legMotionThreshold", "0.08"), .08f); float motion = left.velocity.sqrMagnitude + right.velocity.sqrMagnitude + left.angularVelocity.sqrMagnitude + right.angularVelocity.sqrMagnitude;
            bool legsMoving = motion > threshold * threshold; Vector3 horizontal = Vector3.ProjectOnPlane(torso.velocity, Vector3.up); bool bodySliding = horizontal.sqrMagnitude > .04f;
            if (legsMoving || !bodySliding) { legStillTime = 0f; stepReleaseTime = 0f; return false; }
            legStillTime += Time.fixedDeltaTime; if (legStillTime < .12f) return false;
            stepReleaseTime += Time.fixedDeltaTime; if (stepReleaseTime < .08f) return true;
            legStillTime = 0f; stepReleaseTime = 0f; return false;
        }
        void ApplyModernPhysicsAssist(PhysicsAnimation anim, Vector3 move, Rigidbody torso, BindingFlags flags)
        {
            if (anim == null) return;
            if (settings.GetBool("velocityAssist", false))
            {
                FieldInfo speedField = typeof(PhysicsAnimation).GetField("speed", flags); float speed = speedField == null ? 1f : (float)speedField.GetValue(anim);
                float strength = ParseFloat(settings.Get("velocityAssistStrength", "0.18"), .18f);
                Vector3 impulse = move * speed * strength * Time.fixedDeltaTime;
                Rigidbody[] rigs = anim.GetComponentsInChildren<Rigidbody>();
                for (int i = 0; i < rigs.Length; i++) if (rigs[i] != null && !rigs[i].isKinematic) rigs[i].AddForce(impulse, ForceMode.VelocityChange);
            }
            if (!settings.GetBool("balanceAssist", true) || torso == null) return;
            FieldInfo leftField = typeof(PhysicsAnimation).GetField("leftLeg", flags); FieldInfo rightField = typeof(PhysicsAnimation).GetField("rightLeg", flags);
            Rigidbody left = leftField == null ? null : leftField.GetValue(anim) as Rigidbody; Rigidbody right = rightField == null ? null : rightField.GetValue(anim) as Rigidbody;
            if (left == null || right == null) return;
            bool leftSupported = HasGroundContact(left); bool rightSupported = HasGroundContact(right); if (!leftSupported && !rightSupported) return;
            Vector3 support = leftSupported && rightSupported ? (left.worldCenterOfMass + right.worldCenterOfMass) * .5f : (leftSupported ? left.worldCenterOfMass : right.worldCenterOfMass); Vector3 error = Vector3.ProjectOnPlane(support - torso.worldCenterOfMass, Vector3.up);
            if (error.sqrMagnitude < .0025f) return;
            float balanceStrength = ParseFloat(settings.Get("balanceAssistStrength", "0.035"), .035f); torso.AddForce(error * balanceStrength, ForceMode.VelocityChange);
        }
        bool HasGroundContact(Rigidbody leg)
        {
            if (leg == null) return false;
            RaycastHit[] hits = Physics.RaycastAll(leg.worldCenterOfMass + Vector3.up * .05f, Vector3.down, .7f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;
                UnitHandler owner = hits[i].collider.GetComponentInParent<UnitHandler>();
                if (owner == null || owner != controlledUnit) return true;
            }
            return false;
        }
        void PrimeControlCooldowns()
        {
            if (controlledUnit == null) return; BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic; AttackHandler[] attacks = controlledUnit.GetComponentsInChildren<AttackHandler>(); for (int i = 0; i < attacks.Length; i++) { FieldInfo rate = typeof(AttackHandler).GetField("rate", flags); FieldInfo counter = typeof(AttackHandler).GetField("attackCounter", flags); FieldInfo rate2 = typeof(AttackHandler).GetField("rate2", flags); FieldInfo counter2 = typeof(AttackHandler).GetField("attackCounter2", flags); if (rate != null && counter != null) counter.SetValue(attacks[i], (float)rate.GetValue(attacks[i]) + .01f); if (rate2 != null && counter2 != null && (float)rate2.GetValue(attacks[i]) > 0f) counter2.SetValue(attacks[i], (float)rate2.GetValue(attacks[i]) + .01f); }
        }
        void AdvanceControlCooldowns()
        {
            if (controlledUnit == null) return; BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic; AttackHandler[] attacks = controlledUnit.GetComponentsInChildren<AttackHandler>(); for (int i = 0; i < attacks.Length; i++) { FieldInfo counter = typeof(AttackHandler).GetField("attackCounter", flags); FieldInfo counter2 = typeof(AttackHandler).GetField("attackCounter2", flags); if (counter != null) counter.SetValue(attacks[i], (float)counter.GetValue(attacks[i]) + Time.unscaledDeltaTime); if (counter2 != null) counter2.SetValue(attacks[i], (float)counter2.GetValue(attacks[i]) + Time.unscaledDeltaTime); }
        }
        void UseAttack(bool secondary)
        {
            if (controlledUnit == null) return; BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic; AttackHandler[] attacks = controlledUnit.GetComponentsInChildren<AttackHandler>(); if (attacks.Length == 0) return; FieldInfo hasSecondary = typeof(AttackHandler).GetField("hasAttack2", flags); FieldInfo counterField = typeof(AttackHandler).GetField(secondary ? "attackCounter2" : "attackCounter", flags); FieldInfo rateField = typeof(AttackHandler).GetField(secondary ? "rate2" : "rate", flags); if (secondary && (hasSecondary == null || !(bool)hasSecondary.GetValue(attacks[0]))) return; float counter = counterField == null ? 0f : (float)counterField.GetValue(attacks[0]); float rate = rateField == null ? 0f : (float)rateField.GetValue(attacks[0]); if (rate > 0f && counter <= rate) return; UnitHandler target = FindAimUnit(); Transform attackTarget = target == null ? GetAimTargetTransform() : target.transform; controlledUnit.target = attackTarget; Quaternion aimRotation = controlCamera == null ? controlledUnit.transform.rotation : controlCamera.transform.rotation; ProjectileAttack[] projectiles = controlledUnit.GetComponentsInChildren<ProjectileAttack>(); FieldInfo shooterField = typeof(ProjectileAttack).GetField("shooter", flags); FieldInfo shooter2Field = typeof(ProjectileAttack).GetField("shooter2", flags); List<Transform> rotatedShooters = new List<Transform>(); List<Quaternion> oldRotations = new List<Quaternion>(); for (int i = 0; i < projectiles.Length; i++) { Transform shooter = shooterField == null ? null : shooterField.GetValue(projectiles[i]) as Transform; Transform shooter2 = shooter2Field == null ? null : shooter2Field.GetValue(projectiles[i]) as Transform; if (shooter != null) { rotatedShooters.Add(shooter); oldRotations.Add(shooter.rotation); shooter.rotation = aimRotation; } if (shooter2 != null) { rotatedShooters.Add(shooter2); oldRotations.Add(shooter2.rotation); shooter2.rotation = aimRotation; } } try { MethodInfo method = typeof(AttackHandler).GetMethod(secondary ? "Attack2" : "Attack", flags); if (method != null) method.Invoke(attacks[0], null); } catch (Exception e) { context.Log.Error("Possession attack failed.", e); } for (int i = 0; i < rotatedShooters.Count; i++) rotatedShooters[i].rotation = oldRotations[i]; controlledUnit.target = null;
        }
        UnitHandler FindAimUnit()
        {
            if (controlCamera == null) return FindNearestEnemy(); Ray ray = controlCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0f)); RaycastHit hit; if (Physics.Raycast(ray, out hit, 1000f)) { UnitHandler unit = hit.collider.GetComponentInParent<UnitHandler>(); if (unit != null && unit != controlledUnit && unit.Alive && unit.team != controlledUnit.team) return unit; } return null;
        }
        Transform GetAimTargetTransform()
        {
            if (aimTargetObject == null) { aimTargetObject = new GameObject("Tweaks Possession Aim Target"); UnityEngine.Object.DontDestroyOnLoad(aimTargetObject); } Ray ray = controlCamera == null ? new Ray(GetControlAnchorPosition(), controlledUnit.transform.forward) : controlCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0f)); aimTargetObject.transform.position = ray.origin + ray.direction * 100f; return aimTargetObject.transform;
        }
        UnitHandler FindNearestEnemy()
        {
            UnitHandler best = null; float bestDistance = Single.MaxValue; UnitHandler[] units = context.Game.Units; for (int i = 0; i < units.Length; i++) { UnitHandler candidate = units[i]; if (candidate == null || !candidate.Alive || candidate == controlledUnit || candidate.team == controlledUnit.team) continue; float distance = (candidate.transform.position - controlledUnit.transform.position).sqrMagnitude; if (distance < bestDistance) { bestDistance = distance; best = candidate; } } return best;
        }
        void UseSpecial()
        {
            UseAttack(true);
        }
        void BuildControlHud()
        {
            if (controlHud != null) { controlHud.SetActive(true); return; } controlHud = new GameObject("Tweaks Possession HUD"); UnityEngine.Object.DontDestroyOnLoad(controlHud); Canvas canvas = controlHud.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32500; controlHud.AddComponent<CanvasScaler>(); controlHud.AddComponent<GraphicRaycaster>(); GameObject textObject = new GameObject("Abilities"); textObject.transform.SetParent(controlHud.transform, false); controlHudText = textObject.AddComponent<Text>(); controlHudText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); controlHudText.fontSize = 16; controlHudText.color = Color.white; controlHudText.alignment = TextAnchor.MiddleCenter; RectTransform rect = textObject.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.5f, .04f); rect.anchorMax = new Vector2(.5f, .04f); rect.sizeDelta = new Vector2(560f, 64f); GameObject crosshairObject = new GameObject("Crosshair"); crosshairObject.transform.SetParent(controlHud.transform, false); crosshairText = crosshairObject.AddComponent<Text>(); crosshairText.text = "+"; crosshairText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); crosshairText.fontSize = 24; crosshairText.color = Color.white; crosshairText.alignment = TextAnchor.MiddleCenter; RectTransform crosshairRect = crosshairObject.GetComponent<RectTransform>(); crosshairRect.anchorMin = new Vector2(.5f, .5f); crosshairRect.anchorMax = new Vector2(.5f, .5f); crosshairRect.sizeDelta = new Vector2(32f, 32f);
        }
        void UpdateControlHud()
        {
            if (controlHudText == null || controlledUnit == null) return; AttackHandler[] attacks = controlledUnit.GetComponentsInChildren<AttackHandler>(); string text = "[WASD] MOVE   [MOUSE] LOOK   [LMB/RMB] ATTACK   [SPACE] SPECIAL   [C] VIEW   [F] EXIT\n"; if (attacks.Length > 0) { BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic; FieldInfo rate = typeof(AttackHandler).GetField("rate2", flags); FieldInfo counter = typeof(AttackHandler).GetField("attackCounter2", flags); float r = rate == null ? 0f : (float)rate.GetValue(attacks[0]); float c = counter == null ? 0f : (float)counter.GetValue(attacks[0]); text += r > 0f && c > 0f ? "SPECIAL  " + Mathf.Clamp01(1f - c / r).ToString("0%") : "SPECIAL  READY"; } controlHudText.text = text;
        }
        void ShowPrompt()
        {
            if (prompt != null) { prompt.SetActive(true); return; }
            prompt = new GameObject("Tweaks Finish Prompt"); UnityEngine.Object.DontDestroyOnLoad(prompt); Canvas canvas = prompt.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000; prompt.AddComponent<CanvasScaler>(); prompt.AddComponent<GraphicRaycaster>(); GameObject textObject = new GameObject("Text"); textObject.transform.SetParent(prompt.transform, false); Text text = textObject.AddComponent<Text>(); text.text = "PRESS [TAB] TO CONTINUE"; text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); text.fontSize = 22; text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; RectTransform rect = textObject.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.5f, .12f); rect.anchorMax = new Vector2(.5f, .12f); rect.sizeDelta = new Vector2(420f, 48f);
        }
        void HidePrompt() { if (prompt != null) prompt.SetActive(false); }
        void Set(string key, bool value) { settings.Set(key, value.ToString()); }
        void Set(string key, string value) { settings.Set(key, value); }
        static float ParseFloat(string value, float fallback) { float result; return Single.TryParse(value, out result) ? result : fallback; }
    }
}
