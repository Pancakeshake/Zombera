# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with building, scavenging, and combat.
- Players: Single player
- Target Platform: PC (Windows)
- Render Pipeline: URP

# Game Mechanics
## Load/Save UI Wording Fix
The "Load Game" screen in the pause menu currently displays "Save Game" related titles and wording. This plan fixes those strings to correctly reflect the current mode (Load vs Save).

# UI Changes
- **SaveGameOverlay**:
    - Update Header to "LOAD GAME" and Subtitle to "Choose a slot to load your progress." when in Load mode.
    - Update Header to "SAVE GAME" and Subtitle to "Choose a slot to save your progress." when in Save mode.
- **LoadSavePanel**:
    - Update Title to "LOAD GAME" or "SAVE GAME" based on mode.

# Key Assets & Context
- **Scripts**:
    - `Assets/01_Game/08_UI/Scripts/Menus/SaveGameMenuController.cs`
    - `Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs`
- **Prefabs**:
    - `Assets/Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab`

# Implementation Steps

## 1. Update SaveGameMenuController.cs
- **Description**: Add serialized fields for `headerText` and `subtitleText`. Update `ApplyModeVisuals` to set their text based on `_currentMode`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Update LoadSaveMenuController.cs
- **Description**: Add a serialized field for `titleText`. Update `Show(MenuMode mode)` to set its text based on `mode`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 3. Configure WorldHUDCanvas.prefab
- **Description**: Open the prefab and assign the new text components to the serialized fields in `SaveGameMenuController` and `LoadSaveMenuController`.
- **Assigned role**: developer
- **Dependencies**: Step 1 & 2
- **Parallelizable**: No

# Verification & Testing
- Open `WorldHUDCanvas` in the inspector.
- Verify `SaveGameOverlay` and `LoadSavePanel` have their text fields assigned.
- (Manual) Run the game, open the pause menu, click "Load", and verify all wording is load-related.
- (Manual) Run the game, open the pause menu, click "Save", and verify all wording is save-related.
