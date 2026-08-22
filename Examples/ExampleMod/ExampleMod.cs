using System;
using System.Reflection;
using TABSClosedAlpha;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ExampleMod
{
    public sealed class Main : IMod
    {
        ModContext context;
        GameObject overlay;
        bool boosted;

        public void Initialize(ModContext context)
        {
            this.context = context;
            context.Log.Info("ExampleMod initialized.");
            context.Settings.GetBool("showOverlay", false);
            context.Services.Register("example.greeting", new GreetingService());
            context.Commands.Register("example.ping", args => context.Log.Info("pong"));
            context.Keys.Register(KeyCode.F8, ToggleOverlay);
            context.Events.SceneLoaded += OnSceneLoaded;
            context.Events.Update += OnUpdate;

            // Confirmed game method: UnitHandler.TakeDamage(float, Vector3, string, Vector3).
            MethodInfo damage = typeof(UnitHandler).GetMethod("TakeDamage", BindingFlags.Public | BindingFlags.Instance);
            context.Patches.Prefix(damage, typeof(Main).GetMethod("BeforeUnitDamage", BindingFlags.Static | BindingFlags.NonPublic));

            // The file is optional; this demonstrates external mod assets without assuming game internals.
            Texture2D texture = context.Assets.LoadTexture("example.png");
            if (texture != null) context.Log.Info("Loaded external texture: " + texture.width + "x" + texture.height);
            context.Log.Info("Built-in unit resource lookup is available through UnitLoaderHandler when its singleton exists.");
        }

        public void Shutdown()
        {
            if (overlay != null) UnityEngine.Object.Destroy(overlay);
            context.Log.Info("ExampleMod shutdown.");
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            context.Log.Info("Scene loaded: " + scene.name + " (" + mode + ")");
            if (context.Settings.GetBool("showOverlay", false)) CreateOverlay();
            BoostFirstBattleUnit();
        }

        void OnUpdate()
        {
            if (!boosted) BoostFirstBattleUnit();
        }

        void BoostFirstBattleUnit()
        {
            UnitHandler[] units = context.Game.Units;
            if (units.Length == 0) return;
            // m_health is a real private field discovered in this Closed Alpha's UnitHandler.
            FieldInfo health = context.Game.PrivateField(typeof(UnitHandler), "m_health");
            if (health == null) { context.Log.Warning("UnitHandler.m_health changed in this build; boost skipped."); return; }
            UnitHandler first = units[0];
            float current = (float)health.GetValue(first);
            health.SetValue(first, current + 25f);
            boosted = true;
            context.Log.Info("Added 25 health to " + first.name + ".");
        }

        void CreateOverlay()
        {
            if (overlay != null) return;
            overlay = new GameObject("ExampleMod Overlay");
            UnityEngine.Object.DontDestroyOnLoad(overlay);
            Canvas canvas = overlay.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.AddComponent<CanvasScaler>(); overlay.AddComponent<GraphicRaycaster>(); overlay.AddComponent<ExampleBehaviour>();
            GameObject label = new GameObject("Label"); label.transform.SetParent(overlay.transform, false);
            Text text = label.AddComponent<Text>(); text.text = "ExampleMod active — F8 toggles this overlay"; text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); text.fontSize = 18; text.color = Color.white;
            RectTransform rect = label.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(0f, 1f); rect.anchoredPosition = new Vector2(14f, -24f); rect.sizeDelta = new Vector2(500f, 40f);
        }
        void ToggleOverlay() { if (overlay == null) CreateOverlay(); else overlay.SetActive(!overlay.activeSelf); }
        static void BeforeUnitDamage(UnitHandler __instance, float damage, string damager) { Debug.Log("[ExampleMod] " + __instance.name + " will take " + damage + " damage from " + damager); }
    }
    public sealed class ExampleBehaviour : MonoBehaviour { float elapsed; void Update() { elapsed += Time.deltaTime; transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed) * 0.2f); } }
    public sealed class GreetingService { public string GetGreeting() { return "Hello from ExampleMod"; } }
}
