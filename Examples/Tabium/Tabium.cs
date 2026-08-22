using System;
using System.Reflection;
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
        GameObject hiddenOptions;

        public void Initialize(ModContext context)
        {
            this.context = context;
            settings = context.Settings;
            context.Log.Info("Tabium optimization mod initialized.");
            var softUi = context.Services.Get<SoftUiService>("softui");
            if (softUi == null) { context.Log.Error("SoftUI dependency was not loaded."); return; }
            BuildSettingsUi(softUi);
            context.Events.SceneLoaded += OnSceneLoaded;
            context.Events.Update += MaintainNativeMenus;
            context.Commands.Register("tabium.apply", args => ApplySettings());
            ApplySettings();
        }

        public void Shutdown() { context.Log.Info("Tabium shutdown."); }
        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) { ApplySettings(); }

        void MaintainNativeMenus()
        {
            MainMenuHandler menu = MainMenuHandler.Instance;
            if (menu == null) return;
            bool optionsState = IsNativeMenuOpen(menu, MainMenuHandler.MenuState.Options);
            if (optionsState)
            {
                if (hiddenOptions == null && window != null && window.IsVisible) { OptionsUI ui = UnityEngine.Object.FindObjectOfType<OptionsUI>(); hiddenOptions = ui == null ? GetMenuObject(menu, "OptionsObject") : ui.gameObject; }
                if (hiddenOptions != null) hiddenOptions.SetActive(false);
            }
            else if (hiddenOptions != null) { hiddenOptions.SetActive(true); hiddenOptions = null; }
        }

        void BuildSettingsUi(SoftUiService softUi)
        {
            window = softUi.CreateWindow("tabium", "Tabium").BindTo(() => { MainMenuHandler menu = MainMenuHandler.Instance; return menu != null && IsNativeMenuOpen(menu, MainMenuHandler.MenuState.Options); });
            SoftTab optimization = window.AddTab("optimization", "Optimization");
            optimization.AddLabel("Tabium performance profile", 20);
            optimization.AddLabel("Controls are kept separate from the original 2016 layout.", 12);
            optimization.AddToggle("effects", "Reduce SSAO, anti-aliasing, DOF and bloom", settings.GetBool("reduceEffects", true), value => { Set("reduceEffects", value); ApplySettings(); });
            optimization.AddToggle("shadows", "Disable realtime shadows", settings.GetBool("lowShadows", false), value => { Set("lowShadows", value); ApplySettings(); });
            optimization.AddToggle("frame", "Use stable 60 FPS limit", settings.GetBool("frameLimit", true), value => { Set("frameLimit", value); ApplySettings(); });
            optimization.AddButton("Apply now", ApplySettings);

            SoftTab game = window.AddTab("game", "Game Settings");
            game.AddLabel("Original TABS settings", 20);
            game.AddLabel("These controls call the real Options singleton.", 12);
            Options existing = Options.Instance;
            game.AddDropdown("language", "Language index", new[] { "0", "1", "2", "3" }, existing == null ? 0 : existing.Language, value => { if (Options.Instance != null) Options.Instance.SetLanguage(value); });
            game.AddSlider("master", "Master volume", existing == null ? 1f : existing.MasterVolume, 0f, 1f, value => { if (Options.Instance != null) Options.Instance.SetMasterVolume(value); });
            game.AddSlider("music", "Music volume", existing == null ? 1f : existing.MusicVolume, 0f, 1f, value => { if (Options.Instance != null) Options.Instance.SetMusicVolume(value); });
            game.AddSlider("effects-volume", "Effects volume", existing == null ? 1f : existing.EffectsVolume, 0f, 1f, value => { if (Options.Instance != null) Options.Instance.SetEffectsVolume(value); });
            game.AddSlider("fov", "Field of view", existing == null ? 60f : existing.Fov, 40f, 120f, value => { if (Options.Instance != null) Options.Instance.SetFov(value); });
            game.AddSlider("sensitivity", "Sensitivity", existing == null ? 1f : existing.Sensitivity, 0.1f, 5f, value => { if (Options.Instance != null) Options.Instance.SetSensitivity(value); });
            game.AddToggle("ssao", "SSAO", existing != null && existing.SSAO, value => { if (Options.Instance != null) Options.Instance.SetSSAO(value); });
            game.AddToggle("anti-aliasing", "Anti-aliasing", existing != null && existing.AntiAliasing, value => { if (Options.Instance != null) Options.Instance.SetAntiAliasing(value); });
            game.AddToggle("depth-of-field", "Depth of field", existing != null && existing.DepthOfField, value => { if (Options.Instance != null) Options.Instance.SetDepthOfField(value); });
            game.AddToggle("bloom", "Bloom", existing != null && existing.Bloom, value => { if (Options.Instance != null) Options.Instance.SetBloom(value); });
            game.AddToggle("invert-x", "Invert X", existing != null && existing.InvertedX, value => { if (Options.Instance != null) Options.Instance.SetInvertedX(value); });
            game.AddToggle("invert-y", "Invert Y", existing != null && existing.InvertedY, value => { if (Options.Instance != null) Options.Instance.SetInvertedY(value); });
            game.AddButton("Save and back", SaveAndBack);

            SoftTab advanced = window.AddTab("advanced", "Advanced");
            advanced.AddLabel("Old Unity 5.5 quality controls", 20);
            string[] frameRates = { "30", "60", "90", "120" };
            advanced.AddDropdown("frameRate", "Target FPS", frameRates, FrameRateIndex(), value => { Set("frameRate", frameRates[value]); Set("frameLimit", true); ApplySettings(); });
            advanced.AddSlider("shadowDistance", "Shadow distance", ParseFloat(settings.Get("shadowDistance", "35"), 35f), 0f, 150f, value => { Set("shadowDistance", value.ToString("0")); ApplySettings(); });
            advanced.AddLabel("Changes are saved per mod in config.cfg.", 12);

        }

        void SaveAndBack()
        {
            if (Options.Instance != null) Options.Instance.SubmitPrefs();
            if (MainMenuHandler.Instance != null) MainMenuHandler.Instance.BackToMenu();
        }

        GameObject GetMenuObject(MainMenuHandler menu, string fieldName)
        {
            var field = context.Game.PrivateField(typeof(MainMenuHandler), fieldName);
            return field == null ? null : field.GetValue(menu) as GameObject;
        }

        bool IsNativeMenuOpen(MainMenuHandler menu, MainMenuHandler.MenuState state)
        {
            MethodInfo method = typeof(MainMenuHandler).GetMethod("GetCurrentMenu", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            GameObject menuObject = method == null ? null : method.Invoke(menu, new object[] { state }) as GameObject;
            return menuObject != null && menuObject.activeInHierarchy;
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
