# AI Plan: Roads Step (Step 1) Speed Optimization

## Baseline

- Original 5-city region Roads step: **61,459 ms**.
- Re-baseline 2026-08-17 (smaller cities, item 1 landed): **21,432 ms / 28,824 ms / 19,737 ms**.
- Re-baseline after item 2: **25,624 ms / 27,200 ms / 29,069 ms** — no clear gain, high run-to-run
  variance. Compare per-phase splits (`splines=`, `junctions=`, `mesh=`) rather than totals.
- Re-baseline after items 3+4 (4-site region, items 1–4 in): live 16,153 ms / 18,552 ms;
  headless 20,038 ms — headless is NOT faster (loses ~2–3 s in the splines phase). Splines
  still dominate (9–13 s). Fast-mode runs that report ~30 ms are hitting the cached-roads
  early-out, not building.
- Continue re-baselining after each item.
- Captured phase splits from console logs (`CityHubBuildTimings`):
  - Small city (22 roads): total 4,165 ms — splines 2,754 ms (66%), junctions 848 ms, mesh 561 ms.
  - Big city (45 roads): total 43,753 ms — splines 37,795 ms (86%), junctions 1,772 ms, mesh 4,183 ms.
- After item 7 (per-road `ERRoad.Refresh()` replacing the interim mesh build):
  199-road city total **6,390 ms** — splines 737 ms (resolve 27 + create 534 + roadsOnlyMesh 174),
  junctions 4,126 ms (residual 3,365), mesh 1,524 ms. Junctions ≈ 65% of total.
- After items 11+12 (X fast path + purge defer, 2026-08-18): expect junction X time to
  drop sharply and the step-level clear to drop to ~0 ms. Re-baseline with the split logs:
  `[CityPipelineRunnerWindow] Roads step split: ensureForBuilder=/clearRoads=/generate=/wall=`
  and `[ProceduralRoadSystem] City prefab road network built … junctionT=/junctionX=`.
  Verify junction counts unchanged (T + X wired 100%, no extra failures in the aggregate
  warning) before tuning further.
