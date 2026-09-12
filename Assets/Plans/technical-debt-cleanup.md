# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG set in a zombie-infested world, combining third-person combat, base building, and procedural world elements.
- Players: Single player
- Inspiration / Reference Games: 7 Days to Die, Project Zomboid, Kenshi
- Tone / Art Direction: Realistic/Gritty, URP-based.
- Target Platform: PC (Windows)
- Screen Orientation / Resolution: Landscape 1920x1080
- Render Pipeline: URP

# Game Mechanics
## Core Gameplay Loop
The player scavenges for resources, builds and fortifies a base, manages character stats (health, stamina, hunger), and engages in tactical combat against zombies and other threats. The loop is driven by resource scarcity and character progression.

## Controls and Input Methods
Uses the New Input System for movement, combat, and menu interactions. Supports keyboard/mouse controls.

# UI
HUD for vital stats (HP, Stamina, Encumbrance), Inventory management screens, and Main Menu for game setup.

# Key Asset & Context
- `EquipmentSystem.cs`: Manages UMA character appearance and equipment.
- `PlayerSpawner.cs`: Handles player instantiation and world dependency checks.
- `UnitHealth.cs`: Core health system for units.
- `GameManager.cs`: Central state machine and loading coordinator.
- `WorldManager.cs`: Coordinates world generation and streaming readiness.

# Implementation Steps

## Phase 1: UMA & Equipment System Stability
1. **Fix EquipmentSystem.OnValidate**:
    - Modify `EquipmentSystem.cs` to prevent `avatar.BuildCharacter()` from being called during serialization if the avatar is already in a building state or if it would cause asset destruction errors.
    - Implement a `_isRebuilding` guard and use `EditorApplication.isPlaying` checks more strictly.
2. **Safe Visual Cleanup**:
    - Ensure `ClearAllEquippedVisuals` and `ClearVisuals` (in `WeaponSystem`) handle `DestroyImmediate` safely in the editor without triggering "Destroying assets is not permitted".

## Phase 2: World Initialization & Synchronization
1. **Align Spawner Timeouts**:
    - Increase `orderedSpawnTerrainStageTargetSeconds`, `orderedSpawnRoadStageTargetSeconds`, etc., in the `PlayerSpawner` component (via prefab or scene) to match the `GameManager`'s 35-second threshold.
2. **Improve NavMesh Readiness Checks**:
    - Update `UnitController.ForceEnableAgent` to better handle vertical sampling when terrain is still streaming.
    - Add a retry mechanism in `PlayerSpawner` if `HasNearbyNavMesh` fails initially, rather than falling back to transform movement immediately.
3. **Synchronize GameManager Loading**:
    - Ensure `GameManager`'s `WaitForLoadingStageReadiness` explicitly signals `PlayerSpawner` to begin its finalization only after `WorldManager` confirms all critical stages (Terrain/Roads/Buildings) are "Ready" or "Timed Out".

## Phase 3: Performance & Technical Debt
1. **Cache Components in UnitHealth**:
    - Add `private UnitStats _stats`, `private Unit _unit`, `private ZombieController _zombieAi` fields to `UnitHealth.cs`.
    - Initialize these in `Awake()` to remove `GetComponent` calls from `TakeDamage` and `Die`.
2. **Scene Cleanup**:
    - **MainMenu**: Replace the direct reference to `UMA_GLIB` in `Boot` with a dynamic search (e.g., `FindFirstObjectByType<UMAContext>()`) or move `UMA_GLIB` to a persistent prefab that is instantiated in all necessary scenes.
    - **EventSystem**: Scan `Boot`, `MainMenu`, and `World` scenes to ensure only one `EventSystem` exists at runtime (preferably in `Boot` or managed by `GameManager`).
3. **Fix Missing Scripts**:
    - Identify and remove or re-assign missing scripts on GameObjects reported in console warnings.

# Verification & Testing
1. **Editor Validation**:
    - Open `World.unity`, change equipment in the `EquipmentSystem` inspector, and verify no `NullReferenceException` or asset destruction errors occur.
2. **Runtime Spawn Test**:
    - Play from `Boot.unity`. Observe the loading screen and ensure the player spawns correctly on top of the terrain/road with the `NavMeshAgent` enabled.
    - Check the console for "Falling back to transform movement" warnings—there should be none if NavMesh is ready.
3. **Performance Check**:
    - Run a combat encounter with 10+ zombies and use the Profiler to ensure `UnitHealth.TakeDamage` is not showing excessive `GetComponent` overhead.
4. **Scene Reference Check**:
    - Ensure no "Cross scene references" warnings appear when saving `MainMenu`.
