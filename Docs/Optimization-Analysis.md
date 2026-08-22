# Optimization systems observed in Closed Alpha

Tabium does not implement occlusion culling itself. Runtime diagnostics on the copied game reported for `MainScene`:

```text
cameras=1, occlusionCulling=1, LODGroups=0, staticBatchRenderers=0,
particles=0, lights=0, lightmaps=0
```

This means the menu camera already has `Camera.useOcclusionCulling == true`. The game data also contains Unity occlusion-culling assets,
but this must be checked per loaded scene. Tabium therefore does not blindly force the setting on every camera.

The Closed Alpha contains post-processing/image-effect systems such as SSAO, bloom, depth of field and stylized fog. These are the systems Tabium
currently controls through the real `Options` singleton. The runtime also exposes Unity's quality settings for shadows, anti-aliasing, pixel
lights, soft particles, VSync and frame rate.

`MainScene` has no `LODGroup` or static-batch renderer, but a runtime sample of the `Scotland` battle scene reported 2 cameras with occlusion
culling enabled, 95 static-batch renderers, 3 lightmaps and 1 light. This confirms that the battle scenes already use baked lighting and static
batching. The diagnostics are logged per scene so future optimization changes can target measured bottlenecks instead of disabling systems that
the game already uses.
