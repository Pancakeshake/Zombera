# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG UI Consistency.
- Players: Single player
- Target Platform: PC (Windows)

# Game Mechanics
## UI Logic: Unified Settings and Load/Save Panels
The user wants to synchronize the `MainMenu` settings and load/save functionality with the advanced versions used in the `WorldHUD` (Pause Menu). This requires upgrading the `MainMenuController` to support the V2 settings page and the detailed save game menu.

# UI
- **MainMenu Scene**: Upgrading `SettingsPanel` and `LoadSavePanel`.
- **MainMenuController**: Updating references and logic to use `SettingsPageController` and `SaveGameMenuController`.

# Key Asset & Context
- `MainMenuController.cs`: Main script for the menu.
- `SettingsPageController.cs`: The advanced settings logic.
- `SaveGameMenuController.cs`: The detailed save/load logic.
- `SettingsPage_StyleA.prefab`: Reference for the settings layout.
- `PauseMenuPanel.prefab`: Contains the V2 panels used in the HUD.

# Implementation Steps

## 1. Update MainMenuController.cs and Partials
- **Description**: Update the field types and method calls in `MainMenuController` to match the V2 controllers.
- **Files**:
    - `Assets/01_Game/08_UI/Scripts/Menus/MainMenuController.cs`
    - `Assets/01_Game/08_UI/Scripts/Menus/MainMenuController.Wiring.cs`
    - `Assets/01_Game/08_UI/Scripts/Menus/MainMenuController.StartFlow.cs`
- **Changes**:
    - Add `using Zombera.UI.SettingsV2;` to the headers.
    - Change `SettingsMenuController settingsPanel` -> `SettingsPageController settingsPanel`.
    - Change `LoadSaveMenuController loadSavePanel` -> `SaveGameMenuController loadSavePanel`.
    - Remove `settingsPanel?.Initialize()` calls (V2 initializes on Awake).
    - In `HandleLoadGameRequested`, change `loadSavePanel.Show()` to `loadSavePanel.ShowLoadMode()`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Upgrade MainMenu Scene Panels
- **Description**: Replace the legacy `SettingsPanel` and `LoadSavePanel` in the `MainMenu` scene with the V2 versions.
- **Scene**: `Assets/00_Scenes/MainMenu.unity`
- **Steps**:
    - Open `MainMenu` scene.
    - Delete the existing `SettingsPanel` and `LoadSavePanel` GameObjects under `MainMenuCanvas`.
    - Instantiate the `SettingsPanel` and `LoadSavePanel` from `Assets/Shared/Prefabs/UI/Menus/PauseMenuPanel.prefab` as children of `MainMenuCanvas`.
    - (Optional) Adjust positions/anchors to fit the MainMenu layout.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## 3. Wire MainMenuController References
- **Description**: Connect the new panels to the `MainMenuController` component on the `MainMenu` GameObject.
- **Scene**: `Assets/00_Scenes/MainMenu.unity`
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

# Verification & Testing
1. **Settings Verification**: Open MainMenu. Click "Settings". Verify the advanced V2 settings page opens (with Tabs: General, Graphics, etc.). Verify "Close" works.
2. **Load Game Verification**: Click "Load Game". Verify the detailed save slot menu opens (with details, screenshot, etc.). Verify it displays available slots.
3. **Save Button Missing Check**: Verify that "Load Game" in MainMenu ONLY shows the Load mode (no "Save Game" button or title).
