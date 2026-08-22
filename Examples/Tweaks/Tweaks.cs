using System;
using System.Reflection;
using TABSClosedAlpha;
using SoftUI;
using UnityEngine;
using UnityEngine.UI;

namespace Tweaks
{
    public sealed class Main : IMod
    {
        ModContext context; ModSettings settings; GameObject prompt; bool wasPlaying; bool pausedAfterFinish; float baseFixedDelta;
        public void Initialize(ModContext context)
        {
            this.context = context; settings = context.Settings; baseFixedDelta = Time.fixedDeltaTime;
            SoftUiService softUi = context.Services.Get<SoftUiService>("softui");
            if (softUi == null) { context.Log.Error("SoftUI dependency was not loaded."); return; }
            softUi.ModMenu.Register("tweaks", "Tweaks", BuildSettings);
            context.Events.Update += Update;
            context.Events.SceneLoaded += (scene, mode) => { wasPlaying = false; pausedAfterFinish = false; HidePrompt(); RestoreTime(); };
            context.Log.Info("Tweaks initialized: mouse slow motion, G super slow motion, battle-finish pause, edge camera and F unit control.");
        }
        public void Shutdown() { RestoreTime(); HidePrompt(); }
        void BuildSettings(SoftTab tab)
        {
            tab.AddLabel("TWEAKS", 20);
            tab.AddToggle("pause-finish", "PAUSE SIMULATION WHEN BATTLE FINISHES", settings.GetBool("pauseWhenFinished", true), value => Set("pauseWhenFinished", value));
            tab.AddToggle("edge-camera", "MOVE CAMERA WHEN MOUSE TOUCHES SCREEN EDGE", settings.GetBool("edgeCamera", false), value => Set("edgeCamera", value));
            tab.AddSlider("edge-margin", "EDGE MARGIN", ParseFloat(settings.Get("edgeMargin", "28"), 28f), 4f, 120f, value => Set("edgeMargin", value.ToString("0")));
            tab.AddSlider("edge-speed", "EDGE CAMERA SPEED", ParseFloat(settings.Get("edgeSpeed", "8"), 8f), 1f, 30f, value => Set("edgeSpeed", value.ToString("0.0")));
            tab.AddToggle("nearest-unit", "F CONTROLS UNIT NEAREST TO SCREEN CENTER", settings.GetBool("nearestUnit", true), value => Set("nearestUnit", value));
            tab.AddLabel("HOLD LEFT MOUSE: 0.1x   |   HOLD G: 0.01x   |   F: CONTROL UNIT", 11);
        }
        void Update()
        {
            StartManager start = StartManager.Instance; bool playing = start != null && start.Playing;
            if (playing && !wasPlaying) { pausedAfterFinish = false; HidePrompt(); }
            if (wasPlaying && !playing && settings.GetBool("pauseWhenFinished", true)) PauseAfterFinish();
            wasPlaying = playing;
            if (pausedAfterFinish) { Time.timeScale = 0f; if (Input.GetKeyDown(KeyCode.Tab)) { pausedAfterFinish = false; HidePrompt(); RestoreTime(); } return; }
            if (playing) ApplySlowMotion(); else RestoreTime();
            if (playing && settings.GetBool("edgeCamera", false)) EdgeCamera();
            if (playing && settings.GetBool("nearestUnit", true) && Input.GetKeyDown(KeyCode.F)) ControlNearestUnit();
        }
        void ApplySlowMotion()
        {
            float scale = Input.GetKey(KeyCode.G) ? .01f : (Input.GetMouseButton(0) ? .1f : 1f);
            Time.timeScale = scale; Time.fixedDeltaTime = baseFixedDelta * (scale <= 0f ? 1f : scale);
        }
        void RestoreTime() { Time.timeScale = 1f; Time.fixedDeltaTime = baseFixedDelta; }
        void PauseAfterFinish() { pausedAfterFinish = true; Time.timeScale = 0f; ShowPrompt(); context.Log.Info("Battle finished; simulation paused. Press [TAB] to continue."); }
        void EdgeCamera()
        {
            Camera camera = Camera.main; if (camera == null) return; float margin = ParseFloat(settings.Get("edgeMargin", "28"), 28f); float speed = ParseFloat(settings.Get("edgeSpeed", "8"), 8f); Vector3 direction = Vector3.zero; Vector3 mouse = Input.mousePosition; if (mouse.x <= margin) direction -= camera.transform.right; else if (mouse.x >= Screen.width - margin) direction += camera.transform.right; if (mouse.y <= margin) direction -= Vector3.ProjectOnPlane(camera.transform.up, Vector3.up).normalized; else if (mouse.y >= Screen.height - margin) direction += Vector3.ProjectOnPlane(camera.transform.up, Vector3.up).normalized; direction.y = 0f; if (direction.sqrMagnitude > .001f) camera.transform.position += direction.normalized * speed * Time.unscaledDeltaTime;
        }
        void ControlNearestUnit()
        {
            Camera camera = Camera.main; if (camera == null) return; UnitHandler[] units = context.Game.Units; UnitHandler best = null; float bestDistance = Single.MaxValue; Vector2 center = new Vector2(Screen.width * .5f, Screen.height * .5f); for (int i = 0; i < units.Length; i++) { UnitHandler unit = units[i]; if (unit == null || unit.gameObject == null) continue; Vector3 screen = camera.WorldToScreenPoint(unit.transform.position); if (screen.z <= 0f) continue; float distance = (new Vector2(screen.x, screen.y) - center).sqrMagnitude; if (distance < bestDistance) { bestDistance = distance; best = unit; } } if (best == null) return; FirstPersonCameraHandler handler = UnityEngine.Object.FindObjectOfType<FirstPersonCameraHandler>(); if (handler == null) return; FieldInfo target = context.Game.PrivateField(typeof(FirstPersonCameraHandler), "mCurrentTargetAssigned"); if (target != null) target.SetValue(handler, best.transform); context.Log.Info("Controlling unit nearest to screen center: " + best.name);
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
