# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with procedural world generation using MapMagic and EasyRoads.
- Players: Single player.
- Target Platform: PC (Windows).
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
- Procedural world generation at startup.
- Terrain generation -> Road placement -> Building spawning.
- Scavenging and combat in the generated world.

# UI
- Loading screen progress tracking (already supported by GameManager/WorldManager).

# Key Asset & Context
- `WorldGenerationManager.cs`: New central coordinator for the generation pipeline.
- `ProceduralRoadSystem.cs`: Existing system that needs a manual trigger and refinement pass.
- `ProceduralRoadGenerator.cs`: Static utility that needs a terrain-aware refinement method.
- `StreamedMapMagicCityBuilder.cs`: Existing building system that needs a manual trigger.
- `WorldManager.cs`: Existing manager that will coordinate with the new `WorldGenerationManager`.

# Implementation Steps

## 1. Implement WorldGenerationManager
- Create `Assets/Scripts/World/WorldGenerationManager.cs`.
- Implement `IEnumerator GenerateWorldSequence()` following the two-stage pipeline:
    1. Start MapMagic generation.
    2. Wait for `TerrainTile.OnAllComplete`.
    3. Call `ProceduralRoadSystem.GenerateRoadsForFinishedTerrain()`.
    4. Call `StreamedMapMagicCityBuilder.GenerateBuildingsForFinishedRoads()`.

## 2. Refactor ProceduralRoadSystem for Staged Generation
- Add `public void GenerateRoadsForFinishedTerrain()` to `ProceduralRoadSystem.cs`.
- This method will:
    - Refine the global road plan using terrain suitability (slope/height).
    - Iterate through all active MapMagic tiles and trigger the road building logic (previously in `HandleTileApplied`).
- Update `RefreshTileStreamSubscription` to allow disabling auto-generation on tile apply if managed by `WorldGenerationManager`.

## 3. Implement Terrain-Aware Road Planning
- Add `public static void RefinePlanForTerrain(RoadNetworkRuntime network, RoadNetworkSettings settings)` to `ProceduralRoadGenerator.cs`.
- Implementation:
    - Iterate through all polylines in the network.
    - Sample terrain height at each point using `Terrain.activeTerrains` or `WorldManager.TileStreamBridge`.
    - Check for steep slopes between points.
    - Check for water (using height thresholds or biome masks if available).
    - Adjust or prune road segments that violate constraints.

## 4. Refactor StreamedMapMagicCityBuilder for Staged Generation
- Add `public void GenerateBuildingsForFinishedRoads()` to `StreamedMapMagicCityBuilder.cs`.
- This method will:
    - Wait until `RoadGameplayService` has valid road data from EasyRoads.
    - Iterate through all active tiles and trigger `HandleTileApplied` logic.

## 5. Integrate with WorldManager
- Update `WorldManager.cs` to hold a reference to `WorldGenerationManager`.
- If `useProceduralStreamingWorld` is active, call `WorldGenerationManager.StartGeneration()` during initialization.
- Ensure `IsRoadStageReady` and `IsBuildingStageReady` in `WorldManager` account for the new manager's status.

# Verification & Testing
- Start the game from the `Boot` scene.
- Observe the console logs for the generation sequence.
- Verify that MapMagic finishes first, then EasyRoads roads appear, then buildings.
- Use Scene View to inspect road placement: verify they avoid steep slopes as much as possible.
- Check that buildings are correctly aligned with the roads.
