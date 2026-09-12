---
name: unity-perf-fix-loop
description: >-
  Iterative Unity performance workflow: capture baseline step timing from Editor
  logs (ms), apply a targeted fix, rerun the same steps, compare timings, check
  for errors/regressions, and repeat until the goal is met or max iterations.
  Use when optimizing a slow Unity step, tuning city/road/terrain pipelines,
  or when the user asks to measure-fix-rerun loops from Unity console logs.
---

# Unity Perf Fix Loop

Measure → fix → rerun → compare → repeat until the target step is faster and nothing is broken.

## When to use

- A Unity Editor step logs timing in **ms** (e.g. `buildWall=1234ms`, `terrain=567ms`).
- You need an automated loop: baseline → fix → verify timing + breakage checks.
- The rerun steps are reproducible via MCP (`script-execute`, menu item, or `tests-run`).

## Required inputs (ask if missing)

| Input | Example | Purpose |
|-------|---------|---------|
| `targetStep` | `buildWall`, `terrain`, `organize` | Sub-timing to optimize |
| `logAnchor` | `CityPrefabRoadNetworkBuilder`, `ProceduralRoadSystem` | Log prefix / component to filter |
| `rerunSteps` | Ordered list of MCP actions | Exact steps to reproduce the run |
| `successGoal` | `buildWall ms ↓ 20%` or `total wall ms < 3000` | When to stop |
| `maxIterations` | `5` (default) | Safety cap |
| `breakageChecks` | `tests-run`, `logTypeFilter: Error`, optional screenshot | What “broken” means |

## Tools (prefer MCP over CLI)

Namespace: `project-0-Zombera-ai-game-developer`

| Phase | Tool | Notes |
|-------|------|-------|
| Isolate logs | `console-clear-logs` | Always clear before each measured run |
| Capture logs | `console-get-logs` | Use `lastMinutes: 2` after rerun; `logTypeFilter: null` for all |
| Trigger rerun | `script-execute` | Menu items, component methods, play-mode toggles |
| Verify tests | `tests-run` | Prefer `EditMode`; save scenes first |
| Compile wait | `editor-application-get-state` | Poll until not compiling |
| Revert bad fix | git / undo last code edit | On regression or new errors |

Read `console-get-logs`, `console-clear-logs`, `script-execute`, and `tests-run` skills when calling those tools.

## Session state

Maintain this block in the conversation and update every iteration:

```markdown
## Perf loop state
- Target: <targetStep> on <logAnchor>
- Goal: <successGoal>
- Iteration: <n> / <maxIterations>
- Baseline ms: <value or pending>
- Best ms so far: <value> (iteration <n>)
- Last fix summary: <one line>
- Status: running | goal_met | blocked | stopped

| Iter | Fix | <targetStep> ms | Δ vs baseline | Δ vs prev | Errors | Tests |
|------|-----|-----------------|---------------|-----------|--------|-------|
```

## Workflow

### 0. Preflight

1. Confirm Unity MCP is connected (`ping` or `editor-application-get-state`).
2. Record `targetStep`, `logAnchor`, `rerunSteps`, `successGoal`, `maxIterations`, `breakageChecks`.
3. If the fix touches `.cs` files, note that the next run may wait on domain reload.

### 1. Baseline run

```
console-clear-logs
→ execute rerunSteps (in order)
→ wait for completion (compile / processing / play mode as needed)
→ console-get-logs (lastMinutes: 2, maxEntries: 200)
→ parse timings (see Parsing)
```

Store **baseline** `targetStep` ms. If parsing fails, widen `logAnchor`, increase `lastMinutes`, or add a temporary `Debug.Log` with a unique marker and retry once.

### 2. Iteration loop

Repeat until `goal_met`, `maxIterations`, or `blocked`:

#### A. Propose and apply one fix

- Change **one logical thing** per iteration (single hypothesis).
- Follow Zombera rules: files < 500 lines, complexity < 15, correct owner for the subsystem.
- Summarize the fix in one line for the state table.

#### B. Wait for Unity ready

After script edits:

1. `editor-application-get-state` until not compiling.
2. If a tool returned `Processing`, wait for completion notification before measuring.

#### C. Measured rerun

```
console-clear-logs
→ execute rerunSteps (same order, same parameters)
→ console-get-logs
→ parse timings
```

#### D. Compare timings

- `Δ vs baseline` = `(current - baseline) / baseline * 100%`
- `Δ vs prev` = change from last iteration
- Negative Δ = faster (good for time targets)
- If a sub-step got faster but **total wall** got slower, note the tradeoff; do not claim success on sub-step alone unless that is the goal.

#### E. Breakage checks (all must pass)

1. **Console**: no new `Error` or `Exception` since clear (use `logTypeFilter: Error`, `includeStackTrace: true` if needed).
2. **Tests** (if configured): `tests-run` — `Summary.FailedTests == 0`.
3. **Functional** (if configured): screenshot, scene query, or user-defined assertion.

