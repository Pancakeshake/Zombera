# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with procedural world generation and streaming systems.
- Players: Single-player (controllable squad).
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
The player spawns into a procedural world, scavenges for resources, and manages a squad while surviving zombie threats. The spawning process is complex because it must wait for terrain, roads, and building systems to initialize.

# UI
The player spawner provides loading progress data (`TryGetOrderedSpawnDependencyProgress`) which is used by the UI to show the world initialization status.

# Key Asset & Context
- **PlayerSpawner.cs**: Currently ~2400 lines. It acts as the "God Class" for the spawning phase.
- **Existing Helpers**: `PlayerSpawnSnapper`, `RuntimeNavMeshBootstrapper`, `StartupSquadSpawner`, `SpawnAppearanceStylingService`, `SquadControlUiCoordinator`.
- **Target Logic for Extraction**:
    - **MapMagic Stabilization**: `StabilizeMapMagicGeneration`, `TryRecoverMissingMapMagicTerrain`, etc.
    - **World Spawn Coordination**: `FinalizeWorldSpawnWhenDependenciesReady` (the IEnumerator), `BeginOrderedSpawnProgressTracking` and progress tracking.
    - **Dev Mode Inventory**: `ApplyDevModeSpawnInventory` and its helper methods.

# Implementation Steps

## Step 1: Extract MapMagic Stabilization logic into `MapMagicStabilizer.cs`
- Create `MapMagicStabilizer` class (internal/sealed).
- Migrate methods: `StabilizeMapMagicGeneration`, `TryRecoverMissingMapMagicTerrain`, `CanSafelySwitchMapMagicLods`, `HasAnyActiveMapMagicTerrain`, `TrySwitchMapMagicLods`, `HideActiveDraftTerrains`, `ShouldHideDraftTerrain`.
- Update `PlayerSpawner` to instantiate this helper in `Awake` and delegate calls.
- **Status: COMPLETED**

## Step 2: Extract Ordered Spawn Dependency logic into `WorldSpawnCoordinator.cs`
- Create `WorldSpawnCoordinator` class.
- Migrate methods: `FinalizeWorldSpawnWhenDependenciesReady` (the IEnumerator), `BeginOrderedSpawnProgressTracking`, `BeginOrderedSpawnProgressStage`, `CompleteOrderedSpawnProgressStage`, `EndOrderedSpawnProgressTracking`, `ResetOrderedSpawnProgressTracking`, `ResolveOrderedSpawnStageTimeoutSeconds`.
- This helper will manage the `_orderedSpawnProgress...` state variables currently in `PlayerSpawner`.
- **Status: COMPLETED**

## Step 3: Extract Dev Mode Inventory logic into `DevModeInventoryHelper.cs`
- Create `DevModeInventoryHelper` class.
- Migrate methods: `ApplyDevModeSpawnInventory`, `ResolveDevModeSpawnTargetQuantity`, `ShouldApplyDevModeSpawnInventory`, `IsDevModeEnabled`, `ResolveDevModeSpawnItems`, `AddUniqueItems`.
- **Status: COMPLETED**

## Step 4: Cleanup and Integration in `PlayerSpawner.cs`
- Remove the migrated methods and state variables from `PlayerSpawner.cs`.
- Consolidate private fields to remove duplicates and unused log flags.
- **Status: IN PROGRESS**

# Verification & Testing
1. **Compilation Check**: Ensure the project compiles after moving ~1000 lines of code into 3 new files.
2. **Runtime Spawn Test**:
   - Start from `Boot` scene.
   - Verify that MapMagic stabilization still occurs (MapMagic range settings change as expected).
   - Verify that the loading progress bar still functions (driven by the extracted `WorldSpawnCoordinator`).
   - Verify that the player still receives the dev inventory loadout in debug mode.
3. **Logic Integrity**: Ensure that `PlayerSpawner` still correctly references `gameObject.scene` and other context when delegating to helpers.
