# Project Overview 
- Game Title: Zombera
- High-Level Concept: Survival RPG with procedural world generation.
- Problem: The "Middle Tile" (0,0) is missing in `WorldGenTest` overview, and generation hangs.
- Discovery: The `WorldGenerationManager` waits for `StreamedWorldMetrics.MapMagicAllCompleteEvents`, but this metric was only updated by the `PlayerSpawner`, which is disabled in the test scene. This caused the generation pipeline to stall after Stage 1 (Terrain), resulting in no roads or buildings.

# Implementation Steps

## 1. Move Metric Recording to Bridge
- **Modify `Assets/Scripts/World/MapMagicTileStreamBridge.cs`**:
    - Subscribe to `TerrainTile.OnAllComplete`.
    - In the handler, call `StreamedWorldMetrics.RecordMapMagicAllComplete()`.
    - This ensures the generation pipeline can proceed even if no player is spawned (e.g., in test scenes or overview mode).
    - **Dependency**: None.

## 2. Clean Up `PlayerSpawner.cs`
- **Modify `Assets/Scripts/Characters/PlayerSpawner.cs`**:
    - Remove the call to `StreamedWorldMetrics.RecordMapMagicAllComplete()` from `HandleMapMagicAllComplete`.
    - The `MapMagicTileStreamBridge` will now handle this globally.
    - **Dependency**: Step 1.

## 3. Restore Standard MapMagic Settings in `WorldManager.cs`
- **Modify `Assets/Scripts/World/WorldManager.cs`**:
    - Revert the "aggressive" overview hacks.
    - Match the settings used in the working production flow (`Boot` -> `World`).
    - Specifically:
        - `mainRange = 1` and `generateRange = 1` (for a 3x3 grid).
        - `genAroundMainCam = true`.
        - Remove the `ForceMainTilesRoutine` coroutine.
    - **Dependency**: None.

## 4. Final Scene Calibration (`WorldGenTest.unity`)
- **Configure Main Camera**:
    - Set `Far Clip Plane` to `5000` (ensure terrain at y=0 is visible from y=2000).
- **Dependency**: None.

# Verification & Testing
- **Play Mode Test**: Run `WorldGenTest.unity`.
- **Metrics Check**: Confirm `StreamedWorldMetrics.MapMagicAllCompleteEvents` increments.
- **Pipeline Check**: Verify the Console shows "Road generation complete" and "Building spawn complete".
- **Visual Check**: Confirm the center tile (0,0) is present and fully populated with terrain, roads, and buildings.
