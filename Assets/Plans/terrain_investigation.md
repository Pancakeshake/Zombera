# terrain_investigation.md (Updated)

## Current Status
- Redundant components were cleaned up in a previous step.
- Mismatch in terrain resolution and world seed settings identified.
- Missing `StreamingNavMeshTileService` identified as the cause of "Nav Mesh" stage timeouts.

## Implementation Goal
Fix the "disrupted startup" (timeouts) and ensure the "production" terrain exactly matches the "generator" style.

## Key Discrepancies Found
- **Terrain Resolution:** Production is at 1025 (enum index 5), while Generator is at 513 (enum index 4). Higher resolution causes timeouts and might look "wrong" before final passes complete.
- **World Seeds:** Production had randomized seeds and a fixed radius, while Generator uses seed `12345` and radius `0`.
- **NavMesh Service:** `StreamingNavMeshTileService` is missing in Production, causing a 45s timeout during world loading.

## Implementation Steps
1. **Match MapMagic Settings in World.unity**:
    - Change `tileResolution` from `5` to `4` (513).
    - Set `mainRange` and `genRange` to `1` (matching generator).
2. **Match WorldManager Settings in World.unity**:
    - Set `worldSeed` to `12345`.
    - Set `randomizeWorldSeedEachSession` to `false`.
    - Set `randomMapMagicStartCoordRadius` to `0`.
3. **Add NavMesh Service in World.unity**:
    - Add `StreamingNavMeshTileService` to the `WorldManager` GameObject.
    - Assign `playerSpawner` (find `PlayerSpawner` in scene).
    - Assign `roadSystem` (find `ProceduralRoadSystem` in scene).
4. **Final Sync Validation**:
    - Ensure `MapMagicMicroSplatTileSync` is wired to the `MapMagicTileStreamBridge` on `WorldManager`.

## Verification & Testing
- Start from `Boot.unity`.
- Confirm `LoadingWorld` stage "Terrain" completes quickly.
- Confirm "Nav Mesh" stage completes without timeout.
- Visually verify biomes match the generator's seed `12345` appearance.
