# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with base building and inventory management.
- Players: Single player.
- Render Pipeline: URP.

# Game Mechanics
## Pause and Menu System
- Settings panel in Main Menu becomes a fullscreen overlay.
- In-game `Esc` key opens a Pause Menu.
- Pause Menu includes Resume, Save, Settings, and Quit options.
- Opening the menu pauses the game (GameState.Paused).

# UI
## Main Menu
- `SettingsPanel`: Adjust to fullscreen stretch anchors.
## In-Game (HUD)
- `PauseMenuPanel`: New fullscreen overlay with vertical button stack.
    - **Resume**: Closes menu, resumes game.
    - **Save**: Saves to the active slot or a default slot.
    - **Settings**: Opens the Settings panel (can reuse MainMenu's SettingsMenuController logic).
    - **Quit**: Quits to Main Menu.

# Key Assets & Context
- `HUDManager.cs`: Manages HUD and handles input.
- `GameManager.cs`: Handles game state changes and pausing.
- `SaveManager.cs`: Handles saving the game.
- `SettingsMenuController.cs`: Existing settings logic.
- `PauseMenuController.cs`: (To be created) Logic for the in-game menu.

# Implementation Steps

## 1. Modify `SettingsPanel` in `MainMenu.prefab`
- Set `SettingsPanel` RectTransform anchors to (0,0) to (1,1) with 0 offsets.
- Ensure `Backdrop` is active and covers the screen.

## 2. Implement `PauseMenuController.cs`
- **File**: `Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs`
- **Responsibilities**:
    - `Initialize()`: Setup button listeners.
    - `Show()` / `Hide()`: Toggle visibility.
    - `HandleResume()`: Calls `GameManager.Instance.SetGameState(GameState.Playing)`.
    - `HandleSave()`: Calls `SaveManager.Instance.SaveGame(activeSlot)`.
    - `HandleSettings()`: Opens the settings panel.
    - `HandleQuit()`: Calls `GameManager.Instance.QuitToMainMenu()`.

## 3. Create Pause Menu UI
- Add `PauseMenuPanel` to the HUD Canvas in `HUDManager`.
- Create a vertical stack of buttons (Resume, Save, Settings, Quit).
- Add `PauseMenuController` component to the panel and wire references.

## 4. Integrate with `HUDManager.cs`
- Add `[SerializeField] private PauseMenuController pauseMenu;` to `HUDManager.cs`.
- Update `HandleSquadManagementInput()`:
    - If `Esc` is pressed:
        - If `squadManagementUI` is open, close it.
        - Else if `pauseMenu` is closed, open it and set state to `GameState.Paused`.
        - Else if `pauseMenu` is open, close it and set state to `GameState.Playing`.
- Update `OnGameStateChanged()` to handle hiding the Pause Menu if state changes externally.

## 5. Wiring and Verification
- Update `HUDManager` prefab to include the new `PauseMenu`.
- Test `Esc` behavior in game.
- Verify `Time.timeScale` is 0 when paused.
- Verify Save functionality triggers correctly.

# Verification & Testing
- **Pause Test**: Press `Esc`, verify game freezes (zombies stop moving, etc.).
- **Resume Test**: Click Resume or press `Esc` again, verify game resumes.
- **Save Test**: Click Save, verify console logs or save file update.
- **Settings Test**: Click Settings in Pause Menu, verify settings panel opens over the pause menu.
- **Quit Test**: Click Quit, verify return to Main Menu scene.