# Project Overview
- **Game Title:** Zombera
- **High-Level Concept:** Survival RPG set in a zombie-infested world with base building and tactical combat.
- **Players:** Single-player (based on architecture).
- **Target Platform:** PC (Windows).
- **Render Pipeline:** Universal Render Pipeline (URP).

# Technical Debt & Issue Log
My scan of the project has identified several technical issues and areas for improvement:

1.  **UMA Data Corruption:** The `HumanMale Base Recipe` asset (and its `_Save` variant) contains empty slot IDs in its internal recipe string, which triggers "UMA recipe slot list is empty!" errors.
2.  **Cross-Scene Reference Error:** The `Player_Preview` object in the `MainMenu` scene holds a direct reference to `UMA_GLIB` in the `Boot` scene. This causes Unity editor warnings and will result in null references if the `Boot` scene is not active.
3.  **EquipmentSystem Exception:** A `MissingReferenceException` occurs in the editor when `EquipmentSystem` is destroyed while an editor rebuild is still queued in `delayCall`.
4.  **GameManager Appearance Logic:** The `GameManager` has methods to clear cross-scene references but doesn't fully automate the rebinding of UMA avatars to their appropriate scene-local or global generators.
5.  **Technical Debt:** Several scripts use the obsolete `Object.FindObjectsOfType<T>()` method.
6.  **Redundant Assets:** An empty `New Actions.inputactions` file exists in `Assets/Art/`, which may be a leftover from development.

# Key Assets & Context
- `Assets/UMA/Content/Core/HumanMale/Recipes/BaseRecipes/HumanMale Base Recipe.asset` (Corrupted Data)
- `Assets/00_Scenes/MainMenu.unity` (Cross-scene reference)
- `Assets/01_Game/06_Inventory/Equipment/EquipmentSystem.EditorHooks.cs` (Missing null check)
- `Assets/01_Game/01_Core/Managers/GameManager.Appearance.cs` (Incomplete rebinding logic)

# Implementation Steps

## 1. Fix EquipmentSystem Editor Exception
- **File:** `Assets/01_Game/06_Inventory/Equipment/EquipmentSystem.EditorHooks.cs`
- **Change:** Add a null check for `this` at the beginning of `RunDelayedEditorRebuild`.
- **Reason:** Prevents `MissingReferenceException` when the component is destroyed before the `delayCall` fires.

## 2. Automate UMA Avatar Rebinding in GameManager
- **File:** `Assets/01_Game/01_Core/Managers/GameManager.Appearance.cs`
- **Change:** Update `ClearCrossSceneAppearanceReferences` to find all `DynamicCharacterAvatar` components in the current scenes and call `UmaGlobalLibraryService.TryBindAvatarLibrary` for each.
- **Reason:** Ensures that avatars are correctly wired to the UMA Context/Generator regardless of how the scene was loaded or if references were cleared.

## 3. Clear Cross-Scene Reference in MainMenu
- **Asset:** `Assets/00_Scenes/MainMenu.unity`
- **Action:** Clear the `umaGenerator` property on the `UMAData` component of the `Player_Preview` GameObject.
- **Reason:** Silences Unity's "Cross scene references are not supported" warning and allows `UmaGlobalLibraryService` to resolve the reference at runtime.

## 4. Modernize FindObjects Calls
- **Files:** `Assets/ThirdParty/ALP_Assets/Core/Scripts/Global Lighting/Editor/ALP_LightsSettings.cs` (and others identified in logs).
- **Change:** Replace `Object.FindObjectsOfType<T>()` with `Object.FindObjectsByType<T>(FindObjectsSortMode.None)`.
- **Reason:** Resolves deprecation warnings and follows Unity 6 best practices.

## 5. Cleanup Redundant Input Asset
- **Asset:** `Assets/Art/New Actions.inputactions`
- **Action:** Delete the asset if it is confirmed to be unused (empty `maps` array).

# Verification & Testing
1.  **Console Check:** Open `MainMenu` and `World` scenes; confirm no UMA errors or cross-scene warnings appear.
2.  **Editor Robustness:** Create and delete `EquipmentSystem` components in a test scene to ensure no exceptions are thrown.
3.  **Visual Validation:** Start the game from `Boot` and ensure the player character and previews load their clothing and appearance correctly.
4.  **Build Check:** Ensure the project compiles without "Obsolete" warnings.
