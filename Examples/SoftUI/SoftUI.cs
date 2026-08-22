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
        }
        public SoftWindow CreateWindow(string id, string title)
        {
            var window = new SoftWindow(id, title, hostObject.transform, log);
            windows.Add(window);
            return window;
        }
        internal void Tick() { for (int i = 0; i < windows.Count; i++) windows[i].Tick(); }
        internal void Destroy() { for (int i = 0; i < windows.Count; i++) windows[i].Destroy(); windows.Clear(); if (hostObject != null) UnityEngine.Object.Destroy(hostObject); }
    }

    public sealed class SoftUiHost : MonoBehaviour
    {
        public SoftUiService Service;
        void Update() { if (Service != null) Service.Tick(); }
    }

    public sealed class SoftWindow
    {
        readonly IModLogger log;
        readonly GameObject root;
        readonly GameObject content;
        readonly Dictionary<string, GameObject> pages = new Dictionary<string, GameObject>();
        readonly Dictionary<string, GameObject> buttons = new Dictionary<string, GameObject>();
        Func<bool> visibleWhen;
        string activeTab;
        public string Id { get; private set; }
        public bool IsVisible { get { return root != null && root.activeSelf; } }
        public Transform Transform { get { return root.transform; } }
        public void SetVisible(bool visible) { if (root != null) root.SetActive(visible); }

        internal SoftWindow(string id, string title, Transform canvas, IModLogger log)
        {
            Id = id; this.log = log;
            root = Make("Window " + id, canvas); RectTransform frame = root.GetComponent<RectTransform>(); frame.anchorMin = new Vector2(.5f, .5f); frame.anchorMax = new Vector2(.5f, .5f); frame.pivot = new Vector2(.5f, .5f); frame.anchoredPosition = Vector2.zero; frame.sizeDelta = new Vector2(1040f, 570f);
            root.AddComponent<Image>().color = new Color(.025f, .03f, .045f, .97f);
            var horizontal = root.AddComponent<HorizontalLayoutGroup>(); horizontal.padding = new RectOffset(18, 18, 16, 16); horizontal.spacing = 18; horizontal.childForceExpandHeight = true; horizontal.childForceExpandWidth = false;
            var side = Make("Sidebar", root.transform); side.AddComponent<LayoutElement>().preferredWidth = 230f; var sideLayout = side.AddComponent<VerticalLayoutGroup>(); sideLayout.spacing = 8; sideLayout.childForceExpandHeight = false; sideLayout.padding = new RectOffset(4, 4, 4, 4); AddLabel(side.transform, title, 22, Color.cyan, 42f);
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
                for (int i = 0; i < images.Length; i++) if (images[i].gameObject != root) images[i].color = colors.normalColor;
            }
        }
        public SoftTab AddTab(string id, string label)
        {
            var page = Make("Tab " + id, content.transform); page.SetActive(false); page.AddComponent<VerticalLayoutGroup>().spacing = 8; page.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false; pages[id] = page;
            var button = Make("Tab Button " + id, root.transform.Find("Sidebar")); button.AddComponent<LayoutElement>().preferredHeight = 42f; var image = button.AddComponent<Image>(); image.color = new Color(.10f, .13f, .18f, 1f); var uiButton = button.AddComponent<Button>(); uiButton.targetGraphic = image; uiButton.onClick.AddListener(() => SelectTab(id)); AddLabel(button.transform, label, 15, Color.white, 42f); buttons[id] = button;
            if (activeTab == null) SelectTab(id);
            return new SoftTab(page.transform, this, log);
        }
        public void SelectTab(string id) { if (!pages.ContainsKey(id)) return; foreach (var page in pages.Values) page.SetActive(false); pages[id].SetActive(true); activeTab = id; }
        internal void Tick() { bool visible = visibleWhen == null || SafeVisible(); if (root.activeSelf != visible) root.SetActive(visible); }
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
        public Button AddButton(string label, Action clicked) { var obj = Row(label); var image = obj.AddComponent<Image>(); image.color = new Color(.10f, .32f, .56f, 1f); var button = obj.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => Safe(clicked)); SoftWindow.AddLabel(obj.transform, label, 14, Color.white, 34f).alignment = TextAnchor.MiddleCenter; return button; }
        public Toggle AddToggle(string id, string label, bool value, Action<bool> changed) { var obj = Row(id); obj.AddComponent<Image>().color = new Color(.08f, .10f, .14f, 1f); var toggle = obj.AddComponent<Toggle>(); toggle.isOn = value; toggle.onValueChanged.AddListener(v => Safe(() => changed(v))); var caption = SoftWindow.AddLabel(obj.transform, label, 14, Color.white, 34f); caption.rectTransform.offsetMin = new Vector2(30f, 0f); return toggle; }
        public Slider AddSlider(string id, string label, float value, float min, float max, Action<float> changed) { var obj = Row(id); SoftWindow.AddLabel(obj.transform, label, 13, Color.white, 30f); var sliderObj = new GameObject("Slider"); sliderObj.transform.SetParent(obj.transform, false); sliderObj.AddComponent<RectTransform>(); sliderObj.AddComponent<LayoutElement>().flexibleWidth = 1f; var slider = sliderObj.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.value = value; slider.onValueChanged.AddListener(v => Safe(() => changed(v))); return slider; }
        public Button AddDropdown(string id, string label, string[] choices, int selected, Action<int> changed) { var obj = Row(id); var image = obj.AddComponent<Image>(); image.color = new Color(.08f, .10f, .14f, 1f); var button = obj.AddComponent<Button>(); button.targetGraphic = image; var text = SoftWindow.AddLabel(obj.transform, label + ": " + choices[Mathf.Clamp(selected, 0, choices.Length - 1)], 13, Color.white, 34f); int index = Mathf.Clamp(selected, 0, choices.Length - 1); button.onClick.AddListener(() => { index = (index + 1) % choices.Length; text.text = label + ": " + choices[index]; Safe(() => changed(index)); }); return button; }
        GameObject Row(string name) { var obj = new GameObject(name); obj.transform.SetParent(parent, false); var rect = obj.AddComponent<RectTransform>(); rect.sizeDelta = new Vector2(0f, 38f); obj.AddComponent<LayoutElement>().preferredHeight = 38f; var horizontal = obj.AddComponent<HorizontalLayoutGroup>(); horizontal.spacing = 10; horizontal.childForceExpandWidth = false; return obj; }
        void Safe(Action action) { try { if (action != null) action(); } catch (Exception e) { log.Error("SoftUI control callback failed", e); } }
    }
}
