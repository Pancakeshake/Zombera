# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with zombies, base building, and resource management.
- Players: Single player (currently).
- Target Platform: PC (StandaloneWindows64).
- Screen Orientation: Landscape 1920x1080.
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
The player scavenges resources, builds bases, and survives against zombies. The save/load system is critical for persisting progress across sessions.

## Controls and Input Methods
- New Input System is used.
- UI is managed via UGUI.

# UI
- **Pause Menu**: Needs "Load Game" button. "Save Game" should open an overlay.
- **Load/Save Overlay**: Needs to support creating new save slots and overwriting existing ones.
- **Main Menu**: "Continue" button should load the most recent save.

# Key Asset & Context
- `PauseMenuController.cs`: Handles pause menu logic.
- `MainMenuController.cs`: Handles main menu logic.
- `LoadSaveMenuController.cs`: Handles the save/load slot list.
- `SaveSystem.cs` / `SaveManager.cs`: Handles backend saving/loading.

# Implementation Steps

## 1. UI Utility for Deselection
Create a small utility or method to deselect the current UI element to fix the "stuck highlighted" issue.

## 2. Enhance LoadSaveMenuController
- Add a `Mode` (Load vs Save).
- Add functionality to "Create New Save".
- Update `PopulateSlots` to include a "New Save" button when in Save mode.
- Update `HandleSlotSelected` to handle both Load and Save (overwrite).

## 3. Update PauseMenuController
- Add `loadButton` serializable field.
- Wire `loadButton` to show `LoadSavePanel` in `Load` mode.
- Wire `saveButton` to show `LoadSavePanel` in `Save` mode.
- Ensure buttons are deselected after clicking.

## 4. Update MainMenuController
- Ensure `HandleContinueRequested` is robust.
- Add deselection to all main menu button handlers.

## 5. Scripting Changes
- **File**: `Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs`
    - Add `enum MenuMode { Load, Save }`.
    - Add `Show(MenuMode mode)` method.
    - Implement "New Save" slot creation logic.
- **File**: `Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs`
    - Add `loadButton`.
    - Update `Save()` to call `loadSavePanel.Show(MenuMode.Save)`.
    - Add `Load()` to call `loadSavePanel.Show(MenuMode.Load)`.
- **File**: `Assets/01_Game/08_UI/Scripts/Menus/MainMenuController.StartFlow.cs`
    - Ensure `HandleContinueRequested` correctly loads the latest slot.
    - Add deselection logic.

# Verification & Testing
- **Stuck Highlights**: Hover and click buttons in Main Menu and Pause Menu; ensure they don't stay in the "Pressed" or "Selected" color after the action is performed.
- **Continue**: Start game, save, exit to main menu, click "Continue". Verify it loads the correct save.
- **Save Overlay**: In-game, click "Save Game". Verify the overlay opens. Click "New Save", give it a name (or use a default timestamp), and verify a new slot is created.
- **Load Overlay**: In-game, click "Load Game". Verify the overlay opens. Click a slot and verify it loads.
- **Main Menu Load**: Click "Load Game" on Main Menu. Verify it opens the overlay and loads correctly.
