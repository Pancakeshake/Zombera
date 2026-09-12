# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG set in a zombie-infested world, featuring procedural world generation.
- Players: Single player (currently in testing).
- Target Platform: PC (Windows).
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
- Scavenge, Combat, Build, Survive.
- Procedural world generation ensures a fresh experience each session.

## Controls and Input Methods
- New Input System (Keyboard/Mouse).

# UI
- UGUI-based HUD and menus.

# Key Asset & Context
- `Assets/Scripts/Testing/TestScenePlayerBootstrap.cs`: Handles player spawning in test scenes.
- `Assets/Scripts/World/WorldManager.cs`: Coordinates world generation and readiness.
- `Assets/Scripts/World/MapMagicMicroSplatTileSync.cs`: Syncs MicroSplat materials to MapMagic terrains.
- `Assets/Scenes/Testing/WorldGenTest.unity`: The test scene being fixed.

# Implementation Steps

## 1. Fix Player Spawning Timing
The player currently spawns before the procedural world is ready, causing them to fall through the map.

- **Modify `Assets/Scripts/Testing/TestScenePlayerBootstrap.cs`**:
    - Add a `WaitForWorldReady` coroutine that polls `WorldManager.IsCharacterSpawnDependencyOrderReady`.
    - Inject this wait at the beginning of `SpawnRoutine` before any instantiation or position setting occurs.
    - Ensure it handles the case where `WorldManager` is missing (skip wait).

## 2. Fix Untextured Terrain
The terrain appears untextured because the MicroSplat material sync component is missing from the test scene.

- **Modify `Assets/Scenes/Testing/WorldGenTest.unity`**:
    - Add the `MapMagicMicroSplatTileSync` component to the `[WorldManager]` GameObject.
    - Connect its `tileStreamBridge` reference to the `MapMagicTileStreamBridge` component on the same object.
    - Ensure `applyMapMagicMaterialWhenMissing` is checked.
    - On the `[MapMagic]` GameObject, set the `Terrain Settings -> Material` to `Assets/Terrain/Materials/Microsplat/MicroSplat.mat`.

## 3. Address Road Generation
Roads are likely failing to generate due to MapMagic errors or missing runtime references.

- **Verify `EasyRoadsRoadGameplayBridge` configuration**:
    - Ensure `bakeDerivedRoadDataOnSync` is enabled on the `WorldManager` object's bridge component to ensure road data is usable for gameplay.
    - Check if the `Road Gameplay Stack` is correctly recognized by `WorldManager`.

## 4. Resolve MapMagic NullReferenceException
The `WeldCorners` error in MapMagic is likely preventing the finalization of tiles, including road and layer application.

- **Modify MapMagic Settings**:
    - Open `WorldGenTest.unity`.
    - Inspect the `MapMagic` object.
    - If "Weld" or "Weld Margins" is enabled in Terrain Settings, try disabling it to bypass the `NullReferenceException` in the third-party code.

# Verification & Testing
1. **Enter Play Mode** in `WorldGenTest.unity`.
2. **Observe Logs**: Ensure `[TestScenePlayerBootstrap]` logs "Waiting for world readiness..." and then "World ready for character spawn."
3. **Verify Spawn**: Confirm the player spawns on top of the terrain once it exists.
4. **Visual Check**: Confirm the terrain has the MicroSplat material applied (not untextured).
5. **Road Check**: Check for Gizmos or visual roads (if mesh generation is enabled in the graph) to confirm road data exists in `RoadGameplayService`.
