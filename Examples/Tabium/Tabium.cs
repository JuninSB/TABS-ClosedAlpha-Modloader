using System;
using TABSClosedAlpha;
using SoftUI;
using UnityEngine;

namespace Tabium
{
    public sealed class Main : IMod
    {
        ModContext context;
        ModSettings settings;
        SoftWindow window;

        public void Initialize(ModContext context)
        {
            this.context = context;
            settings = context.Settings;
            context.Log.Info("Tabium optimization mod initialized.");
            var softUi = context.Services.Get<SoftUiService>("softui");
            if (softUi == null) { context.Log.Error("SoftUI dependency was not loaded."); return; }
            BuildSettingsUi(softUi);
            context.Events.SceneLoaded += OnSceneLoaded;
            context.Commands.Register("tabium.apply", args => ApplySettings());
            ApplySettings();
        }

        public void Shutdown() { context.Log.Info("Tabium shutdown."); }
        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) { ApplySettings(); }

        void BuildSettingsUi(SoftUiService softUi)
        {
            window = softUi.CreateWindow("tabium", "Tabium").BindTo(() =>
            {
                MainMenuHandler menu = MainMenuHandler.Instance;
                return menu != null && menu.CurrentMenuState.ToString() == "Options";
            });
            SoftTab optimization = window.AddTab("optimization", "Optimization");
            optimization.AddLabel("Tabium performance profile", 20);
            optimization.AddLabel("Controls are kept separate from the original 2016 layout.", 12);
            optimization.AddToggle("effects", "Reduce SSAO, anti-aliasing, DOF and bloom", settings.GetBool("reduceEffects", true), value => { Set("reduceEffects", value); ApplySettings(); });
            optimization.AddToggle("shadows", "Disable realtime shadows", settings.GetBool("lowShadows", false), value => { Set("lowShadows", value); ApplySettings(); });
            optimization.AddToggle("frame", "Use stable 60 FPS limit", settings.GetBool("frameLimit", true), value => { Set("frameLimit", value); ApplySettings(); });
            optimization.AddButton("Apply now", ApplySettings);

            SoftTab advanced = window.AddTab("advanced", "Advanced");
            advanced.AddLabel("Old Unity 5.5 quality controls", 20);
            string[] frameRates = { "30", "60", "90", "120" };
            advanced.AddDropdown("frameRate", "Target FPS", frameRates, FrameRateIndex(), value => { Set("frameRate", frameRates[value]); Set("frameLimit", true); ApplySettings(); });
            advanced.AddSlider("shadowDistance", "Shadow distance", ParseFloat(settings.Get("shadowDistance", "35"), 35f), 0f, 150f, value => { Set("shadowDistance", value.ToString("0")); ApplySettings(); });
            advanced.AddLabel("Changes are saved per mod in config.cfg.", 12);
        }

        void ApplySettings()
        {
            if (!settings.GetBool("enabled", true)) return;
            bool reduceEffects = settings.GetBool("reduceEffects", true);
            bool lowShadows = settings.GetBool("lowShadows", false);
            bool frameLimit = settings.GetBool("frameLimit", true);
            Options options = Options.Instance;
            if (options != null && reduceEffects) { options.SetSSAO(false); options.SetAntiAliasing(false); options.SetDepthOfField(false); options.SetBloom(false); }
            if (lowShadows) { QualitySettings.shadows = ShadowQuality.Disable; QualitySettings.shadowDistance = ParseFloat(settings.Get("shadowDistance", "35"), 35f); QualitySettings.pixelLightCount = 1; QualitySettings.softParticles = false; }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = frameLimit ? ParseInt(settings.Get("frameRate", "60"), 60) : -1;
            context.Log.Info("Applied Closed Alpha effects optimization.");
        }

        void Set(string key, bool value) { settings.Set(key, value.ToString()); }
        void Set(string key, string value) { settings.Set(key, value); }
        int FrameRateIndex() { int rate = ParseInt(settings.Get("frameRate", "60"), 60); return rate <= 30 ? 0 : rate <= 60 ? 1 : rate <= 90 ? 2 : 3; }
        static int ParseInt(string value, int fallback) { int result; return Int32.TryParse(value, out result) ? result : fallback; }
        static float ParseFloat(string value, float fallback) { float result; return Single.TryParse(value, out result) ? result : fallback; }
    }
}
