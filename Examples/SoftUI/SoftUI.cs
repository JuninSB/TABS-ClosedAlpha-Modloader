using System;
using System.Collections.Generic;
using TABSClosedAlpha;
using UnityEngine;
using UnityEngine.UI;

namespace SoftUI
{
    public sealed class Main : IMod
    {
        public void Initialize(ModContext context)
        {
            var service = new SoftUiService(context.Log);
            context.Services.Register("softui", service);
            context.Log.Info("SoftUI library ready.");
        }
        public void Shutdown() { }
    }

    public sealed class SoftUiService
    {
        readonly IModLogger log;
        readonly List<SoftWindow> windows = new List<SoftWindow>();
        readonly GameObject hostObject;
        public ModMenuService ModMenu { get; private set; }
        internal SoftUiService(IModLogger log)
        {
            this.log = log;
            hostObject = new GameObject("SoftUI Host");
            UnityEngine.Object.DontDestroyOnLoad(hostObject);
            var host = hostObject.AddComponent<SoftUiHost>();
            host.Service = this;
            var canvas = hostObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = hostObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            hostObject.AddComponent<GraphicRaycaster>();
            ModMenu = new ModMenuService(this, log);
        }
        public SoftWindow CreateWindow(string id, string title)
        {
            var window = new SoftWindow(id, title, hostObject.transform, log);
            windows.Add(window);
            return window;
        }
        public void InstallMainMenuButton(GameObject mainMenuRoot) { if (ModMenu != null) ModMenu.InstallMainMenuButton(mainMenuRoot); }
        internal void Tick() { for (int i = 0; i < windows.Count; i++) windows[i].Tick(); if (ModMenu != null) ModMenu.Tick(); }
        internal void Destroy() { for (int i = 0; i < windows.Count; i++) windows[i].Destroy(); windows.Clear(); if (hostObject != null) UnityEngine.Object.Destroy(hostObject); }
    }

    public sealed class SoftUiHost : MonoBehaviour
    {
        public SoftUiService Service;
        void Update() { if (Service != null) Service.Tick(); }
    }

