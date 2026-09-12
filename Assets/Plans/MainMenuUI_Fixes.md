# Project Overview
- Game Title: Zombera
- High-Level Concept: Main Menu UI bug fixes.
- Players: Single player

# Game Mechanics
## UI Logic Fixes: Load Game and Settings Panels
The "Load Game" button in the Main Menu is currently disabled when no save files are present, which prevents users from even opening the browser to see the interface. Additionally, the `SettingsPanel` is reporting coroutine errors because it tries to animate its "Hide" state while inactive during initialization.

# UI
- **MainMenu Scene**: Ensuring the "Load Game" button remains interactable for UI testing and exploration.

# Key Asset & Context
- `MainMenuController.cs`: Handles button states and panel routing.
- `SettingsPageController.cs`: The tabbed settings logic.
- `SaveGameMenuController.cs`: The save slot browser logic.

# Implementation Steps

## 1. Fix SettingsPanel Coroutine Error
- **Description**: Update `SettingsPageController.cs` to avoid starting animation coroutines if the GameObject is inactive.
- **File**: `Assets/01_Game/08_UI/Scripts/Menus/SettingsV2/SettingsPageController.cs`
- **Changes**: 
    - In `CloseSettings()`, check `if (!gameObject.activeInHierarchy) { gameObject.SetActive(false); return; }` before starting the coroutine.
    - In `OpenSettings()`, check `if (!gameObject.activeInHierarchy) { Show(); return; }` (or similar safety).
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Enable Load Game Button Always
- **Description**: Modify `MainMenuController.cs` to keep the "Load Game" button interactable even if no saves are found, so users can see the "No Save Slots" state in the new UI.
- **File**: `Assets/01_Game/08_UI/Scripts/Menus/MainMenuController.cs`
- **Changes**: Update `RefreshLoadGameButtonState` to set `loadGameButton.interactable = (loadSavePanel != null);`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 3. Ensure LoadSavePanel is Hidden on Start
- **Description**: Add `loadSavePanel?.Hide();` to the `Initialize()` method in `MainMenuController.cs` to ensure the new panel is closed when the menu first loads.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

# Verification & Testing
1. **Load Game UI**: Open Main Menu. Verify "Load Game" is now clickable even if no saves exist. Click it and verify the `LoadSavePanel` opens and shows "NO SAVE SLOTS".
2. **Console Check**: Verify the "Coroutine couldn't be started because the the game object 'SettingsPanel' is inactive!" error is gone.
3. **Settings Verification**: Verify "Settings" still opens and functions correctly.
