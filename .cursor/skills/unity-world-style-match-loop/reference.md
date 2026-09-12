# Unity World Style Match Loop — Reference

## Judgment model

| What | Authoritative? | Notes |
|------|----------------|-------|
| Agent rubric scores (B,T,S,V,W,A,L) | **Yes** | Agent must open images each iter |
| AAA readability (materials, atmosphere, biome read) | **Yes** | How to score S,V,W,A,L — execution quality |
| `reference.png` | Layout intent | Zone map / concept — especially **B** |
| `CompareSimilarityScore` (`sim`) | **No** | Stall/regression guardrail only |

**Stop when rubric goals are met**, not when `sim` is highest.

## Visual rubric (score 1–10 each iteration)

Score against **reference layout** (where things should be) plus **AAA readability** (how good it looks in-engine).  
Do not score against pixel match to `reference.png`.

| ID | Dimension | 10 = target | AAA lens (execution) |
|----|-----------|-------------|----------------------|
| **B** | Biome layout | Distinct zones match `reference.png` (NW badlands, south desert, NE wet, center forest, etc.) | Zones readable from aerial; clear transitions, not noise |
| **T** | Topography | Mountains, valleys, coastline per reference | Natural silhouettes; beaches readable |
| **S** | Surfaces | Correct materials per zone (rock, dirt, sand, snow) | Material breakup; not mono-green or muddy |
| **V** | Vegetation | Density/placement matches zone intent | Canopy depth; treeline; no trees on cliffs |
| **W** | Water | Ocean/lake read matches reference | Turquoise shallows → deep blue; shore line |
| **A** | Atmosphere | Day sky, valley haze per reference | Depth haze; not flat/overcast |
| **L** | Lighting | Ridge shadows, valley fill | Soft contrast like polished open-world aerials |

**Overall** = rounded average of B,T,S,V,W,A,L (or user-weighted). For biome-map work, weight **B** and **S** heavily until B ≥ 6.

### Scoring procedure (each iteration)

1. Open `reference.png` and latest `iter-NNN.png` **side by side** (required — do not skip).
2. Score each dimension 1–10 with **one sentence of evidence** citing layout (vs reference) or quality (vs AAA bar).
3. Note `sim` from logs but **do not** let it override rubric (if `sim` ↑ and rubric flat → inconclusive, not success).
4. Identify **primary gap** = lowest dimension still below goal (usually **B** or **S** during biome pass).
5. Record Δ vs baseline and Δ vs previous iteration.

### “Good enough” defaults

| Profile | Goal |
|---------|------|
| Biome-map pass (current) | **B ≥ 6**, S ≥ 5, T ≥ 5, overall ≥ 5.5 |
| Terrain-only pass | T ≥ 7, S ≥ 6, overall ≥ 6.5 |
| Full nature pass | B ≥ 6, T ≥ 7, S ≥ 7, V ≥ 7, overall ≥ 7 |
| Full environment pass | All dimensions ≥ 7, overall ≥ 7.5 |

Adjust when user specifies stricter targets.

### When `sim` is useful

| Signal | Action |
|--------|--------|
| `sim` unchanged 2× and rubric unchanged | Stall — mark step `blocked`, pivot |
| `sim` drops sharply vs previous iter, rubric also worse | Likely regression — consider revert |
| `sim` ↑, rubric flat or down | **Ignore `sim`** — inconclusive iter |
| Comparing to **last kept iter** (same camera) | Optional regression check |

## Gap → code ownership (suggestions only)

**Not exhaustive.** When a gap is not covered below, search the repo and add new step types to `plannedSteps`.

