# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG UI fixes.
- Players: Single player

# Game Mechanics
## UI Logic Fix: Save/Load Routing
Currently, the `PauseMenuController` prioritizes the `saveGameOverlay` for both Save and Load operations. This causes the "Load" menu to show the "Save Game" panel (even if in Load mode), which prevents independent customization of the two screens. This plan fixes the routing so "Save" uses the dedicated `saveGameOverlay` and "Load" uses the dedicated `loadSavePanel`.

# UI
- `PauseMenuPanel` (HUD and Menu)

# Key Asset & Context
- `PauseMenuController.cs`: Handles routing for Save/Load buttons.

# Implementation Steps

## 1. Modify PauseMenuController.cs
- **Description**: Update the `Load()` and `Save()` methods to correctly prioritize their respective panels.
- **File**: `Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs`
- **Changes**:
    - In `Load()`, check for `loadSavePanel` first. If it exists, call `loadSavePanel.ShowLoadMode()`. Fallback to `saveGameOverlay.ShowLoadMode()` only if `loadSavePanel` is null.
    - In `Save()`, ensure it prioritizes `saveGameOverlay` (already doing this, but ensure consistency).
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

# Verification & Testing
1. **Save Routing**: Click "Save" in the pause menu. Verify that the `SaveGameOverlay` object is the one that becomes active.
2. **Load Routing**: Click "Load" in the pause menu. Verify that the `LoadSavePanel` object is the one that becomes active.
3. **Fallback Check**: Temporarily null out one of the references in the inspector. Verify the menu falls back to the other panel correctly.
