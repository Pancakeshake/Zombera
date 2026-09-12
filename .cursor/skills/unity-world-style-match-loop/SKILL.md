---
name: unity-world-style-match-loop
description: >-
  Iterative Unity world-styling workflow: build a living plannedSteps queue
  (any pipeline stages, trees/grass, water, Enviro 3, MCP actions, or new
  capabilities), run the pipeline, capture scene-visible screenshots via MCP,
  compare each iteration to reference.png, and tune toward matching the
  reference look. Use when replicating reference.png, matching terrain/world
  styling, when the user asks for a visual match loop on the hub pipeline,
  or when continuing a scheduled/Cloud style-match session from session.md.
---

# Unity World Style Match Loop

Run pipeline → capture scene → compare to `reference.png` → adjust → repeat until the world styling is close enough and nothing is broken.

Sibling skill: **unity-perf-fix-loop** (optimizes **ms**; this skill optimizes **visual match**).

## When to use

- Replicate or approach the look of `reference.png` (repo root).
- Tune terrain shape, surface paint, wilderness trees/grass, water, or Enviro 3 sky/weather.
- Need an automated loop: baseline capture → targeted change → pipeline rerun → visual compare → log check.

## Required inputs (ask if missing)

| Input | Default | Purpose |
|-------|---------|---------|
| `referencePath` | `reference.png` (repo root) | Target image to match |
| `plannedSteps` | Starts empty — **you build this list** | Living queue of work items for the session (not finite) |
| `pipelineEndStage` | `BindWeatherConsumers` (style-match cap) | Never exceed this for visual match — city/roads run only in full hub pipeline |
| `successGoal` | User-defined rubric scores or “good enough” on key dimensions | When to stop |
| `maxIterations` | `200` (code cap `DefaultSkillMaxIterations`) | Safety cap (skill mode — agent-driven steps) |
| `captureMode` | `screenshot-scene-view` | How to capture the scene each iteration |
| `captureSize` | `1920×1080` | Consistent resolution for comparison |
| `authoringScene` | `Assets/00_Scenes/02_System Dev Scenes/World Generation.unity` | **Required** — open this scene before any pipeline run |
| `breakageChecks` | Console errors + pipeline completes | What “broken” means |

## Reference look (reference.png)

High-angle aerial island/peninsula:

| Dimension | Target |
|-----------|--------|
| **Topography** | Central jagged mountain range; deep valleys; irregular coastline with sandy beaches |
| **Surfaces** | Rocky gray/white peaks; brown dirt mid-slopes; dark green forest floors; light sand at shore |
| **Vegetation** | Dense dark-green canopy in valleys; lighter grass on mid-slopes; sparse at high elevation |
| **Water** | Turquoise shallows → navy deep; white surf/foam at shore |
| **Atmosphere** | Bright daylight; wispy valley mist/clouds (Enviro 3) |

Full rubric and stage→lever mapping: [reference.md](reference.md).  
Pipeline presets and MCP rerun recipes: [world-builder-profile.md](world-builder-profile.md).

## Judgment model (authoritative)

**The agent decides quality. Automated `sim` is a guardrail only.**

| Layer | Role | Use for |
|-------|------|---------|
| **Agent visual rubric** (B,T,S,V,W,A,L) | **Primary** — open `iter-NNN.png` + `reference.png` every iter | Stop condition, next hypothesis, keep/revert |
| **AAA readability bar** | Lens for scoring S,V,W,A,L | Material breakup, biome readability, atmosphere depth — not pixel match |
| **`reference.png`** | Layout / zone intent (especially **B**) | Where biomes, coast, mountains should live — not 1:1 pixels |
| **`CompareSimilarityScore` (`sim`)** | **Secondary** — auto MAE at 128×72 | Stall detection, regression hint, tie-breaker — **never** optimize toward `sim` |

Do **not** treat higher `sim` as success when rubric is flat or worse (seen at iter 225 vs 230).  
Stop when **rubric goals** are met, not when `sim` peaks.

