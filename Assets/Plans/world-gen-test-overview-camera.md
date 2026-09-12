# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with procedural world generation.
- Task: Remove player spawning in the `WorldGenTest` scene and configure the Main Camera as an overview camera for multiple tiles.

# Game Mechanics
- Procedural generation should continue to function, following the Main Camera instead of a player.
- Handcrafted towns and roads should still spawn.

# UI
- No changes to UI expected, though the loading screen might finish faster or skip the player stage.

# Key Asset & Context
- `Assets/Scenes/Testing/WorldGenTest.unity`: The scene to modify.
- `Assets/Scripts/Testing/TestScenePlayerBootstrap.cs`: Spawns the player in test scenes.
- `Assets/Characters/PlayerFollowCamera.cs`: Makes the camera follow the player.

# Implementation Steps

## 1. Scene Modification (WorldGenTest)
- **Identify and Disable Player Bootstrap**:
    - Locate the `PlayerBootstrap` GameObject in `WorldGenTest`.
    - Disable the `TestScenePlayerBootstrap` component.
- **Configure Overview Camera**:
    - Locate the `Main Camera`.
    - Disable the `PlayerFollowCamera` component.
    - Set `Transform.position` to `(0, 2000, 0)` (High overview).
    - Set `Transform.rotation` to `(90, 0, 0)` (Top-down view).
    - Set `Camera.farClipPlane` to `10000` to ensure multiple tiles are visible.

## 2. Verify MapMagic Tracking
- MapMagic is already configured in `WorldManager.cs` to follow the `Main Camera` (`tiles.genAroundMainCam = true`).
- Since no player will be spawned, MapMagic will default to the `Main Camera` for tile generation.

## 3. Manager Verification
- Ensure `WorldGenerationManager` and `GameManager` (if present) do not hang.
- `GameManager` logic already skips the player loading stage if no `PlayerSpawner` is found.

# Verification & Testing
1. **Play Mode Test**: Enter Play Mode in `WorldGenTest.unity`.
2. **Generation**: Confirm that terrain, roads, and towns still generate correctly.
3. **Viewpoint**: Confirm the camera remains at its high overview position and doesn't follow any objects.
4. **Logs**: Check for any null reference exceptions or warnings from camera/player systems.
