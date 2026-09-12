# Project Overview
- **Game Title**: Zombera
- **High-Level Concept**: A survival RPG set in a zombie-infested world, combining third-person combat, base building, and inventory management.
- **Players**: Single player (Standalone Windows).
- **Target Platform**: PC (Windows).
- **Render Pipeline**: Universal Render Pipeline (URP).

# Game Mechanics
## Core Gameplay Loop
- Scavenge resources, fight zombies, build bases, and manage character stats.
- Keyboard/Mouse input using the New Input System.

# UI
- **MainMenu**: The primary entry point with Start, Load, Settings, and Quit options.
- **Settings Panel**: Needs to be the modern version from `WorldHUDCanvas`.
- **Load Game Panel**: Needs to be the modern version from `WorldHUDCanvas`.
- **Requirement**: Panels must be fullscreen when opened.

# Key Asset & Context
- **MainMenu Scene**: `Assets/00_Scenes/MainMenu.unity`
- **MainMenu Prefab**: `Assets/Shared/Prefabs/UI/Menus/MainMenu.prefab`
- **Source Prefab**: `Assets/Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab`
- **Source Paths**: 
  - `WorldHUDCanvas/PauseMenuPanel/SettingsPanel` (Uses `SettingsPageController`)
  - `WorldHUDCanvas/PauseMenuPanel/LoadSavePanel` (Uses `SaveGameMenuController`)
- **MainMenuController**: `Assets/01_Game/08_UI/Scripts/Menus/MainMenuController.cs`

# Implementation Steps

## 1. Extract Modern UI Panels from WorldHUDCanvas
- **Description**: Source the preferred panels from the `WorldHUDCanvas` prefab as requested.
  - Open `Assets/Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab`.
  - Extract/Duplicate `SettingsPanel` and `LoadSavePanel` from under `PauseMenuPanel`.
  - Create new standalone prefabs or temporary assets for these to ensure they can be easily placed in the Main Menu:
    - `Assets/01_Game/08_UI/Prefabs/SettingsPanel_Modern.prefab`
    - `Assets/01_Game/08_UI/Prefabs/LoadSavePanel_Modern.prefab`
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Update MainMenu Prefab
- **Description**: Replace legacy panels with the modern versions in the `MainMenu` prefab.
  - Open `Assets/Shared/Prefabs/UI/Menus/MainMenu.prefab`.
  - Remove the legacy `SettingsPanel` and `LoadSavePanel` objects.
  - Instantiate the new `SettingsPanel_Modern.prefab` and `LoadSavePanel_Modern.prefab` as children of `MainMenuCanvas`.
  - Rename them to `SettingsPanel` and `LoadSavePanel` to match the `MainMenuController` search strings.
  - Ensure they are inactive by default.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## 3. Configure Fullscreen Layout and Wiring
- **Description**: Ensure fullscreen behavior and wire references to the controller.
  - Select both panels in the `MainMenu.prefab`.
  - Set `RectTransform`:
    - **Anchors**: Min (0, 0), Max (1, 1).
    - **Offsets**: (0, 0, 0, 0).
  - Select the `MainMenu` root object and update `MainMenuController`:
    - Assign `settingsPanel` to the new `SettingsPanel` child.
    - Assign `loadSavePanel` to the new `LoadSavePanel` child.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

## 4. Final Scene Sync and Validation
- **Description**: Ensure the `MainMenu` scene reflects the updated prefab.
  - Open `Assets/00_Scenes/MainMenu.unity`.
  - Select the `MainMenu` object and ensure all overrides are applied or reverted as necessary to match the updated prefab.
  - Verify that the `MainMenuController` references are correctly pointing to the new panels.
- **Assigned role**: developer
- **Dependencies**: Step 3
- **Parallelizable**: No

# Verification & Testing
- **Manual Verification**:
  1. Open `MainMenu` scene and enter Play Mode.
  2. Click "Settings": Verify the panel from `WorldHUDCanvas` opens fullscreen.
  3. Click "Load Game": Verify the panel from `WorldHUDCanvas` opens fullscreen and shows save slots.
  4. Verify "Back/Close" buttons on both panels function correctly.
- **Visual Check**:
  - Verify that the panels cover the entire screen and do not have any margins or offsets.
