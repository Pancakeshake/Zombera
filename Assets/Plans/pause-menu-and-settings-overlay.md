# Project Overview
- Game Title: Zombera
- Concept: Survival RPG with Base Building.
- Task: Add In-Game Pause Menu (Esc) and make Settings a fullscreen overlay.

# Implementation Steps

## 1. Create `PauseMenuController.cs`
- **Path**: `Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs`
- **Logic**:
    - Handles Resume, Save, Settings, and Quit.
    - Manages cursor lock state (Locked during play, None during pause).
    - Interfaces with `GameManager` to set `GameState.Paused`.

## 2. Update `WorldHUD` for Pause Integration
- **File**: `Assets/01_Game/08_UI/Scripts/HUD/WorldHUD.cs`
    - Add `using Zombera.UI.Menus;`
    - Add `[SerializeField] private PauseMenuController pauseMenu;`
    - Update `Awake()` to call `InitializePauseMenu()`.
- **File**: `Assets/01_Game/08_UI/Scripts/HUD/WorldHUD.Construction.TimeControls.partial.cs`
    - Implement `InitializePauseMenu()`: Finds the controller in children and initializes it.
    - Update `HandleSquadManagementInput()`: 
        - Redirect `Escape` key to `pauseMenu` if squad management is closed.
        - Open/Close `pauseMenu` and sync with `GameManager.SetGameState`.

## 3. Prefab Modifications
- **MainMenu.prefab**:
    - Update `SettingsPanel` RectTransform to fullscreen (anchors 0,0 to 1,1).
    - Adjust `Backdrop` color for better overlay contrast.
- **HUD.prefab**:
    - Add a `PauseMenuPanel` to `WorldHUDCanvas`.
    - Setup a centered card with buttons: RESUME, SAVE, SETTINGS, QUIT.
    - Wire the `PauseMenuController` component.
    - Add or wire a `SettingsPanel` (reused from Main Menu or duplicate) for in-game settings access.

# Verification & Testing
1. **MainMenu**: Open Settings, verify it covers the entire screen.
2. **In-Game**: Press `Esc`, verify the Pause Menu appears and game simulation stops.
3. **Resume**: Click Resume or press `Esc` again, verify game resumes and menu closes.
4. **Save**: Click Save while paused, verify save operation occurs (check logs).
5. **Settings**: Click Settings in Pause Menu, verify Settings overlay appears correctly over the Pause Menu.
6. **Quit**: Click Quit, verify return to Main Menu scene.