| Dimension | Example pipeline stages | Profile / assets | MCP tools |
|-----------|-----------------|------------------|-----------|
| **B** | `ClassifyBiomesAndBuildability`, `PaintNaturalSurfaces` | `LandformProfile`, biome palette, macro region bias | `assets-modify` on biome/SO profiles |
| T | `GenerateBaseLandforms`, `ErodeLandforms`, `SolveHydrology`, `CarveWaterFeatures`, `BuildOceanSurfaces` | `WorldGenerationProfile`, hydrology settings | `terrain-set-heights`, `terrain-sample-heights` |
| S | `PaintNaturalSurfaces`, `ClassifyBiomesAndBuildability` | Terrain layers, `WorldSurfacePainter` config | `terrain-paint-layer`, `terrain-add-layer` |
| V | `PlaceWildernessNature`, `PlaceCityTrees` | `WorldNatureProfile` entries (density, biomes, slope) | `terrain-place-trees`, `terrain-set-detail-prototypes` |
| W | `BuildOceanSurfaces`, `BuildWaterSurfaces` | Water materials, ocean profile | `assets-get-data` on water prefabs |
| A, L | `ConfigureSkyAndWeather`, `InitializeEnvironmentState`, `BindWeatherConsumers` | `WorldEnvironmentProfile`, Enviro 3 config/zone | `gameobject-find` Enviro manager, `object-modify` |

Base path: `Assets/01_Game/02_World/CityPipeline/WorldBuilder/`

### Step discovery prompts

When planning new steps, ask:

- What does the reference show that the capture lacks?
- Is the gap **layout (B)** or **execution (S,V,W,A,L)**?
- Is there a `WorldBuildStageId` for it? If not, can MCP tools or profiles achieve it?
- If neither exists, does the project need a new stage, placer, or asset wired in?
- After this step, what **new** gaps appear? Add follow-up steps immediately.

## Wilderness nature / grass levers

**`WorldNatureProfile`** (`Profiles/WorldNatureProfile.cs`):

- `DensityPerKm2` — tree/rock density per biome
- `AllowedBiomeIds` — where each prototype scatters
- `MaxSlopeDegrees`, `ElevationRangeMeters` — keep trees off peaks (reference: sparse above treeline)
- `MaxInstancesPerTile` — cap per tile performance

**Terrain detail (grass)**:

- `terrain-set-detail-prototypes` — grass/detail mesh prototypes
- `PaintNaturalSurfaces` stage — natural surface splat maps drive where grass can appear

## Enviro 3 levers

Stages: `ConfigureSkyAndWeather` → `InitializeEnvironmentState` → `BindWeatherConsumers`

**`WorldEnvironmentProfile`**:

- `enviroConfig`, `enviroWeatherPresetLibrary`, `enviroZone` references
- Backend: `EnviroWorldEnvironmentBackend` (via `EnsureWorldBuilderStack`)

For reference look:

- Clear daytime preset
- Volumetric clouds or localized fog in valleys (not full overcast)
- Water/sky color harmony with turquoise coastal water

If stage logs `IWorldEnvironmentBackend not bound` → run `EnsureWorldBuilderStack` first; verify Enviro package present.

## MCP capture checklist

Before every capture:

- [ ] Scene View focused (for `screenshot-scene-view`) or camera ref set (for `screenshot-camera`)
- [ ] Same width/height as baseline
- [ ] Game not in broken half-loaded state (pipeline finished, no compile errors)
- [ ] Enviro had one frame to settle (`yield return null` in script if needed)

**Framing script (optional, `script-execute` body mode)** — aerial over terrain center:

```csharp
var sceneView = UnityEditor.SceneView.lastActiveSceneView;
if (sceneView == null)
    throw new System.InvalidOperationException("No Scene View open.");

var terrains = UnityEngine.Object.FindObjectsByType<UnityEngine.Terrain>(UnityEngine.FindObjectsSortMode.None);
if (terrains.Length == 0)
    throw new System.InvalidOperationException("No terrains found.");

var bounds = terrains[0].terrainData.bounds;
var center = terrains[0].transform.position + bounds.center;
for (var i = 1; i < terrains.Length; i++)
{
    var b = terrains[i].terrainData.bounds;
    var c = terrains[i].transform.position + b.center;
    bounds.Encapsulate(new UnityEngine.Bounds(c, b.size));
    center = bounds.center;
}

sceneView.pivot = center;
sceneView.rotation = UnityEngine.Quaternion.Euler(55f, 0f, 0f);
sceneView.size = bounds.extents.magnitude * 1.2f;
sceneView.Repaint();
```

