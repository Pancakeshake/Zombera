# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG set in a zombie-infested world with base building, inventory management, and tactical combat.
- Players: Single player (with squad members/survivors)
- Inspiration / Reference Games: State of Decay, Project Zomboid, 7 Days to Die
- Tone / Art Direction: Realistic, gritty survival
- Target Platform: PC (StandaloneWindows64)
- Screen Orientation / Resolution: Landscape
- Render Pipeline: Universal Render Pipeline (URP)

# Game Mechanics
## Core Gameplay Loop
1. Scavenge: Explore the world and loot containers for resources.
2. Build: Construct and fortify bases using scavenged materials.
3. Survive: Manage unit stats (Health, Stamina, Hunger) and fight off zombie threats.
4. Progress: Improve skills through action and survive for as many days as possible.

## Controls and Input Methods
- Movement: WASD
- Interaction: E (Interacting with containers, picking up items)
- Combat: Mouse buttons (Attacking)
- UI: I (Inventory), ESC (Pause/Menu)
- Input Backend: Unity New Input System

# UI
- Main Menu: Start New Game, Load Game, Settings.
- Save/Load Menu: List of slots with metadata (Day, Time, Location, Progress, Screenshot).
- HUD: Health/Stamina bars, Inventory slots, Minimap.

# Key Asset & Context
- `SaveManager.cs`: Orchestrates the capture and restoration of the game state.
- `SaveSystem.cs`: Handles low-level I/O, serialization (JSON), and slot management.
- `SaveGameMenuController.cs`: Controls the Save/Load UI panels.
- `GameSaveData`: The data structure containing all persisted states.

# Implementation Steps

## Step 1: Restore Symmetry in SaveManager
Update `SaveManager` to fully restore the state it captures during saving.
- **Inventory Restoration**: Update `RestorePlayerState` to clear current items and add saved items to `UnitInventory`.
- **Base Restoration**: Implement `RestoreBaseData`. Find all `Blueprint` objects in the scene. If their `BuildingId` matches a saved completed ID, call `MarkCompleted()`.
- **Loot Restoration**: Implement `RestoreLootContainerData`. Find all `LootContainer` objects and call `RestoreLootState` with items reconstructed from saved IDs.
- **Item Lookup**: Implement a utility method to find `ItemDefinition` assets by `itemId` (e.g., using `Resources.FindObjectsOfTypeAll<ItemDefinition>()` or a pre-populated registry).

## Step 2: Enhance SaveSystem API
Add missing functionality to the `SaveSystem` to support full menu operations.
- **Delete API**: Implement `DeleteSave(string slotId)` to remove the `.sav` and `.sav.bak` files and update the index.
- **Rename API**: Implement `RenameSave(string slotId, string newName)` to update the metadata name and rename files.
- **Metadata Discovery**: Update `LoadMetadataIndex` or add `GetAllSlotMetadata()` to ensure existing saves on disk are discovered and their metadata is available for the UI without loading the full payload.

## Step 3: UI Implementation & Polish
Wire up the UI buttons and add visual feedback.
- **Menu Logic**: Update `SaveGameMenuController` to use the new metadata discovery.
- **Delete/Rename**: Implement `HandleDelete` and `HandleRename` in `SaveGameMenuController`, wiring the existing buttons.
- **Screenshots**: Implement a utility to decode Base64 strings from `SaveMetadata.screenshotBase64` into `Sprite` objects for display in the save slots and details panel.

## Step 4: Logic & Lifecycle Fixes
- **Autosave Initialization**: Update `GameManager` to set a default `ActiveSlotId` (e.g., "NewGame_Timestamp") in `SaveManager` when starting a new game, enabling the autosave timer.
- **Fallback Removal**: Remove or fix the `saveSystem?.LoadGame(slotId)` fallback in `GameManager` to ensure `SaveManager` always handles restoration.

# Verification & Testing
1. **Symmetry Test**: Start game, build a structure, loot a container, save. Quit and reload. Verify structure is built, container is empty/half-full as left, and inventory is correct.
2. **Menu Test**: Create a save, restart game. Verify save appears in menu immediately. Delete save and verify file is gone and UI refreshes.
3. **Autosave Test**: Start new game, wait for autosave interval. Verify a new save file is created without manual intervention.
4. **Visual Test**: Capture a save and verify the screenshot appears correctly in the load menu.
