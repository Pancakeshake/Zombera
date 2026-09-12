# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG settings and load/save UI fixes.
- Players: Single player
- Target Platform: PC

# Game Mechanics
## UI Fixes
- **Settings Panel**: Fix the auto-open behavior when pausing.
- **Load/Save Panel**: Upgrade the "Load" panel to have the same rich details as the "Save" panel (screenshots, stats, etc.).

# UI
- `PauseMenuPanel` hierarchy in `WorldHUDCanvas.prefab` and `PauseMenuPanel.prefab`.

# Key Assets & Context
- `PauseMenuController.cs`: Handles pause menu transitions.
- `SaveGameMenuController.cs`: The "rich" load/save UI controller.
- `WorldHUDCanvas.prefab` / `PauseMenuPanel.prefab`: Main UI prefabs.

# Implementation Steps

## 1. Script Updates
- **Description**: Modify `PauseMenuController.cs` to use `SaveGameMenuController` for the `loadSavePanel` field.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Prefab Fixes - WorldHUDCanvas.prefab
- **Description**: 
    - Set `SettingsPanel` GameObject to **inactive** (it is currently active by default).
    - Remove the current empty `LoadSavePanel`.
    - Duplicate `SaveGameOverlay` and rename the duplicate to `LoadSavePanel`.
    - Re-wire `PauseMenuController` to point its `loadSavePanel` and `saveGameOverlay` fields to the correct objects.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## 3. Prefab Fixes - PauseMenuPanel.prefab
- **Description**: 
    - Delete the legacy `SettingsPanel` (the one with `SettingsMenuController`).
    - Set the new `SettingsPanel` (the one with `SettingsPageController`) to **inactive**.
    - Perform the same `LoadSavePanel` upgrade (duplicate `SaveGameOverlay`, rename, re-wire).
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

# Verification & Testing
1. **Settings Visibility**: Pause the game. Verify that the Settings panel is NOT visible immediately. Click "Settings" to verify it opens.
2. **Load Panel Content**: Click "Load" in the pause menu. Verify that the load panel now shows rich details (slots, screenshots, etc.) similar to the "Save" panel.
3. **Reference Check**: Ensure `PauseMenuController` has no missing references in the modified prefabs.
