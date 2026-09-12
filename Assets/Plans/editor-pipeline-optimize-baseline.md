# Editor pipeline optimize — baseline notes

Fixture: seed 16, Medium, FastIteration off, Reset → BuildEasyRoadsMeshes (procedural meshes).

## Partial baseline (2026-09-03, before Nature cache fix)

From Unity Editor.log during first async road-meshes run:

| Stage | durationMs |
|-------|------------|
| ResetGeneratedWorld | 1221 |
| AllocateTerrainGrid | 587 |
| GenerateBaseLandforms | 3271 |
| SolveHydrology | 547 |
| CarveWaterFeatures | 3104 |
| BuildOceanSurfaces | 2163 |
| ClassifyBiomesAndBuildability | 899 |
| SelectCitySitesAndLandmarks | 38 |
| PlanInterCityHighways | 5 |
| ApplyCityPads | 209 |
| PaintNaturalSurfaces | **38443** |
| PlaceWildernessPois | 98 |
| PlaceWildernessNature | **~170000** |

Highway A* already skipped (`PlanInterCityHighways` ~5ms). Nature dominated cold path; concurrent Nature-only re-runs also polluted `hub-perf-latest.json`.

## Changes landed

1. EasyRoads fallback removed from `BuildEasyRoadsMeshesStage`; hub Road Meshes menus + async `StartUpToRoadMeshesAsync` with vertical-slice config, no FastIteration.
2. Carve reuses ocean from `HydrologyPlan.WaterClass` (no second `BuildOceanMask`).
3. `LandformHeightmapBaker` SetHeights counters for baselines.
4. `WorldNaturePlacer` caches terrains once per `Place()`; grass gate no longer `FindObjectsByType` per sample.

## GenerateBaseLandforms speed (2026-09-04)

`LandformGenerator` now row-parallel (`Parallel.For`) with a main-thread `LandformComposeParams` snapshot (no ScriptableObject reads on workers), shared ocean-warp cache, orogen distance cull, and noise early-outs. Stage logs: `[GenerateBaseLandforms] WxH … ranges= fill= total=`.

Isolated bench (seed 16, 16 m cells, default-ish profiles, `FastLandforms=false`):

| Grid | Cells | fill / total |
|------|-------|--------------|
| Medium ~8 km (501×501) | ~251k | **189 ms** (was ~3271 ms stage baseline) |
| Large ~16 km (1001×1001) | ~1.0M | **639 ms** (was ~15 s serial estimate) |

`LandformGeneratorOrogenTests.Generate_SameSeed_IsDeterministic` passes. Two other orogen tests (`…LegacyInterior…`, `Validate_DefaultOrogenMap…`) fail with the same numbers on pre-opt code — pre-existing, not regressions.

## Next

Re-run 3× async Reset→RoadMeshes after MCP reconnect; confirm Nature ≪ 170s; then Paint / dirty-rect if still top.
