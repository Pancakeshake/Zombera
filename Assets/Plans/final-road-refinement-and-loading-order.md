# Project Overview
- Goal: Refine road pathing to avoid sharp terrain cliffs and implement an ordered spawning sequence (Terrain -> Road -> NavMesh) that completes during the loading phase to minimize runtime lag.

# Problem Analysis
1. **Wall-like Roads**: Current roads create sharp terrain cliffs. This is caused by insufficient path relaxation and steep vertical transitions.
2. **Runtime Lag**: Generating roads and NavMesh during gameplay causes frame rate spikes.
3. **Incorrect Order**: NavMesh bakes often start before roads have finished deforming the terrain, leading to incorrect AI paths.

# Proposed Design

## 1. High-Resolution Road Refinement
- **Horizontal Path Relaxation**: Implement a multi-pass gradient descent algorithm in `ProceduralRoadSystem` that nudges road points toward local flat spots within a 20m radius.
- **Vertical Smoothing**: Apply a Laplacian smoothing pass to road heights to ensure a smooth grade.
- **Wider Shoulders**: Update `RoadNetworkSettings` to default to a 4-6m shoulder, allowing for a gentler transition from road to terrain.

## 2. Ordered Spawning Sequence
- **Dependency Pipeline**:
    1. **Terrain (MapMagic)**: Finishes tile generation.
    2. **Roads (ProceduralRoadSystem)**: Listens to MapMagic `TileApplied`. Refines path, builds EasyRoads mesh, and deforms terrain. Fires a new `RoadsApplied` event.
    3. **NavMesh (StreamingNavMeshTileService)**: Listens for `RoadsApplied`. Bakes NavMesh for the modified terrain.
- **Loading Phase Enforcement**: Update `WorldManager` to track `NavMesh` as a critical spawn dependency. The loading screen will remain active until the initial burst of NavMesh bakes is complete.

# Implementation Steps

## Step 1: Update ProceduralRoadSystem.cs
- Add `public event Action<TerrainTile> RoadsApplied;`.
- Enhance `RefineRoadPoints` with a 20m scan radius and 8-direction neighbor sampling.
- Implement a 3-pass Laplacian vertical smoothing.
- Ensure `BuildRoadNetwork` is called before firing the `RoadsApplied` event.

## Step 2: Update StreamingNavMeshTileService.cs
- Modify the event subscription: If a `ProceduralRoadSystem` is present, unsubscribe from `TerrainTile.OnTileApplied` and subscribe to `ProceduralRoadSystem.RoadsApplied`.
- This guarantees that the NavMesh captures the "flattened" terrain under the roads.

## Step 3: Update WorldManager.cs
- Add `NavMesh` to the `CharacterSpawnDependencyStage` enum.
- Implement `IsNavMeshStageReady()` which checks `StreamingNavMeshTileService.HasAnyBakedTiles` or specific tile counts.
- Update `IsCharacterSpawnDependencyOrderReady` to include the NavMesh stage.
- Ensure `SetSimulationActive(true)` only happens after the NavMesh stage is ready for the player's starting location.

# Verification & Testing
1. **Visual Smoothing**: Check that roads no longer create vertical walls in the terrain.
2. **Order Check**: Verify console logs: `MapMagic Applied` -> `Created Roads` -> `Bake NavMesh`.
3. **Loading Transition**: Confirm the loading screen stays up until the NavMesh is ready for the player to walk on.
