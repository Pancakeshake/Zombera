---
name: unity-river-hydrology-loop
description: >-
  Iterative Unity hydrology + Crest water loop: tune HydrologyProfile and
  WorldWaterProfile, rerun water stages (no paint), capture aerial + river
  bank close-ups, score soft-bank blending vs Assets/Plans/river-refs targets,
  and repeat until rivers look right. Use for realistic rivers, hard water
  cut-in fixes, Crest foam/shore tuning, or scheduled /loop river sessions.
---

# Unity River Hydrology Loop

Tune hydrology + Crest → rebuild water (skip paint) → capture Ocean/Lake/River → score soft blend → repeat until rivers are good.

Sibling: **unity-world-style-match-loop** (full world look). This skill is **water-only** and stops before surface paint / wilderness / city.

**Pipeline owner:** `WorldBuilderHubPipelineRunner` hydrology APIs — **not** `StyleMatchLoopRunner.TryRunSkillIteration` (style-match stage cap blocks `BuildWaterSurfaces`).

## When to use

- Perfect rivers / lakes / ocean join on WorldBuilder + Crest
- Fix **hard knife-edge cut** where water meets terrain
- Pre-paint hydrology iteration (faster captures on checkerboard / carved beds)
- Scheduled unattended river tuning via `/loop`

## Required inputs (ask if missing)

| Input | Default | Purpose |
|-------|---------|---------|
| `refsDir` | `Assets/Plans/river-refs/` | Before/after references |
| `beforeAerial` | `unity-prepaint-aerial.png` | Current-layout aerial |
| `beforeCloseup` | `unity-prepaint-river-closeup.png` | Bank hard-cut evidence |
| `targetAerial` | `river-target-prepaint-aerial.png` | Soft-blend aerial goal |
| `targetCloseup` | `river-target-prepaint-closeup-softblend.png` | Soft bank goal |
| `targetCorridor` | `river-target-prepaint-corridor.png` | Valley corridor goal |
| `sessionPath` | `Library/StyleMatch/river-session.md` | Living session state |
| `pipelineEndStage` | `BuildWaterSurfaces` | Never run paint for this loop |
| `mapTier` | **`Large` (required)** | Never Medium/Small |
| `seed` | **required** (campaign default `1`) | World seed — never silently fall back to 16 |
| `fixedRegionSeed` | `1589847769` (Hub screenshot campaign) | `CityPrefabRoadNetworkBuilder.FixedRegionSeedOverride` |
| `maxIterations` | `60` | Safety cap |
| `successGoal` | See rubric below | Stop condition |
| `authoringScene` | `Assets/00_Scenes/02_System Dev Scenes/1_World_Generation/1_World Generation.unity` | Required scene |

## Judgment model (authoritative)

**Agent visual rubric wins.** Pixel `sim` against style-match `reference.png` is irrelevant here.

| Layer | Role |
|-------|------|
| **R1–R6 rubric** vs target images | Primary — open before + after + targets every iter |
| **Hard-cut check** on close-up | Must improve; knife-edge cyan = fail |
| Console / pipeline errors | Breakage → revert |

### Success goal (default)

Stop when **all** are true for 2 consecutive kept iterations:

- **R1 Continuity** ≥ 7
- **R2 Width hierarchy** ≥ 6
- **R3 Bed carve** ≥ 7
- **R4 Soft bank blend** ≥ 8 ← hardest; primary gate
- **R5 Surface / Crest read** ≥ 6
- **R6 Ocean / mouth join** ≥ 6

If R4 is below 8, keep iterating even if others look fine.

## Phase order (do not skip ahead)

