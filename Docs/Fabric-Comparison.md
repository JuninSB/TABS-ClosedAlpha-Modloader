# Loader design compared with Fabric

Fabric Loader uses metadata (`fabric.mod.json`), entrypoints, dependency constraints, conflict/break rules, mod discovery and a separate game API.
It also supports nested JARs and Mixin class transformation. See the official Fabric Loader documentation for the model.

This project cannot reuse Fabric's Java classloader, mappings or Mixin implementation because Closed Alpha is a Unity Mono/.NET game with a
non-obfuscated `Assembly-CSharp.dll`. Its equivalent mechanisms are .NET assemblies, `IMod.Initialize`, Harmony patches, Unity scene events and
reflection against the confirmed TABS types.

The loader now follows the useful parts of the Fabric model:

- deterministic discovery and topological dependency loading;
- required dependencies with version operators (`>=`, `<=`, `>`, `<`, `=`);
- optional `recommends` and informational `suggests` entries;
- hard `breaks` rules and bilateral conflicts;
- per-mod failure isolation and namespaced logging;
- `ModContext.IsModLoaded(id)` for feature detection;
- a stable loader API separated from game-specific APIs.

Example metadata:

```json
{
  "id": "my-mod",
  "version": "1.2.0",
  "main": "MyMod.dll",
  "mainType": "MyMod.Main",
  "dependencies": [{ "id": "softui", "version": ">=1.0.0" }],
  "recommends": ["tabium"],
  "suggests": ["examplemod"],
  "breaks": [{ "id": "old-mod", "version": ">=2.0.0" }]
}
```
