# Unity Profiler Fix Loop — Reference

## Averaging rules

For each measured window, collect `sampleCount` snapshots. Store **means** for comparison; optionally note **max** FrameTimeMs (hitch detector).

| Field | Source tool | Aggregate |
|-------|-------------|-----------|
| `FrameTimeMs` | `profiler-capture-frame` / rendering / script | mean (+ max) |
| `Fps` | capture-frame / rendering | mean |
| `MonoMemoryUsageMB` | `profiler-get-script-stats` | mean (+ delta first→last) |
| `GCMemoryUsageMB` | script-stats | mean (+ delta first→last) |
| `TotalAllocatedMemoryMB` | `profiler-get-memory-stats` | mean |
| `TotalReservedMemoryMB` | memory-stats | mean |
| `GraphicsMemoryMB` | memory-stats | mean |

**GC climb:** if Mono or GC MB rises > ~5% from first to last sample in one window, treat as `gc` signal even if mean looks fine.

Ignore a sample if `FrameTimeMs` is 0 or `Fps` is 0 (startup / paused frame) — drop and note in the table.

## MCP call templates

### Play Mode

```json
// editor-application-set-state — enter
{ "isPlaying": true, "isPaused": false }

// editor-application-set-state — exit
{ "isPlaying": false, "isPaused": false }
```

### Profiler session

```json
// profiler-start / profiler-clear-data / profiler-stop / profiler-get-status
{ "nothing": "" }

// profiler-save-data
{ "filePath": "Temp/profiler-loop/iter-001.json" }
```

### Samples (call all four per sample tick)

```json
// profiler-capture-frame
{ "nothing": "" }

// profiler-get-rendering-stats
{ "nothing": "" }

// profiler-get-script-stats
{ "nothing": "" }

// profiler-get-memory-stats
{ "nothing": "" }
```

### Scene View guardrail

```json
// screenshot-scene-view
{ "width": 1920, "height": 1080 }
```

### Console breakage

```json
// console-clear-logs — before Play Mode
{}

// console-get-logs — after sampling
{
  "maxEntries": 50,
  "logTypeFilter": "Error",
  "includeStackTrace": true,
  "lastMinutes": 5
}
```

## Goal examples

| Goal string | Pass when |
|-------------|-----------|
| `avg FrameTimeMs ↓ 20%` | `(baseline - current) / baseline ≥ 0.20` |
| `avg Fps ≥ 60` | mean Fps ≥ 60 |
| `MonoMB stable` | first→last sample delta ≤ 2% and no Error |
| `AllocatedMB ↓ 10%` | mean Allocated ≤ 90% of baseline |

Always require breakage checks to pass in addition to the numeric goal.

## Hypothesis cheatsheet

### `frame` (CPU / render)

- Remove `FindObjectsByType` / GetComponent from Update
- Cache transforms, layers, masks
- Throttle ticks; event-driven over per-frame polls
- Reduce overdraw, shadow casters, realtime lights
- MicroSplat / terrain / Crest: check expensive per-frame updates

### `gc`

- No LINQ / string concat / `new` in Update/FixedUpdate
- Reuse arrays; `OverlapSphereNonAlloc` etc.
- Avoid boxing in hot paths
- Temporary `Profiler.BeginSample` around suspects if MCP snapshots are too coarse

### `memory`

- Asset churn / duplicate materials & textures
- Streaming unload not running
- Crest / Enviro / terrain data held after leave Play Mode (compare reserved after exit)

## When to fall back to unity-perf-fix-loop

Use **unity-perf-fix-loop** instead when the bottleneck is an **Editor pipeline step** that already logs `name=Nms` (city walls, terrain paint, hub stages). This profiler skill is for **Play Mode runtime** frame/memory behavior.

## Known MCP limits

- No hierarchy / sample-name breakdown from these tools — only scalars.
- `profiler-enable-module` is local bookkeeping; do not treat it as Unity module configuration.
- Scene View screenshot reflects **editor Scene View**, not Game View camera. Prefer Scene View for “world still looks OK”; use `screenshot-game-view` only if the user asks for player-camera visuals.
