# Project Overview
- **Game Title**: Zombera
- **High-Level Concept**: Survival RPG set in a zombie-infested world with third-person combat, base building, and inventory management.
- **Players**: Single player (with squad elements)
- **Target Platform**: PC (Windows)
- **Render Pipeline**: Universal Render Pipeline (URP)
- **Input System**: New Input System

# Game Mechanics
## Core Gameplay Loop
- Scavenge for resources, fight zombies/bandits, build bases, and survive (manage health/stamina/stats).
## Controls and Input Methods
- Uses New Input System. 
- Primary issue: 'Esc' key is not bringing up the pause menu overlay in the game world.

# UI
- **Pause Menu Overlay**: Needs to include "Resume", "Save", "Settings", "Quit", and a new "Save Logs" button.
- **HUD**: `WorldHUDController` (on `WorldHUDCanvas`) is the active HUD in the world scene.

# Key Asset & Context
- `PauseMenuController.cs`: Handles pause menu logic. Needs "Save Logs" button and logic.
- `WorldHUDController.cs`: Main HUD controller. Needs to handle global 'Esc' input to toggle the pause menu.
- `[PauseMenuPanel]` Prefab: The UI prefab for the pause menu.
- `[WorldHUDCanvas]` Prefab/Object: The UI root for the world HUD.

# Implementation Steps

## 1. Modify PauseMenuController.cs
- **File**: `Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs`
- **Changes**:
    - Add `[SerializeField] private Button saveLogsButton;`
    - In `Initialize()`, add listener for `saveLogsButton.onClick`.
    - Implement `SaveLogs()` method:
        - Copy `Player.log` from `Application.persistentDataPath` to a timestamped file in the same directory.
        - Log the success/failure to the console.

## 2. Modify WorldHUDController.cs
- **File**: `Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.cs` (Partial)
- **Changes**:
    - Add `[SerializeField] private PauseMenuController pauseMenu;`

## 3. Update WorldHUDController.Lifecycle.cs
- **File**: `Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.Lifecycle.cs`
- **Changes**:
    - In `Update()`, update the `Escape` key logic:
        - If `ActiveTab != TabId.None`, call `CloseTab()` (existing behavior).
        - If `ActiveTab == TabId.None`:
            - If `pauseMenu` is not visible, call `pauseMenu.Show()` and set `GameManager.Instance.SetGameState(GameState.Paused)`.
            - If `pauseMenu` is visible, call `pauseMenu.Resume()`.

## 4. Update UI Prefabs (Via Scripts/Editor)
- **Task**: Add the "Save Logs" button to the `[PauseMenuPanel]` and wire everything up.
- **Details**:
    - Add a button named `SaveLogsButton` as a child of `[PauseCard]` in `[PauseMenuPanel]`.
    - Set its label to "SAVE LOGS".
    - Update `PauseMenuController` references.
    - Assign `[PauseMenuPanel]` to the `pauseMenu` field in `WorldHUDController` on `[WorldHUDCanvas]`.

# Verification & Testing
- **Manual Test**: Play the game from `Boot` scene, transition to `World` scene.
- **Esc Test**: Press `Esc` in the world. Verify the Pause Menu overlay appears.
- **Quit Test**: Press `Quit` and verify it returns to Main Menu.
- **Save Logs Test**: Press `Save Logs`. Check `Application.persistentDataPath` for the generated log file.
- **Resume Test**: Press `Esc` again or `Resume` and verify gameplay continues.
