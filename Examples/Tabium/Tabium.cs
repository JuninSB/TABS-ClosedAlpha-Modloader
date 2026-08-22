using System;
using System.Reflection;
using TABSClosedAlpha;
using SoftUI;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Tabium
{
    public sealed class Main : IMod
    {
        ModContext context;
        ModSettings settings;
        SoftWindow window;
        GameObject optionsRoot;
        GameObject hiddenNativeOptions;
        SoftUiService softUiService;
        bool menuDiagnosticsLogged;
        bool lastOptionsState;
        readonly Dictionary<string, GameObject> nativeCategories = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        readonly List<GameObject> nativeTabButtons = new List<GameObject>();
        bool tabiumSelected;
        GameObject nativeTabBar;

        public void Initialize(ModContext context)
        {
            this.context = context;
            settings = context.Settings;
            context.Log.Info("Tabium optimization mod initialized.");
            var softUi = context.Services.Get<SoftUiService>("softui");
            if (softUi == null) { context.Log.Error("SoftUI dependency was not loaded."); return; }
            softUiService = softUi;
            BuildSettingsUi(softUi);
            context.Events.SceneLoaded += OnSceneLoaded;
            context.Events.Update += MaintainNativeMenus;
            context.Commands.Register("tabium.apply", args => ApplySettings());
            ApplySettings();
        }

        public void Shutdown() { context.Log.Info("Tabium shutdown."); }
        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) { ApplySettings(); LogOptimizationDiagnostics(scene.name); }

        void LogOptimizationDiagnostics(string sceneName)
        {
            try
            {
                Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>(); int occlusionCameras = 0; for (int i = 0; i < cameras.Length; i++) if (cameras[i].useOcclusionCulling) occlusionCameras++;
                LODGroup[] lodGroups = UnityEngine.Object.FindObjectsOfType<LODGroup>(); Renderer[] renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(); ParticleSystem[] particles = UnityEngine.Object.FindObjectsOfType<ParticleSystem>(); Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
                int staticBatchRenderers = 0; for (int i = 0; i < renderers.Length; i++) { PropertyInfo property = typeof(Renderer).GetProperty("isPartOfStaticBatch", BindingFlags.Instance | BindingFlags.Public); if (property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(renderers[i], null)) staticBatchRenderers++; }
                int lightmaps = LightmapSettings.lightmaps == null ? 0 : LightmapSettings.lightmaps.Length;
                context.Log.Info("Optimization diagnostics " + sceneName + ": cameras=" + cameras.Length + ", occlusionCulling=" + occlusionCameras + ", LODGroups=" + lodGroups.Length + ", staticBatchRenderers=" + staticBatchRenderers + ", particles=" + particles.Length + ", lights=" + lights.Length + ", lightmaps=" + lightmaps + ", AA=" + QualitySettings.antiAliasing + ", shadows=" + QualitySettings.shadows + ", shadowDistance=" + QualitySettings.shadowDistance + ", vSync=" + QualitySettings.vSyncCount);
            }
            catch (Exception e) { context.Log.Warning("Optimization diagnostics failed: " + e.Message); }
        }

        void MaintainNativeMenus()
        {
            MainMenuHandler menu = MainMenuHandler.Instance;
            if (menu == null) menu = UnityEngine.Object.FindObjectOfType<MainMenuHandler>();
            if (menu == null) { if (!menuDiagnosticsLogged) { context.Log.Warning("MainMenuHandler not found yet; Mods button is waiting for the main menu."); menuDiagnosticsLogged = true; } return; }
            if (softUiService != null) { GameObject mainMenuRoot = GetMenuObject(menu, "MainMenuObject"); if (mainMenuRoot != null) { softUiService.InstallMainMenuButton(mainMenuRoot); softUiService.SetMainMenuButtonVisible(mainMenuRoot.activeInHierarchy); } else if (!menuDiagnosticsLogged) { context.Log.Warning("MainMenuObject is not initialized yet; Mods button is waiting."); menuDiagnosticsLogged = true; } }
        }

        void BuildSettingsUi(SoftUiService softUi)
        {
            softUi.ModMenu.Register("tabium", "Tabium", BuildTabiumSettings);
            return;
            /* Legacy Options implementation retained below for reference; the active Tabium UI is the separate Mods menu page. */
            window = softUi.CreateWindow("tabs-settings", "Settings").BindTo(() => { MainMenuHandler menu = MainMenuHandler.Instance; return menu != null && menu.CurrentMenuState == MainMenuHandler.MenuState.Options; });
            Options existing = Options.Instance;
            SoftTab video = window.AddTab("video", "VIDEO");
            video.AddLabel("VIDEO", 22);
            video.AddSlider("fov", "FIELD OF VIEW", existing == null ? 60f : existing.Fov, 40f, 120f, value => { if (Options.Instance != null) Options.Instance.SetFov(value); });
            video.AddToggle("ssao", "SSAO", existing != null && existing.SSAO, value => { if (Options.Instance != null) Options.Instance.SetSSAO(value); });
            video.AddToggle("anti-aliasing", "ANTI-ALIASING", existing != null && existing.AntiAliasing, value => { if (Options.Instance != null) Options.Instance.SetAntiAliasing(value); });
            video.AddToggle("depth-of-field", "DEPTH OF FIELD", existing != null && existing.DepthOfField, value => { if (Options.Instance != null) Options.Instance.SetDepthOfField(value); });
            video.AddToggle("bloom", "BLOOM", existing != null && existing.Bloom, value => { if (Options.Instance != null) Options.Instance.SetBloom(value); });
            video.AddSlider("shadowDistance", "SHADOW DISTANCE", ParseFloat(settings.Get("shadowDistance", "35"), 35f), 0f, 150f, value => { Set("shadowDistance", value.ToString("0")); ApplySettings(); });

            SoftTab audio = window.AddTab("audio", "AUDIO");
            audio.AddLabel("AUDIO", 22);
            audio.AddSlider("master", "MASTER VOLUME", existing == null ? 1f : existing.MasterVolume, 0f, 1f, value => { if (Options.Instance != null) Options.Instance.SetMasterVolume(value); });
            audio.AddSlider("music", "MUSIC VOLUME", existing == null ? 1f : existing.MusicVolume, 0f, 1f, value => { if (Options.Instance != null) Options.Instance.SetMusicVolume(value); });
            audio.AddSlider("effects-volume", "EFFECTS VOLUME", existing == null ? 1f : existing.EffectsVolume, 0f, 1f, value => { if (Options.Instance != null) Options.Instance.SetEffectsVolume(value); });

            SoftTab gameplay = window.AddTab("gameplay", "GAMEPLAY");
            gameplay.AddLabel("GAMEPLAY", 22);
            gameplay.AddDropdown("language", "LANGUAGE", new[] { "0", "1", "2", "3" }, existing == null ? 0 : existing.Language, value => { if (Options.Instance != null) Options.Instance.SetLanguage(value); });
            gameplay.AddSlider("sensitivity", "SENSITIVITY", existing == null ? 1f : existing.Sensitivity, 0.1f, 5f, value => { if (Options.Instance != null) Options.Instance.SetSensitivity(value); });
            gameplay.AddToggle("invert-x", "INVERTED X", existing != null && existing.InvertedX, value => { if (Options.Instance != null) Options.Instance.SetInvertedX(value); });
            gameplay.AddToggle("invert-y", "INVERTED Y", existing != null && existing.InvertedY, value => { if (Options.Instance != null) Options.Instance.SetInvertedY(value); });
            gameplay.AddButton("SAVE SETTINGS", SaveSettings);
            gameplay.AddButton("BACK TO MENU", SaveAndBack);

            SoftTab tabium = window.AddTab("tabium", "TABIUM");
            tabium.AddLabel("TABIUM", 22);
            tabium.AddLabel("PERFORMANCE OPTIONS", 13);
            tabium.AddToggle("effects", "REDUCE EFFECTS", settings.GetBool("reduceEffects", true), value => { Set("reduceEffects", value); ApplySettings(); });
            tabium.AddToggle("shadows", "DISABLE REALTIME SHADOWS", settings.GetBool("lowShadows", false), value => { Set("lowShadows", value); ApplySettings(); });
            tabium.AddToggle("frame", "FRAME RATE LIMIT", settings.GetBool("frameLimit", true), value => { Set("frameLimit", value); ApplySettings(); });
            string[] frameRates = { "30", "60", "90", "120" };
            tabium.AddDropdown("frameRate", "TARGET FPS", frameRates, FrameRateIndex(), value => { Set("frameRate", frameRates[value]); Set("frameLimit", true); ApplySettings(); });
            tabium.AddButton("APPLY TABIUM", ApplySettings);
        }

        void BuildTabiumSettings(SoftTab tab)
        {
            Options existing = Options.Instance;
            tab.AddLabel("TABIUM", 22);
            tab.AddLabel("SODIUM-INSPIRED PERFORMANCE SETTINGS", 12);
            tab.AddToggle("effects", "REDUCE EFFECTS", settings.GetBool("reduceEffects", true), value => { Set("reduceEffects", value); ApplySettings(); });
            tab.AddToggle("shadows", "DISABLE REALTIME SHADOWS", settings.GetBool("lowShadows", false), value => { Set("lowShadows", value); ApplySettings(); });
            tab.AddToggle("frame", "FRAME RATE LIMIT", settings.GetBool("frameLimit", true), value => { Set("frameLimit", value); ApplySettings(); });
            string[] frameRates = { "30", "60", "90", "120" };
            tab.AddDropdown("frameRate", "TARGET FPS", frameRates, FrameRateIndex(), value => { Set("frameRate", frameRates[value]); Set("frameLimit", true); ApplySettings(); });
            tab.AddSlider("shadowDistance", "SHADOW DISTANCE", ParseFloat(settings.Get("shadowDistance", "35"), 35f), 0f, 150f, value => { Set("shadowDistance", value.ToString("0")); ApplySettings(); });
            tab.AddButton("APPLY TABIUM", ApplySettings);
        }

        void SetupNativeOptions(GameObject root)
        {
            optionsRoot = root;
            nativeCategories.Clear();
            nativeCategories["Video"] = FindNamed(root.transform, "Video");
            nativeCategories["Audio"] = FindNamed(root.transform, "AUDIO");
            nativeCategories["Gameplay"] = FindNamed(root.transform, "game");
            GameObject styleSource = null;
            foreach (GameObject item in nativeCategories.Values) if (item != null) { styleSource = item; break; }
            if (window != null) window.AttachToNative(root.transform, styleSource);
            for (int i = 0; i < nativeTabButtons.Count; i++) if (nativeTabButtons[i] != null) UnityEngine.Object.Destroy(nativeTabButtons[i]);
            nativeTabButtons.Clear();
            if (nativeTabBar != null) UnityEngine.Object.Destroy(nativeTabBar);
            nativeTabBar = new GameObject("SoftUI Native Options Tabs");
            nativeTabBar.transform.SetParent(root.transform, false);
            RectTransform barRect = nativeTabBar.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(.10f, .04f); barRect.anchorMax = new Vector2(.90f, .04f);
            barRect.pivot = new Vector2(.5f, .5f); barRect.sizeDelta = new Vector2(0f, 48f);
            HorizontalLayoutGroup barLayout = nativeTabBar.AddComponent<HorizontalLayoutGroup>();
            barLayout.spacing = 12f; barLayout.padding = new RectOffset(8, 8, 3, 3); barLayout.childForceExpandWidth = true; barLayout.childForceExpandHeight = true;
            Button sample = root.GetComponentInChildren<Button>(true);
            if (sample == null) { context.Log.Warning("Native options has no Button; keeping original selector."); return; }
            HideOriginalCategoryButtons(root.transform);
            string[] labels = { "Video", "Audio", "Gameplay", "Tabium" };
            for (int i = 0; i < labels.Length; i++)
            {
                GameObject copy = CreateCleanTabButton(labels[i], nativeTabBar.transform, sample);
                nativeTabButtons.Add(copy);
            }
            context.Log.Info("Native options categories detected: Video, AUDIO, game; added Tabium category.");
            SelectNativeCategory("Video");
        }

        GameObject CreateCleanTabButton(string label, Transform parent, Button style)
        {
            GameObject obj = new GameObject("SoftUI Native Tab " + label);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            LayoutElement element = obj.AddComponent<LayoutElement>();
            element.minWidth = 150f; element.preferredWidth = 180f; element.flexibleWidth = 1f;
            Image image = obj.AddComponent<Image>();
            if (style != null)
            {
                Image sourceImage = style.GetComponent<Image>();
                if (sourceImage != null) { image.sprite = sourceImage.sprite; image.material = sourceImage.material; image.type = sourceImage.type; image.color = sourceImage.color; }
            }
            Button button = obj.AddComponent<Button>();
            if (style != null) button.colors = style.colors;
            button.targetGraphic = image;
            string id = label;
            button.onClick.AddListener(() => SelectNativeCategory(id));
            GameObject textObject = new GameObject("Label");
            textObject.transform.SetParent(obj.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = new Vector2(8f, 0f); textRect.offsetMax = new Vector2(-8f, 0f);
            Text sourceText = style == null ? null : style.GetComponentInChildren<Text>(true);
            Text text = textObject.AddComponent<Text>();
            text.font = sourceText == null ? Resources.GetBuiltinResource<Font>("Arial.ttf") : sourceText.font;
            text.fontSize = sourceText == null ? 14 : sourceText.fontSize;
            text.color = sourceText == null ? Color.white : sourceText.color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = label.ToUpperInvariant();
            return obj;
        }

        void HideOriginalCategoryButtons(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Button button = child.GetComponent<Button>();
                Text text = child.GetComponentInChildren<Text>(true);
                if (button != null && text != null)
                {
                    string value = text.text == null ? "" : text.text.Trim();
                    if (String.Equals(value, "Video", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Audio", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "AUDIO", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Gameplay", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "game", StringComparison.OrdinalIgnoreCase)) child.gameObject.SetActive(false);
                }
                HideOriginalCategoryButtons(child);
            }
        }

        void SelectNativeCategory(string name)
        {
            tabiumSelected = String.Equals(name, "Tabium", StringComparison.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, GameObject> item in nativeCategories) if (item.Value != null) item.Value.SetActive(!tabiumSelected && String.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase));
            if (window != null) window.SetVisible(tabiumSelected);
        }

        void ApplyNativeCategory() { if (optionsRoot == null) return; if (window != null) window.SetVisible(tabiumSelected); }

        static GameObject FindNamed(Transform root, string wanted)
        {
            if (String.Equals(root.name, wanted, StringComparison.OrdinalIgnoreCase)) return root.gameObject;
            for (int i = 0; i < root.childCount; i++) { GameObject found = FindNamed(root.GetChild(i), wanted); if (found != null) return found; }
            return null;
        }

        void SaveSettings() { if (Options.Instance != null) Options.Instance.SubmitPrefs(); context.Log.Info("TABS settings saved."); }

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
