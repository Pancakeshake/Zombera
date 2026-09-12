# Project Overview
- Game Title: Zombera
- World Management Strategy: Move `WorldManager` to the `Boot` scene to serve as a persistent, authoritative world coordinator.

# Game Mechanics
## Core Gameplay Loop
The `WorldManager` coordinates chunk streaming, procedural world seeding, and integration with MapMagic, City, and Road systems. Moving it to `Boot` ensures that world session state (like the seed) persists and is ready before the world scene loads.

# Implementation Steps

## 1. Prepare WorldManager for Persistence
- **File:** `Assets/Scripts/World/WorldManager.cs`
- **Change:** 
    - Update `EnsureProceduralStreamingBridge`, `EnsureStreamedCityBuilderProvisioned`, and `EnsureEasyRoadsRoadBridgeProvisioned` to use `Object.FindFirstObjectByType` instead of just `GetComponent` when searching for their respective bridge/builder components. This allows the persistent `WorldManager` to find these components in the `World` scene after it loads.
    - Ensure `ResolveRuntimeReferences` handles all serialized systems as runtime-resolved dependencies.
    - Verify `IsSimulationActive` logic to ensure ticks don't run while in the Main Menu.

## 2. Relocate WorldManager to Boot Scene
- **Action:**
    - Create a prefab for the `WorldManager` object currently in the `World` scene (to preserve its settings).
    - Open `Assets/Scenes/Boot.unity`.
    - Add the `WorldManager` prefab as a child of `CoreRoot` (which is marked `DontDestroyOnLoad` by `GameManager`).
    - Save the `Boot` scene.

## 3. Clean up World Scene
- **Action:**
    - Open `Assets/Scenes/World.unity`.
    - Remove the `WorldManager` GameObject.
    - Save the `World` scene.

## 4. Update GameManager Reference
- **Action:**
    - In the `Boot` scene, select the `CoreRoot` object.
    - In the `GameManager` component, assign the new `WorldManager` child object to the `worldManager` field.
    - This ensures `GameManager` has a direct, reliable reference to the authoritative manager.

# Key Asset & Context
- `Assets/Scripts/World/WorldManager.cs`: Main script to modify.
- `Assets/Scenes/Boot.unity`: Target scene for the manager.
- `Assets/Scenes/World.unity`: Current (to be removed) location.

# Verification & Testing
1. **Startup Flow:** Start the game from the `Boot` scene. Verify that `WorldManager` is present in `DontDestroyOnLoad`.
2. **Transition to World:** Enter the game from the Main Menu. Verify that `WorldManager.InitializeWorld()` is called and correctly resolves dependencies (ChunkLoader, TileStreamBridge, etc.) in the `World` scene.
3. **Procedural Consistency:** Verify that the World Seed is correctly applied and consistent.
4. **Building Suppression:** Test that building input is correctly suppressed if `forceWorldSpawnedBuildingsOnly` is enabled.
