# Project Overview
- Game Title: Zombera
- High-Level Concept: Procedural world survival with RTS and Building elements.
- Render Pipeline: URP
- Key Systems: MapMagic, Crest Ocean, Enviro 3, UMA.

# Problem Analysis
The `World.unity` scene has a flat hierarchy with many top-level systems, making it difficult to manage. Additionally, some core logic (like the Player object) is placed directly in the scene, which can conflict with procedural spawning and centralization (Boot scene). The user also wants to know about the safety of reorganization.

# Recommendations

## 1. Hierarchy Reorganization (Logic & Workflow Upgrade)
Group top-level objects into "Category Roots" (Empty GameObjects) to improve scene navigation and logic grouping.

### Proposed Structure:
- **`[SYSTEMS]`**: Group `Enviro 3`, `MapMagic`, `ProceduralWorldRuntime`, `RoadGameplayService`, `Building Manager`, `RTS Control Stack`.
- **`[ENVIRONMENT]`**: Group `Crest Water Body`, `Crest Ocean`, `Environment` (Lighting/Camera).
- **`[UI]`**: Move `WorldHUDCanvas` here.
- **`[RUNTIME_CONTAINERS]`**: Use this as a parent for objects spawned at runtime (e.g., `RuntimeWorldSystems`, `PlacedWalls`).

**Warning**: If you move `RuntimeWorldSystems` or `PlacedWalls` into a parent, ensure you do **not** rename them, as scripts like `WorldManager` and `BuildPlacementController` search for them by name using `GameObject.Find`.

## 2. Character Logic Upgrade
The `Player` object currently lives in the `World` scene. 
- **Recommendation**: Turn the `Player` object into a Prefab (if it isn't already) and ensure it is spawned via the `PlayerSpawner`. 
- **Reasoning**: This ensures that when you start from the `Boot` scene, you don't end up with a "stale" player in the world or a duplicate if the spawner also runs.

## 3. Visual/Styling Upgrades
- **Lighting Integration**: Ensure `Enviro 3` is driving the environment lighting. Check that the `Directional Light` (Sun) has the `EnviroVolumetricFogLight` component active.
- **Crest Ocean**: Ensure the ocean's material/lighting is synced with `Enviro`'s time-of-day.
- **Global Volume**: Add or refine a URP `Volume` (in the `Environment` root) with:
    - **Bloom**: For high-intensity light effects.
    - **Tonemapping**: Use 'ACES' for a more cinematic look.
    - **Color Adjustments**: Slight saturation and contrast boost to fit the "Zombera" tone.

## 4. Asset Folder Management (Moving Files)
- **Moving Assets (Project Window)**: You can safely move script files, prefabs, textures, and materials into new folders. Unity tracks these by GUID, so references will **not** break.
- **Logic Folders**: I recommend a structure like `Assets/Scripts/Systems/[SystemName]` and `Assets/Prefabs/Environment`.

# Implementation Steps

## Step 1: Hierarchy Cleanup
1. Create the `[SYSTEMS]`, `[ENVIRONMENT]`, and `[UI]` empty root objects in the `World` scene.
2. Parent the existing managers and environment objects into their respective categories.
3. **Verify**: Run the scene and check for "Object reference not set" or "GameObject not found" errors in the console.

## Step 2: Player Prefabrication
1. If the `Player` root object in the scene has custom settings, apply them to the `Player` prefab.
2. Remove the `Player` object from the `World` scene.
3. Ensure `PlayerSpawner` is correctly configured to spawn the player prefab at the `spawnPoint`.

## Step 3: Visual Polish
1. Select the `Global Volume` (or create one).
2. Tweak Tonemapping and Bloom to enhance the "Enviro 3" lighting.

# Verification & Testing
1. **Boot Test**: Start from the `Boot` scene. Verify the world loads, lighting looks correct, and the player spawns via the `PlayerSpawner`.
2. **Logic Check**: Verify that `SelectionManager` and `BuildingManager` still function correctly after being parented under `[SYSTEMS]`.
3. **UI Check**: Verify `WorldHUDCanvas` still renders and responds to input.
