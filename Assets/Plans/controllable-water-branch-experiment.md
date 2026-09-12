# Controllable Water Branch Experiment

**Status:** Proposed  
**Suggested branch:** `experiment/controllable-water`  
**Decision being tested:** Can Zombera ship a controllable WorldBuilder-owned water presentation without Crest, while preserving the existing hydrology, terrain, crossing, reset, and publish pipeline?

## 1. Outcome

Build a deliberately narrow replacement water backend:

- one bounded ocean surface at the authoritative sea level and map footprint;
- river ribbons and lake basin meshes built from `HydrologyPlan` at their real `SurfaceWorldY` values;
- simple material-driven motion using river flow direction, without fluid simulation;
- mesh-level crossing treatment driven by resolved `WaterCrossing` data;
- deterministic, idempotent build, scoped clear, full teardown, and rebuild behavior through WorldBuilder;
- no active Crest components or Crest runtime path in the experiment branch.

The success bar is a stable, acceptable result from the normal play camera and a clean WorldBuilder lifecycle. It is not Crest feature parity.

## 2. Non-goals

Do not spend this branch on:

- FFT ocean research, spectral waves, or an infinite-ocean system;
- underwater rendering, caustics, volumetrics, buoyancy, or swimming;
- physically simulated river flow;
- Crest-style clip, foam, depth-cache, or shoreline feature parity;
- authoring a general water framework before the experiment proves useful;
- changing hydrology solve/carve rules to compensate for presentation bugs;
- changing road-crossing planning rules unless their existing data is insufficient to present them;
- runtime world initialization in `MainMenu`.

## 3. Current Architecture to Preserve

The authoritative stage order is defined only by `WorldBuildStageRegistry.BuildDefaultDescriptors`:

```text
ErodeLandforms
  -> SolveHydrology
  -> CarveWaterFeatures
  -> BuildOceanSurfaces
  -> BuildWaterSurfaces

PlanRoadsAndHighways
  -> RefineRoadsAgainstTerrain
  -> ResolveWaterCrossings
  -> ResolveMountainTunnels
  -> StampInfrastructureTerrain
  -> BuildEasyRoadsMeshes
```

Important constraints:

- `BuildOceanSurfaces` and `BuildWaterSurfaces` are full-map stages.
- `ResolveWaterCrossings` is later and supports all scopes.
- Hard prerequisites are execution dependencies; assets do not automatically call `Runner.Invalidate`.
- Terrain-affecting stages already call `IOceanWaterRenderer.ResyncOceanToBounds`; the new ocean implementation must make this cheap and idempotent.
- `ResetGeneratedWorldStage` already distinguishes scoped clear from `TerrainAndContent` teardown. Replacement backends must honor that contract.

Keep these existing seams:

- `IOceanWaterRenderer`
- `IWorldWaterRenderer`
- `IWorldWaterProfileBinder`
- `OceanSurfaceBuildRequest`
- `WorldBuilderService.OceanWaterRenderer`
- `WorldBuilderService.WaterRenderer`
- `WorldBuilderPipelineBindArgs`
- `BuildOceanSurfacesStage`
- `BuildWaterSurfacesStage`
- `HydrologyPlan`, `RiverPolyline`, and `LakeRecord`

Do not reorder the water stages just to obtain crossings. That would spread the experiment through the DAG and make the result harder to evaluate or revert.

## 4. Key Finding Behind the Experiment

The hydrology data already contains the required height truth:

- ocean cells use the configured sea level;
- lakes expose `LakeRecord.SurfaceWorldY` and per-cell `HydrologyPlan.SurfaceWorldY`;
- rivers expose a centerline and per-cell surface heights;
- widths, depths, flow accumulation, basin cells, outlines, and river-system linkage already exist.

The active Crest inland construction path currently flattens its main generated river/lake geometry to sea level. The first implementation goal is therefore presentation correctness from existing data, not a new hydrology solver.

## 5. Proposed Branch Architecture

### 5.1 Ocean backend

Add `SimpleOceanWaterBackend : MonoBehaviour, IOceanWaterRenderer, IWorldWaterProfileBinder`.

Responsibilities:

- create one deterministic generated root;
- build a plane or a small set of simple strips from `OceanSurfaceBuildRequest.SurfaceBoundsXZ` and boundary layout;
- position all ocean geometry at `HydrologyProfile.SeaLevelWorldY` plus a small serialized render offset;
- use the allocated terrain/session footprint instead of camera-relative expansion;
- update bounds in place in `ResyncOceanToBounds`;
- treat Crest-specific depth-cache and primary-light calls as safe no-ops unless the simple material needs a light reference;
- own and release generated meshes/material instances in `ClearOcean` and `TearDownOcean`.

