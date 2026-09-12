# World Builder — Style Match Profile

Preset for **unity-world-style-match-loop** targeting `reference.png` (mountain island, dense valleys, coastal water, Enviro daylight).

## Default configuration

| Field | Value |
|-------|-------|
| `referencePath` | `reference.png` (repo root) |
| `seed` | `16` (`WorldBuilderHubPipelineRunner.ThreeOceansOneMountainReferenceSeed`) |
| `mapTier` | `WorldMapSizeTier.Medium` (via `ApplyVerticalSliceTestConfiguration`) |
| `authoringScene` | `Assets/00_Scenes/02_System Dev Scenes/World Generation.unity` |
| `initialEndStage` | `BindWeatherConsumers` (style-match cap — no city/roads) |
| `plannedSteps` | **Living queue — agent builds and extends** (not preset) |
| `maxIterations` | `200` |
| `UseFastIteration` | `true` (loop); `false` for final validation |
| `timeoutSeconds` | `900` |

Do not change seed/map tier between iterations unless the user asks.

**Scene:** Always use `Assets/00_Scenes/02_System Dev Scenes/World Generation.unity`.

```csharp
var path = "Assets/00_Scenes/02_System Dev Scenes/World Generation.unity";
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
if (!scene.isLoaded)
    throw new System.InvalidOperationException("Failed to open World Generation scene.");
```

## Open-ended step planning

**The phase lists below are examples, not a complete or mandatory sequence.** The agent should:

1. Analyze `reference.png` and each capture
2. Add whatever steps are needed to `plannedSteps` (registry stages, MCP tools, profiles, ad-hoc scripts, new code)
3. Grow `pipelineEndStage` to cover the highest registry stage still required
4. Keep adding steps until the rubric goal is met or the user stops the loop

When in doubt, search the codebase (`WorldBuildStageRegistry`, `CityPipelineRunnerWindow`, Enviro integration) for additional levers and add them to the plan.

## Example stage groupings (extend freely)

These groupings illustrate common visual work — **add any other stage or action** when the reference demands it.

Run registry stages **in dependency order** through the chosen end stage. Prerequisites are enforced by the pipeline.

### Phase 1 — Terrain & surfaces (baseline)

| # | StageId | Display name | Visual impact |
|---|---------|--------------|---------------|
| 1–13 | … | Reset → Apply City Pads | Layout, hydrology, pads |
| 14 | `PaintNaturalSurfaces` | Paint Natural Surfaces | Splat maps, base surface look |

**Menu:** `Tools → World → Run Hub Pipeline Up To Paint Natural Surfaces`

### Phase 2 — Wilderness vegetation

Runs **after** Roads (`SyncInfrastructureSurfaces`) so POIs/nature see stamped corridors. Outside the style-match cap (`BindWeatherConsumers`).

| End StageId | Adds |
|-------------|------|
| `PlaceWildernessPois` | Wilderness POI anchors |
| `PlaceWildernessNature` | Trees, rocks, nature scatter (`WorldNatureProfile`) |

Use when **V** (vegetation) score is low after surfaces look correct. Requires an explicit post-roads pipeline run (not style-match capped).

### Phase 3 — Water polish

| End StageId | Adds |
|-------------|------|
| `BuildOceanSurfaces` | Ocean shell (already in baseline terrain path) |

Refined shore water (`BuildWaterSurfaces`) is a **city/infrastructure** stage — not run in style-match.

Use when **W** (water) score is low and ocean surfaces are insufficient.

### Phase 4 — Enviro 3 sky & weather

| End StageId | Adds |
|-------------|------|
| `ConfigureSkyAndWeather` | Enviro config from `WorldEnvironmentProfile` |
| `InitializeEnvironmentState` | Runtime environment state (time, weather) |
| `BindWeatherConsumers` | Weather bindings |

Use when **A** (atmosphere) or **L** (lighting) scores are low.

**Style-match pipeline cap:** `BindWeatherConsumers` (`WorldBuilderHubPipelineRunner.StyleMatchPipelineEndStage`). City, roads, and street dressing stages run **after** this in the full hub pipeline only.

### Phase 5 — City & infrastructure (full pipeline only — not style-match)

| First StageId | Section |
|---------------|---------|
| `PlanRoadsAndHighways` | Roads through `GenerateParkedCars` |

