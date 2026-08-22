using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TABSClosedAlpha
{
    [BepInPlugin("com.juninsb.tabsclosedalpha.modloader", "TABS ClosedAlpha Modloader", "1.0.0")]
    public sealed class LoaderPlugin : BaseUnityPlugin
    {
        LoaderRuntime runtime;
        void Awake()
        {
            runtime = new LoaderRuntime(Directory.GetParent(Application.dataPath).FullName, Logger);
            runtime.Start();
            var host = new GameObject("TABS ClosedAlpha Modloader");
            DontDestroyOnLoad(host);
            host.AddComponent<LoaderBehaviour>().Runtime = runtime;
            SceneManager.sceneLoaded += runtime.Events.RaiseSceneLoaded;
            SceneManager.sceneUnloaded += runtime.Events.RaiseSceneUnloaded;
        }
        void OnDestroy() { if (runtime != null) runtime.Stop(); }
    }
    public sealed class LoaderBehaviour : MonoBehaviour
    {
        public LoaderRuntime Runtime;
        void Update() { Runtime.Keys.Poll(); Runtime.Events.RaiseUpdate(); }
        void FixedUpdate() { Runtime.Events.RaiseFixedUpdate(); }
    }
    public sealed class LoaderRuntime
    {
        readonly string gameRoot; readonly BepInEx.Logging.ManualLogSource logger; readonly List<IMod> loaded = new List<IMod>(); readonly HashSet<string> loadedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal readonly ModEvents Events = new ModEvents(); internal readonly ModServices Services = new ModServices(); internal readonly ModKeys Keys = new ModKeys(); internal readonly ModCommands Commands = new ModCommands();
        internal LoaderRuntime(string gameRoot, BepInEx.Logging.ManualLogSource logger) { this.gameRoot = gameRoot; this.logger = logger; }
        internal IModLogger For(string id) { return new ModLogger(logger, id); }
        internal bool IsModLoaded(string id) { return !String.IsNullOrEmpty(id) && loadedIds.Contains(id); }
        internal void Start()
        {
            Write("[Loader] Starting"); Write("[Loader] Game detected: Unity 5.5.0x1-CollabPreview / Mono");
            string mods = Path.Combine(gameRoot, "Mods"); Directory.CreateDirectory(mods);
            var manifests = Discover(mods); foreach (var item in Resolve(manifests)) Load(item);
        }
        internal void Stop() { for (int i = loaded.Count - 1; i >= 0; --i) try { loaded[i].Shutdown(); } catch (Exception e) { Write("[Loader] Shutdown failure: " + e); } loadedIds.Clear(); }
        List<ModCandidate> Discover(string mods)
        {
            var disabledPath = Path.Combine(Path.Combine(gameRoot, "Loader"), "disabled-mods.txt"); Directory.CreateDirectory(Path.GetDirectoryName(disabledPath));
            var disabled = new HashSet<string>(File.Exists(disabledPath) ? File.ReadAllLines(disabledPath).Where(x => !String.IsNullOrEmpty(x) && !x.TrimStart().StartsWith("#")).Select(x => x.Trim()) : new string[0], StringComparer.OrdinalIgnoreCase);
            var result = new List<ModCandidate>();
            foreach (var file in Directory.GetFiles(mods, "mod.json", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) try { var metadata = ModJson.Parse(File.ReadAllText(file)); Validate(metadata); if (disabled.Contains(metadata.Id)) { Write("[Loader] Disabled " + metadata.Id); continue; } if (result.Any(x => String.Equals(x.Metadata.Id, metadata.Id, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Duplicate mod id: " + metadata.Id); result.Add(new ModCandidate { Root = Path.GetDirectoryName(file), Metadata = metadata }); } catch (Exception e) { Write("[Loader] Invalid manifest " + file + ": " + e.Message); }
            return result;
        }
        IEnumerable<ModCandidate> Resolve(List<ModCandidate> candidates)
        {
            var byId = candidates.ToDictionary(x => x.Metadata.Id, StringComparer.OrdinalIgnoreCase);
            var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates)
            {
                foreach (var broken in candidate.Metadata.Breaks)
                {
                    ModCandidate other; if (byId.TryGetValue(broken.Id, out other) && VersionMatches(other.Metadata.Version, broken.Version)) { blocked.Add(candidate.Metadata.Id); Write("[Loader] Breaks rule: skipped " + candidate.Metadata.Id + " because " + broken.Id + " is present."); }
                }
                foreach (var recommendation in candidate.Metadata.Recommends)
                {
                    ModCandidate other; if (!byId.TryGetValue(recommendation.Id, out other)) Write("[Loader] Recommended dependency missing for " + candidate.Metadata.Id + ": " + recommendation.Id);
                    else if (!VersionMatches(other.Metadata.Version, recommendation.Version)) Write("[Loader] Recommended version mismatch for " + candidate.Metadata.Id + ": " + recommendation.Id + " " + recommendation.Version);
                }
            }
            var conflictFree = candidates.Where(candidate => !blocked.Contains(candidate.Metadata.Id) && !candidates.Any(other => !System.Object.ReferenceEquals(candidate, other) && (candidate.Metadata.Conflicts.Contains(other.Metadata.Id) || other.Metadata.Conflicts.Contains(candidate.Metadata.Id)))).ToList();
            foreach (var candidate in candidates) if (!conflictFree.Contains(candidate)) Write("[Loader] Conflict detected; skipped " + candidate.Metadata.Id);
            var map = conflictFree.ToDictionary(x => x.Metadata.Id, StringComparer.OrdinalIgnoreCase); var output = new List<ModCandidate>(); var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in conflictFree.OrderBy(x => x.Metadata.Id, StringComparer.OrdinalIgnoreCase)) try { Visit(candidate, map, state, output); } catch (Exception e) { state[candidate.Metadata.Id] = 3; Write("[Loader] Dependency error for " + candidate.Metadata.Id + ": " + e.Message); } return output.Distinct().ToList();
        }
        void Visit(ModCandidate item, Dictionary<string, ModCandidate> map, Dictionary<string, int> state, List<ModCandidate> output)
        {
            int seen; if (state.TryGetValue(item.Metadata.Id, out seen)) { if (seen == 1) throw new InvalidOperationException("Dependency cycle at " + item.Metadata.Id); if (seen == 3) throw new InvalidOperationException(item.Metadata.Id + " is unavailable because its dependency resolution failed."); return; } state[item.Metadata.Id] = 1;
            foreach (var dependency in item.Metadata.Dependencies.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)) { ModCandidate target; if (!map.TryGetValue(dependency.Id, out target)) throw new InvalidOperationException(item.Metadata.Id + " requires missing mod " + dependency.Id); if (!VersionMatches(target.Metadata.Version, dependency.Version)) throw new InvalidOperationException(item.Metadata.Id + " requires " + dependency.Id + " " + dependency.Version + ", found " + target.Metadata.Version); Visit(target, map, state, output); } state[item.Metadata.Id] = 2; output.Add(item);
        }
        static bool VersionMatches(string actual, string requirement)
        {
            if (String.IsNullOrEmpty(requirement) || requirement == "*") return true;
            string value = requirement.Trim();
            string op = "=";
            if (value.StartsWith(">=") || value.StartsWith("<=")) { op = value.Substring(0, 2); value = value.Substring(2).Trim(); }
            else if (value.StartsWith(">") || value.StartsWith("<") || value.StartsWith("=")) { op = value.Substring(0, 1); value = value.Substring(1).Trim(); }
            Version left, right; try { left = new Version(actual); right = new Version(value); } catch (Exception) { return String.Equals(actual, value, StringComparison.OrdinalIgnoreCase); }
            int compare = left.CompareTo(right);
            if (op == ">=") return compare >= 0; if (op == "<=") return compare <= 0; if (op == ">") return compare > 0; if (op == "<") return compare < 0; return compare == 0;
        }
        void Load(ModCandidate item)
        {
            try { string dll = Path.Combine(item.Root, item.Metadata.Main); if (!File.Exists(dll)) throw new FileNotFoundException("Main DLL missing", dll); AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args) { string candidate = Path.Combine(item.Root, new AssemblyName(args.Name).Name + ".dll"); return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null; }; var assembly = Assembly.LoadFrom(dll); var type = assembly.GetType(item.Metadata.MainType, false) ?? assembly.GetTypes().FirstOrDefault(t => typeof(IMod).IsAssignableFrom(t) && !t.IsAbstract); if (type == null) throw new TypeLoadException("No IMod implementation found; set mainType in mod.json."); var mod = (IMod)Activator.CreateInstance(type); mod.Initialize(new ModContext(item.Metadata, item.Root, this)); loaded.Add(mod); loadedIds.Add(item.Metadata.Id); Write("[Loader] Loaded " + item.Metadata.Name + " " + item.Metadata.Version); } catch (Exception e) { Write("[Loader] Failed " + item.Metadata.Id + ": " + e); }
        }
        static void Validate(ModMetadata m) { if (m == null || String.IsNullOrEmpty(m.Id) || String.IsNullOrEmpty(m.Name) || String.IsNullOrEmpty(m.Version) || String.IsNullOrEmpty(m.Main)) throw new InvalidOperationException("id, name, version and main are required."); if (!String.IsNullOrEmpty(m.ApiVersion) && !m.ApiVersion.StartsWith(LoaderApi.Version.Split('.')[0] + ".", StringComparison.Ordinal)) throw new InvalidOperationException("Requires incompatible loader API " + m.ApiVersion + "."); if (Path.IsPathRooted(m.Main) || m.Main.IndexOf("..", StringComparison.Ordinal) >= 0) throw new InvalidOperationException("main must be a DLL path inside the mod directory."); }
        internal static void Write(string message) { Debug.Log(message); }
    }
    internal sealed class ModLogger : IModLogger { readonly BepInEx.Logging.ManualLogSource logger; readonly string id; internal ModLogger(BepInEx.Logging.ManualLogSource logger, string id) { this.logger = logger; this.id = id; } public void Info(string message) { logger.LogInfo("[" + id + "] " + message); } public void Warning(string message) { logger.LogWarning("[" + id + "] " + message); } public void Error(string message) { logger.LogError("[" + id + "] " + message); } public void Error(string message, Exception exception) { logger.LogError("[" + id + "] " + message + "\n" + exception); } }
    internal sealed class ModCandidate { internal string Root; internal ModMetadata Metadata; }
}
