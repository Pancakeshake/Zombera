# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with zombies, base building, and squad management.
- Players: Single player (controlling a squad).
- Render Pipeline: URP
- Input System: New Input System

# Refactoring Goal
Trim down "God Files" into smaller, modular components to improve maintainability and reduce the complexity of `GameManager`, `PlayerInputController`, and `PlayerSpawner`.

# Key Assets & Context
- `Assets/01_Game/01_Core/GameManager.cs` (2676 lines)
- `Assets/01_Game/01_Core/PlayerInputController.cs` (2467 lines)
- `Assets/01_Game/03_Characters/Scripts/PlayerSpawner.cs` (2391 lines)

# Implementation Steps

## Phase 1: PlayerInputController Decomposition
1. **Create `PlayerBowController.cs`**:
   - Extract all bow-related fields (`_bowQueuedDurationSeconds`, `_activeBowRangedTarget`, etc.).
   - Extract logic for `TickBowRangedCombatState`, `QueueBowShot`, and `TryReleaseQueuedBowShot`.
2. **Create `InputTargetingService.cs`**:
   - Extract `TryGetUnitHealthUnderCursor`, `TryResolveNearestTargetFromHits`, and the raycast/spherecast buffers.
   - Make this a component or a static utility used by other input scripts.
3. **Create `PlayerSquadSelectionInput.cs`**:
   - Extract `HandleSquadSelectionInput`, `SelectSquadMembersInDragRectangle`, and the `OnGUI` drag-box drawing.
4. **Update `PlayerInputController.cs`**:
   - Remove extracted logic and replace with references to the new components.
   - Retain only high-level routing logic.

## Phase 2: PlayerSpawner Modularization
1. **Create `MapMagicPlayModeStabilizer.cs`**:
   - Extract `StabilizeMapMagicGeneration` and its associated settings.
2. **Create `WorldLoadingDependencyManager.cs`**:
   - Extract the `FinalizeWorldSpawnWhenDependenciesReady` coroutine and the `OrderedSpawnDependencyStages` logic.
3. **Update `PlayerSpawner.cs`**:
   - Simplify the class to focus purely on the instantiation of the player prefab and hand-off to the other systems.

## Phase 3: GameManager Modularization
1. **Create `GameSceneManager.cs`**:
   - Extract `LoadWorldSceneAsync` and the scene-loading lifecycle events.
2. **Create `WorldInitializationService.cs`**:
   - Extract the `BeginWorldSessionRoutine` and the staged loading logic.

# Verification & Testing
1. **Functional Test**: Ensure player can still move, attack with melee/bow, and select squad members.
2. **Bootstrap Test**: Start from `Boot` scene and ensure the world loads correctly with all dependencies (Terrain, Roads, etc.).
3. **Regression Test**: Check that UI visibility and character customization still work as expected.