Start with a single plane. Only add strips or coarse LOD rings if the play-camera review shows a real coverage or precision problem.

### 5.2 Inland backend

Add `HydrologyMeshWaterRenderer : MonoBehaviour, IWorldWaterRenderer, IWorldWaterProfileBinder`.

Split geometry responsibilities into small collaborators:

- `RiverRibbonMeshBuilder`: samples each `RiverPolyline`, creates left/right banks from width, and writes vertices at sampled `HydrologyPlan.SurfaceWorldY`.
- `LakeSurfaceMeshBuilder`: triangulates each lake outline/basin and places it at `LakeRecord.SurfaceWorldY`.
- `WaterMeshChunkBuilder`: groups output into bounded chunks, sets normals/tangents/UVs, and avoids oversized meshes.
- `WaterCrossingClipUtility`: rejects or splits triangles against resolved crossing clip regions.

Presentation data:

- UV0: stable world-space water detail coordinates;
- UV1 or vertex color: normalized downstream distance / flow strength;
- tangent or an additional UV channel: planar flow direction;
- river width: `RiverPolyline.WidthMeters`;
- river height: sample the plan field through the existing hydrology sampling utility, with a serialized surface offset to prevent z-fighting;
- lake height: `LakeRecord.SurfaceWorldY`, not sea level;
- lake motion: low-speed neutral ripple unless a reliable inlet/outlet direction is available.

The generated surfaces are visual only in this branch:

- no MeshCollider;
- no NavMesh contribution;
- no terrain carving;
- no gameplay water volumes unless an existing gameplay owner explicitly consumes them.

### 5.3 Crossing update seam

Add a small optional interface rather than changing the stage order:

```csharp
public interface IWorldWaterCrossingConsumer
{
    void ApplyCrossings(
        HydrologyPlan hydrology,
        IReadOnlyList<WaterCrossing> crossings,
        WorldBuildScope scope);
}
```

After `ResolveWaterCrossingsStage` stores crossings in artifacts and `HydrologyPlan.Crossings`, it should call this interface when the bound inland renderer implements it.

Requirements:

- the call is idempotent;
- a scoped update only rebuilds affected inland chunks where practical;
- a full-map fallback is acceptable for the experiment if it is measured and fast enough;
- regular clear/teardown owns all clip state, so no second reset API is required;
- the stage stays thin: crossing-to-clip policy and mesh changes live in the renderer/collaborator.

Initial presentation policy:

- preserve continuous water beneath bridge spans;
- prevent visible water/road coplanar intersections at solid embankments or explicitly blocked crossing regions;
- keep ford water visible unless the resolved crossing data says the surface is occluded;
- derive every decision from existing `WaterCrossing` classification/data; do not infer road semantics from object names.

If the current crossing record cannot distinguish those cases, record that as a bounded follow-up decision rather than expanding this branch into a crossing-system rewrite.

### 5.4 Profile and tuning

Retain `WorldWaterProfile` and preserve all existing serialized field names while the experiment is reversible. Add a clearly separated **Controllable Water** section for:

- ocean and inland materials;
- ocean render offset;
- river/lake render offset;
- river width multiplier and minimum visible width;
- ribbon sample spacing;
- mesh chunk size;
- flow speed and normal/detail scale;
- crossing clip padding;
- shadow casting and reflection-probe settings.

Do not scatter these as constants across mesh builders. If the experiment is accepted, a later merge can separate generic presentation settings from Crest compatibility data.

## 6. What to Disconnect and When

### Disconnect first

1. Replace the hard-coded Crest provisioning in `WorldBuilderStackBootstrap` with the two experiment backend types.
2. Bind those implementations through the existing ocean/inland fields; do not bind both backends at once.
3. Remove Crest components from the experiment scene/prefab stack so play-mode results cannot silently fall back to Crest.
4. Confirm a clean compile and WorldBuilder reset before deleting any Crest integration source or package reference.

This makes the branch truthfully Crest-free at runtime immediately, while keeping the deletion step reversible until the replacement lifecycle is proven.

### Remove after the replacement passes build/clear smoke tests

- `Assets/01_Game/02_World/Integration/Crest/` implementation code;
- Crest-only assembly references and scripting defines;
- Crest package/asset dependencies from the package or project configuration;
- Crest-only generated prefabs/material assets that are no longer referenced;
- `CrestWeatherAdapter` binding;
- Crest-specific tests, replacing reusable stage assertions with backend-neutral names;
- stale comments and retired-root cleanup that claim Crest is the current owner.

