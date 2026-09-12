# Project Overview
- Game Title: Zombera
- High-Level Concept: Open-world zombie survival and squad-based RTS-lite with procedural elements.
- Players: Single player management of a squad.
- Target Platform: PC (StandaloneWindows64)
- Render Pipeline: URP
- Input System: New Input System

# Game Mechanics
## Core Gameplay Loop
- Scavenge, build, and defend against zombie hordes.
- Manage a squad of survivors with distinct stats and equipment.
- Save and load game progress including world state, player state, and squad state.

# UI
- Save/Load menus integrated into Main Menu and Pause Menu.
- Dynamic slot lists with screenshots and metadata.

# Key Asset & Context
- `GameManager`: Orchestrates world sessions and loading flows.
- `SaveManager`: Manages save data persistence and runtime restoration.
- `PlayerSaveProvider`: Handles saving/loading of player and squad member states.
- `PlayerSpawner`: Handles initial spawning of player and startup squads.
- `SaveGameMenuController`: UI controller for the save/load interface.

# Implementation Steps

## 1. Defer Runtime Restore in Loading Flow
**Description**: Modify the loading sequence to preload data early but apply the restoration only after units have spawned.
**Assigned role**: developer
**Dependencies**: None
**Parallelizable**: No
- **SaveManager.cs**:
    - Add `private GameSaveData _pendingRestoreData`.
    - Add `private bool _hasPendingRestore`.
    - Create `PreloadLoadGame(string slotId)`: performs data fetching and caching into `_pendingRestoreData` (similar to `LoadGame` but without the restore call).
    - Update `LoadGame(string slotId)` to call `PreloadLoadGame` and then `ApplyDeferredRestore`.
    - Create `ApplyDeferredRestore()`: executes `RestoreRuntimeState(_pendingRestoreData)` and clears the pending flag/data.
- **GameManager.Provisioning.cs**:
    - In `TryFinalizePendingWorldSession`, if loading a game, call a new helper `PrepareLoadSession(slotId)`.
- **GameManager.cs**:
    - Implement `PrepareLoadSession(string slotId)`: sets a session flag `_isRestoringFromSave = true` and calls `saveManager.PreloadLoadGame(slotId)`.
    - Update `ApplyLoadGame` (or replace usage) to use the preloading flow.
- **GameManager.WorldSession.cs**:
    - In `BeginWorldSessionRoutine`, after character visuals are ready (or at the end of the routine), call `saveManager.ApplyDeferredRestore()` if `_isRestoringFromSave` is true.
    - Reset `_isRestoringFromSave = false` at the end of the session startup.

## 2. Robust Squad Restore Fallback
**Description**: Enhance `PlayerSaveProvider` to match saved squad data to spawned units even if unique IDs change.
**Assigned role**: developer
**Dependencies**: None
**Parallelizable**: Yes
- **PlayerSaveProvider.cs**:
    - Update `RestoreSquadState(GameSaveData saveData)`:
        - Track `unmatchedData` (saved members not found by ID).
        - Track `availableUnits` (spawned squad members not yet assigned state).
        - For each `unmatchedData`, find the nearest `availableUnits` of the same role.
        - Apply state to matched fallbacks.
        - Add `Debug.Log` diagnostics for unmatched count and fallback match success.

## 3. Prevent Startup Squad Overwrite
**Description**: Prevent `PlayerSpawner` from creating a default "new game" squad when loading a save.
**Assigned role**: developer
**Dependencies**: Step 1
**Parallelizable**: No
- **PlayerSpawner.cs**:
    - Add `public bool isLoadingSaveSession { get; set; }`.
- **PlayerSpawner.SpawnPipeline.cs**:
    - In `EnsureStartupTestSquad`, return early if `isLoadingSaveSession` is true.
- **GameManager.WorldSession.cs**:
    - In `BeginWorldSessionRoutine`, find the `PlayerSpawner` and set `spawner.isLoadingSaveSession = _isRestoringFromSave`.

## 4. UI Refresh and Consistency
**Description**: Fix event subscription in `SaveGameMenuController` and ensure seconds are shown in timestamps.
**Assigned role**: developer
**Dependencies**: None
**Parallelizable**: Yes
- **SaveGameMenuController.cs**:
    - In `EnsureReferences`, check if `_saveSystem` has changed. If so, unsubscribe from the old one and subscribe to the new one.
    - Ensure `HandleSaveListChanged` is called correctly.
- **SaveManager.cs**:
    - In `PopulateMetadata`, change the timestamp format to `MMM dd, yyyy\nHH:mm:ss`.

## 5. Deprecate Legacy Load/Save Menu
**Description**: Ensure only `SaveGameMenuController` is used.
**Assigned role**: developer
**Dependencies**: None
**Parallelizable**: Yes
- **PauseMenuController.cs**: Verify it uses `SaveGameMenuController`. (Existing code confirms this, but ensure no logic depends on `LoadSaveMenuController`).
- **LoadSaveMenuController.cs**: Add `[Obsolete]` attribute or a warning log to discourage usage in favor of `SaveGameMenuController`.

# Verification & Testing
- **New Game Test**: Start a new game, verify a startup squad is spawned and no restore is attempted.
- **Save/Load Test**: Save a game, quit to main menu, and load. Verify:
    - Squad members are restored to their saved positions/stats.
    - No duplicate startup squad is created.
    - UI timestamp shows seconds and refreshes immediately after saving.
- **ID Mismatch Test**: Manually modify a save file (if possible) or trigger a condition where UnitIDs might mismatch, verify fallback matching kicks in.
- **Diagnostics Check**: Check console logs for "Restore applied", "Fallback squad match", and timestamp changes.
