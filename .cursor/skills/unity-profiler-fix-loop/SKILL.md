---
name: unity-profiler-fix-loop
description: >-
  Iterative Unity runtime performance workflow: enter Play Mode, sample the
  Unity Profiler MCP tools (frame/FPS, rendering, script/GC, memory), capture a
  Scene View screenshot as a visual guardrail, apply one targeted fix, retest
  with the same protocol, and compare until the goal is met. Use when optimizing
  Play Mode FPS/frame time, GC spikes, memory growth, or when the user asks for
  a profiler measure-fix-retest loop (sibling of unity-perf-fix-loop).
---

# Unity Profiler Fix Loop

Measure (Profiler + Scene View) → pick bottleneck → one fix → retest → compare → repeat.

Sibling skills:
- **unity-perf-fix-loop** — Editor/pipeline **console log ms** (build steps).
- **unity-world-style-match-loop** — visual match to `reference.png`.

This skill optimizes **runtime** stats from the Profiler MCP tools, not pipeline log timings.

## When to use

- Play Mode feels slow, hitchy, or memory-heavy.
- Need an automated loop: baseline snapshot → fix → same sampling → compare.
- User asks to “read the profiler”, “fix FPS”, or “profiler measure-fix-retest”.

## Required inputs (ask if missing)

| Input | Default | Purpose |
|-------|---------|---------|
| `authoringScene` | Ask — or current open scene | Scene under test |
| `warmupSeconds` | `3` | Settle after entering Play Mode |
| `sampleCount` | `5` | Snapshots to average (spaced ~0.5–1s) |
| `successGoal` | Ask (e.g. `avg FrameTimeMs ↓ 20%` or `avg Fps ≥ 60`) | Stop condition |
| `maxIterations` | `5` | Safety cap |
| `primaryMetric` | `auto` | `auto` \| `frame` \| `memory` \| `gc` — auto = worst category from baseline |
| `captureMode` | `screenshot-scene-view` | Visual breakage guardrail |
| `captureSize` | `1920×1080` | Consistent screenshot size |
| `rerunSteps` | Default Play Mode recipe below | Optional extra actions while sampling |
| `breakageChecks` | Console errors + Scene View OK | What “broken” means |

## Tools (prefer MCP over CLI)

Namespace: `project-0-Zombera-ai-game-developer`

| Phase | Tool | Notes |
|-------|------|-------|
| Ready | `editor-application-get-state` | Wait until not compiling; confirm play state |
| Scene | `scene-open` / `scene-list-opened` | Open `authoringScene` if needed |
| Play | `editor-application-set-state` | `isPlaying: true` / `false` |
| Profiler on | `profiler-start` | Enables profiler + opens window |
| Clear | `profiler-clear-data` | Before each measured window |
| Sample | `profiler-capture-frame` | FrameTimeMs, Fps |
| Sample | `profiler-get-rendering-stats` | FPS, vsync, threading |
| Sample | `profiler-get-script-stats` | Frame/fixed dt, Mono/GC MB |
| Sample | `profiler-get-memory-stats` | Reserved/allocated/graphics MB |
| Bundle | `profiler-save-data` | Optional JSON dump per iteration |
| Visual | `screenshot-scene-view` | Guardrail (not the primary metric) |
| Logs | `console-clear-logs` / `console-get-logs` | Errors after each run |
| Status | `profiler-get-status` | Confirm enabled / supported |
| Off | `profiler-stop` | End of session (optional) |

Read the matching `.claude/skills/profiler-*` and `screenshot-scene-view` / `editor-application-set-state` skills before calling.

**Limitation:** MCP profiler tools are **point-in-time snapshots** (Unity Time + Profiler scalars). They do **not** expose hierarchical CPU markers or historical Profiler window frames. Infer category from frame / GC / memory deltas, then dig into code with SigMap / stack traces / temporary `Profiler.BeginSample` if needed.

## Session state

Maintain and update every iteration:

```markdown
## Profiler loop state
- Scene: <authoringScene>
- Goal: <successGoal>
- Primary metric: <auto→resolved category>
- Iteration: <n> / <maxIterations>
- Baseline: FrameTimeMs=<avg> Fps=<avg> MonoMB=<avg> AllocMB=<avg>
- Best so far: <metric> (iteration <n>)
- Last fix: <one line>
- Status: running | goal_met | blocked | stopped

| Iter | Fix | FrameMs | Fps | MonoMB | AllocMB | Δ primary | Errors | Scene OK |
|------|-----|---------|-----|--------|---------|-----------|--------|----------|
```

## Default Play Mode sampling recipe

Use the **same** recipe every iteration:

```
1. editor-application-get-state → wait until not compiling
2. Ensure authoringScene is open
3. console-clear-logs
4. profiler-start
5. profiler-clear-data
6. editor-application-set-state { isPlaying: true }
7. Wait warmupSeconds (poll get-state / sleep via AwaitShell if needed)
8. Execute optional rerunSteps (move camera, trigger gameplay, etc.)
9. For i in 1..sampleCount:
     - profiler-capture-frame
     - profiler-get-rendering-stats
     - profiler-get-script-stats
     - profiler-get-memory-stats
     - brief gap (~0.5–1s) between samples
10. Average numeric fields across samples (see reference.md)
11. screenshot-scene-view (width/height = captureSize)
12. console-get-logs (errors)
13. editor-application-set-state { isPlaying: false }
14. Optional: profiler-save-data → Temp/profiler-loop/iter-NNN.json
```

If Play Mode fails to enter (compile errors), fix compile first — do not measure.

## Workflow

### 0. Preflight

1. Confirm Unity MCP connected (`editor-application-get-state` or `ping`).
2. Record inputs; resolve `authoringScene`.
3. Note: script edits force domain reload — exit Play Mode before editing.

### 1. Baseline

Run the sampling recipe with **no code changes**. Store averages as **baseline**.  
If `primaryMetric == auto`, resolve category (see Bottleneck triage).

### 2. Iteration loop

Until `goal_met`, `maxIterations`, or `blocked`:

#### A. One fix

- Exit Play Mode first.
- Change **one logical thing** (single hypothesis tied to the primary category).
- Follow Zombera rules: files < 500 lines, complexity < 15, correct owner.
- Wait for compile (`editor-application-get-state`).

#### B. Retest

Same sampling recipe → fill state table row.

#### C. Compare

- Primary Δ vs baseline (and vs previous accepted).
- For time/FPS goals: lower `FrameTimeMs` / higher `Fps` is better.
- For memory/GC: lower Mono/Alloc (and stable across samples) is better.
- Do not claim success on a secondary metric if the primary regressed.

#### D. Breakage checks (all must pass)

1. **Console:** no new Error/Exception since clear.
2. **Scene View:** screenshot looks sane (no pink materials, empty/broken view, obvious regression vs prior iter).
3. **Play Mode:** entered and sampled successfully.

On breakage or primary regression → **revert last fix**, mark iter failed, pivot hypothesis.

#### E. Decide

| Outcome | Action |
|---------|--------|
| Goal met + no breakage | Stop — success report |
| Better, goal not met | Keep fix, next hypothesis |
| Same/worse, no breakage | Revert or different fix |
| Breakage | Revert, then pivot |
| `maxIterations` | Stop — best effort + gap |

### 3. Final report

```markdown
## Profiler fix loop — done

**Result:** goal_met | best_effort | blocked

| Iter | Fix | FrameMs | Fps | MonoMB | AllocMB | Δ primary | Errors | Scene OK |
|------|-----|---------|-----|--------|---------|-----------|--------|----------|

**Best:** <metrics> (iter <n>) — <fix>
**Kept changes:** <files> or **Reverted:** <reason>
**Follow-ups:** <optional>
```

## Bottleneck triage (`primaryMetric: auto`)

After baseline averages, pick **one** category:

| Category | Signals | Typical fixes |
|----------|---------|---------------|
| `frame` | High `FrameTimeMs`, low `Fps` | Cull work in Update, batching, cheaper rendering, less per-frame Find/alloc |
| `gc` | High/rising Mono or GC MB across samples; hitchy FrameMs | Remove per-frame allocs, reuse buffers, NonAlloc physics |
| `memory` | High Allocated/Reserved/Graphics MB | Leak hunt, unload unused, texture/mesh budgets |

Tie-break: prefer `frame` if Fps is clearly under goal; else `gc` if Mono climbs sample-to-sample; else `memory`.

See [reference.md](reference.md) for averaging rules, MCP templates, and hypothesis cheatsheet.

## Guardrails

- **Exit Play Mode before code edits** — avoid lost domain-reload state.
- **One fix per iteration** — clean attribution and revert.
- **Identical sampling recipe** — changing warmup/sampleCount mid-loop invalidates comparison.
- **Scene View is a guardrail** — primary success is profiler metrics vs `successGoal`.
- **World vs menu** — do not drive world systems in `MainMenu`; use Playing / LoadingWorld / Paused.
- **No commit unless asked.**
- **Stop when blocked** — MCP down, Play Mode won’t start, or samples unusable after 2 attempts → ask the user.

## Example invocation

> `/unity-profiler-fix-loop` — World Generation scene; goal avg FrameTimeMs ↓ 20%; max 5 iters.

1. Baseline Play Mode samples → auto picks `frame`.
2. Fix e.g. cache a per-frame lookup → retest → compare.
3. Stop when goal met and Scene View + console clean.

## Additional resources

- [reference.md](reference.md) — averaging, MCP JSON templates, triage details
