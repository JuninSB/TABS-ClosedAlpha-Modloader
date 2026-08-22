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
            if (!optionsState) { optionsRoot = null; nativeCategories.Clear(); tabiumSelected = false; return; }
            GameObject root = GetMenuObject(menu, "OptionsObject");
            if (root == null) { OptionsUI ui = UnityEngine.Object.FindObjectOfType<OptionsUI>(); root = ui == null ? null : ui.gameObject; }
            if (root != null && root != optionsRoot) SetupNativeOptions(root);
            ApplyNativeCategory();
        }

        void BuildSettingsUi(SoftUiService softUi)
        {
            window = softUi.CreateWindow("tabium", "Tabium").BindTo(() => { MainMenuHandler menu = MainMenuHandler.Instance; return tabiumSelected && menu != null && IsNativeMenuOpen(menu, MainMenuHandler.MenuState.Options); });
            SoftTab optimization = window.AddTab("optimization", "Optimization");
            optimization.AddLabel("Tabium performance profile", 20);
            optimization.AddLabel("Native TABS options are grouped beside this Tabium tab.", 12);
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