- Spline creation is the dominant phase and grows superlinearly with city size.
- The pipeline pump repaint issue (bugs.md #1) is already fixed: `CityPipelineRunnerWindow.PumpActiveStep`
  time-slices to 12 ms per tick and throttles `SceneView.RepaintAll()` to once per 100 ms.

## Checklist (work one item at a time, re-baseline after each)

1. [x] **Single-call road creation** — `ProceduralRoadSystem.TileBuild.cs::CreateCityPrefabRoad`
   Replace `CreateRoad(name, type)` + `AddMarkers(markers)` + per-marker `SetMarkerControlType`
   with the one-call overload `CreateRoad(name, type, markers, controlTypes)`
   (`StraightXZ` for straight roads, `Spline` for `curvedMarkers`).
   Each EasyRoads call triggers an internal road refresh; the old path cost
   `~markers.Length + 2` calls per road — the dominant cost of the splines phase.
   Verify junction shapes on curved arterials after landing.
   ✅ Done 2026-08-17 — re-baseline the 5-city run.

2. [x] **Skip redundant `SetWidth`** — `ProceduralRoadSystem.TileBuild.cs::ApplyRoadPostCreation`
   `road.SetWidth(path.widthMeters)` runs after every road creation and likely triggers a
   refresh. Skip it when the width already matches the road type's default width.
   ✅ Done 2026-08-17 — city builds now skip SetWidth when `road.GetWidth()` ≈ requested width
   (helper `RoadWidthNeedsUpdate`, 0.001 m tolerance, falls back to SetWidth on API failure).

3. [x] **Scope NaN mesh repair** — `ProceduralRoadSystem.CityPrefabBuild.cs::RepairNonFiniteRoadNetworkMeshes`
   Currently `GetComponentsInChildren<MeshFilter>` + `mesh.vertices` readback over the entire
   "Road Network" root after each build. Limit to `context.CreatedRoads` / `context.CreatedConnections`
   (keep full-scan fallback for non-city tile builds).
   ✅ Done 2026-08-17 — repair cluster moved to new partial `ProceduralRoadSystem.MeshRepair.cs`;
   scans only the created roads (by name via `GameObject.Find(road.GetName())`) and connections.

4. [x] **Fast / Headless Roads mode** — `CityPipelineRunnerWindow`
   Serialized toggle that maps the Roads step to `WrapSync(() => builder.GenerateCityRoadNetwork())` —
   the existing synchronous path, no coroutine yields and no per-tick repaints at all.
   Safe: the old 10 s editor deadline no longer exists.
   ✅ Done 2026-08-17 — `fastRoadsMode` toggle in the window controls; Roads step calls the
   synchronous `GenerateCityRoadNetwork()` when on (no live preview, Stop can't interrupt mid-step).
   Fast mode now calls `ClearGeneratedRoads()` first so cached-road early-out can't fake a
   ~30 ms "build". A/B result: headless is consistently ~10-20% SLOWER than live — keep the
   toggle for A/B testing, but the live pump remains the recommended mode.

5. [ ] **Batch ground-height sampling** — `CityPrefabRoadNetworkBuilder.RoadStack.cs::ResolveGroundHeight`
   One `Physics.Raycast` per road point. Batch with `RaycastCommand`, or precompute a height grid
   via `Terrain.SampleHeight` when `Terrain.activeTerrain` covers the bounds; raycast only as fallback.

6. [~] **Grid-based marker cluster lookup** — `ProceduralRoadSystem.Junctions.CityHub.cs::FindProximityClusterIndex`
   O(markers × clusters) linear scan. Replace with a uniform XZ grid over the build bounds
   (self-contained, low risk).
   📌 2026-08-18: measured NOT a hotspot — ~800 markers × ~150 clusters ≈ 120k cheap
   distance checks (< 1 ms). Junction time is EasyRoads connector spawn/connect, not the
   cluster lookup. Deprioritized; skip unless profiling shows otherwise.

7. [x] **Audit double mesh build** — `BuildCityHubRoadMeshesOnly` builds all road meshes,
   then `BuildCityHubNetworkMeshes` runs a full `BuildRoadNetwork()` again.
   Check the EasyRoads docs for a partial build that includes connections, or confirm the
   final build can be scoped. Do not reorder: roads need internal road data before
   `ConnectToStart` / `ConnectToEnd`.
   ✅ Done 2026-08-18 — step 1: terrain flags off in interim build (21,568 → 13,775ms).
   Step 2: interim build replaced by per-road `ERRoad.Refresh()` loop — junctions still
   wire 100% (115/115, failures=0). roadsOnlyMesh 3,177 → 174ms; splines phase 14,391 →
   737ms; 199-road city total 6,390ms (junctions 4,126 residual 3,365 mesh 1,524).

8. [x] **Gate per-junction logging** — e.g. "City arterial T junction … wired on deferred retry"
   logs per junction. Move behind `logFocusedRoadGenerationDiagnostics`; console rendering is
   expensive during generation.
   ✅ Done 2026-08-18 — new opt-in serialized flag `logPerJunctionWiringFailures` (default
   off) gates the NaN-anchor, snap-fallback, and deferred-retry per-junction logs.
   New counters `JunctionCellsFailed` / `JunctionNaNSkips` feed one aggregate warning per
   build (`WarnIfCityHubJunctionWiringHadFailures`, sync + live paths) and the focused
   diagnostics summary. Deferred-retry success decrements `JunctionCellsFailed` so the
   aggregate reflects the true end state.

9. [x] **Instrumentation** — wrap `TryCreateRoadObjectFromSplitPath` and junction wiring in
   `Profiler.BeginSample` and add per-road aggregate timers behind `logFocusedRoadGenerationDiagnostics`
   so EasyRoads DLL hotspots show up. (Do this first if item 1 doesn't move the needle enough.)
   ✅ Done 2026-08-17 — new partial `ProceduralRoadSystem.Instrumentation.cs`:
   constant-string profiler samples (`RoadSplines.CreateLoop` / `RoadSplines.EasyRoads.CreateRoad` /
   `RoadSplines.PostCreation`, `CityHub.Junctions.{Wire,Manifest,Residual}`, `CityHub.Mesh.{RoadsOnly,Network}`),
   aggregate stopwatch fields on `RoadGenerationDiagnostics` (`SplineCreate*Ms` + road count), and
   `LogCityHubBuildDiagnostics` moved here with a `splineCreate=` summary appended.
   How to use: open Profiler (CPU), record one Roads step, filter "RoadSplines" / "CityHub".
   First capture (2026-08-17, 3 cities live): easyRoads=539ms for 217 roads (~2.5ms/road) —
   CreateRoad is NOT the hotspot. Splines phase (11.1s) is dominated by something else;
   added splineResolve= and roadsOnlyMesh= sub-phase timers (the interim BuildRoadNetwork
   lives inside the "splines" phase and spams "Failed extracting collision mesh ...
   non-finite" errors). Item 7 is now the prime suspect.

10. [x] **Undo-group collapse tail (window DurationMs vs body)** — solved the mystery
    2026-08-18: the window's `DurationMs` (17,678 ms) minus the measured step body
    (8,187 ms) is NOT build work — `CityPipelineRunnerWindow.FinishStep` runs
    `Undo.CollapseUndoOperations(Undo.GetCurrentGroup())` BEFORE stopping the step
    stopwatch. Chunked collapse (`CityBuildUndoChunker`, 7 phase boundaries) proved
    the attribution on the 15:55 run: every chunk ≤10 ms EXCEPT
    `after 'terrain flatten': 8841ms` — the 20 full-TerrainData undo snapshots from
    `CityHubTerrainFlattener.RecordTerrainUndo` dominate the merge. Fixed: pipeline
    runs now skip terrain undo — `CityPrefabRoadNetworkBuilder.RecordTerrainUndo`
    (serialized flag, default true) is set false by `RoadsRoutine` in
    `CityPipelineRunnerWindow.Steps.cs` and restored in `finally`;
    `CityHubTerrainFlattener.FlattenInnerFootprint(settings, recordUndo = true, …)`
    gates the call. Inspector/context-menu generation keeps undo; the window's Reset
    step owns terrain restoration for pipeline runs. Expected result: Roads step
    ≈ 10.5 s (buildWall compute 9.7 s + small chunks), FinishStep collapse ≈ 0 ms.

11. [x] **Single-shot X-junction wiring (residual pass)** — `TryConnectJunctionCell`
    still routed 4+ marker cells through `TryFinalizeJunctionWithRotations`, whose
    trial loop spawns/destroys up to 4 connectors per junction (X measured
    ~1.6 s of a ~4.1 s junction phase).
    ✅ Done 2026-08-18 — `TryConnectCityHubXJunctionFast` mirrors the T fast path:
    one spawn at identity rotation + deterministic port assignment, one 90° retry,
    then falls through to the legacy trial loop on failure. Editor city-prefab builds
    only, only when the selected prefab is the X crossing and the cell is not an
    arterial-T collapse (forceEdgeT). Play-mode and roundabout paths unchanged.

12. [x] **Defer redundant EasyRoads purge in the pipeline clear** — the Roads step
    ran the full prepare/purge chain 3× (step-level `ClearGeneratedRoads` purged the
    real ~190-road/110-junction network, then the routine-internal clear and the build
    prepare re-purged empty containers).
    ✅ Done 2026-08-18 — `ProceduralRoadSystem.DeferEasyRoadsPurgeOnClear`
    (runtime, non-serialized) skips `PrepareEasyRoadsNetworkForEditorPreview` inside
    `ClearPinnedTilePreviewRoads`; `RoadsRoutine` sets it for the step (both fast and
    live paths) and restores in `finally`. The build routine's own prepare still
    purges exactly once, before new roads are created. Abort safety: if generation
    fails before publishing, `RoadsRoutine.finally` detects
    `!target.HasGeneratedRoadNetwork` and runs a full `ClearGeneratedRoads()` so
    stale EasyRoads geometry never survives. Clear log now appends
    "(purge deferred)" when skipped.

13. [x] **Fixed-seed region scatter + X fast-path outcome counters (A/B testability)**
    ✅ Done 2026-08-18 —
    - `RoadGenerationDiagnostics.JunctionXRetries` / `JunctionXFallbacks` count the
      X fast path's 90° retries and legacy-loop give-ups; logged in
      `[ProceduralRoadDiag]` as `junctionXRetries=` / `junctionXFallbacks=`.
    - `CityPrefabRoadNetworkBuilder.fixedRegionSeedOverride` (serialized, 0 = normal
      flow) forces `ResolveRegionSeed()` AND pins the scatter seed, so repeat Roads
      runs build the identical layout. Exposed in the builder inspector region section
      ("Fixed Seed Override (A/B Tests)") and in the World Builder window
      ("Fixed Region Seed").
    - Re-baseline after Unity refresh (clean domain): 184-road/107-junction run
      total 6,794 ms (junctionT 1,851/60, junctionX 872/47, mesh 2,164) — the
      earlier ~9.5 s runs were confounded by MCP log-file churn + per-run random
      layouts. Run-to-run variance is still ~30% (6.8 / 7.2 / 8.8 s across three
      post-refresh runs); use the fixed seed + counter logs for real comparisons.
      EasyRoads duplicate-marker / NaN-collision warnings persist (~30-50 per run,
      all from `SnapRoadEndpointToConnectionSocket`) but wiring stays 100%
      (`cellsFailed=0`). Next lever: marker-snap distance guard in the residual pass.

14. [x] **Marker-snap degenerate-geometry guard** — `SetMarkerPosition` /
     `SetMarkerControlType` each trigger a full EasyRoads `Refresh()`, and snapping
     an endpoint onto (or already-at) the socket/neighbour position produces
     zero-length segments → "two markers at the same position" + NaN collision-mesh
     errors.
     ✅ Done 2026-08-18 — `ShouldSkipMarkerSnap` in `Junctions.MarkerOps.cs`:
     skips the write when the endpoint is already at the target (≤0.05 m, redundant
     refresh) or the target would collapse onto the road's neighbouring marker
     (≤0.5 m XZ). Applied in `SnapRoadEndpointToConnectionSocket` (now returns bool)
     and `SnapCityHubRoadMarkerToJunctionAnchor`. New counter
     `junctionSnapSkips=` in `[ProceduralRoadDiag]` counts skipped snaps (via
     `SnapRoadEndpointToSocketCounted` in Ports.cs — extracted to keep
     `TryConnectRoadMarkerWithRetries` under cognitive-complexity 15).
     Test with seed 12345: compare against the 9,506 / 9,851 ms baseline pair;
     watch `junctionSnapSkips` and confirm EasyRoads warning counts drop while
     `cellsFailed=0` holds.
     📊 TEST 1 (seed 12345, 3 runs): totals 8,436 / 7,618 / 8,305 ms
     (baseline 9,506 / 9,851) — residual dropped to 3,888–4,509 (baseline
     4,720 / 4,856). BUT `junctionSnapSkips=2` and EasyRoads warnings stayed at
     84/run (10 spline-point + 10 same-position + 64 NaN collision) — stack traces
     still point at `SnapRoadEndpointToConnectionSocket` via the T fast path, so the
     0.5 m threshold was too tight: EasyRoads collapses markers closer than its road
     resolution (~4 m default), not just true zero-length pairs.
     🔧 Threshold raised to 4.0 m. Re-test expectations: `junctionSnapSkips` ≈ 10–20,
     warnings ≈ 0, `cellsFailed=0`, residual time lower than baseline.
     📊 TEST 2 (4 m): `junctionSnapSkips=22`, spline-point + same-position warnings → 0,
     NaN errors unchanged at 64. Instrumented every snap: ALL 412 snaps move exactly
     11.0 m (road endpoints are generated 11 m short of their sockets); 16 two-marker
     roads produce the 4×16 NaN errors with fully finite inputs — EasyRoads generates
     the NaN internally during refresh.
     ✅ TEST 3 (skip pre-snap entirely, `skipJunctionSocketSnaps`):
     `junctionSnapSkips=434`, NaN errors 0, warnings 0, `cellsFailed=0`,
     `junctionConnectFailures=0` — ConnectToStart/End pulls the road to the socket
     itself; the pre-snap was fully redundant. Junction phase 2,626 ms (was 5,787–
     5,887 baseline): junctionT 1,430/66 (21.7 ms each), junctionX 1,065/59
     (18.1 ms each). Build total 6,950 ms, step wall 7,657 ms — best yet for seed
     12345. `skipJunctionSocketSnaps` now defaults TRUE (kept as an escape hatch).
     Still guarded by ShouldSkipMarkerSnap when disabled; the snap-together fallback
     (SnapCityHubRoadMarkerToJunctionAnchor) keeps its own guard.     🧹 CLEANUP 2026-08-18: pre-connect snap chain DELETED permanently —
     `SnapRoadEndpointToConnectionSocket` (CityHub.cs), `SnapRoadEndpointToSocketCounted`
     wrapper (Ports.cs), `skipJunctionSocketSnaps` flag, `LogJunctionSnapDetail`, and the
     `JunctionSnapSkips` counter/log are all removed. Junction connects now call
     ConnectToStart/End directly (plus the removed pre-snap `EnsureRoadMarkerSnapComponent`
     calls at the two Ports.cs sites). `ShouldSkipMarkerSnap` + its 4 m threshold remain
     ONLY for the deferred-arterial snap-together fallback
     (`SnapCityHubRoadMarkerToJunctionAnchor`).
     Post-cleanup verification run: total 6,684 ms, junctions 2,231 ms (T 1,333/66,
     X 772/59), warnings 0, cellsFailed=0.

15. [x] **Mesh phase: terrain manipulation off in the final build**
    Sub-timers added to `CityHubBuildTimings` (mesh=…(build= sidewalk= repair=
    sanitize= hierarchy=)): build=3,523 ms of 3,541 ms total mesh — sidewalk 1 ms,
    repair 9 ms, sanitize 0 ms, hierarchy 4 ms. EasyRoads docs
    (`ERRoadNetwork.html`): `BuildRoadNetwork(bool splatmaps, bool trees, bool detail,
    ERRoad[] roads)` — the bools are TERRAIN flags, not build scoping; the no-arg
    build re-manipulates splatmaps/trees/detail across the whole region, but the
    city pipeline flattens + paints terrain itself in later steps.
    ✅ Done 2026-08-18 — `BuildCityHubNetworkMeshes` now calls
    `BuildRoadNetwork(false, false, false, CreatedRoads)` (same pattern as the tile
    preview path). Expected: mesh phase drops from ~3.5 s toward road/connection
    mesh-gen only (~1–1.5 s). Verify seed 12345: visual check roads + terrain intact.
    ❌ REVERTED 2026-08-18 — caused visual breakage in the pipeline (roads/terrain
    after the Roads step). No-arg `BuildRoadNetwork()` restored (comment left in
    code explaining why). The terrain manipulation IS load-bearing for the city
    build. Mesh phase stays ~3.5 s; sub-timers kept for future work. Other levers
    for the mesh phase: none found via API — sidewalk/repair/sanitize/hierarchy
    are all ≈ 0. Accept mesh cost; focus on spline + T fast path instead.
## Notes

- Free wins while iterating: `stepDelaySeconds = 0`, `pauseAfterStep` off, run the single
  Roads step (▶) instead of Run All.
- `roadsPerFrame` / `junctionCellsPerFrame` no longer matter much — the pump time slice dominates.
- After any code change, regenerate SigMap (`sigmap --adapter copilot`).