Do **not** extend the style-match loop into this block unless the user explicitly asks for city content.

### Phase 6 — Finalize (full pipeline only)

| End StageId | Adds |
|-------------|------|
| `FinalizeTerrainTiles` | Terrain tile finalization after environment |

## Full stage catalog (registry — starting index, not a cap)

All `WorldBuildStageId` values exist; use any that help match the reference. Discover the full ordered list in `WorldBuildStageRegistry.cs` or grep `WorldBuildStageId`.

| StageId | Section | Display name |
|---------|---------|--------------|
| `ResetGeneratedWorld` | Setup | Reset Generated World |
| `EnsureWorldBuilderStack` | Setup | Ensure World Builder Stack |
| `ValidateWorldProfile` | Setup | Validate World Profile |
| `CreateGlobalWorldPlan` | Planning | Create Global World Plan |
| `AllocateTerrainGrid` | Terrain | Allocate Terrain Grid |
| `GenerateBaseLandforms` | Terrain | Generate Base Landforms |
| `ReserveCityPads` | Planning Field | Reserve City Pads |
| `ErodeLandforms` | Terrain | Erode Landforms |
| `SolveHydrology` | Hydrology | Solve Hydrology |
| `CarveWaterFeatures` | Hydrology | Carve Water Features |
| `BuildOceanSurfaces` | Hydrology | Build Ocean Surfaces |
| `ClassifyBiomesAndBuildability` | Biomes | Classify Biomes And Buildability |
| `SelectCitySitesAndLandmarks` | Sites | Select City Sites And Landmarks |
| `ApplyCityPads` | Sites | Apply City Pads |
| `PaintNaturalSurfaces` | Surfaces | Paint Natural Surfaces |
| `ConfigureSkyAndWeather` | Environment | Configure Sky And Weather |
| `InitializeEnvironmentState` | Environment | Initialize Environment State |
| `BindWeatherConsumers` | Environment | Bind Weather Consumers |
| `PlanRoadsAndHighways` | Roads | Plan Roads And Highways |
| … | … | (roads stamp/mesh/sync) |
| `PlaceWildernessPois` | Wilderness | Place Wilderness POIs |
| `PlaceWildernessNature` | Wilderness | Place Wilderness Nature |
| `FinalizeTerrainTiles` | Finalize | Finalize Terrain Tiles |
| `QueueNavMesh` | Finalize | Queue NavMesh |
| `ValidateAndPublish` | Finalize | Validate And Publish |

**Style-match cap:** stop at `BindWeatherConsumers`. Wilderness runs **after** Roads in registry order — nature is outside the style-match cap unless the user explicitly requests a post-roads run.

**Also valid plan items (not registry stages):** profile edits, `terrain-*` MCP tools, Enviro component tweaks, material/shader changes, new pipeline stages, menu/script actions.

## Rerun recipes

### Skill iteration — stage range + fast mode (preferred)

Use `StyleMatchLoopRunner.TryRunSkillIteration` with scoped options (fast mode on by default):

```csharp
// Surface-only tweak (~10–15s vs ~47s full reset)
var opts = Zombera.Editor.StyleMatch.StyleMatchSkillIterationOptions.SurfacesOnlyAfterChange(
    "lower snow line -28m");
Zombera.Editor.StyleMatch.StyleMatchLoopRunner.TryRunSkillIteration(5, opts, out var result, out var error);

// Biome + surfaces
var biomeOpts = Zombera.Editor.StyleMatch.StyleMatchSkillIterationOptions.BiomesAndSurfacesAfterChange(
    "macro region offsets +40%");

// Full reset, fast iteration (default PipelineRunAfterChange)
var fullOpts = Zombera.Editor.StyleMatch.StyleMatchSkillIterationOptions.PipelineRunAfterChange(
    "forceOceanOnAllEdges");

// Final validation — full fidelity, full reset
var validateOpts = Zombera.Editor.StyleMatch.StyleMatchSkillIterationOptions.FullFidelityPipelineRun();
validateOpts.HypothesisNote = "validate kept snow line change";
validateOpts.MutationConfirmed = true;
```

Low-level hub API (same path the skill runner uses):

```csharp
Zombera.Editor.WorldBuilderHubPipelineRunner.TryRunStageRangeSync(
    Zombera.World.CityPipeline.WorldBuilder.WorldBuildStageId.PaintNaturalSurfaces,
    Zombera.World.CityPipeline.WorldBuilder.WorldBuildStageId.BindWeatherConsumers,
    useFastIteration: true,
    out var error);
```

