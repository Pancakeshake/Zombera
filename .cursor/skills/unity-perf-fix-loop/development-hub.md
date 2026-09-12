# Development Hub — Reset → Paint Natural Surfaces

Profile for `unity-perf-fix-loop` on the World Builder Development Hub pipeline.

## Scope

**From:** `ResetGeneratedWorld`  
**Through:** `PaintNaturalSurfaces` (Paint Natural Surfaces)  
**Stage count:** 14 (registry order)

| # | StageId | Section | Display name |
|---|---------|---------|--------------|
| 1 | `ResetGeneratedWorld` | Setup | Reset Generated World |
| 2 | `EnsureWorldBuilderStack` | Setup | Ensure World Builder Stack |
| 3 | `ValidateWorldProfile` | Setup | Validate World Profile |
| 4 | `CreateGlobalWorldPlan` | Planning | Create Global World Plan |
| 5 | `AllocateTerrainGrid` | Terrain | Allocate Terrain Grid |
| 6 | `GenerateBaseLandforms` | Terrain | Generate Base Landforms |
| 6b | `ReserveCityPads` | Planning Field | Reserve City Pads |
| 7 | `ErodeLandforms` | Terrain | Erode Landforms |
| 8 | `SolveHydrology` | Hydrology | Solve Hydrology |
| 9 | `CarveWaterFeatures` | Hydrology | Carve Water Features |
| 10 | `BuildOceanSurfaces` | Hydrology | Build Ocean Surfaces |
| 11 | `ClassifyBiomesAndBuildability` | Biomes | Classify Biomes And Buildability |
| 12 | `SelectCitySitesAndLandmarks` | Sites | Select City Sites And Landmarks |
| 13 | `ApplyCityPads` | Sites | Apply City Pads |
| 14 | `PaintNaturalSurfaces` | Surfaces | Paint Natural Surfaces |

## Default configuration

| Field | Value |
|-------|-------|
| `logAnchor` | `[WorldBuilderHub]` |
| `rerunSteps` | Hub pipeline up to Paint Natural Surfaces (see below) |
| `successGoal` | Per-step: slowest stage ↓ 15%; range: `totalMs` ↓ 10% |
| `maxIterations` | `8` per targeted stage (rotate slowest-first) |
| `breakageChecks` | Console errors + pipeline completes without `LogError` |

### Test settings (vertical slice)

The hub runner applies by default:

- `WorldMapSizeTier.Medium`
- Seed `16` (three oceans + north mountains reference layout)
- `ReuseCachedRoadsOnSameSeed = false`
- `fastRoadsMode = false` (full surface resolution)

Do not change these between iterations unless the user asks.

## Rerun recipe

**Menu (human):** `Tools → World → Run Hub Pipeline Up To Paint Natural Surfaces`

**MCP `script-execute` (body mode):**

```csharp
var builder = UnityEngine.Object.FindFirstObjectByType<Zombera.World.Roads.CityPrefabRoadNetworkBuilder>();
if (builder == null)
    throw new System.InvalidOperationException("CityPrefabRoadNetworkBuilder not found in open scene(s).");

if (!Zombera.Editor.WorldBuilderHubPipelineRunner.TryRunUpToPaintNaturalSurfaces(builder, out var error))
    throw new System.InvalidOperationException(error ?? "Hub pipeline failed.");
```

Requires an open scene with `CityPrefabRoadNetworkBuilder` (city/world authoring scene).

## Log formats to parse

### Per-stage timing (primary)

```
[WorldBuilderHub] stage=SolveHydrology name="Solve Hydrology" durationMs=45230
```

Regex:

```
\[WorldBuilderHub\] stage=(?<stageId>\w+)\s+name="[^"]+"\s+durationMs=(?<ms>\d+)
```

### Pipeline total

```
[WorldBuilderHub] pipeline=ResetGeneratedWorld..PaintNaturalSurfaces totalMs=312000 stageCount=14
```

Regex:

```
\[WorldBuilderHub\] pipeline=ResetGeneratedWorld\.\.PaintNaturalSurfaces totalMs=(?<ms>\d+)
```

### Reset pre-step (sub-timings)

```
[CityPipelineRunnerWindow] Reset pre-step: paintClear=120ms mapMagicWait=45000ms, wall=45150ms
```

Parse `paintClear`, `mapMagicWait`, `wall` when optimizing Reset.

### GUI single-step runs (optional)

If running steps individually in the hub window:

```
[CityPipelineRunnerWindow] FinishStep 'Solve Hydrology': undoCollapse=2ms, durationMs=45230ms
```

## Multi-step range workflow

Use this when optimizing **any step** in the Reset → Paint Natural range.

### Phase 1 — Baseline scan (once per session)

1. `console-clear-logs`
2. Run hub pipeline rerun recipe
3. `console-get-logs` (`lastMinutes: 15`, `maxEntries: 500`)
4. Parse **all** `[WorldBuilderHub] stage=… durationMs=…` lines into a table
5. Sort by `durationMs` descending → build **hot list**
6. Store `totalMs` from pipeline summary line

```markdown
## Hub baseline — Reset → Paint Natural Surfaces
| Rank | StageId | durationMs | % of total |
|------|---------|------------|------------|
| 1 | SolveHydrology | 45230 | 38% |
| ... | ... | ... | ... |
**totalMs:** 118400
**Hot target:** SolveHydrology
```

### Phase 2 — Per-step fix loop

For each hot stage (slowest first, or user-picked):

