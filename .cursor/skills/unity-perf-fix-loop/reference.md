# Unity Perf Fix Loop — Reference

## Zombera timing log examples

These are real patterns from city/road/terrain pipelines. Use them to validate regex parsing.

### CityPrefabRoadNetworkBuilder (summary line)

```
context=120ms flatten=340ms buildWall=2100ms (compute=1800ms, yieldGaps=300ms) publish=90ms, wall=2650ms
```

| Key | Typical meaning |
|-----|-----------------|
| `context` | Setup / district context |
| `flatten` | Terrain flatten pass |
| `buildWall` | Main network build (often the optimization target) |
| `compute` | CPU work inside buildWall |
| `yieldGaps` | Time spent in `yield`/spread frames |
| `publish` | Publish / finalize |
| `wall` | Total wall-clock time |

### ProceduralRoadSystem.TileBuild

```
, buildNetwork=450ms, organize=90ms.
```

### CityLotTerrainPainter

```
ms (residual skipped) terrains=4.
ms residual=12ms (erase work 890ms) terrains=4.
ms — get=5ms stamp=120ms set=40ms sync=8ms.
```

### District / lot placement

```
placed 128 lots in 890ms.
painted 64 lot surface(s) in 1200ms.
```

## Regex cheatsheet

Replace `{step}` with the target step name (e.g. `buildWall`).

```
# key=value ms (most common)
(?i){step}\s*=\s*(\d+)\s*ms

# total wall clock
(?i)wall\s*=\s*(\d+)\s*ms

# "in Nms" style
(?i)in\s+(\d+)ms

# comma-separated
(?i),\s*{step}\s*=\s*(\d+)ms
```

## MCP call templates

### Clear + get logs

```json
// console-clear-logs
{}

// console-get-logs (after rerun)
{
  "maxEntries": 200,
  "logTypeFilter": null,
  "includeStackTrace": false,
  "lastMinutes": 2
}

// errors only
{
  "maxEntries": 50,
  "logTypeFilter": "Error",
  "includeStackTrace": true,
  "lastMinutes": 2
}
```

### Smoke test after each iteration

```json
{
  "testMode": "EditMode",
  "testNamespace": "Zombera",
  "includePassingTests": false,
  "includeMessages": true,
  "includeLogs": true,
  "logType": "Warning"
}
```

Adjust `testNamespace` / `testClass` to the relevant assembly.

## Breakage checklist

- [ ] No new `Error` / `Exception` in console since last clear
- [ ] No new compile errors (`editor-application-get-state`)
- [ ] Configured `tests-run` passes (if any)
- [ ] Target log line still emitted (pipeline did not silently skip work)
- [ ] Phase totals roughly consistent (e.g. `wall` ≈ sum of major phases ± yield)
- [ ] No obvious functional regression (user-defined check)

## When to stop iterating

| Signal | Action |
|--------|--------|
| Goal met + checks pass | Done — keep changes |
| Diminishing returns (< 3% improvement twice in a row) | Stop — report best iteration |
| Fix helps sub-step but hurts `wall` | Revert unless sub-step is the explicit goal |
| Two consecutive parse failures | Add logging or ask user for `logAnchor` |
| Same error after revert | Blocked — surface to user |

## Revert guidance

1. `git checkout -- <files>` for the last iteration's edits only.
2. Re-run baseline rerun steps to confirm timings returned to prior range.
3. Document the failed hypothesis in the state table.