### Sync — Paint Natural Surfaces (default baseline)

**MCP `script-execute` (body mode):**

```csharp
var builder = UnityEngine.Object.FindFirstObjectByType<Zombera.World.Roads.CityPrefabRoadNetworkBuilder>();
if (builder == null)
    throw new System.InvalidOperationException("CityPrefabRoadNetworkBuilder not found.");

if (!Zombera.Editor.WorldBuilderHubPipelineRunner.TryRunUpToPaintNaturalSurfaces(builder, out var error))
    throw new System.InvalidOperationException(error ?? "Hub pipeline failed.");
```

### Sync — custom end stage

```csharp
var builder = UnityEngine.Object.FindFirstObjectByType<Zombera.World.Roads.CityPrefabRoadNetworkBuilder>();
if (builder == null)
    throw new System.InvalidOperationException("CityPrefabRoadNetworkBuilder not found.");

var window = UnityEngine.ScriptableObject.CreateInstance<Zombera.Editor.CityPipelineRunnerWindow>();
try
{
    window.SetBuilder(builder);
    window.ApplyVerticalSliceTestConfiguration();
    window.EnableFastIterationMode();
    if (!window.TryRunPipelineUpToStageSynchronously(
            Zombera.World.CityPipeline.WorldBuilder.WorldBuildStageId.PlaceWildernessNature,
            out var error,
            900f))
        throw new System.InvalidOperationException(error ?? "Pipeline failed.");
}
finally
{
    UnityEngine.Object.DestroyImmediate(window);
}
```

Replace `PlaceWildernessNature` with target `WorldBuildStageId`.

### Async — style-match end (wilderness + Enviro, no city)

**Menu:** `Tools → World → Run Hub Pipeline Up To Style Match End (Async)`

```csharp
if (Zombera.Editor.WorldBuilderHubPipelineRunner.IsPipelineRunning)
    throw new System.InvalidOperationException("Hub pipeline already running.");

if (!Zombera.Editor.WorldBuilderHubPipelineRunner.StartUpToStyleMatchEndAsync(out var error))
    throw new System.InvalidOperationException(error ?? "Failed to start async hub pipeline.");
```

### Async — Paint Natural only (surfaces baseline)

**Menu:** `Tools → World → Run Hub Pipeline Up To Paint Natural Surfaces (Async)`

```csharp
if (!Zombera.Editor.WorldBuilderHubPipelineRunner.StartUpToPaintNaturalSurfacesAsync(out var error))
    throw new System.InvalidOperationException(error ?? "Failed to start async hub pipeline.");
```

Poll `WorldBuilderHubPipelineRunner.IsPipelineRunning` until false. Perf report: `Library/WorldBuilderReports/hub-perf-latest.json` (timing only; use screenshots for visual).

## Profile assets to tune

| Asset | Path pattern | Style levers |
|-------|--------------|--------------|
| `WorldGenerationProfile` | `Assets/.../WorldGenerationProfile` | Master profile link |
| `WorldNatureProfile` | Linked from generation profile | Tree density, biomes, slope limits |
| `WorldEnvironmentProfile` | Linked from generation profile | Enviro config, weather presets, zone |
| Terrain layers | On terrain data / painter | Rock, grass, sand, forest floor |
| Terrain detail | Terrain detail prototypes | Grass mesh density |

Use `assets-find` + `assets-get-data` to locate assigned profiles on the active builder.

## Suggested first plan for reference.png (customize freely)

This is one possible starting `plannedSteps` — **replace, extend, or reorder** as captures dictate:

1. **s1:** Run through `PaintNaturalSurfaces` → baseline capture + scores
2. **s2+:** Add steps for each gap — e.g. landforms, surface paint, `PlaceWildernessNature`, water, Enviro, grass detail, post-processing, etc.

Do not stop when this list ends; keep appending steps until the goal is met.

## Example invocation

> Use **unity-world-style-match-loop** with this profile: seed 16, build a living `plannedSteps` list from reference.png, baseline through Paint Natural Surfaces, add steps (trees, grass, Enviro, water, anything needed) each iteration until overall ≥ 7.5. Max 8 iterations.
