# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with complex UI systems.
- Task: Fix a "two-click" issue when opening the Save Game Overlay and ensure the Pause menu buttons are disabled when sub-menus are open.

# Game Mechanics
## UI - Pause Menu
The Pause Menu contains a main card with buttons (Resume, Save, Settings, Quit) and sub-panels (Save Overlay, Settings, Load/Save).

# Key Asset & Context
- **Scripts**:
  - `Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs`
  - `Assets/01_Game/08_UI/Scripts/Menus/SaveGameMenuController.cs`
  - `Assets/01_Game/08_UI/Scripts/Menus/SettingsMenuController.cs`
  - `Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs`
- **Prefab**: `Assets/Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab`

# Implementation Steps

## 1. Clean up Prefab Redundancy
- **Description**: Remove duplicate `PauseCard` objects from the `PauseMenuPanel` in the `WorldHUDCanvas` prefab. This prevents multiple buttons overlapping and fighting for input.
- **Files**: `Assets/Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab`
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Update Sub-Panel Controllers
- **Description**: Add `OnHide` and `OnShow` events to `SettingsMenuController` and `LoadSaveMenuController` to allow the `PauseMenuController` to react to their state changes.
- **Files**: 
  - `Assets/01_Game/08_UI/Scripts/Menus/SettingsMenuController.cs`
  - `Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs`
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 3. Update PauseMenuController Logic
- **Description**: 
  - Subscribe to `OnHide` events of all sub-panels in `Initialize()`.
  - In `Save()`, `Load()`, and `ShowSettings()`, disable the `mainContentRoot` (the Pause Card).
  - Implement a handler to re-enable `mainContentRoot` when any sub-panel is closed.
  - Add extra logging to diagnose the "two-click" issue if it remains.
- **Files**: `Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs`
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

## 4. Verification
- **Description**: Enter Play Mode in the tester scene. Open the Pause menu. Click the Save button.
- **Assigned role**: developer
- **Files**: `Assets/00_Scenes/SystemDevelopment/3_Ui.unity`
- **Verification**:
  - Verify Save Overlay opens on the *first* click.
  - Verify the main Pause Card buttons disappear when the overlay is active.
  - Verify the main buttons reappear when the overlay is closed.

# Verification & Testing
- **Two-Click Test**: Repeat the process of opening/closing the Save Overlay multiple times to ensure it always responds on the first click.
- **Sub-Menu Consistency**: Check Settings and Load menus to ensure they also hide the main card.
- **Input Blocking**: Ensure no buttons from the main card can be clicked while hidden/disabled.
