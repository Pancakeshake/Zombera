# Project Overview
- **Game Title**: Zombera
- **UI Context**: `MainMenu` scene and `WorldHUDCanvas` prefab.

# UI Task
- **Objective**: Rewire the newly styled `LoadSavePanel` (from `WorldHUDCanvas`) into the `MainMenu` prefab.
- **Requirement**: The panel must be **fullscreen** when opened in the Main Menu.
- **Note**: The user clarified that the previous "red" styling was incorrect and the same method (duplicating from HUD) should be used.

# Key Asset & Context
- **MainMenu Prefab**: `Assets/Shared/Prefabs/UI/Menus/MainMenu.prefab`
- **Source Panel**: `Assets/Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab` -> `PauseMenuPanel/LoadSavePanel`
- **Modern Prefab Cache**: `Assets/01_Game/08_UI/Prefabs/LoadSavePanel_Modern.prefab`
- **Controller**: `MainMenuController` on the `MainMenu` root.

# Implementation Steps

## 1. Refresh Modern Load/Save Prefab
- **Description**: Update the standalone prefab with the latest styling from `WorldHUDCanvas`.
  - Open `Assets/Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab`.
  - Extract the `LoadSavePanel` (the one synced with `SaveGameOverlay`) and update `Assets/01_Game/08_UI/Prefabs/LoadSavePanel_Modern.prefab`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Update MainMenu Prefab
- **Description**: Replace the outdated `LoadSavePanel` in the `MainMenu` prefab.
  - Open `Assets/Shared/Prefabs/UI/Menus/MainMenu.prefab`.
  - Delete the existing `LoadSavePanel` (which has the "red" legacy styling).
  - Instantiate the new `LoadSavePanel_Modern.prefab` as a child of `MainMenuCanvas`.
  - Rename it to `LoadSavePanel`.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## 3. Configure Fullscreen Visuals
- **Description**: Ensure the panel covers the screen visually and technically.
  - Set `LoadSavePanel` RectTransform to Full Stretch:
    - **Anchors**: Min(0,0), Max(1,1).
    - **Offsets**: (0,0,0,0).
  - **Background Check**: If the new panel from HUD is transparent (because it relies on the Pause Menu background), add a dark, semi-transparent `Image` component (or a "Backdrop" child) to the root of the `LoadSavePanel` in the `MainMenu` prefab to ensure it obscures the main menu buttons when open.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

## 4. Re-Wire Controller
- **Description**: Assign the new instance to the `MainMenuController`.
  - Select the `MainMenu` root in the prefab.
  - Assign the new `LoadSavePanel` child to the `loadSavePanel` field of the `MainMenuController`.
- **Assigned role**: developer
- **Dependencies**: Step 3
- **Parallelizable**: No

# Verification & Testing
- **Manual Verification**:
  1. Open `MainMenu` scene and enter Play Mode.
  2. Click "Load Game": Verify the panel covers the entire screen and looks identical to the Save panel in HUD (but with Load terminology).
  3. Verify the background correctly obscures the main menu.
  4. Test "Back" and "Close" buttons.
- **Visual Check**:
  - Ensure no "red" styling elements remain.
  - Ensure anchors and offsets are exactly 0.
