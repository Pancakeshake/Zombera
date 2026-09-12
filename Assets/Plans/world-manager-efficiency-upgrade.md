# Project Overview
- Game Title: Zombera
- World System: Procedural streaming via MapMagic and EasyRoads3D.
- Central Manager: `WorldManager.cs` handles coordinate streaming, simulation, and dependency tracking.

# Problem Analysis
The current `World` scene structure is fragmented. `WorldManager` resides on one object, while procedural runtime bridges (`MapMagicTileStreamBridge`, `StreamingNavMeshTileService`) reside on `ProceduralWorldRuntime`. 
The `WorldManager` script spends CPU cycles every 2 seconds (`_nextReferenceResolveAt`) performing `FindFirstObjectByType` scans to find missing references like the `EasyRoadsRoadGameplayBridge` or `StreamingNavMeshTileService`. Additionally, some components required for roads (like `EasyRoadsMapMagicSync`) are missing entirely.

# Proposed "100% Efficient" Plan

## 1. Structural Consolidation
Move all world-related managers into a single, cohesive hierarchy. This improves Inspector visibility and allows for direct reference assignment, eliminating runtime searching.

### Recommended Hierarchy:
- **`[WORLD_SYSTEMS]`** (Persistent Root)
    - `WorldManager` (The Brain)
    - `RoadGameplayService` (The Road Stack Root)
        - `MapMagicTileStreamBridge` (Spline/Tile Input)
        - `EasyRoadsMapMagicSync` (EasyRoads Mesh Generator)
        - `EasyRoadsRoadGameplayBridge` (Gameplay Graph Output)
    - `StreamingNavMeshTileService` (NavMesh Logic)
    - `ZombieManager` (Ambient Spawning)

## 2. Reference Hard-Wiring
Assign all serialized references in the Inspector on the `WorldManager` and `RoadGameplayService` objects.
- Set `autoResolveReferences` to `false` on `EasyRoadsRoadGameplayBridge` after assigning.
- Ensure `WorldManager` has its `tileStreamBridge`, `easyRoadsRoadBridge`, `easyRoadsMapMagicSync`, and `roadGameplayService` fields explicitly assigned.

## 3. Road Stack Tuning
Configure the `RoadGameplayService` to use the "Unified Road Stack" mode:
- Enable `useSingleRoadGameplayStackObject` on `WorldManager`.
- Ensure the `RoadGameplayService` object contains the `tileStreamBridge`, `easyRoadsRoadBridge`, and `easyRoadsMapMagicSync` components.

## 4. Performance Tuning
- **Update Frequency**: The `WorldManager` already uses staggered intervals (`0.25s` for streaming, `10s` for simulation). These are efficient.
- **Suppression Optimization**: Increase `easyBuildRuntimeRescanSeconds` from `10s` to `30s` or `60s` if the world is large, as the `FindObjectsByType<MonoBehaviour>` scan is expensive.

# Key Asset & Context
- **`WorldManager.cs`**: The central coordinator.
- **`EasyRoadsMapMagicSync.cs`**: Required for procedural road mesh creation.
- **`RoadGameplayService.cs`**: The data holder for the road network.

# Implementation Steps

## Step 1: Consolidate the "ProceduralWorldRuntime"
- **Action**: Rename `ProceduralWorldRuntime` to `[WORLD_SYSTEMS]`.
- **Action**: Move the `WorldManager` component (and any other loose managers like `ZombieManager`) onto this object or its immediate children.
- **Dependency**: None.

## Step 2: Wire the Unified Road Stack
- **Action**: Create or locate the `RoadGameplayService` object as a child of `[WORLD_SYSTEMS]`.
- **Action**: Add `MapMagicTileStreamBridge`, `EasyRoadsMapMagicSync`, and `EasyRoadsRoadGameplayBridge` to the `RoadGameplayService` object.
- **Action**: Manually assign the references between these components in the Inspector to bypass the `ResolveRuntimeReferences` logic.
- **Dependency**: Step 1.

## Step 3: Run the Setup Tool
- **Action**: Run `Tools > 1.Quick Dev Tools > Roads > Setup World Road Generation Stack`.
- **Action**: This will ensure the `GameplayRoadGraph` and `GameplayRoadDerivedData` assets are correctly linked.
- **Dependency**: Step 2.

## Step 4: Validate in Play Mode
- **Action**: Enter Play Mode and check the `WorldManager` Inspector.
- **Action**: Verify `IsSimulationActive` is true and no "Missing Reference" warnings appear in the console every 2 seconds.

# Verification & Testing
1. **Reference Check**: Ensure `WorldManager` fields are all "green" (assigned) in the Inspector.
2. **Road Generation**: Confirm that EasyRoads meshes appear on the MapMagic tiles at runtime.
3. **CPU Profiling**: Ensure `WorldManager.Update` does not show spikes from `ResolveRuntimeReferences`.
4. **Boot Sequence**: Start from the `Boot` scene to verify the `WorldManager` correctly initializes with the persistent systems.
