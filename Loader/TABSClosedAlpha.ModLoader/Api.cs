using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TABSClosedAlpha
{
    public static class LoaderApi { public const string Version = "1.0"; }
    public interface IMod { void Initialize(ModContext context); void Shutdown(); }

    public sealed class ModContext
    {
        readonly LoaderRuntime runtime;
        internal ModContext(ModMetadata metadata, string root, LoaderRuntime runtime) { Metadata = metadata; RootPath = root; this.runtime = runtime; }
        public ModMetadata Metadata { get; private set; }
        public string RootPath { get; private set; }
        public string AssetsPath { get { return Path.Combine(RootPath, "Assets"); } }
        public IModLogger Log { get { return runtime.For(Metadata.Id); } }
        public ModEvents Events { get { return runtime.Events; } }
        public ModServices Services { get { return runtime.Services; } }
        public ModKeys Keys { get { return runtime.Keys; } }
        public ModCommands Commands { get { return runtime.Commands; } }
        public ModAssets Assets { get { return new ModAssets(AssetsPath, Log); } }
        public ModSettings Settings { get { return new ModSettings(Path.Combine(RootPath, "config.cfg"), Log); } }
        public ModPatches Patches { get { return new ModPatches(Metadata.Id, Log); } }
        public TabsGame Game { get { return TabsGame.Instance; } }
    }

    public interface IModLogger { void Info(string message); void Warning(string message); void Error(string message); void Error(string message, Exception exception); }
    public sealed class ModMetadata { public string Id; public string Name; public string Version; public string ApiVersion; public string Author; public string Description; public string Main; public string MainType; public List<ModDependency> Dependencies = new List<ModDependency>(); public List<string> Conflicts = new List<string>(); }
    public sealed class ModDependency { public string Id; public string Version; }

    public sealed class ModEvents
    {
        public event Action<Scene, LoadSceneMode> SceneLoaded;
        public event Action<Scene> SceneUnloaded;
        public event Action Update;
        public event Action FixedUpdate;
        internal void RaiseSceneLoaded(Scene s, LoadSceneMode m) { Safe(SceneLoaded, s, m); }
        internal void RaiseSceneUnloaded(Scene s) { Safe(SceneUnloaded, s); }
        internal void RaiseUpdate() { Safe(Update); }
        internal void RaiseFixedUpdate() { Safe(FixedUpdate); }
        static void Safe(Action action) { if (action == null) return; foreach (Action item in action.GetInvocationList()) try { item(); } catch (Exception e) { LoaderRuntime.Write("[Events] " + e); } }
        static void Safe(Action<Scene> action, Scene scene) { if (action == null) return; foreach (Action<Scene> item in action.GetInvocationList()) try { item(scene); } catch (Exception e) { LoaderRuntime.Write("[Events] " + e); } }
        static void Safe(Action<Scene, LoadSceneMode> action, Scene scene, LoadSceneMode mode) { if (action == null) return; foreach (Action<Scene, LoadSceneMode> item in action.GetInvocationList()) try { item(scene, mode); } catch (Exception e) { LoaderRuntime.Write("[Events] " + e); } }
    }

    public sealed class ModServices
    {
        readonly Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public void Register<T>(string id, T service) where T : class { if (String.IsNullOrEmpty(id) || service == null) throw new ArgumentException(); values[id] = service; }
        public T Get<T>(string id) where T : class { object value; return values.TryGetValue(id, out value) ? value as T : null; }
    }
    public sealed class ModKeys
    {
        readonly Dictionary<KeyCode, Action> bindings = new Dictionary<KeyCode, Action>();
        public void Register(KeyCode key, Action action) { bindings[key] = action; }
        internal void Poll() { foreach (var pair in bindings) if (Input.GetKeyDown(pair.Key)) try { pair.Value(); } catch (Exception e) { LoaderRuntime.Write("[Keys] " + e); } }
    }
    public sealed class ModCommands
    {
        readonly Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>(StringComparer.OrdinalIgnoreCase);
        public void Register(string name, Action<string[]> action) { commands[name] = action; }
        public bool Execute(string name, params string[] args) { Action<string[]> command; if (!commands.TryGetValue(name, out command)) return false; command(args); return true; }
    }
    public sealed class ModAssets
    {
        readonly string root; readonly IModLogger log;
        internal ModAssets(string root, IModLogger log) { this.root = root; this.log = log; }
        public string PathFor(string relativePath) { return Path.Combine(root, relativePath); }
        public AssetBundle LoadBundle(string relativePath) { string path = PathFor(relativePath); if (!File.Exists(path)) { log.Warning("AssetBundle missing: " + path); return null; } return AssetBundle.LoadFromFile(path); }
        public Texture2D LoadTexture(string relativePath) { string path = PathFor(relativePath); if (!File.Exists(path)) { log.Warning("Texture missing: " + path); return null; } var texture = new Texture2D(2, 2); return texture.LoadImage(File.ReadAllBytes(path)) ? texture : null; }
    }
    public sealed class ModSettings
    {
        readonly string path; readonly IModLogger log; readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal ModSettings(string path, IModLogger log) { this.path = path; this.log = log; Load(); }
        public string Get(string key, string defaultValue) { string value; if (values.TryGetValue(key, out value)) return value; values[key] = defaultValue; Save(); return defaultValue; }
        public void Set(string key, string value) { values[key] = value; Save(); }
        public bool GetBool(string key, bool defaultValue) { bool parsed; return Boolean.TryParse(Get(key, defaultValue.ToString()), out parsed) ? parsed : defaultValue; }
        void Load() { if (!File.Exists(path)) return; foreach (var line in File.ReadAllLines(path)) { int p = line.IndexOf('='); if (p > 0) values[line.Substring(0, p).Trim()] = line.Substring(p + 1).Trim(); } }
        void Save() { try { using (var writer = new StreamWriter(path, false)) foreach (var pair in values) writer.WriteLine(pair.Key + "=" + pair.Value); } catch (Exception e) { log.Error("Could not save settings", e); } }
    }
    public sealed class ModPatches
    {
        readonly Harmony harmony; readonly IModLogger log;
        internal ModPatches(string id, IModLogger log) { harmony = new Harmony("tabsclosedalpha." + id); this.log = log; }
        public void Prefix(MethodBase original, MethodInfo prefix) { Patch(original, prefix, null); }
        public void Postfix(MethodBase original, MethodInfo postfix) { Patch(original, null, postfix); }
        public void Patch(MethodBase original, MethodInfo prefix, MethodInfo postfix) { if (original == null) throw new ArgumentNullException("original"); try { harmony.Patch(original, prefix == null ? null : new HarmonyMethod(prefix), postfix == null ? null : new HarmonyMethod(postfix)); } catch (Exception e) { log.Error("Patch failed: " + original, e); } }
        public void UnpatchAll() { harmony.UnpatchSelf(); }
    }

    public sealed class TabsGame
    {
        internal static readonly TabsGame Instance = new TabsGame();
        public UnitHandler[] Units { get { return UnityEngine.Object.FindObjectsOfType<UnitHandler>(); } }
        public UnitHandler GetUnitDefinition(string unitName) { return UnitDatabase.Instance == null ? null : UnitDatabase.Instance.GetUnit(unitName); }
        public UnityEngine.Object LoadBuiltinUnit(string unitPath) { return UnitLoaderHandler.Instance == null ? null : UnitLoaderHandler.Instance.LoadUnitByPath(unitPath); }
        public void LoadWorld(string sceneName) { if (LevelLoaderHandler.Instance == null) throw new InvalidOperationException("LevelLoaderHandler is not available in this scene."); LevelLoaderHandler.Instance.LoadWorld(sceneName); }
        public StartManager Battle { get { return StartManager.Instance; } }
        public GameMode Mode { get { return GameMode.Instance; } }
        public T Find<T>() where T : UnityEngine.Object { return UnityEngine.Object.FindObjectOfType<T>(); }
        public FieldInfo PrivateField(Type type, string name) { return type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic); }
    }
}