See [reference.md](reference.md) for scoring procedure and biome-map profile goals.

## Dynamic pipeline plan (not finite)

The step list is **open-ended**. You own a living `plannedSteps` queue — add, reorder, split, or remove items every iteration based on what the reference needs and what the capture shows.

**Do not treat any catalog, phase list, or example as a closed set.** Examples (trees, grass, Enviro 3, water) are starting points only.

### Step types (mix freely)

| Type | Examples |
|------|----------|
| **Registry stage** | Any `WorldBuildStageId` through `ValidateAndPublish` |
| **Profile / asset tweak** | `WorldNatureProfile`, `WorldEnvironmentProfile`, terrain layers, materials |
| **MCP scene action** | `terrain-paint-layer`, `terrain-set-detail-prototypes`, `terrain-place-trees`, `object-modify` |
| **Environment setup** | Ensure Enviro stack, bind zone, set time-of-day / weather preset |
| **Ad-hoc editor action** | `script-execute` menu item, single stage rerun, camera framing |
| **New capability** | Implement or wire a missing stage, placer, or profile field if the reference requires it |

### How to plan steps

1. **Study `reference.png`** — list every visual requirement you see (not only terrain).
2. **Capture baseline** — note what is missing or wrong vs reference.
3. **Draft `plannedSteps`** — ordered hypotheses; each item = one runnable unit of work.
4. **After each iteration** — re-read capture vs reference; **append** new steps, **promote** blocked items, **drop** completed or irrelevant ones.
5. **Recompute `pipelineEndStage`** — highest registry stage needed by pending `plannedSteps`, **capped at `BindWeatherConsumers`** for style-match (no city/roads).

### Style-match pipeline scope (no city)

Registry order after Planning Field:

1. `ConfigureSkyAndWeather` → `InitializeEnvironmentState` → `BindWeatherConsumers`
2. Water → Sites → Layout → `PaintNaturalSurfaces`
3. Roads → `PlaceWildernessPois` → `PlaceWildernessNature` → City…
4. **Style-match cap** is `BindWeatherConsumers` — wilderness is **after roads** and is **not** in the style-match range unless the user explicitly requests it.

Use `WorldBuilderHubPipelineRunner.StartUpToStyleMatchEndAsync()` or `StyleMatchPipelineEndStage`.

### Skill mode vs batch tuning (important)

| Mode | Who drives | Use when |
|------|------------|----------|
| **Skill mode** (default) | **Agent** — compare capture vs `reference.png`, update `Library/StyleMatch/session.md`, one targeted change per iteration | Matching reference look (max **200** iterations) |
| **Batch tuning** (deprecated) | Unity `StyleMatchLoopRunner` cycles 20 hardcoded MicroSplat/painter bumps | Perf sweeps only — **not** visual matching |

**Skill mode:** agent maintains `plannedSteps` in `Library/StyleMatch/session.md`. Each iteration: change → pipeline → capture → score → next step. Stop batch loops via **Tools → World → Style Match → Stop Batch Loop**.

**Do not** run blind 20/100 batch menus for reference matching.

### `plannedSteps` entry format

```markdown
- [ ] <id> | <type> | <what to run/change> | <rubric dims> | status: pending|done|skipped|blocked
```

Example session plan (illustrative — yours may differ):

```markdown
## Planned steps (living)
- [x] s1 | registry | Run through PaintNaturalSurfaces | T,S | done
- [ ] s2 | profile | Raise WorldNatureProfile valley tree density | V | pending
- [ ] s3 | registry | Extend pipeline to PlaceWildernessNature | V | pending
- [ ] s4 | mcp | terrain-set-detail-prototypes for slope grass | V,S | pending
- [ ] s5 | registry | Run through BindWeatherConsumers (Enviro) | A,L | pending
- [ ] s6 | ad-hoc | script-execute Enviro valley fog preset | A | pending
```

