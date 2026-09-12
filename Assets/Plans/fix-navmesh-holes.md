# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG in a zombie-infested world with procedural terrain and streaming NavMesh.
- Players: Single player (based on character selection and squad mechanics).
- Inspiration / Reference Games: Survival RPGs (e.g., DayZ, Project Zomboid).
- Tone / Art Direction: Realistic/Gritty URP.
- Target Platform: PC (Windows).
- Screen Orientation / Resolution: Landscape.
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
Scavenge, combat, build, and survive in an open world with procedural elements. NavMesh is essential for AI (Zombies and Squad) movement.
## Controls and Input Methods
New Input System; third-person controls.

# UI
NavMesh visualization is visible in the provided screenshot, showing "holes" (missing blue overlay) on terrain.

# Key Asset & Context
- **PlayerSpawner.cs**: Defines the NavMesh baking settings (Voxel Size, Max Slope, Min Region Area, etc.) used by the streaming service.
- **StreamingNavMeshTileService.cs**: Performs the actual baking of NavMesh tiles at runtime using settings from `PlayerSpawner`.
- **World.unity**: The main gameplay scene where the `[PlayerSpawner]` and `[WorldManager]` (containing the streaming service) reside.

# Implementation Steps
1. **Analyze Current Settings**:
   - The current `navMeshMinRegionArea` (1.25) and `streamingNavMeshMinRegionAreaFloor` (0.6) are likely too high, causing small walkable patches on terrain to be pruned, resulting in "holes".
   - The `navMeshMaxSlopeDegrees` (55) may be excluding some intended walkable slopes.
   - The `streamingNavMeshVoxelScale` (1.6) results in a coarse voxel size (~0.56m), which can lead to inaccuracies on uneven terrain.

2. **Update PlayerSpawner Settings in World Scene**:
   - **Reduce Pruning**: Set `navMeshMinRegionArea` to `0.1` and `streamingNavMeshMinRegionAreaFloor` to `0.1`. This will stop the NavMesh builder from discarding small disconnected regions, which often appear as "holes" on slopes or complex terrain.
   - **Improve Slope Coverage**: Increase `navMeshMaxSlopeDegrees` from `55` to `60` to ensure moderately steep but walkable areas are included.
   - **Maintain Performance**: Keep `useCoarseStreamingNavMeshTuning` enabled and `streamingNavMeshVoxelScale` at `1.6` as requested by the user to balance performance.

3. **Verify Settings Propagation**:
   - Ensure the `StreamingNavMeshTileService` on `[WorldManager]` is correctly receiving these updated settings from `PlayerSpawner` during its initialization/configuration phase.

# Verification & Testing
1. **In-Editor Validation**:
   - Select the `[PlayerSpawner]` in the `World` scene and verify the updated values.
2. **Runtime Verification**:
   - Enter Play Mode from the `Boot` scene.
   - Navigate to the `World` scene.
   - Use the NavMesh visualization (or the log diagnostics in `StreamingNavMeshTileService`) to confirm that the NavMesh is now more continuous and lacks the "patchy" holes seen in the screenshot.
   - Specifically, check areas that previously had orange holes to see if they are now covered by the blue NavMesh overlay.
3. **Performance Check**:
   - Monitor the console for any "Slow tile bake" warnings from `StreamingNavMeshTileService` to ensure the performance balance is maintained.
