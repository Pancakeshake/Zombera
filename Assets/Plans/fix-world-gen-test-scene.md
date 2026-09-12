# Project Overview
- Game Title: Zombera
- Issue: No terrain or world gen is spawning in the `WorldGenTest` scene, although the player spawns correctly.
- Root Cause: `WorldGenerationManager` reports `MapMagicObject not found`. The current `MapMagic` GameObject in the scene is missing the required component.

# Implementation Steps

## 1. Fix WorldGenTest Scene
- Open `Assets/Scenes/Testing/WorldGenTest.unity`.
- Replace the current empty `MapMagic` GameObject with the `Assets/Prefabs/Maps/MapMagic.prefab`.
- Assign `Assets/Terrain/Graphs/City.asset` to the `MapMagicObject.graph` property on the newly instantiated prefab.
- Ensure the `WorldGenerationManager` on the `WorldManager` GameObject has its `mapMagic` reference set to the new `MapMagic` object.

## 2. Verify Generation Pipeline
- Run the scene and verify the `WorldGenerationManager` logs:
    - "Stage 1: Starting MapMagic terrain generation..."
    - "MapMagic terrain generation complete."
    - "Stage 2: Generating roads with EasyRoads..."

# Verification & Testing
- Open `Assets/Scenes/Testing/WorldGenTest.unity`.
- Press Play.
- Confirm that terrain chunks appear around the player.
- Confirm that roads and buildings are generated after terrain completion.
