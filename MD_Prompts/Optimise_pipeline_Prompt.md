# Optimize WorldBuilder pipeline

Act as a Unity performance engineer. **Do not invent alternate pipelines.**

1. Route via [`docs/world-builder-pipeline.md`](../docs/world-builder-pipeline.md) → STOP.  
2. DAG SoT: `WorldBuildStageRegistry.BuildDefaultDescriptors`.  
3. Measure: `WorldBuilderService.LastReport[id].DurationSeconds` for stages that actually ran; edit **owner** types (e.g. `WorldSurfacePainter`, `WorldNaturePlacer`), not stage wrappers.  
4. Incorrect-impl log: [`Build_Pipeline_Reference_Prompt.md`](Build_Pipeline_Reference_Prompt.md) — append anti-patterns; not stage SoT.  
5. Propose a plan before large code changes. Keep determinism / quality / editor + runtime init working.
