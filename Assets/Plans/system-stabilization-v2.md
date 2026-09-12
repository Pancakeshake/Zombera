# Project Overview
- Game Title: Zombera
- Goal: Fix UMA initialization errors, resolve input conflicts, and optimize scene hierarchy based on the latest scan.

# Key Asset & Context
- `Assets/Scenes/Boot.unity`: Core persistent scene.
- `Assets/Scenes/World.unity`: Main gameplay scene.
- `UMA_GLIB`: The UMA Context/Generator provider.

# Implementation Steps

## 1. Global System Consolidation
- **UMA:** Move the `UMA_GLIB` object from the `World` scene to the `Boot` scene (parent it under `CoreRoot`). Delete any `UMA_GLIB` objects in `MainMenu` or `World`. This ensures a single persistent `UMAContext`, which will resolve the "null context" errors on the Player.
- **WorldManager:** (As planned previously) Move `WorldManager` to the `Boot` scene under `CoreRoot`.

## 2. Input & EventSystem Cleanup
- **Boot Scene:** Inspect the `EventSystem` object. It currently has two `EventSystem` components. Remove one of them.
- **World Scene:** Delete the `UIEventSystem` object. The persistent `EventSystem` from the `Boot` scene will handle all UI input.

## 3. Redundant Component Stripping
- **Main Camera (World):** Remove the following components. They should only exist on the `Player` object:
    - `BuildingInput`
    - `FirstPersonBuildingView`
    - `PlacementBuildingState`
    - `AdjustmentBuildingState`
    - `DestructionBuildingState`
    - `UpgradeBuildingState`
- **MapMagic (World):** Remove the same building-related components listed above. `MapMagic` should only have the `MapMagicObject` and `Transform`.
- **Enviro 3 (World):** Remove the `Rigidbody` and `BoxCollider` components. These are unnecessary for a global weather manager and can cause physics overhead or raycast issues.

## 4. Road System Verification
- Since the `Road Gameplay Stack` was removed, verify that the `RoadGameplayService` in the `World` scene still correctly references `GameplayRoadGraph` and `GameplayRoadGraph_Derived`. (Verified as correct in scan).

# Verification & Testing
1. **UMA Check:** Start from `Boot`. Ensure the Player character in `World` loads without "Object reference not set" or "Recipe slot list is empty" errors.
2. **Input Check:** Ensure UI buttons (Main Menu, HUD) are clickable and the `EventSystem` is functional.
3. **Building Check:** Ensure building works correctly only when the `Player` object is controlled.
4. **Console Audit:** Confirm the number of errors and warnings has decreased. The MapMagic "Collection was modified" error may persist as it's an internal multithreading issue, but UMA errors should be gone.