Then call `screenshot-scene-view` with fixed `width`/`height`.

## Console patterns

### Stage success

```
[WorldBuilderHub] stage=PaintNaturalSurfaces name="Paint Natural Surfaces" durationMs=...
```

### Stage skipped (investigate)

```
[PlaceWildernessNatureStage] IWorldNaturePlacer not bound; skipping nature.
[ConfigureSkyAndWeatherStage] IWorldEnvironmentBackend not bound; skipping Enviro configure.
```

### Pipeline failure

```
WorldBuildStageException
[WorldBuilderHub] Async pipeline failed:
```

## Breakage checklist

- [ ] No new `Error` / `Exception` since last `console-clear-logs`
- [ ] Pipeline reached `pipelineEndStage` without exception
- [ ] No unexpected “Skipping” for stages required by current rubric gap
- [ ] Terrain tiles visible in capture (not empty gray)
- [ ] If Enviro stage ran: Enviro manager exists in scene
- [ ] If nature stage ran: tree count increased (`scene-get-data` or hierarchy spot-check)

## When to stop iterating

| Signal | Action |
|--------|--------|
| Rubric overall ≥ goal + checks pass | Done — keep changes |
| Same primary gap 3 iterations with no rubric gain | Blocked — report options to user |
| Visual improves one dimension but regresses another > 1 point | Revert unless user accepts tradeoff |
| MCP disconnected / compile errors | Blocked |
| Diminishing returns (< 0.3 overall rubric improvement twice) | Stop — report best iteration |
| `sim` improves but rubric does not | Not success — pivot hypothesis |

## Comparison iteration template

```markdown
### Iteration <n> — <change summary>

**Capture:** iter-<n>.png (agent-reviewed) | sim=<auto> (guardrail only)

| Dim | Score | Δ baseline | Notes |
|-----|-------|------------|-------|
| B | | | layout vs reference zones |
| T | | | |
| S | | | |
| V | | | |
| W | | | |
| A | | | |
| L | | | |
| **Overall** | | | |

**Primary gap next:** <dimension> — <planned lever>
**Errors:** none | <list>
**Verdict:** keep | revert | inconclusive
```

## Iteration mechanics

### Per-iteration flow (required vs optional)

**Enforced in code** via `StyleMatchLoopRunner.TryRunSkillIteration(iter, options, …)`  
Menus: **Tools → World → Style Match → Run Next Skill Iteration** (and variants).

| Phase | Required | MCP / code | Notes |
|-------|----------|------------|-------|
| Compile ready | **Yes** | `editor-application-get-state` → `IsCompiling == false` | Abort if domain reload pending |
| Clear console | **Yes** | `console-clear-logs` (or runner clears via LogEntries) | Skip only with `SkipClearConsole` |
| Open authoring scene | **Yes** | **Always** `Assets/00_Scenes/02_System Dev Scenes/World Generation.unity` (Single) | **Never** City Prefab Creation Hub or other scenes |
| Run pipeline | **Yes**\* | `TryRunUpToStyleMatchEndSync` | \*Skip with `CaptureOnly` |
| Enviro capture lighting | **Yes** | `SetClearMidday` + valley haze | Skip with `SkipEnviroLighting` |
| Capture | **Yes** | `StyleMatchCamera` → `iter-NNN.png` | 1920×1080 ortho |
| Compare reference | **Yes** | `StyleMatchReferenceCompare` (auto, timed ms) | After validate-capture |
| Validate capture | **Yes** | File exists + `MinCaptureBytes` (default 1.5 MB) | Skip with `ValidateCapture: false` |
| Update session | **Yes** | Agent updates `session.md` scores + plan | After visual compare |

