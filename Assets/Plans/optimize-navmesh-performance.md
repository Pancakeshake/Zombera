# Project Overview
- Game Title: Zombera
- High-Level Concept: Zombie survival with procedural world streaming (MapMagic).
- Players: Single player
- NavMesh System: Custom `StreamingNavMeshTileService` using `NavMeshBuilder`.

# Problem Analysis
The `StreamingNavMeshTileService` is reporting "Slow BuildNavMeshData" and "Slow tile bake" warnings. Individual tile bakes are taking between 500ms and 800ms. Since these bakes happen on the main thread (synchronously via `NavMeshBuilder.BuildNavMeshData`), they cause significant frame hitches during world streaming.

The current `navMeshVoxelSize` of `0.35f` is likely too fine for the terrain tiles being processed, leading to high computation costs.

# Proposed Changes

## 1. Optimize NavMesh Tuning Parameters
- **Increase Voxel Size**: Increase `navMeshVoxelSize` from `0.35` to `0.5` or `0.6`. This is the most effective way to reduce bake time. Voxel size has an exponential impact on Recast bake performance.
- **Adjust Tile Size**: Ensure `navMeshTileSize` is optimized for the terrain tile size. If terrain tiles are large, a slightly larger `navMeshTileSize` (e.g., 512) might reduce boundary complexity, though `0.35` to `0.5` voxel size is the priority.
- **Reduce Vertical Extent**: If the terrain isn't extremely mountainous, reducing `navMeshVerticalHalfExtent` (currently `180m`) can reduce the volume Recast has to analyze.

## 2. Refine Throttling and Budgeting
- **Increase Bake Cooldown**: If hitches are still noticeable, increase `minSecondsBetweenTileApplyBakes` to spread bakes further apart.
- **Limit Rebuild Rate**: Ensure `rebuildTilesPerFrame` is kept low (1) during initial load to prevent blocking the loading screen for too long.

## 3. Code Optimization (Optional/Advanced)
- **Async Baking**: The current implementation is synchronous. While `BuildNavMeshData` is used, the system could be refactored to use `NavMeshBuilder.UpdateNavMeshData` with an `AsyncOperation` to move the heavy lifting off the main thread, though this requires more complex state management for tile registration.

# Implementation Steps

## Step 1: Update StreamingNavMeshTileService.cs Defaults
- **File**: `Assets/Scripts/World/StreamingNavMeshTileService.cs`
- **Change**: 
    - Update `navMeshVoxelSize` default to `0.5f`.
    - (Optional) Increase `tileBakeSpikeWarningMilliseconds` to `250f` and `perAgentBakeSpikeWarningMilliseconds` to `200f` to reflect a more realistic "slow" threshold for a streaming game, once bakes are faster.

## Step 2: Update PlayerSpawner.cs Defaults
- **File**: `Assets/Scripts/Characters/PlayerSpawner.cs`
- **Change**:
    - Update `navMeshVoxelSize` default to `0.5f` to match the service.

## Step 3: Inspector Synchronization
- The user should apply these changes to the `StreamingNavMeshTileService` and `PlayerSpawner` components on the `GameManager` or `GlobalSystems` object in the `Boot` or `World` scene, as serialized values will override script defaults.

# Verification & Testing
1. **Performance Check**: Run the game and watch the console logs during world streaming.
2. **Hitch Analysis**: Verify that the "Slow BuildNavMeshData" warnings occur less frequently and with lower `elapsedMs` values (targeting < 200ms).
3. **Pathfinding Quality**: Ensure zombies and squad members can still navigate the terrain correctly with the increased voxel size.
4. **Loading Time**: Verify that the "Nav Mesh" loading stage in `GameManager` completes faster.
