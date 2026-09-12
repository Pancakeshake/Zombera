# Project Overview
- Game Title: Zombera
- High-Level Concept: Testing setup for the new two-stage world generation pipeline and character flow.
- Purpose: Bypass Boot and MainMenu for rapid iteration on world generation.

# Implementation Steps

## 1. Create QuickWorldTestBootstrapper.cs
- Implement a script that ensures `GameManager` and `ProceduralWorldSession` are active.
- Place it in `Assets/Scripts/Testing/QuickWorldTestBootstrapper.cs`.

## 2. Create WorldGenTest.unity Scene
- Use a `RunCommand` script to:
    1. Create a new scene `Assets/Scenes/Testing/WorldGenTest.unity`.
    2. Instantiate the `[GameManager]` prefab.
    3. Add a `MapMagic` object (if a prefab exists or via component).
    4. Add a `WorldManager` and configure it.
    5. Add the `WorldGenerationManager`.
    6. Run the `WorldRoadGenerationSetupTool.SetupWorldRoadGeneration` logic to wire up roads/city.
    7. Add `QuickWorldTestBootstrapper`.
    8. Add `TestScenePlayerBootstrap` and configure it with the `Player` prefab and a spawn point.

## 3. Configuration
- Set `WorldGenerationManager.generateOnStart = false` (managed by bootstrapper).
- Set `ProceduralRoadSystem.autoGenerateOnTileApply = false`.
- Set `StreamedMapMagicCityBuilder.autoGenerateOnTileApply = false`.

# Verification & Testing
- Open `Assets/Scenes/Testing/WorldGenTest.unity`.
- Press Play.
- Verify:
    1. Console shows "ProceduralWorldSession started".
    2. MapMagic generates terrain.
    3. `WorldGenerationManager` logs "Stage 2: Generating roads...".
    4. `WorldGenerationManager` logs "Stage 3: Spawning buildings...".
    5. `TestScenePlayerBootstrap` spawns the player after world is ready.