**Optionals** (`StyleMatchSkillIterationOptions`):

| Option | When to use |
|--------|-------------|
| `UseFastIteration` | Default `true` for loop work; set `false` or use `FullFidelityPipelineRun()` for validation |
| `PipelineStartStage` | Partial rerun when earlier artifacts are still valid (see stage-scope table below) |
| `PipelineEndStage` | Default `BindWeatherConsumers`; lower for terrain-only passes |
| `CaptureOnly` | Re-shot after pipeline already ran; camera/lighting tweak only |
| `SkipClearConsole` | Console clear locked; **filter logs by timestamp** instead |
| `SkipEnviroLighting` | Debugging surfaces without Enviro |
| `ValidateCapture: false` | Temporary debug (not for scoring) |
| `MinCaptureBytes` | Tune if valid captures are smaller (default `1_500_000`) |
| `CheckPerfRegression` + `SavePerfBaselineIfMissing` | First run seeds baseline; later runs flag ≥2× stage ms |
| `FailOnConsoleErrors` | Strict mode — fail iter if Unity console has errors post-run |
| `HypothesisNote` | Logged per iter (`PHASE iter=N start: …`) |

**Agent script-execute recipe (full iter):**

```csharp
var opts = StyleMatchSkillIterationOptions.PipelineRun();
opts.HypothesisNote = "s5: remove buildability gate on nature placer";
StyleMatchLoopRunner.TryRunSkillIteration(5, opts, out var result, out var error);
```

**Capture-only recipe:**

```csharp
StyleMatchLoopRunner.TryRunSkillIteration(
    5, StyleMatchSkillIterationOptions.CaptureOnlyRun(), out _, out _);
```

### Pipeline iteration speed (fast mode + stage scope)

Each skill iteration uses `StyleMatchSkillIterationOptions`:

| Option | Default | Purpose |
|--------|---------|---------|
| `UseFastIteration` | `true` | Coarser surface paint stride + reduced landform/biome noise (~3× faster paint) |
| `PipelineStartStage` | `null` (= Reset) | Partial rerun — skip earlier stages when artifacts still valid |
| `PipelineEndStage` | `BindWeatherConsumers` | Style-match cap (no city/roads) |

**Pick the smallest stage range** for the hypothesis:

| Change type | Start stage | Factory helper |
|-------------|-------------|----------------|
| Landforms / hydrology / ocean layout | `ResetGeneratedWorld` (default) | `PipelineRunAfterChange(note)` |
| Biome classification / macro regions | `ClassifyBiomesAndBuildability` | `BiomesAndSurfacesAfterChange(note)` |
| Surface painter / MicroSplat / palette only | `PaintNaturalSurfaces` | `PaintOnlyAfterChange(note)` (~5.5s) or `SurfacesOnlyAfterChange(note)` (~6s) |
| Wilderness trees / Enviro sky only | `ConfigureSkyAndWeather` | `EnvironmentOnlyAfterChange(note)` |
| Lighting / camera framing only | — | `CaptureOnlyRun()` |
| Final validation before keeping a change | `ResetGeneratedWorld` | `FullFidelityPipelineRun()` with `MutationConfirmed` + note |

**Fast vs full fidelity:** loop with `UseFastIteration: true` (default). Run **one** `FullFidelityPipelineRun()` (or `UseFastIteration = false`) before marking a change as kept.

Re-baseline perf (`perf-baseline.json`) after changing fast/full mode or `PipelineStartStage` — comparisons are only valid for the same stage path + mode.

### Stall detection (stop wasting iterations)

Before starting the next iter, check `session-log.md` / `session.md`:

