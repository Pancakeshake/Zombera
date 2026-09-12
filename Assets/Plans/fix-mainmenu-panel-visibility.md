# Project Overview
- **Game Title**: Zombera
- **UI Context**: `MainMenu` scene and prefab.
- **Issues**: 
  1. "Load Game" button doesn't open the load panel.
  2. "Settings" panel is "too small".
  3. Redundant `WorldHUDCanvas` objects in the Main Menu scene.

# UI Task
- **Objective**: Fix panel visibility and layout in the Main Menu.
- **Plan**:
  - Remove interfering HUD objects from the Main Menu scene.
  - Optimize `SettingsPanel` layout to be truly fullscreen (0 margins).
  - Ensure `LoadSavePanel` and `SettingsPanel` are correctly managed by the `MainMenuController`.

# Key Assets & Context
- **Scene**: `Assets/00_Scenes/MainMenu.unity`
- **Prefab**: `Assets/Shared/Prefabs/UI/Menus/MainMenu.prefab`
- **Controller**: `MainMenuController.cs` and `SettingsPageController.cs`.

# Implementation Steps

## 1. Clean Main Menu Scene
- **Description**: Remove redundant and interfering UI objects.
  - Open `Assets/00_Scenes/MainMenu.unity`.
  - Delete all instances of `WorldHUDCanvas`. These belong in the gameplay scene and their presence in Main Menu (with higher sorting order) can block interactions or obscure panels.
  - Save the scene.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Adjust Settings Panel Sizing (Prefab)
- **Description**: Make the Settings panel fill the entire screen and adjust its internal content to be more expansive.
  - Open `Assets/Shared/Prefabs/UI/Menus/MainMenu.prefab`.
  - In `SettingsPanel/MainFrame`:
    - Set **Offsets** to (0, 0, 0, 0) (currently 24px).
  - In `SettingsPanel/MainFrame/BodyRow`:
    - Ensure the `HorizontalLayoutGroup` is configured correctly.
    - Increase `LeftNavPanel` preferred width to **350** (from 300).
    - Increase `RightInfoPanel` preferred width to **450** (from 400).
    - Set `Spacing` to **30**.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 3. Verify Load Panel Wiring (Prefab & Logic)
- **Description**: Ensure the Load panel is correctly initialized and hidden at start.
  - In `Assets/Shared/Prefabs/UI/Menus/MainMenu.prefab`:
    - Ensure `LoadSavePanel` is inactive by default.
    - Verify `MainMenuController` has the correct reference to the `LoadSavePanel` child.
  - In `MainMenuController.cs`:
    - Double check `Initialize()` to ensure it hides panels correctly and is called in `Awake`. (Confirmed in code).
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

# Verification & Testing
- **Manual Verification**:
  1. Open `MainMenu` scene and enter Play Mode.
  2. Click "Settings": Verify it covers the entire screen with no border.
  3. Click "Load Game": Verify the Load Panel appears on top of the menu buttons.
  4. Verify that no HUD elements (TopBar/BottomBar from gameplay) are visible in the Main Menu.
- **Logic Check**:
  - Click "Back" or "Close" on panels: Verify they hide and return to the main buttons.