Before deleting assets, resolve exact references and GUID consumers. Do not remove the package first and leave compile-time references behind.

### Explicitly keep

- hydrology solve, rasterize, filter, and carve code;
- terrain height/bathymetry results;
- WorldBuilder water stages and reset orchestration;
- ocean request/bounds utilities that are backend-neutral in behavior;
- water-crossing planning and artifact ownership;
- existing serialized public APIs unless intentionally migrated with an asset-safe path.

## 7. Implementation Phases and Commit Boundaries

### Phase 0 — Baseline and branch safety

- Create the experiment branch.
- Capture one representative full-map WorldBuilder run and play-camera screenshot with Crest.
- Record console errors/warnings, build time for both water stages, generated hierarchy roots, renderer count, triangle count, and material count.
- Identify Crest package/asmdef dependencies, scene components, prefab references, and asset GUID consumers before changing them.

**Checkpoint:** baseline is reproducible and unrelated working-tree changes are untouched.

### Phase 1 — Backend switch and lifecycle skeleton

- Add empty but functional ocean/inland backend components.
- Change bootstrap provisioning and bindings to the new implementations.
- Implement deterministic root ownership plus `Clear`, `ClearOcean`, `TearDown`, and `TearDownOcean`.
- Keep all existing stage calls valid; Crest-specific optional methods may be no-ops.

**Checkpoint:** the project compiles, no Crest runtime component is created, and two reset/rebuild cycles leave exactly one ocean root and one inland root.

### Phase 2 — Bounded ocean MVP

- Build the sea plane from request bounds and configured sea level.
- Apply the simple ocean material.
- Implement idempotent in-place resync after city pads, crossings, tunnels, and terrain finalization.
- Confirm the surface does not appear in non-ocean maps/layouts where the request indicates no ocean presentation.

**Checkpoint:** ocean coverage and coastline emergence look acceptable from the target play camera with no visible bounds gap during normal play.

### Phase 3 — Inland meshes at real heights

- Build lake meshes first; validate exact elevated lake levels.
- Build river ribbons from source to mouth with stable joins and UV flow direction.
- Chunk and name generated meshes deterministically.
- Add a minimal URP water material/shader using panning detail rather than simulated waves.

**Checkpoint:** elevated lakes are not flattened, downhill rivers do not visibly staircase or rise upstream, and no obvious terrain z-fighting appears from the play camera.

### Phase 4 — Crossings and clips

- Add `IWorldWaterCrossingConsumer`.
- Notify it from `ResolveWaterCrossingsStage` after artifact publication.
- Convert supported crossing records to clip/update regions.
- Rebuild only intersecting chunks where the existing scope/data makes that reliable.

**Checkpoint:** bridges, fords, and solid crossings follow the defined presentation policy with no stale cutouts after rerun.

### Phase 5 — Crest removal and cleanup

- Remove Crest integration code and configuration in dependency-safe order.
- Remove Crest assets/package only after reference search is clean.
- Rename backend-neutral tests and comments.
- Regenerate SigMap after the refactor/new symbols.

**Checkpoint:** clean domain reload and full WorldBuilder run with no Crest namespace, missing script, missing material, or missing GUID error.

### Phase 6 — Ship/no-ship evaluation

- Run the complete verification matrix below.
- Compare the result with the Phase 0 baseline from the actual play camera.
- Log accepted shortcomings and estimate only the fixes needed for shipping.
- Decide to merge, continue for one bounded polish pass, or discard the branch.

## 8. Expected File Map

Likely new runtime files:

- `Assets/01_Game/02_World/Integration/ControllableWater/SimpleOceanWaterBackend.cs`
- `Assets/01_Game/02_World/Integration/ControllableWater/HydrologyMeshWaterRenderer.cs`
- `Assets/01_Game/02_World/Integration/ControllableWater/RiverRibbonMeshBuilder.cs`
- `Assets/01_Game/02_World/Integration/ControllableWater/LakeSurfaceMeshBuilder.cs`
- `Assets/01_Game/02_World/Integration/ControllableWater/WaterMeshChunkBuilder.cs`
- `Assets/01_Game/02_World/Integration/ControllableWater/WaterCrossingClipUtility.cs`
- `Assets/01_Game/02_World/CityPipeline/WorldBuilder/Content/IWorldWaterCrossingConsumer.cs`

Likely modified files:

- `Assets/01_Game/02_World/CityPipeline/WorldBuilder/Core/WorldBuilderStackBootstrap.cs`
- `Assets/01_Game/02_World/CityPipeline/WorldBuilder/Profiles/WorldWaterProfile.cs`
- `Assets/01_Game/02_World/CityPipeline/WorldBuilder/Stages/ResolveWaterCrossingsStage.cs`
- relevant asmdefs, profile assets, tests, and scene-visible WorldBuilder stack references.

Avoid changing `WorldBuildStageRegistry` unless implementation uncovers a genuine dependency error. The proposed crossing consumer does not require a DAG change.

Every C# file must remain below 500 lines, and each method below cognitive complexity 15. Geometry builders should be separate collaborators rather than a single renderer partial full of mesh algorithms.

## 9. Verification Matrix

### Automated editor tests

- stage registry still reports `CarveWaterFeatures -> BuildOceanSurfaces -> BuildWaterSurfaces`;
- ocean backend uses request bounds and sea level;
- repeated ocean resync changes existing geometry instead of duplicating it;
- river vertices sample `HydrologyPlan.SurfaceWorldY` within a small numeric tolerance;
- lake vertices use `LakeRecord.SurfaceWorldY`;
- river UV/flow direction is stable across rebuilds;
- crossing notification occurs after crossings are stored in artifacts/plan;
- crossing application is idempotent;
- content clear removes generated children and owned runtime meshes/material instances;
- full teardown removes persistent hosts and a subsequent rebuild succeeds;
- two identical builds produce equivalent topology, naming, bounds, and object counts.

Extend backend-neutral coverage near the existing WorldBuilder hydrology, sampler, carver, water-stage, and reset tests. Do not make tests depend on a particular scene camera unless they are explicitly integration tests.

### Unity integration passes

Run these through the normal WorldBuilder entry point:

1. full build from a clean generated world;
2. second full build without restarting Unity;
3. content-only clear and rebuild;
4. `TerrainAndContent` teardown and rebuild;
5. water-stage rerun after upstream hydrology/terrain change;
6. crossing-stage rerun after road refinement change;
7. finalization, NavMesh queue, and publish;
8. enter play mode from the world scene and verify no world water initializes in `MainMenu`.

After each logical phase:

- check the Unity console;
- inspect generated hierarchy/object counts;
- capture a Game-view screenshot from the target play camera;
- inspect at least one ocean coast, elevated lake, river slope, bridge, ford/solid crossing, and map boundary.

### Performance checks

- no per-frame mesh rebuilds;
- no per-frame `FindObjectsByType`, LINQ, or material instantiation;
- no renderer-owned `Update` unless it only updates cached material parameters without allocation;
- record water build time, mesh count, vertex/triangle count, draw-call impact, and memory against the Phase 0 baseline;
- investigate any geometry chunk or material count that grows after repeated rebuilds.

## 10. Acceptance Criteria

The experiment passes when all of the following are true:

- ocean height and footprint always come from the active WorldBuilder session/profile;
- lakes render at their actual `SurfaceWorldY`;
- rivers follow their planned elevations and widths without conspicuous discontinuities from the normal play camera;
- simple flow direction is legible on rivers;
- supported crossings have correct, repeatable water presentation after the crossing stage runs;
- clear, teardown, rerun, and full rebuild leave no duplicate roots, stale meshes, stale clips, or leaked material instances;
- finalization and NavMesh/publish still complete;
- no Crest runtime component or missing Crest reference remains;
- the Unity console is clean of new errors;
- the result is visually acceptable during representative play, even if close-up/ocean-specialist views remain below Crest quality.

## 11. Stop Conditions

Stop and evaluate rather than expanding scope if:

- acceptable river/lake geometry requires changing the hydrology solver rather than its presentation;
- current crossing data cannot support useful clipping without redesigning crossing planning;
- the simple ocean needs camera-relative FFT/LOD research to look acceptable from the actual game camera;
- clean rebuild requires bypassing WorldBuilder lifecycle ownership;
- performance requires a streaming water architecture before the normal play camera can be served.

These are findings from the experiment, not reasons to disguise a larger rewrite as branch polish.

## 12. Merge Decision

If the branch passes, merge the backend-neutral contracts and the controllable implementation first. Add an explicit backend selector only if retaining Crest is still valuable; do not restore simultaneous auto-provisioning. Preserve the same WorldBuilder bindings so one backend owns ocean and one owns inland presentation for a session.

If the branch fails, discard it after saving the baseline, measurements, screenshots, and the specific failed acceptance criterion. Do not merge partial bootstrap changes or leave Crest and simple water active together.