| Phase | Focus | Stages | Primary assets |
|-------|--------|--------|----------------|
| **A Network** | Count, outlets, continuity | Baseline: `Reset`→`BuildWaterSurfaces`; later: `SolveHydrology`→`BuildWaterSurfaces` | `HydrologyProfile` |
| **B Carve / soft banks** | Shoulder, depth, shore blend width | `SolveHydrology` → `BuildWaterSurfaces` | `HydrologyProfile` carve fields |
| **C Crest presentation** | Foam, shore width, materials, waves | `BuildOceanSurfaces` → `BuildWaterSurfaces` | `WorldWaterProfile` |
| **D Polish** | Mouth flare, meanders, lake caps | full water path | both profiles |

One hypothesis per iteration. Prefer profile tweaks over code until profiles cannot express the fix.

## Assets (SoT levers)

- `Assets/02_Shared/ScriptableObjects/World/Profiles/HydrologyProfile.asset`
- `Assets/02_Shared/ScriptableObjects/World/Profiles/WorldWaterProfile.asset`

Full field → gap map: [reference.md](reference.md).

## Capture contract (every iteration)

Use `RiverHydrologyCaptureRunner.Capture(iter, focusOverrides)`:

1. **Aerial** — landmass overview
2. **River close-up** — water–terrain junction (hard-cut / R4)
3. **Lake close-up** — inland lake join
4. **Ocean shore** — CrestOcean coast join (R6)

Store under `Library/StyleMatch/river-captures/iter-NNN-{aerial,river-closeup,lake-closeup,ocean-shore}.png` + `iter-NNN-meta.txt`.

Fail the iteration if meta shows `riverFocus=MISSING` / `ocean=MISSING`, or any PNG &lt; 200KB.

Persist focus positions from iter 0 into `river-session.md` and reuse via `FocusOverrides`.

## Tools (prefer MCP)

Namespace: `project-0-Zombera-ai-game-developer`

| Phase | Tool |
|-------|------|
| Preflight | `editor-application-get-state`, `ping` |
| **Log hygiene** | **`console-clear-logs` before every pipeline run and before every `console-get-logs`** |
| Open scene | `scene-open` → World Generation |
| Profile tweak | `assets-get-data` / `assets-modify` on Hydrology / WorldWater profiles |
| Pipeline | `script-execute` → `WorldBuilderHubPipelineRunner.TryRunHydrologyWaterVerifySync` / `TryRunHydrologyStageRangeSync` |
| Capture | `script-execute` → `RiverHydrologyCaptureRunner.Capture` |
| Logs | `console-clear-logs` → `console-get-logs` (small `maxEntries` / `lastMinutes`) |
| Revert | `git checkout --` profile assets on regression |

### MCP console contract (hard)

1. Call `console-clear-logs` **before** starting each pipeline rebuild.
2. After the run, pull logs with a **small** window only (never against a multi-hour uncleared console — MCP rejects ~268MB payloads).
3. Clear again before the next iteration’s rebuild.

## Pipeline APIs (water-only)

```csharp
// Iter 0 baseline (Large, campaign seeds)
WorldBuilderHubPipelineRunner.TryRunHydrologyWaterVerifySync(
    WorldBuilderHubPipelineRunner.HydrologyLoopCampaignWorldSeed,      // 1
    WorldBuilderHubPipelineRunner.HydrologyLoopCampaignFixedRegionSeed, // 1589847769
    out var error,
    1800f);

// Phase B carve / network (after baseline artifacts exist)
WorldBuilderHubPipelineRunner.TryRunHydrologyStageRangeSync(
    WorldBuildStageId.SolveHydrology,
    WorldBuildStageId.BuildWaterSurfaces,
    worldSeed: 1,
    fixedRegionSeedOverride: 1589847769,
    out error,
    1800f);

// Phase C Crest-only
WorldBuilderHubPipelineRunner.TryRunHydrologyStageRangeSync(
    WorldBuildStageId.BuildOceanSurfaces,
    WorldBuildStageId.BuildWaterSurfaces,
    worldSeed: 1,
    fixedRegionSeedOverride: 1589847769,
    out error,
    1800f);

// Capture
var cap = Zombera.Editor.StyleMatch.RiverHydrologyCaptureRunner.Capture(iter, focusOverrides);
```

