# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG set in a zombie-infested world with base building and inventory management.
- Players: Single player.
- Render Pipeline: URP.
- Target Platform: PC (StandaloneWindows64).

# Game Mechanics
## Core Gameplay Loop
Scavenge resources, fight zombies, build bases, and survive while managing stats and inventory.
## Controls and Input Methods
Uses the New Input System for movement, combat, and UI interaction.

# UI
## Main Menu Enhancement
- **Continue Button**: Appears above "New Game" if a save exists. Loads the most recent save.
- **Settings Page**: Fully functional menu with Volume Slider and Quality Dropdown.
- **Load Game Menu**: A scrollable list of available saves, allowing the player to choose a slot to load.

# Key Assets & Context
- `MainMenuController.cs`: Coordinates main menu actions.
- `SettingsMenuController.cs`: Manages audio and quality settings.
- `SaveSystem.cs`: Handles save file I/O and indexing.
- `MainMenu.prefab`: The main menu UI asset.

# Implementation Steps

## 1. Expose Save Metadata in `SaveSystem`
Modify `SaveSystem.cs` to provide information about available saves.
- Add `public List<string> GetAvailableSlotIds()`: Returns the list of slots from the metadata index.
- Add `public string GetMostRecentSlotId()`: Returns the slot ID of the most recently modified save file.

## 2. Create `LoadSaveMenuController`
Implement a new controller to manage the Load Save panel.
- **File**: `Assets/01_Game/08_UI/Scripts/Menus/LoadSaveMenuController.cs`
- **Responsibilities**:
    - Populates a list of buttons for each save slot.
    - Handles slot selection and triggers loading via `GameManager`.
    - Handles closing the panel.

## 3. Update `MainMenuController`
Modify `MainMenuController.cs` and its partials to integrate the new features.
- **Fields**:
    - `[SerializeField] private Button continueButton;`
    - `[SerializeField] private LoadSaveMenuController loadSavePanel;`
- **Logic**:
    - In `Initialize()`, check if any saves exist.
    - Enable/Disable `continueButton` based on save existence.
    - If `continueButton` is active, ensure it is positioned above `startGameButton`.
    - Implement `HandleContinueRequested()`: Calls `GameManager.Instance.LoadGame(latestSlot)`.
    - Implement `HandleLoadGameRequested()`: Calls `loadSavePanel.Show()`.

## 4. Enhance `MainMenu.prefab`
- **Settings Panel**:
    - Add a `Slider` for Master Volume.
    - Add a `TMP_Dropdown` for Quality Settings.
    - Assign these to the `SettingsMenuController` component.
- **Continue Button**:
    - Instantiate a new button above "Start Game".
    - Label it "CONTINUE".
- **Load Save Panel**:
    - Create a new panel (can be a copy of `SettingsPanel` for consistency).
    - Add a `ScrollRect` with a `VerticalLayoutGroup` for the save list.
    - Assign the `LoadSaveMenuController` and wire its references.

## 5. Wiring and Testing
- Wire all new buttons in `MainMenuController.Wiring.cs`.
- Verify the "Continue" button only appears when a save exists.
- Verify the "Settings" menu correctly adjusts volume and quality.
- Verify the "Load Game" menu correctly lists and loads saves.

# Verification & Testing
- **Save Existence Test**: Delete all saves, verify "Continue" is hidden. Create a save, verify "Continue" appears.
- **Settings Test**: Change volume, verify `AudioListener.volume` updates. Change quality, verify `QualitySettings` level updates.
- **Load Test**: Create multiple saves with different names/progress. Open "Load Game" and verify all appear and load the correct state.
