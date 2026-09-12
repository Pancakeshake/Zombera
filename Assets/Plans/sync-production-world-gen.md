# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with procedural world generation and base building.
- Players: Single player
- Target Platform: PC (StandaloneWindows64)
- Render Pipeline: URP

# Game Mechanics
## Core Gameplay Loop
The game relies on a procedural world generated via MapMagic and custom systems (Roads, Buildings). The production workflow involves booting from the `Boot` scene, which initializes the `GameManager` and transitions to the `World` scene.

# Key Asset & Context
- **World.unity**: The main production scene.
- **1_World_Generation.unity**: The reference scene where world generation is "correct".
- **WorldManager.cs**: Manages the world initialization and generation trigger.
- **WorldGenerationManager.cs**: Executes the staged generation pipeline (Terrain -> Roads -> Buildings).
- **ProceduralRoadSystem.cs**: Procedural road generation using EasyRoads (Stage 2).

# Problem Analysis
The user reports that while the "world generation" (test scene) is correct, the "main production workflow" is not working the same way. 
Comparison reveals that in the "correct" test scene (`1_World_Generation`), the `WorldGenerationManager` component has **no roadSystem assigned**. In the production scene (`World.unity`), it has a `ProceduralRoadSystem` assigned. 
The `WorldGenerationManager` pipeline (Stage 2) waits for `roadSystem.ProcessedTileCount > 0`. If no roads are processed (e.g., due to configuration or range issues), the pipeline hangs, preventing Stage 3 (Buildings) and the final "Ready" state, which in turn causes the `GameManager` to time out during loading.

# Implementation Steps
1. **Analyze World.unity Configuration**:
   - Inspect the `WorldGenerationManager` component on the `WorldManager` object in `Assets/Scenes/World.unity`.
   - Inspect the `ProceduralRoadSystem` component on the `RoadGameplayService` object.
2. **Synchronize Production to Test Setup**:
   - In `Assets/Scenes/World.unity`, unassign the `roadSystem` field from the `WorldGenerationManager` component to match the `1_World_Generation.unity` setup.
   - This ensures Stage 2 (Roads) is skipped or handled as a pass-through, avoiding the hang on `ProcessedTileCount`.
3. **Verify Reference Wiring**:
   - Ensure `mapMagic` in `WorldGenerationManager` is correctly assigned or left null (to be found at runtime) as per the test scene.
   - Ensure `townSpawner` and `cityBuilder` are correctly assigned to the `WorldManager` instance.
4. **GameManager Validation**:
   - Verify that `GameManager` correctly calls `WorldManager.InitializeWorld()` during the loading sequence.

# Verification & Testing
1. **Play from Boot**: Run the game from `Assets/Scenes/Boot.unity`.
2. **Observe Loading Sequence**: Ensure the "Initializing world systems..." overlay progresses and eventually vanishes as the state transitions to `Playing`.
3. **Inspect World State**: In the `World` scene, verify that Terrain, Roads (from MapMagic/EasyRoadsBridge), and Buildings are present.
4. **Check Console**: Monitor for `[WorldGenerationManager] World generation pipeline finished.` log.