Do **not** use `StyleMatchLoopRunner.TryRunSkillIteration` for this loop.  
Do **not** set end stage past `BuildWaterSurfaces`. No `PaintNaturalSurfaces`.

## Session state

On first run, copy [session.template.md](session.template.md) → `Library/StyleMatch/river-session.md`.

Update every iteration / wake. Header schema: `river-hydro-session/v1`.

## Workflow

### 0. Preflight

1. Unity MCP connected; not compiling.
2. **`console-clear-logs`**.
3. Open World Generation scene (Single).
4. Confirm refs exist under `Assets/Plans/river-refs/`.
5. Copy session template if missing; set `status: running`, `seed`, `fixed_region_seed`.
6. Draft initial `plannedSteps` starting Phase A → B (carve/soft bank before Crest polish).

### 1. Baseline (iteration 0)

```
console-clear-logs
→ TryRunHydrologyWaterVerifySync(seed:1, fixedRegion:1589847769)
→ console-get-logs (small window) — assert mapTier=Large seed=1
→ RiverHydrologyCaptureRunner.Capture(0)
→ score R1–R6 vs targets (especially river-closeup vs softblend target)
→ persist focus positions + write session.md
→ refresh Assets/Plans/river-refs/unity-prepaint-*.png from iter-000 captures
```

### 2. Iteration loop

One change per iter:

1. Compile check
2. Pick highest-priority pending step (phase order)
3. Mutate **one** profile field (or tightly coupled pair)
4. **`console-clear-logs`**
5. Run scoped `TryRunHydrologyStageRangeSync`
6. `console-get-logs` (small) — revert on new errors
7. `RiverHydrologyCaptureRunner.Capture(iter, sessionFocusOverrides)`
8. Score R1–R6; **R4 from river close-up is mandatory**
9. Keep if improved; else revert profile
10. Append/reorder `plannedSteps`
11. Stop on goal / max / blocked (same gap 3×)

### 3. Scheduled mode (`/loop`)

```
/loop 10m continue river hydrology loop from Library/StyleMatch/river-session.md —
one iteration only, update session.md, stop when goal_met or blocked
```

Each wake: read session → preflight → **exactly one** iteration → write session → exit.  
If `status` is `goal_met` / `blocked` / `stopped`, exit without work.

Unity post-success cooldown is 5 minutes — use **≥10m** interval.

## Guardrails

- **Large map only** — every iteration must call hydrology hub APIs with `ApplyHydrologyLoopConfiguration`. If logs show Medium/Small, **abort**.
- **Seed is explicit** — campaign uses seed `1` + fixed region `1589847769`. Assert log `seed=1`. Do not silently use 16.
- **Pre-paint only** — no surface paint, trees, Enviro polish in this loop.
- **Soft banks first** — do not chase Crest beauty while R4 &lt; 7.
- **One hypothesis** — easier revert.
- **Same inland focus** — reuse session focus overrides after iter 0.
- **MCP clear logs** — every rebuild / every log pull.
- **No commit** unless user asks.
- Prefer changing `HydrologyProfile` / `WorldWaterProfile` owners; don’t invent a fourth water stack.
- Reuse `RiverPolyline` + carve + Crest builders — do not greenfield a new water system mid-loop.

## Example invocation

**Interactive:**
> `/unity-river-hydrology-loop` — seed 1, fixed region 1589847769, Large, session `Library/StyleMatch/river-session.md`, max 40 iters, do not ask permission between iters. Focus on soft bank blend (R4).

**Scheduled:**
> `/loop 10m continue river hydrology loop from Library/StyleMatch/river-session.md — one iteration per wake, update session, stop on goal_met or blocked`

## Additional resources

- [reference.md](reference.md) — rubric detail, lever map, capture notes
- [session.template.md](session.template.md) — copy to `Library/StyleMatch/river-session.md`
