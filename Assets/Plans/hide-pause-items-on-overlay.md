# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with third-person combat and base building.
- Players: Single player.
- Render Pipeline: URP.
- Target Platform: PC (Windows).

# Game Mechanics
## Core Gameplay Loop
The player pauses the game to manage settings or save/load progress. The Pause Menu provides access to these sub-menus.

# UI
- **PauseCard**: The main container for pause menu buttons (Resume, Save, Settings, etc.).
- **SaveGameOverlay**: A sub-panel that opens when the player clicks "Save".

# Key Asset & Context
- `Assets/01_Game/08_UI/PauseCard.prefab`: The prefab containing the buttons.
- `Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs`: Controls the pause menu state.
- `Assets/01_Game/08_UI/Scripts/Menus/SaveGameMenuController.cs`: Controls the save overlay.
- `Assets/01_Game/08_UI/Scripts/Menus/SettingsMenuController.cs`: Controls settings.
- `Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs`: Controls load/save list.

# Implementation Steps

## 1. Add OnHide Events to Sub-Menu Controllers
Add event hooks so the PauseMenuController knows when a sub-menu is closed.

### [File: Assets/01_Game/08_UI/Scripts/Menus/SettingsMenuController.cs]
- Add `using System;`
- Add `public event Action OnHide;`
- In `Hide()`, invoke `OnHide?.Invoke();`

### [File: Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs]
- Add `using System;`
- Add `public event Action OnHide;`
- In `Hide()`, invoke `OnHide?.Invoke();`

## 2. Update PauseMenuController to Manage Main Content Visibility
Wire up the events and toggle the visibility of the main buttons.

### [File: Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs]
- **Initialize()**: 
    - Subscribe `HandleOverlayHidden` to `saveGameOverlay.OnHide`.
    - Subscribe `HandleOverlayHidden` to `settingsPanel.OnHide`.
    - Subscribe `HandleOverlayHidden` to `loadSavePanel.OnHide`.
- **Save()**:
    - Add `if (mainContentRoot != null) mainContentRoot.SetActive(false);` when showing `saveGameOverlay` or `loadSavePanel`.
- **ShowSettings()**:
    - Add `if (mainContentRoot != null) mainContentRoot.SetActive(false);`.
- **Load()**:
    - Add `if (mainContentRoot != null) mainContentRoot.SetActive(false);`.
- **HandleOverlayHidden()**:
    - Ensure this method exists (it does) and correctly sets `mainContentRoot.SetActive(true)`.

# Verification & Testing
1. **Enter Play Mode** from the Boot scene.
2. **Pause the game** (Esc).
3. **Click "Save"**: Verify the main buttons disappear and the Save Overlay appears.
4. **Click "Back"** on Save Overlay: Verify the main buttons reappear.
5. **Click "Settings"**: Verify the main buttons disappear.
6. **Click "Close"** on Settings: Verify the main buttons reappear.
7. **Verify Styling**: Ensure the background card or other styled elements of `PauseCard.prefab` remain visible if they are outside of the `mainContentRoot` (Content object).