1. Set `targetStep = <StageId>` (e.g. `SolveHydrology`)
2. Run standard perf-fix iteration (one fix → full pipeline rerun → compare **entire table**)
3. **Accept fix only if:**
   - `targetStep` ms improved (or `totalMs` improved if that is the goal)
   - No stage **after** the fix regressed more than **5%** vs its baseline row
   - No new console errors
4. Update baseline table with best-known timings
5. Move to next hot stage or stop when `totalMs` goal met

### Phase 3 — Range regression check

After each accepted fix, compare full 14-row table vs original baseline:

- Flag any stage with **> 5% regression** (may indicate broken caching or invalidation)
- `totalMs` must not increase unless a sub-step tradeoff was explicit

## Stage → code ownership (where to fix)

| StageId | Primary code location |
|---------|----------------------|
| `ResetGeneratedWorld` | `Stages/ResetGeneratedWorldStage.cs`, `EditorPreResetRoutine` |
| `EnsureWorldBuilderStack` | `Stages/EnsureWorldBuilderStackStage.cs`, `WorldBuilderStackProvisioner` |
| `ValidateWorldProfile` | `Stages/ValidateWorldProfileStage.cs` |
| `CreateGlobalWorldPlan` | `Stages/CreateGlobalWorldPlanStage.cs` |
| `AllocateTerrainGrid` | `Stages/AllocateTerrainGridStage.cs`, `WorldTerrainGrid` |
| `GenerateBaseLandforms` | `Stages/GenerateBaseLandformsStage.cs`, `LandformHeightmapBaker` |
| `ReserveCityPads` | `Stages/ReserveCityPadsStage.cs`, `CityPadCoreUtility` |
| `ErodeLandforms` | `Stages/ErodeLandformsStage.cs`, `ThermalErosionSolver` |
| `SolveHydrology` | `Stages/SolveHydrologyStage.cs`, `Hydrology/` |
| `CarveWaterFeatures` | `Stages/CarveWaterFeaturesStage.cs` |
| `BuildOceanSurfaces` | `Stages/BuildOceanSurfacesStage.cs` |
| `ClassifyBiomesAndBuildability` | `Stages/ClassifyBiomesAndBuildabilityStage.cs`, `BiomeClassifier` |
| `SelectCitySitesAndLandmarks` | `Stages/SelectCitySitesAndLandmarksStage.cs` |
| `ApplyCityPads` | `Stages/ApplyCityPadsStage.cs` |
| `PaintNaturalSurfaces` | `Stages/PaintNaturalSurfacesStage.cs`, `WorldSurfacePainter` |

Base path: `Assets/01_Game/02_World/CityPipeline/WorldBuilder/`

## Example invocation

> Use **unity-perf-fix-loop** with the Development Hub profile: scan Reset → Paint Natural Surfaces, baseline all 14 stages, then optimize the slowest stages until totalMs drops 10%. Max 3 iterations per stage.

## Five-fix batch mode (different stages, revert on slower)

Use when applying up to **5 targeted fixes** across different stages in one session.

### What counts as an iteration

An **iteration** is always:

1. **Plan** — read code, pick up to 5 fixes (one per stage)
2. **Apply** — land those code changes in the repo
3. **Scan** — run full Reset → Paint Natural pipeline (async)
4. **Compare** — diff `hub-perf-latest.json` vs the **last accepted** report
5. **Keep or revert** — revert the whole batch (or individual fixes) if slower or broken

A **scan-only rerun** (no code changes since the last accepted report) is **not** an iteration — it is a stability/verification check only. Do not call it an iteration in reports.

### Rules

1. **Baseline first** (iteration 0) — scan only, no perf fixes yet; save `hub-perf-latest.json` as the comparison anchor.
2. **Iteration 1+** — must include **new code changes** before the scan. Never scan twice in a row without edits between.
3. **Up to 5 fixes per iteration** — one hypothesis per stage; all 5 land, then **one** pipeline scan.
4. **Accept batch** if `totalMs` improved (or target stages improved) **and** no stage regressed > 5% **and** no new console errors.
5. **Revert batch** (`git checkout -- <files>`) if `totalMs` or key stages are worse than last accepted anchor.
6. Update the accepted anchor only after a kept batch; then plan the next iteration.

### Async rerun (does not block MCP)

**Menu:** `Tools → World → Run Hub Pipeline Up To Paint Natural Surfaces (Async)`

**MCP `script-execute` (body mode):**

```csharp
if (Zombera.Editor.WorldBuilderHubPipelineRunner.IsPipelineRunning)
    throw new System.InvalidOperationException("Hub pipeline already running.");

if (!Zombera.Editor.WorldBuilderHubPipelineRunner.StartUpToPaintNaturalSurfacesAsync(out var error))
    throw new System.InvalidOperationException(error ?? "Failed to start async hub pipeline.");
```

Poll `WorldBuilderHubPipelineRunner.IsPipelineRunning` until false, then read:

`Library/WorldBuilderReports/hub-perf-latest.json`

### Batch tracking table

| Iter | Code changes (summary) | totalMs | Δ vs accepted | Verdict |
|------|------------------------|---------|---------------|---------|
| 0 | *(baseline — no fixes)* | 106606 | — | anchor |
| 1 | 5 fixes: lake bbox, biome dict, … | … | … | keep / revert |

Per-stage columns optional: `PaintNaturalSurfaces`, `CarveWaterFeatures`, etc.

## Timeout note

Default hub timeout is **900s** (15 min). If `mapMagicWait` dominates Reset, expect long wall times on first run — do not shorten timeout without user approval.
