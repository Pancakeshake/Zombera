# Project Overview
- Goal: Fix city road alignment (intersections), normalize city road heights, prevent building/road overlap, and shape cities to avoid mountains. Move roads before NavMesh in the loading sequence.

# Problem Analysis
1. **Grid Discontinuity**: City roads are spawned as independent polylines that overlap but don't "connect," leading to visual artifacts.
2. **Layered Intersections**: Roads crossing at different heights because they strictly follow rugged terrain.
3. **Mountain Cities**: The city grid cuts through steep mountains instead of stopping or winding around them.
4. **Incorrect Loading Order**: NavMesh is baking before roads have deformed the terrain.

# Proposed Design

## 1. City Shaping & Mountain Avoidance
- **Slope Filtering**: In `ProceduralRoadSystem.HandleTileApplied`, before spawning a `Local` road segment, calculate the average slope of the terrain under it.
- **Culling**: If the slope exceeds `maxCityRoadSlopeDegrees` (default 10°), the segment is discarded. This forces the city to stop at the base of mountains.

## 2. Height Normalization & Plateauing
- **City Flattening**: For `Local` roads, the system will sample the terrain height at the segment's start and end, and interpolate a flat grade between them.
- **Intersection Alignment**: This ensures that when two city roads cross, they are at exactly the same altitude.

## 3. Intersection Logic (EasyRoads v3)
- **Node Snapping**: Implement a `MarkerCache` to track road endpoints.
- **Auto-Connection**: If a road point is within 4m of another, they snap to the same position, and `ConnectToStart/End` is called to create a proper EasyRoads intersection mesh.

## 4. Building Avoidance
- **Dynamic Setback**: Update `StreamedMapMagicCityBuilder` to calculate setback as `roadWidth * 0.5f + buildingPadding`. This ensures buildings never clip into the asphalt.

## 5. Loading Sequence (Terrain -> Road -> NavMesh)
- **GameManager Order**: Swap the `NavMesh` and `Roads` loading steps.
- **Progress UI**: Update the loading bar to show specific road generation progress.

# Implementation Steps

## Step 1: Update ProceduralRoadSystem.cs
- Implement `IsCitySegmentValid` and `NormalizeCityHeights`.
- Implement `markerCache` and `ConnectRoadsAtJunctions` logic.

## Step 2: Update StreamedMapMagicCityBuilder.cs
- Update `SpawnLotsAlongRoad` to respect the road's half-width for its setback calculation.

## Step 3: Update GameManager.cs
- Swap the order of `WaitForNavMeshLoadingStage` and the `Roads` stage in `BeginWorldSessionRoutine`.
- Update `BuildRoadStageStatus` to check `proceduralRoadSystem.ProcessedTileCount`.

## Step 4: Settings Tune
- Reduce `maxCityRoadSlopeDegrees` to 10f in `RoadNetworkSettings`.

# Verification & Testing
1. **Visual City**: Verify city roads form clean intersections (crosses/T-junctions) rather than overlapping sticks.
2. **Mountain Flow**: Confirm city grid stops at steep slopes.
3. **Loading Sequence**: Check console: `Terrain complete` -> `Roads complete` -> `NavMesh complete`.