    /// <summary>Shared in-game Mods menu. Mods register pages instead of modifying the TABS Options scene.</summary>
    public sealed class ModMenuService
    {
        sealed class Entry { public string Id; public string Title; public Action<SoftTab> Build; public SoftWindow Window; public bool Added; }
        readonly SoftUiService service; readonly IModLogger log; readonly List<Entry> entries = new List<Entry>();
        GameObject mainMenuRoot; GameObject modsButton; SoftWindow listWindow; SoftTab listPage; bool opened;
        internal ModMenuService(SoftUiService service, IModLogger log) { this.service = service; this.log = log; }
        public void Register(string id, string title, Action<SoftTab> build)
        {
            for (int i = 0; i < entries.Count; i++) if (String.Equals(entries[i].Id, id, StringComparison.OrdinalIgnoreCase)) { entries[i].Title = title; entries[i].Build = build; return; }
            Entry entry = new Entry { Id = id, Title = title, Build = build }; entries.Add(entry);
            if (listPage != null) AddEntry(entry);
        }
        internal void InstallMainMenuButton(GameObject root)
        {
            if (root == null || root == mainMenuRoot) return;
            if (modsButton != null) UnityEngine.Object.Destroy(modsButton);
            mainMenuRoot = root;
            Button sample = root.GetComponentInChildren<Button>(true);
            if (sample == null) { log.Warning("SoftUI could not find a native main-menu button for Mods."); return; }
            modsButton = CreateButton("MODS", sample, sample.transform.parent); modsButton.name = "SoftUI Mods Button";
            modsButton.GetComponent<Button>().onClick.AddListener(OpenMenu);
            log.Info("Mods button added to the native main menu.");
        }
        GameObject CreateButton(string label, Button style, Transform parent)
        {
            GameObject obj = new GameObject("SoftUI Button " + label); obj.transform.SetParent(parent, false); RectTransform rect = obj.AddComponent<RectTransform>(); rect.localScale = Vector3.one;
            LayoutElement layout = obj.AddComponent<LayoutElement>(); layout.minWidth = 170f; layout.preferredWidth = 190f; layout.minHeight = 44f; layout.preferredHeight = 44f;
            Image image = obj.AddComponent<Image>(); Image sourceImage = style == null ? null : style.GetComponent<Image>(); if (sourceImage != null) { image.sprite = sourceImage.sprite; image.material = sourceImage.material; image.type = sourceImage.type; image.color = sourceImage.color; }
            Button button = obj.AddComponent<Button>(); if (style != null) button.colors = style.colors; button.targetGraphic = image;
            GameObject textObj = new GameObject("Text"); textObj.transform.SetParent(obj.transform, false); RectTransform textRect = textObj.AddComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = new Vector2(8f, 0f); textRect.offsetMax = new Vector2(-8f, 0f);
            Text sourceText = style == null ? null : style.GetComponentInChildren<Text>(true); Text text = textObj.AddComponent<Text>(); text.font = sourceText == null ? Resources.GetBuiltinResource<Font>("Arial.ttf") : sourceText.font; text.fontSize = sourceText == null ? 15 : sourceText.fontSize; text.color = sourceText == null ? Color.white : sourceText.color; text.alignment = TextAnchor.MiddleCenter; text.horizontalOverflow = HorizontalWrapMode.Overflow; text.text = label;
            return obj;
        }
        void OpenMenu()
        {
            opened = true; if (mainMenuRoot != null) mainMenuRoot.SetActive(false);
            if (listWindow == null) { listWindow = service.CreateWindow("mods-menu", "MODS"); listPage = listWindow.AddTab("mods", "MODS"); listPage.AddLabel("INSTALLED MODS", 22); listPage.AddButton("BACK TO GAME", CloseMenu); }
            for (int i = 0; i < entries.Count; i++) AddEntry(entries[i]);
            listWindow.SetVisible(true);
        }
        void AddEntry(Entry entry)
        {
            if (entry.Added || listPage == null) return; entry.Added = true; listPage.AddButton(entry.Title.ToUpperInvariant(), () => OpenEntry(entry));
        }
        void OpenEntry(Entry entry)
        {
            if (listWindow != null) listWindow.SetVisible(false);
            if (entry.Window == null) { entry.Window = service.CreateWindow("mod-settings-" + entry.Id, entry.Title); SoftTab page = entry.Window.AddTab("settings", "SETTINGS"); if (entry.Build != null) entry.Build(page); page.AddButton("BACK TO MODS", () => { entry.Window.SetVisible(false); if (listWindow != null) listWindow.SetVisible(true); }); }
            entry.Window.SetVisible(true);
        }
        void CloseMenu()
        {
            opened = false; if (listWindow != null) listWindow.SetVisible(false); for (int i = 0; i < entries.Count; i++) if (entries[i].Window != null) entries[i].Window.SetVisible(false); if (mainMenuRoot != null) mainMenuRoot.SetActive(true);
        }
        internal void Tick() { }
    }

    public sealed class SoftWindow
    {
        readonly IModLogger log;
        readonly GameObject root;
        readonly GameObject content;
        readonly Dictionary<string, GameObject> pages = new Dictionary<string, GameObject>();
        readonly Dictionary<string, GameObject> buttons = new Dictionary<string, GameObject>();
        Func<bool> visibleWhen;
        bool manualVisibilitySet;
        bool manualVisibility;
        string activeTab;
        public string Id { get; private set; }
        public bool IsVisible { get { return root != null && root.activeSelf; } }
        public Transform Transform { get { return root.transform; } }
        public void SetVisible(bool visible) { manualVisibilitySet = true; manualVisibility = visible; if (root != null) root.SetActive(visible); }

