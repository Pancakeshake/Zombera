# Project Overview
- Game Title: Zombera
- Goal: Resolve hierarchy conflicts (duplicates, misplacements) to stabilize input and audio systems.

# Key Asset & Context
- `Assets/Scenes/Boot.unity`: Core persistent root.
- `Assets/Scenes/MainMenu.unity`: UI and Character Preview.
- `Assets/Scenes/World.unity`: Gameplay scene.

# Implementation Steps

## 1. EventSystem Consolidation
- **Boot Scene:** Open `Boot.unity`. Under `CoreRoot`, there are two `EventSystem` objects. Delete the second one. Ensure the remaining one has `EventSystem` and `InputSystemUIInputModule`.
- **World Scene:** Open `World.unity`. Delete `UIEventSystem`. The persistent one from `Boot` will take over.

## 2. AudioListener Cleanup
- **MainMenu Scene:** Open `MainMenu.unity`. Locate `UMA_GLIB -> UMAPreviewAvatar -> UMAPreviewCamera`. Disable or remove its `AudioListener`.
- **World Scene:** Open `World.unity`. 
    - Locate `UMA_GLIB -> UMAPreviewAvatar -> UMAPreviewCamera`. Disable its `AudioListener`.
    - Ensure `Environment -> Main Camera` has the active `AudioListener`.

## 3. Boot Scene Cleanup
- **Action:** Open `Boot.unity`.
    - Remove the standalone `Save Manager` (Duplicate of `SaveManager` on `CoreRoot`).
    - Confirm `WorldManager` is correctly parented under `CoreRoot` and remove any secondary instances.

## 4. MainMenu UI Cleanup
- **Action:** Open `MainMenu.unity`.
    - Delete the root-level `CharacterCreatorPanel`. The one inside `MainMenu -> MainMenuCanvas` is the correct one.

## 5. Building System Component Pruning
- **World Scene:** Open `World.unity`.
    - **MapMagic:** Remove `BuildingInput`, `BuildingController`, `FirstPersonBuildingView`, `PlacementBuildingState`, `AdjustmentBuildingState`, `DestructionBuildingState`, `UpgradeBuildingState`, `EasyBuildRadialMenuInputBridge`, and `EasyBuildCursorPlacementBinder`.
    - **Main Camera:** Remove `BuildingInput`, `BuildingController`, `FirstPersonBuildingView`, `PlacementBuildingState`, `AdjustmentBuildingState`, `DestructionBuildingState`, and `UpgradeBuildingState`.
    - **Player:** Keep the components on the `Player` as they handle player-specific building logic.

# Verification & Testing
1. **Input Test:** Start from `Boot`. Navigate `MainMenu`. Ensure buttons are clickable.
2. **Audio Test:** Check Console for "Multiple AudioListeners" warnings. There should be none.
3. **Building Test:** Enter `World`. Ensure building UI (Radial Menu) and placement still function correctly from the `Player` object.
4. **Validation:** Run `StartupReadinessValidator` from the `DebugManager` to ensure no new issues are reported.