| Signal | Action |
|--------|--------|
| Same `HypothesisNote` as previous iter | **Do not rerun** — apply the change first or mark step `blocked` |
| `sim` unchanged for **2** consecutive iters (±0.001) with same rubric scores | Mark step `blocked`; pivot hypothesis or stage scope |
| `sim` improved but rubric flat | Trust rubric over pixel proxy — do not loop on `sim` alone |
| SO-only change on existing world | Use `CaptureOnlyRun()` or narrow start stage — never full reset |
| 3× same primary gap (T/S/V/W/A/L) | Add alternative step types; report options to user |

**Never** queue duplicate pipeline runs to "wait for compile" — poll `IsCompiling` then run once.

### Iteration lock (MCP — mandatory)

`TryRunSkillIteration` acquires a file lock (`Library/StyleMatch/iteration-lock.json`) and writes **`Library/StyleMatch/agent-iter.log`**:

| Log line | Meaning |
|----------|---------|
| `running N: …` | Pipeline in progress — **poll only**, do not re-invoke `script-execute` |
| `ok N compare=…ms sim=…` | Done — score and continue |
| `fail N: …` | Failed — read error, fix, then start **one** new run |
| `blocked N: iter M running` | Duplicate MCP call rejected — keep polling |

Poll `StyleMatchLoopRunner.TryGetAgentIterationLog` or read `agent-iter.log` every 30–60s. MCP timeout is **not** failure.

Clear stuck lock: **Tools → World → Style Match → Force Clear Iteration Lock**.

After `ok N`, a **5-minute cooldown** blocks new runs (stops MCP retry duplicates). Use `BypassCompletionCooldown` only for manual retry.

### MicroSplat compile guard

`Remap MicroSplat World Surface Slots` and `CompileWorldConfig` run **once per editor session** unless forced. Prevents texture-array reimport storms during loops.

Reset when slots actually change: **Tools → World → Style Match → Reset MicroSplat Compile Session**.

**Never** call remap/compile inside iteration batch scripts unless palette slot mappings changed.

### Extending pipeline end stage (MCP template)

Generic sync run (replace `LAST_STAGE`):

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
            Zombera.World.CityPipeline.WorldBuilder.WorldBuildStageId.LAST_STAGE,
            out var error,
            900f))
        throw new System.InvalidOperationException(error ?? "Pipeline failed.");
}
finally
{
    UnityEngine.Object.DestroyImmediate(window);
}
```

Replace `LAST_STAGE` with e.g. `PlaceWildernessNature`, `ConfigureSkyAndWeather`, `BuildWaterSurfaces`.

Async (long runs): see [world-builder-profile.md](world-builder-profile.md).

## Scheduled mode

For Cloud Agent or `/loop` wakes. See [SKILL.md — Scheduled and Cloud mode](../SKILL.md#scheduled-and-cloud-mode).

### Wake preflight checklist

- [ ] Read `Library/StyleMatch/session.md` — exit if `status` is terminal
- [ ] MCP connected (`ping` / `editor-application-get-state`)
- [ ] `IsCompiling == false`
- [ ] Read `Library/StyleMatch/agent-iter.log` — if `running N`, poll only (30–60s), never duplicate `script-execute`
- [ ] If lock stuck >45 min: **Tools → World → Style Match → Force Clear Iteration Lock**
- [ ] Schedule interval ≥10 minutes (5-minute post-`ok N` cooldown in Unity)

### One wake protocol

1. Parse session header (`schema: style-match-session/v1`)
2. Pick one pending `plannedSteps` item → one hypothesis
3. Apply change → `TryRunSkillIteration` → poll until done
4. Score B,T,S,V,W,A,L vs `reference.png` (authoritative)
5. Update session header + tables; set `next_wake_action`
6. Exit turn (do not run multiple iters per wake)

### Viewing Cloud Agent runs

Open [cursor.com/agents](https://cursor.com/agents) to see conversation, artifacts, and status. Captures also land in `Library/StyleMatch/iter-NNN.png` on the Unity machine.