        internal SoftWindow(string id, string title, Transform canvas, IModLogger log)
        {
            Id = id; this.log = log;
            root = Make("Window " + id, canvas); RectTransform frame = root.GetComponent<RectTransform>(); frame.anchorMin = new Vector2(.5f, .5f); frame.anchorMax = new Vector2(.5f, .5f); frame.pivot = new Vector2(.5f, .5f); frame.anchoredPosition = Vector2.zero; frame.sizeDelta = new Vector2(1120f, 650f);
            root.AddComponent<Image>().color = new Color(.12f, .105f, .09f, .96f);
            var horizontal = root.AddComponent<HorizontalLayoutGroup>(); horizontal.padding = new RectOffset(24, 24, 22, 22); horizontal.spacing = 24; horizontal.childForceExpandHeight = true; horizontal.childForceExpandWidth = false;
            var side = Make("Sidebar", root.transform); side.AddComponent<LayoutElement>().preferredWidth = 250f; var sideLayout = side.AddComponent<VerticalLayoutGroup>(); sideLayout.spacing = 10; sideLayout.childForceExpandHeight = false; sideLayout.padding = new RectOffset(4, 4, 4, 4); AddLabel(side.transform, title.ToUpperInvariant(), 22, new Color(.87f, .78f, .62f, 1f), 46f);
            content = Make("Content", root.transform); content.AddComponent<LayoutElement>().flexibleWidth = 1f; content.AddComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 20, 12, 12);
            root.SetActive(false);
        }
        public SoftWindow BindTo(Func<bool> predicate) { visibleWhen = predicate; return this; }
        /// <summary>Reuses the native TABS canvas and visual theme for this window.</summary>
        public SoftWindow AttachToNative(Transform parent, GameObject styleSource)
        {
            if (parent != null) root.transform.SetParent(parent, false);
            Image background = root.GetComponent<Image>();
            if (background != null) background.color = new Color(1f, 1f, 1f, 0f);
            RectTransform nativeFrame = root.GetComponent<RectTransform>();
            nativeFrame.anchorMin = new Vector2(.08f, .16f); nativeFrame.anchorMax = new Vector2(.92f, .78f);
            nativeFrame.offsetMin = Vector2.zero; nativeFrame.offsetMax = Vector2.zero;
            if (styleSource != null) ApplyNativeTheme(styleSource);
            return this;
        }
        void ApplyNativeTheme(GameObject source)
        {
            Text sampleText = source.GetComponentInChildren<Text>(true);
            if (sampleText != null)
            {
                Text[] texts = root.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < texts.Length; i++) { texts[i].font = sampleText.font; texts[i].color = sampleText.color; }
            }
            Button sampleButton = source.GetComponentInChildren<Button>(true);
            if (sampleButton != null)
            {
                ColorBlock colors = sampleButton.colors;
                Button[] buttonsInWindow = root.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttonsInWindow.Length; i++) buttonsInWindow[i].colors = colors;
                Image[] images = root.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++) if (images[i].gameObject != root) images[i].color = new Color(colors.normalColor.r, colors.normalColor.g, colors.normalColor.b, .92f);
            }
        }
        public SoftTab AddTab(string id, string label)
        {
            var page = Make("Tab " + id, content.transform); page.SetActive(false); page.AddComponent<VerticalLayoutGroup>().spacing = 8; page.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false; pages[id] = page;
            var button = Make("Tab Button " + id, root.transform.Find("Sidebar")); button.AddComponent<LayoutElement>().preferredHeight = 46f; var image = button.AddComponent<Image>(); image.color = new Color(.22f, .18f, .14f, 1f); var uiButton = button.AddComponent<Button>(); uiButton.targetGraphic = image; uiButton.onClick.AddListener(() => SelectTab(id)); AddLabel(button.transform, label.ToUpperInvariant(), 15, new Color(.94f, .9f, .82f, 1f), 46f); buttons[id] = button;
            if (activeTab == null) SelectTab(id);
            return new SoftTab(page.transform, this, log);
        }
        public void SelectTab(string id) { if (!pages.ContainsKey(id)) return; foreach (var page in pages.Values) page.SetActive(false); pages[id].SetActive(true); activeTab = id; }
        internal void Tick() { bool visible = manualVisibilitySet ? manualVisibility : (visibleWhen == null || SafeVisible()); if (root.activeSelf != visible) root.SetActive(visible); }
        bool SafeVisible() { try { return visibleWhen(); } catch (Exception e) { log.Error("SoftUI visibility predicate failed", e); return false; } }
        internal void Destroy() { if (root != null) UnityEngine.Object.Destroy(root); }
        static GameObject Make(string name, Transform parent) { var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.AddComponent<RectTransform>(); return obj; }
        internal static Text AddLabel(Transform parent, string value, int size, Color color, float height) { var obj = Make("Text", parent); obj.AddComponent<LayoutElement>().preferredHeight = height; var text = obj.AddComponent<Text>(); text.text = value; text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleLeft; return text; }
    }

    public sealed class SoftTab
    {
        readonly Transform parent; readonly SoftWindow window; readonly IModLogger log;
        internal SoftTab(Transform parent, SoftWindow window, IModLogger log) { this.parent = parent; this.window = window; this.log = log; }
        public Text AddLabel(string text, int size) { return SoftWindow.AddLabel(parent, text, size, Color.white, size + 12f); }
        public Button AddButton(string label, Action clicked) { var obj = Row(label); var image = obj.AddComponent<Image>(); image.color = new Color(.28f, .23f, .18f, 1f); var button = obj.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => Safe(clicked)); SoftWindow.AddLabel(obj.transform, label.ToUpperInvariant(), 14, new Color(.94f, .9f, .82f, 1f), 38f).alignment = TextAnchor.MiddleCenter; return button; }
        public Toggle AddToggle(string id, string label, bool value, Action<bool> changed) { var obj = Row(id); obj.AddComponent<Image>().color = new Color(.18f, .15f, .12f, 1f); var toggle = obj.AddComponent<Toggle>(); toggle.isOn = value; toggle.onValueChanged.AddListener(v => Safe(() => changed(v))); var caption = SoftWindow.AddLabel(obj.transform, label.ToUpperInvariant(), 14, new Color(.94f, .9f, .82f, 1f), 38f); caption.rectTransform.offsetMin = new Vector2(30f, 0f); return toggle; }
        public Slider AddSlider(string id, string label, float value, float min, float max, Action<float> changed) { var obj = Row(id); SoftWindow.AddLabel(obj.transform, label, 13, Color.white, 30f); var sliderObj = new GameObject("Slider"); sliderObj.transform.SetParent(obj.transform, false); sliderObj.AddComponent<RectTransform>(); sliderObj.AddComponent<LayoutElement>().flexibleWidth = 1f; var slider = sliderObj.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.value = value; slider.onValueChanged.AddListener(v => Safe(() => changed(v))); return slider; }
        public Button AddDropdown(string id, string label, string[] choices, int selected, Action<int> changed) { var obj = Row(id); var image = obj.AddComponent<Image>(); image.color = new Color(.08f, .10f, .14f, 1f); var button = obj.AddComponent<Button>(); button.targetGraphic = image; var text = SoftWindow.AddLabel(obj.transform, label + ": " + choices[Mathf.Clamp(selected, 0, choices.Length - 1)], 13, Color.white, 34f); int index = Mathf.Clamp(selected, 0, choices.Length - 1); button.onClick.AddListener(() => { index = (index + 1) % choices.Length; text.text = label + ": " + choices[index]; Safe(() => changed(index)); }); return button; }
        GameObject Row(string name) { var obj = new GameObject(name); obj.transform.SetParent(parent, false); var rect = obj.AddComponent<RectTransform>(); rect.sizeDelta = new Vector2(0f, 38f); obj.AddComponent<LayoutElement>().preferredHeight = 38f; var horizontal = obj.AddComponent<HorizontalLayoutGroup>(); horizontal.spacing = 10; horizontal.childForceExpandWidth = false; return obj; }
        void Safe(Action action) { try { if (action != null) action(); } catch (Exception e) { log.Error("SoftUI control callback failed", e); } }
    }
}
