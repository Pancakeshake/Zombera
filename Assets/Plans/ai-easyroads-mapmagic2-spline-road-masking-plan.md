# AI Plan: EasyRoads + MapMagic 2 Splines with Terrain Masking

## 1) Objective
Spawn EasyRoads roads from MapMagic 2 spline/road outputs at runtime, and stamp/mask terrain exactly where roads are generated so visuals, gameplay, and terrain data stay aligned.

## 2) Recommended Architecture (Use Existing Stack)
Use MapMagic spline output as the authoritative road source, then flow into existing runtime systems:

1. `MapMagicTileStreamBridge` emits tile apply events.
2. `ProceduralRoadSystem` reads MapMagic spline output per tile and creates EasyRoads roads.
3. `RoadTerrainStamper` deforms/paints terrain under generated road polylines.
4. `RoadGameplayService` receives sampled road data for AI/spawn/city systems.
5. `RoadGameplayTooling.MaskBake` bakes mask textures for downstream systems (MicroSplat/nav/spawn/sidewalk/decal/roadside lots).

This avoids building a second custom pipeline and matches current project structure.

## 3) What Already Exists (Confirmed)

### Runtime spline extraction + road creation
- `Assets/01_Game/02_World/Roads/ProceduralRoadSystem.SourceExtraction.cs`
  - `TryExtractMapMagicSplineRoads(...)` reads `SplineOutput200.ApplyData` and scene spline fallback.
- `Assets/01_Game/02_World/Roads/ProceduralRoadSystem.TileBuild.cs`
  - Creates `ERRoad` objects per tile.

### Terrain deformation/painting under roads
- `Assets/01_Game/02_World/Roads/RoadTerrainStamper.cs`
  - `StampRoadIntoTerrain(...)` modifies terrain heights + alphamaps around each road polyline.

### EasyRoads runtime to gameplay graph bridge
- `Assets/01_Game/02_World/Roads/EasyRoadsRoadGameplayBridge.cs`
  - Reflects EasyRoads runtime roads (`ERRoad`/`ERRoadNetwork`) and syncs center lines to gameplay graph/service.

### Mask bake tooling
- `Assets/Editor/RoadGameplayTooling.MaskBake.cs`
  - Bakes flatten/microsplat/nav/spawn/path/sidewalk/decal/roadside-lot masks from gameplay road data.
- `Assets/01_Game/02_World/Roads/RoadGameplayMaskTypes.cs`
  - Defines `RoadGameplayMaskBakeSettings` and `RoadGameplayMaskSet` assets.

### One-click setup tools
- `Assets/Editor/WorldRoadGenerationSetupTool.cs`
- `Assets/Editor/RoadNetworkSetupTool.cs`

## 4) Production Plan

## Phase A: Lock source of truth and remove dual-generation conflicts
1. Use MapMagic spline output as primary source:
   - `RoadNetworkSettings.useMapMagicSplineOutput = true`
   - `RoadNetworkSettings.fallbackToDeterministicWhenMissing = false` (for strict behavior)
2. Keep one runtime road generation owner:
   - enable `ProceduralRoadSystem`
   - disable legacy duplicate systems if still present (`RoadNetworkSystem` components)
3. Ensure scene wiring via setup tool:
   - run `Tools/1.Quick Dev Tools/Roads/Setup World Road Generation Stack`

Deliverable:
- MapMagic tile events deterministically drive one EasyRoads generation path.

## Phase B: Runtime road spawning over MapMagic splines
1. Confirm tile apply path is active:
   - `ProceduralRoadSystem.autoGenerateOnTileApply = true`
2. Configure spline sampling quality in `RoadNetworkSettings`:
   - `mapMagicSplineWidthMeters`
   - `mapMagicSplineResPerMeter`
   - `mapMagicSplineMinSamplesPerSegment`
   - `mapMagicSplineMaxSamplesPerSegment`
3. Validate junction handling and marker snapping on tile borders:
   - `markerCacheCellSizeMeters`
   - `markerSnapDistanceMeters`

Deliverable:
- EasyRoads roads appear per streamed tile and visually follow MapMagic spline lines.

## Phase C: Terrain stamping/masking under spawned roads (runtime)
1. Enable terrain deformation/paint in `RoadNetworkSettings`:
   - `applyTerrainDeformationAndPaint = true`
   - tune `terrainShoulderMeters` and `terrainBlendFalloff`
2. Ensure terrain has intended road splat layer naming (or explicit mapping task in Phase E).
3. Validate per-tile query margins for border continuity:
   - `tileQueryMarginMeters`

Deliverable:
- Terrain height and splat texture are stamped where roads are generated, including shoulders.

## Phase D: Bake reusable global masks for MapMagic/MicroSplat/gameplay
1. Build/update gameplay graph from authoring/runtime data.
2. Bake mask set via tooling:
   - `Tools/3. Roads/... Bake Selected Graph Masks`
3. Use outputs from `RoadGameplayMaskSet` in your world materials/graph workflows:
   - `terrainFlattenMask`
   - `microSplatPaintMask`
   - `navModifierMask`
   - `spawnDensityMask`
   - `pathAreaMask`
   - `sidewalkMask`
   - `decalPlacementMask`
   - `roadsideLotMask`

Deliverable:
- Stable textures drive texturing/nav/spawn systems consistently beyond immediate runtime stamping.

## Phase E: Hardening for large streaming worlds
1. Add explicit terrain layer index mapping (instead of name heuristic) for road paint robustness.
2. Add seam-safe blending pass near tile edges to avoid visible cut lines.
3. Add road sync observability thresholds in `EasyRoadsRoadGameplayBridge` (already has spike/debounce controls).
4. Add validation command that checks:
   - MapMagic spline output present
   - bridge bound
   - terrain stamping enabled
   - mask assets assigned/generated

Deliverable:
- Predictable behavior under streaming stress and team-proof setup.

## 5) Key Settings Baseline (Starting Point)
`RoadNetworkSettings`:
1. `useMapMagicSplineOutput = true`
2. `fallbackToDeterministicWhenMissing = false`
3. `applyTerrainDeformationAndPaint = true`
4. `spawnRoadMeshes = true`
5. `mapMagicSplineResPerMeter = 0.14` (adjust after profiling)
6. `terrainShoulderMeters = 2.0`
7. `terrainBlendFalloff = 0.65`

`ProceduralRoadSystem`:
1. `autoGenerateOnTileApply = true`
2. `autoResolveReferences = true` (until scene is stable)

## 6) Validation Checklist
1. New tile streams in: roads appear within expected debounce window.
2. Road centerline aligns with MapMagic spline lines.
3. Terrain under road is flattened/blended; no floating roads.
4. Tile borders show no major seam pops in road height/paint.
5. `RoadGameplayService.RuntimeSamples` is populated and consistent with road mesh layout.
6. Mask textures bake successfully and are non-empty for known roaded regions.

## 7) Rollout Strategy
1. Stage 1: Editor-only bake and scene setup validation in one test world.
2. Stage 2: Enable runtime tile-driven spawn on a limited streaming radius.
3. Stage 3: Enable full-world streaming and monitor spike logs.
4. Stage 4: Lock settings into default assets/prefabs and document one-button setup path for team.

## 8) Best Option Decision
Best production option is integration, not replacement:
1. Keep MapMagic spline outputs as source roads.
2. Use existing `ProceduralRoadSystem` + `RoadTerrainStamper` for runtime road + terrain stamping.
3. Use existing `RoadGameplayTooling.MaskBake` for durable mask outputs feeding world shading/nav/spawn systems.

This gives the lowest implementation risk and highest consistency with your current architecture.