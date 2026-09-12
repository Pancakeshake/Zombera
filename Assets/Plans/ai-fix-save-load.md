# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG set in a zombie-infested world with base building and squad management.
- Players: Single player
- Target Platform: PC (Windows)
- Render Pipeline: URP

# Game Mechanics
## Core Gameplay Loop
The game focuses on scavenging, combat, building, and survival. Character progression and squad management are key elements.

# Key Asset & Context
- `GameManager.cs`: Main state coordinator.
- `SaveManager.cs`: Orchestrates save/load operations and providers.
- `PlayerSaveProvider.cs`: Handles player and squad data persistence.
- `PlayerSpawner.cs`: Manages player and squad instantiation.
- `SaveGameMenuController.cs`: Detailed save/load UI.
- `MainMenuController.cs` & `PauseMenuController.cs`: Menu controllers.

# Implementation Steps

## 1. Implement Deferred Loading Stages in GameManager and SaveManager
**Description**: Split the loading process into a preload stage (caching data) and an apply stage (runtime restoration) to ensure units exist before their state is restored.
**Assigned role**: developer
**Dependencies**: None
**Files**:
- `Assets/01_Game/09_SaveSystem/Core/SaveManager.cs`:
    - Add `private GameSaveData _cachedLoadData;` and `private bool _hasCachedData;`.
    - Add `public void PreloadLoadData(string slotId)` to fetch and cache data without applying it to providers.
    - Add `public void ApplyDeferredRestoration()` to execute `RestoreRuntimeState(_cachedLoadData)` and clear the cache.
- `Assets/01_Game/01_Core/Managers/GameManager.cs`:
    - Add `public bool IsLoadingSession { get; private set; }`.
    - Update `ApplyLoadGame` to call `saveManager.PreloadLoadData` and set `IsLoadingSession = true`.
- `Assets/01_Game/01_Core/Managers/GameManager.WorldSession.cs`:
    - In `BeginWorldSessionRoutine`, call `saveManager.ApplyDeferredRestoration()` near the end of the routine (after `WaitForWorldCharacterVisualsReady`).
    - Reset `IsLoadingSession = false` after restoration.

## 2. Robust Squad Restore on UnitId Mismatch
**Description**: Improve squad restoration by adding a deterministic fallback when `UnitId` matching fails.
**Assigned role**: developer
**Dependencies**: None
**Files**:
- `Assets/01_Game/09_SaveSystem/Providers/PlayerSaveProvider.cs`:
    - Update `RestoreSquadState` to collect unmatched saved members.
    - For unmatched members, try matching by `squadRole` and nearest position among remaining active units.
    - Add logging for unmatched members and matched fallback count.

## 3. Prevent Startup Squad Spawn During Load
**Description**: Ensure that loading a save doesn't trigger the default startup squad spawning logic.
**Assigned role**: developer
**Dependencies**: Step 1
**Files**:
- `Assets/01_Game/03_Characters/Scripts/PlayerSpawner.SpawnPipeline.cs`:
    - In `EnsureStartupTestSquad`, add a check: `if (GameManager.Instance != null && GameManager.Instance.IsLoadingSession) return;`.

## 4. Fix Save-Slot Refresh and Unify UI Panel Usage
**Description**: Fix the event subscription in `SaveGameMenuController` and ensure consistency across menus.
**Assigned role**: developer
**Dependencies**: None
**Files**:
- `Assets/01_Game/08_UI/Scripts/Menus/SaveGameMenuController.cs`:
    - Update `EnsureReferences` to handle `SaveListChanged` subscription/unsubscription safely.
    - Remove subscription from `Awake` and `OnDestroy` (manage it in `EnsureReferences` or `OnEnable`/`OnDisable`).
- `Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs`:
    - Add `SaveListChanged` subscription logic similar to `SaveGameMenuController`.
- `Assets/01_Game/08_UI/Scripts/Menus/MainMenuController.StartFlow.cs`:
    - Ensure `loadSavePanel` is used as a `SaveGameMenuController` where appropriate or consistently handled.

## 5. Improve Save Feedback Clarity
**Description**: Include seconds in the save timestamp so that immediate consecutive saves are visibly different.
**Assigned role**: developer
**Dependencies**: None
**Files**:
- `Assets/01_Game/09_SaveSystem/Core/SaveManager.cs`:
    - Update `PopulateMetadata` to use format `"MMM dd, yyyy\nHH:mm:ss"`.

# Verification & Testing
1. **Load Order Test**: Start a game, save, then load. Verify that squad members are restored correctly and no duplicate startup squad appears.
2. **Squad Mismatch Test**: Manually modify a save file's `UnitId` for a squad member and verify the fallback matching works based on position/role.
3. **Save List Refresh Test**: Perform a save from the pause menu and immediately verify the save slot list updates correctly in the UI.
4. **Timestamp Test**: Save twice within the same minute and verify the timestamps differ by seconds.
