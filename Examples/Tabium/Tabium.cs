using System;
using System.Collections.Generic;
using TABSClosedAlpha;
using UnityEngine;
using UnityEngine.UI;

namespace Tabium
{
    public sealed class Main : IMod
    {
        ModContext context;
        ModSettings settings;
        GameObject panel;
        Toggle effectsToggle;
        Toggle shadowsToggle;
        Toggle frameLimitToggle;
        float retryTimer;
        bool lastOptionsState;

        public void Initialize(ModContext context)
        {
            this.context = context;
            settings = context.Settings;
            context.Log.Info("Tabium optimization mod initialized.");
            context.Events.SceneLoaded += OnSceneLoaded;
            context.Events.Update += OnUpdate;
            context.Commands.Register("tabium.apply", args => ApplySettings());
            ApplySettings();
        }

        public void Shutdown()
        {
            if (panel != null) UnityEngine.Object.Destroy(panel);
            context.Log.Info("Tabium shutdown.");
        }

        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            retryTimer = 0f;
            ApplySettings();
            EnsureSettingsPanel();
        }

        void OnUpdate()
        {
            retryTimer += Time.unscaledDeltaTime;
            if (retryTimer > 1f) { retryTimer = 0f; EnsureSettingsPanel(); }
            var optionsUi = UnityEngine.Object.FindObjectOfType<OptionsUI>();
            bool isVisible = optionsUi != null && optionsUi.gameObject.activeInHierarchy;
            if (panel != null && isVisible != lastOptionsState) { panel.SetActive(isVisible); lastOptionsState = isVisible; }
        }

        void ApplySettings()
        {
            bool enabled = settings.GetBool("enabled", true);
            if (!enabled) return;
            bool reduceEffects = settings.GetBool("reduceEffects", true);
            bool lowShadows = settings.GetBool("lowShadows", false);
            bool frameLimit = settings.GetBool("frameLimit", true);

            Options options = Options.Instance;
            if (options != null)
            {
                if (reduceEffects) { options.SetSSAO(false); options.SetAntiAliasing(false); options.SetDepthOfField(false); options.SetBloom(false); }
                context.Log.Info("Applied Closed Alpha effects optimization.");
            }
            if (lowShadows)
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.shadowDistance = 35f;
                QualitySettings.pixelLightCount = 1;
                QualitySettings.softParticles = false;
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = frameLimit ? 60 : -1;
        }

        void EnsureSettingsPanel()
        {
            OptionsUI optionsUi = UnityEngine.Object.FindObjectOfType<OptionsUI>();
            if (optionsUi == null) return;
            if (panel != null) { panel.SetActive(optionsUi.gameObject.activeInHierarchy); return; }
            panel = new GameObject("Tabium Settings");
            panel.transform.SetParent(optionsUi.transform, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f); rect.anchorMax = new Vector2(0.5f, 0.5f); rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = new Vector2(260f, -70f); rect.sizeDelta = new Vector2(330f, 210f);
            Image background = panel.AddComponent<Image>(); background.color = new Color(0.02f, 0.02f, 0.02f, 0.82f);
            var layout = panel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(16, 16, 12, 10); layout.spacing = 4; layout.childForceExpandHeight = false;
            AddLabel("TABIUM OPTIMIZATION", 18, Color.cyan);
            AddLabel("Unity 5.5 / Closed Alpha profile", 12, Color.white);
            effectsToggle = AddToggle("Reduce post effects (SSAO, AA, DOF, Bloom)", settings.GetBool("reduceEffects", true), value => { settingsValue("reduceEffects", value); ApplySettings(); });
            shadowsToggle = AddToggle("Disable realtime shadows", settings.GetBool("lowShadows", false), value => { settingsValue("lowShadows", value); ApplySettings(); });
            frameLimitToggle = AddToggle("Use stable 60 FPS limit", settings.GetBool("frameLimit", true), value => { settingsValue("frameLimit", value); ApplySettings(); });
            AddLabel("Settings are saved in the Tabium mod folder.", 11, Color.gray);
            lastOptionsState = optionsUi.gameObject.activeInHierarchy;
            panel.SetActive(lastOptionsState);
        }

        void settingsValue(string key, bool value) { settings.Get(key, value.ToString()); settings.Set(key, value.ToString()); }

        Text AddLabel(string value, int size, Color color)
        {
            GameObject item = new GameObject("Label"); item.transform.SetParent(panel.transform, false); item.AddComponent<LayoutElement>().preferredHeight = size + 8; Text text = item.AddComponent<Text>(); text.text = value; text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleLeft; return text;
        }

        Toggle AddToggle(string label, bool value, Action<bool> changed)
        {
            GameObject item = new GameObject(label); item.transform.SetParent(panel.transform, false); item.AddComponent<LayoutElement>().preferredHeight = 26f; item.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.35f); Toggle toggle = item.AddComponent<Toggle>(); toggle.isOn = value; toggle.onValueChanged.AddListener(v => changed(v));
            GameObject caption = new GameObject("Caption"); caption.transform.SetParent(item.transform, false); Text text = caption.AddComponent<Text>(); text.text = label; text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); text.fontSize = 12; text.color = Color.white; text.alignment = TextAnchor.MiddleLeft; RectTransform rect = caption.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0f, 0f); rect.anchorMax = new Vector2(1f, 1f); rect.offsetMin = new Vector2(26f, 0f); rect.offsetMax = Vector2.zero; return toggle;
        }
    }
}