If breakage detected → **revert the last fix**, mark iteration failed, try a different approach. Do not stack fixes on a broken state.

#### F. Decide next action

| Outcome | Action |
|---------|--------|
| Goal met + no breakage | Stop — report success with table |
| Faster but goal not met | Continue loop with next hypothesis |
| Slower or same + no breakage | Revert or try different fix; do not keep regressive changes |
| Breakage | Revert, fix breakage or pivot hypothesis |
| `maxIterations` reached | Stop — report best result and remaining gap |

### 3. Final report

```markdown
## Perf fix loop — done

**Result:** goal_met | best_effort | blocked

| Iter | Fix | <targetStep> ms | Δ baseline | Errors | Tests |
|------|-----|-----------------|------------|--------|-------|

**Best:** <ms> (iter <n>) — <fix summary>
**Kept changes:** <files> or **Reverted:** <reason>
**Follow-ups:** <optional next targets>
```

## Parsing timings from logs

Extract ms from log `Message` strings. Common Zombera patterns:

| Pattern | Example message fragment |
|---------|--------------------------|
| `name=Nms` | `buildWall=1200ms flatten=300ms` |
| `name in Nms` | `placed 42 lots in 890ms` |
| `name=N ms` | `residual=12 ms` |
| `, name=Nms` | `, buildNetwork=450ms, organize=90ms` |

**Algorithm**

1. Filter entries where `Message` contains `logAnchor` (case-insensitive).
2. Prefer the **last** matching entry from the measured window (final summary line).
3. Regex for `targetStep`: `(?i){targetStep}\s*=\s*(\d+)\s*ms` or `(?i)in\s+(\d+)ms` when `targetStep` is `total`/`wall`.
4. If multiple matches in one line, take the capture group tied to `targetStep`.
5. Store integers only; missing value → parsing failed.

For multi-phase lines (e.g. `context=…ms flatten=…ms buildWall=…ms`), parse all phases into a dict for optional secondary comparison.

See [reference.md](reference.md) for Zombera log examples and rerun templates.

## Rerun step patterns

**Menu / editor command** (`script-execute`, body mode):

```csharp
UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Your/Menu/Path");
```

**Call a component method** — resolve the target object first (`gameobject-find`), then invoke via `reflection-method-call` or `script-execute` with `GameObjectRef`.

**EditMode test** (when the step is covered by tests):

```json
{
  "testMode": "EditMode",
  "testClass": "YourTestClass",
  "includeLogs": true,
  "logType": "Log"
}
```

Use the **same** rerun recipe every iteration. Changing rerun steps invalidates comparison.

## Guardrails

- **One fix per iteration** — easier attribution and safer revert.
- **Clear logs before every measured run** — never compare against stale console output.
- **Do not optimize in MainMenu** — gate world work by `GameManager.Instance.CurrentState` when applicable.
- **Revert on regression** — keep the best-known-good code if a fix slows the target or breaks checks.
- **Stop when blocked** — compilation errors, MCP disconnected, or unparseable logs after 2 attempts → ask the user.
- **No commit unless asked** — report diffs; let the user commit.

## Example invocation

> `/unity-perf-fix-loop` — optimize `buildWall` in city road generation; rerun via Tools → City → Build Roads; goal 25% faster; max 4 iterations.

1. Set state block with `targetStep=buildWall`, `logAnchor=CityPrefabRoadNetworkBuilder`.
2. Baseline: clear → menu rerun → parse `buildWall` ms.
3. Loop: e.g. reduce yield gaps → rerun → compare → run EditMode smoke test.
4. Stop when `buildWall` ≥ 25% faster and tests pass.

## Additional resources

- [reference.md](reference.md) — log examples, regex cheatsheet, breakage checklist
- [development-hub.md](development-hub.md) — **World Builder Development Hub** profile (Reset → Paint Natural Surfaces, 14-stage range scan)

## Multi-step range mode

When optimizing a **range** of steps (not just one `targetStep`):

### Iteration 0 — baseline (scan only)

Run the full pipeline once with **no perf edits** in that session. Save the report as the comparison anchor.

### Iteration 1+ — change, then scan

Each iteration **must** include new code changes before measuring:

1. **Baseline scan** (iteration 0 only) — parse all step timings into a sorted table.
2. **Plan batch** — up to 5 fixes across different stages (or one fix per iteration in strict mode).
3. **Apply** — land the edits; wait for compile.
4. **Scan** — rerun the full range (async hub pipeline).
5. **Compare** — full 14-row table vs last **accepted** anchor.
6. **Keep or revert** — revert batch if slower; update anchor if faster.
7. Repeat until goal met or iteration cap.

A **scan-only rerun** without code changes is a stability check, not an iteration.

For single-fix strict mode: one change → scan → compare per loop (see workflow §2).

For the World Builder Development Hub (Reset through Paint Natural Surfaces), use the preset in [development-hub.md](development-hub.md).