Discover all registry stages: `WorldBuildStageRegistry` / [world-builder-profile.md](world-builder-profile.md). **Any stage not listed there may still be added** if you create or enable it.

## Tools (prefer MCP)

Namespace: `project-0-Zombera-ai-game-developer`

| Phase | Tool | Notes |
|-------|------|-------|
| Preflight | `editor-application-get-state`, `ping` | Confirm Unity connected |
| Open scene | `scene-open`, `scene-list-opened` | World authoring scene with `CityPrefabRoadNetworkBuilder` |
| Run pipeline | `script-execute` | Hub runner up to `pipelineEndStage` |
| Wait / poll | `editor-application-get-state` | Not compiling; async pipeline finished |
| Capture | `screenshot-scene-view` or `screenshot-camera` | **Same mode + framing every iteration** |
| Inspect scene | `gameobject-find`, `scene-get-data`, `terrain-get` | Validate objects exist after stage |
| Logs | `console-clear-logs` → run → `console-get-logs` | Errors + `[WorldBuilderHub]` stage lines |
| Profile tweaks | `assets-get-data`, `assets-modify`, `object-modify` | Nature/environment/terrain profiles |
| Revert bad iter | `git checkout -- <files>` | When visual score regresses |

Read tool-specific skills (`screenshot-scene-view`, `console-get-logs`, `script-execute`, `terrain-paint-layer`, etc.) before calling.

## Session state

On first run, copy [session.template.md](session.template.md) to `Library/StyleMatch/session.md`.

Maintain and update every iteration **and every scheduled wake** (before exiting):

```markdown
## Session header (required — parse on every wake)
schema: style-match-session/v1
status: running
mode: style
iteration: <n>
max_iterations: <cap>
pipeline_end: BindWeatherConsumers
reference: reference.png
success_goal: <rubric goal>
best_iteration: <n>
best_overall: <score>
last_hypothesis: <one line>
last_capture: iter-NNN.png
last_sim: <auto guardrail>
next_wake_action: continue | baseline | stop
stop_reason: null | goal_met | max_iterations | mcp_down | compile_blocked | user_stopped
mcp_preflight_ok: true | false
last_wake_at: <ISO-8601 UTC>
pending_steps_count: <n>
```

Full template with score tables: [session.template.md](session.template.md).

Score each dimension **1–10** vs reference after every capture (see [reference.md](reference.md)).

## Scheduled and Cloud mode

For **mostly unattended** runs: skill + **Cloud Agent** or local **`/loop 10m`**. A skill does not run alone — each wake is one agent turn.

### Setup

1. Unity open on World Generation scene; MCP connected (`ping`).
2. Copy [session.template.md](session.template.md) → `Library/StyleMatch/session.md` if missing.
3. Start with: `/unity-world-style-match-loop match reference.png, max 20 iterations, update session.md each iter, do not ask permission between iters`
4. Schedule: `/loop 10m continue style match loop — read Library/StyleMatch/session.md, run exactly one iteration, update session.md, exit`  
   - Use **≥10 minute** interval (Unity enforces 5-minute post-success cooldown).
5. View runs at [cursor.com/agents](https://cursor.com/agents). Unity still runs where MCP points (usually your machine).

### One wake = one iteration (mandatory)

Each scheduled wake **must**:

1. **Read** `Library/StyleMatch/session.md` header — if `status` is `goal_met`, `blocked`, or `stopped`, **exit** (do not run).
2. **Preflight** (fail-fast — set `mcp_preflight_ok: false`, `status: blocked`, `stop_reason: mcp_down`, exit):
   - `ping` or `editor-application-get-state`
   - Not compiling; no stale lock (poll `agent-iter.log` — if `running N`, poll only, do not re-invoke)
   - `Library/StyleMatch/iteration-lock.json` not stuck >45 min (menu: Force Clear Iteration Lock)
3. **Execute exactly one iteration** — one hypothesis, one `TryRunSkillIteration`, poll `agent-iter.log` until `ok N` or `fail N` (MCP timeout ≠ failure).
4. **Score** — open `iter-NNN.png` + `reference.png`; update rubric table; set `last_sim` from logs (guardrail only).
5. **Write session.md** — bump `iteration`, update header fields, set `next_wake_action: stop` if goal met else `continue`, set `last_wake_at`, append wake log row.
6. **Exit** — do not idle-wait for pipeline on next tick; do not stack multiple iters in one wake.

### Stop signals (set status + stop_reason, then exit all future wakes)

| Signal | stop_reason | Action |
|--------|-------------|--------|
| Rubric goal met | `goal_met` | `status: stopped`, `next_wake_action: stop` |
| `iteration >= max_iterations` | `max_iterations` | Report best iteration |
| MCP/Unity unreachable | `mcp_down` | User must reconnect before resume |
| Compile never finishes | `compile_blocked` | Fix errors; manual resume |
| Same gap 3× no rubric gain | `blocked` | Report options in session.md |
| User stops `/loop` | `user_stopped` | — |

Wake protocol details: [reference.md — Scheduled mode](reference.md#scheduled-mode).

## Performance gate (ms)

Track stage timings alongside visual scores. Sibling skill: **unity-perf-fix-loop**.

### Baseline anchor

On **iteration 0**, save `Library/StyleMatch/perf-baseline.json` from `hub-perf-latest.json` (or parse `[WorldBuilderHub] stage=… durationMs=…` logs). This is the **speed anchor** for all shared stages.

### After every pipeline run

1. Read `Library/WorldBuilderReports/hub-perf-latest.json`.
2. For each stage that exists in **both** baseline and latest run, compute `Δ% = (latest - baseline) / baseline * 100`.
3. Flag **perf regression** if any shared stage has **Δ% ≥ 100%** (2× baseline or slower) **or** `totalMs` for the same `lastStage` path is ≥ 2× baseline `totalMs`.

### Mode switch

| Mode | When | Actions |
|------|------|---------|
| **style** (default) | No perf regression on shared stages | Visual gap → one change → rerun → capture → score |
| **perf** | Any shared stage ≥ 100% slower than baseline | **Pause visual changes.** One perf fix per iteration (see unity-perf-fix-loop). Rerun **same** `lastStage` path. Revert if slower. |
| **style** (resume) | All flagged shared stages within **~15%** of baseline | Resume visual `plannedSteps` |

New stages added when extending `pipelineEndStage` (e.g. `PlaceWildernessNature`) are **not** compared to baseline — only stages present in both reports.

### Session perf table (maintain alongside visual scores)

```markdown
| Iter | Mode | totalMs | Δ total vs baseline | Slowest regression | Action |
|------|------|---------|---------------------|--------------------|--------|
```

## Workflow

### 0. Preflight

1. Read `reference.png` — internalize rubric targets.
2. Confirm Unity MCP (`editor-application-get-state`).
3. **Open `Assets/00_Scenes/02_System Dev Scenes/World Generation.unity`** (Single mode). Do not use Road system, City Prefab Creation Hub, or other scenes unless the user explicitly overrides.
4. Ensure `CityPrefabRoadNetworkBuilder` exists in that scene (`gameobject-find`).
5. Record `successGoal`, `maxIterations`, capture settings.
6. **Draft initial `plannedSteps`** from reference analysis (start minimal — often `PaintNaturalSurfaces` baseline only).
7. **Lock camera framing** before baseline (Scene View aerial over landmass center, or dedicated `StyleMatchCamera` via `script-execute`). Reuse exact framing every iteration.

### 1. Baseline (iteration 0)

```
console-clear-logs
→ run pipeline to pipelineEndStage (see world-builder-profile.md)
→ wait for completion (sync or poll IsPipelineRunning)
→ console-get-logs (lastMinutes: 15, maxEntries: 300)
→ screenshot-scene-view (or screenshot-camera)
→ score all rubric dimensions vs reference.png
```

Store baseline scores. If pipeline fails, parse `[WorldBuilderHub]` / stage exception logs and fix before scoring.

### 2. Iteration loop (strict agent-in-the-loop)

Repeat until `goal_met`, `maxIterations`, or `blocked`. **One iteration = one hypothesis.**

| Step | Action | Tool / owner |
|------|--------|----------------|
| **1** | **Compile check** | `editor-application-get-state` → `IsCompiling == false`; `console-get-logs` errors → fix or continue |
| **2** | **Hypothesis** | Agent picks **one** change toward `reference.png` (SO profile or code) |
| **3** | **Apply change** | `assets-modify` / code edit in Cursor |
| **4** | **Pipeline + capture** | `TryRunSkillIteration(iter, PipelineRunAfterChange(note))` |
| **5** | **Auto compare (guardrail)** | `StyleMatchReferenceCompare` logs `sim` — do not use as quality score |
| **6** | **Score rubric (authoritative)** | Agent scores **B**,T,S,V,W,A,L vs reference layout + AAA bar; update `session.md` |
| **7** | **Loop** | Go to step 1 |

**Do not** run blind batch/marathon loops for visual matching.

#### Compare-reference (guardrail only)

After each capture, `StyleMatchReferenceCompare` runs automatically:

- Resizes capture + `reference.png` to 128×72, computes MAE → `sim` (0–1)
- Logs `compare-reference: {ms}ms mae={…} sim={…}` to console and `session-log.md`

**`sim` is not a quality score.** Capture is a 3D ortho render; `reference.png` is often a layout/concept map — pixel alignment is weak. Use `sim` only to detect stalls (unchanged 2×) or sudden regressions vs the previous iter. **Rubric scores decide keep/revert and the next hypothesis.**

#### Per-iteration flow

Enforced via `StyleMatchLoopRunner.TryRunSkillIteration`. Required: compile ready → clear console → open World Generation scene → pipeline → Enviro lighting → capture → compare → validate → agent updates `session.md`.

Phase table, options, and script recipes: [reference.md — Iteration mechanics](reference.md#iteration-mechanics).

**After every iter (agent, not auto):**

1. Read `iter-NNN.png` vs `reference.png` — score B,T,S,V,W,A,L  
2. `console-get-logs` — `[StyleMatch] PHASE`, `[WorldBuilderHub]`, `[WorldNaturePlacer]`  
3. Update `Library/StyleMatch/session.md` — mark step done, append new steps  
4. **Do not pause for permission** — continue to next iter until goal or cap 200

### Pipeline iteration speed

Use `StyleMatchSkillIterationOptions` with the smallest stage range for each hypothesis. Default: `UseFastIteration: true`, cap at `BindWeatherConsumers`.

```csharp
var opts = StyleMatchSkillIterationOptions.SurfacesOnlyAfterChange("lower snow line -28m");
StyleMatchLoopRunner.TryRunSkillIteration(5, opts, out var result, out var error);
```

Stage-scope table, stall detection, iteration lock, MicroSplat guard, MCP templates: [reference.md — Iteration mechanics](reference.md#iteration-mechanics).

#### A. Gap analysis + replan

Compare latest capture to `reference.png`. Pick **one primary gap** (largest score deficit).

**Think broadly** — what steps might be needed? Add them to `plannedSteps` even if not in any example list. Suggested levers in [reference.md](reference.md) are hints, not limits.

Examples of steps you might add mid-session:

- Registry stages not yet run (roads, parks, power lines, NavMesh, etc.) if visible in reference
- Extra grass layers, detail meshes, tree prototypes, rock scatter
- Enviro 3 sky, weather, fog, volumetric clouds, time-of-day
- Water shader/material, foam, shoreline decals
- URP volume, post-processing, light probes
- Scene objects (volumes, reflection probes, wind zones)
- Code or profile changes when no MCP tool exists yet

#### B. Pick next step from `plannedSteps`

Execute the **highest-priority pending** item (or one logical batch if steps are tightly coupled). When a step needs registry stages:

- Add every prerequisite stage to the plan (or run through the required end stage)
- Do **not** skip dependencies — consult `WorldBuildStageRegistry` or [world-builder-profile.md](world-builder-profile.md)
- Update `pipelineEndStage` to the highest stage required by remaining plan

After the step runs, mark it `done`, `skipped` (with reason), or `blocked`.

#### C. Apply one logical change

- **One hypothesis per iteration** — one `plannedSteps` item (or one tightly coupled batch).
- **Replenish the plan** after scoring — add new steps whenever the reference reveals more gaps.
- Prefer **serialized profile assets** over hard-coded constants.
- Follow Zombera rules: files < 500 lines, complexity < 15, correct pipeline stage owner.

#### D. Wait for Unity ready

After `.cs` or asset edits: poll `editor-application-get-state` until not compiling. **Never start `TryRunSkillIteration` while `IsCompiling` is true.**

#### E. Measured rerun

Use `TryRunSkillIteration` (required phases run automatically) or MCP equivalent:

```
editor-application-get-state (compile ready)
→ console-clear-logs
→ TryRunSkillIteration(iter, PipelineRun())
→ console-get-logs (PHASE + WorldBuilderHub + errors)
→ compare iter-NNN.png vs reference.png
→ re-score rubric dimensions
```

#### F. Breakage checks (all must pass)

1. **Console**: no new `Error` / `Exception` since clear.
2. **Pipeline**: completes without `WorldBuildStageException`; target stages not skipped (check for “Skipping” warnings).
3. **Scene**: expected objects present (terrain, ocean, trees if stage ran, Enviro manager if sky stage ran).

If breakage → **revert last change**, mark iteration failed, try different approach.

#### G. Decide next action

| Outcome | Action |
|---------|--------|
| `overall` ≥ goal + no breakage | Stop — report success |
| Improved `overall` | Keep change; continue on next gap |
| Same or worse visually | Revert; try different lever |
| Breakage | Revert; fix or pivot |
| `maxIterations` | Stop — report best iteration |
| Stuck (same gap 3×) | Add alternative step types to plan; if still blocked, report options |
| `plannedSteps` empty but goal not met | Re-analyze reference; draft new steps — do not stop early |

### 3. Final report

```markdown
## World style match loop — done

**Result:** goal_met | best_effort | blocked

**Best iteration:** <n> — overall <score>/10

| Iter | Change | Overall | Δ vs baseline | Errors |
|------|--------|---------|---------------|--------|

**Kept changes:** <files/assets> or **Reverted:** <reason>
**Screenshots:** iter-0-baseline.png, iter-<n>-best.png (if saved)
**Follow-ups:** <remaining gaps>
```

## Guardrails

- **One change per iteration** — easier attribution and safer revert.
- **Same capture framing every time** — otherwise comparisons are meaningless.
- **Clear logs before every run** — stale errors invalidate checks.
- **Fixed seed (16)** unless user requests otherwise — layout must stay comparable.
- **Do not run world pipeline in MainMenu** — gate by `GameManager.Instance.CurrentState` when in play mode.
- **Revert on visual regression** — keep best-known-good assets/code.
- **Enviro runtime quirk** — Enviro may add `BoxCollider`/`Rigidbody` at runtime; isolate via layers before editing third-party code.
- **No commit unless asked** — report diffs; let the user commit.

## Example invocation

**Interactive:**
> `/unity-world-style-match-loop` — match `reference.png`. Baseline through Paint Natural Surfaces, then add steps each iteration. Max 6 iterations.

**Scheduled (after session.md exists):**
> `/loop 10m continue style match loop from Library/StyleMatch/session.md — one iteration per wake, update session.md, stop when goal_met or blocked`

## Additional resources

- [reference.md](reference.md) — visual rubric, scoring template, iteration mechanics, scheduled mode
- [world-builder-profile.md](world-builder-profile.md) — stage catalog, profile levers, rerun recipes
- [session.template.md](session.template.md) — copy to `Library/StyleMatch/session.md` on first run
