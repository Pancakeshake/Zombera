# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with zombies, base building, and squad management.
- Players: Single player
- Target Platform: PC (StandaloneWindows64)
- Screen Orientation / Resolution: Landscape
- Render Pipeline: URP

# UI
The focus is on the `SaveGameOverlay` within the `3_Ui` development scene. This panel allows players to manage save slots (Save, Delete, Rename).

# Key Asset & Context
- **Scene**: `Assets/00_Scenes/SystemDevelopment/3_Ui.unity`
- **Controller**: `SaveGameMenuController.cs`
- **Item**: `SaveSlotItem.cs`
- **Managers**: `SaveManager.cs`, `SaveSystem.cs`
- **Goal**: Ensure the Save UI is robust, especially in the testing scene, by adding preflight checks, explicit feedback, and stable ID management.

# Implementation Steps

## 1. Robust Initialization (Managers)
### SaveSystem.cs
- **Modify `SaveGameData`**: Call `EnsureSaveFolderExists()` at the start of the method to guarantee the path exists even if `Initialize()` was skipped.
- **Assigned role**: developer
- **Dependencies**: None

### SaveManager.cs
- **Modify `SaveGame`**: Check if `saveSystem` is null or uninitialized. If uninitialized, call `saveSystem.Initialize()`.
- **Assigned role**: developer
- **Dependencies**: None

## 2. Stable ID Management (UI)
### SaveSlotItem.cs
- **Modify `SaveMetadata` handling**: Ensure the item stores a `slotId` even if it's a "New Slot" placeholder (e.g., pre-generated or handled via the controller).
- **Assigned role**: developer
- **Dependencies**: None

### SaveGameMenuController.cs
- **Add Status UI**: Identify or add a text field for status messages (e.g., "Saving...", "Error: SaveManager missing").
- **Update `HandleSave`**:
    - Perform hard preflight check for `_saveManager` and `_saveSystem`.
    - Show error message in UI if missing.
    - Generate and cache `slotId` for "New Slot" once.
    - Show "Saving..." status.
    - Log diagnostics: `selected slot id`, `is new slot`, `save manager found`, `save system initialized`.
    - On success: Refresh list and re-select the slot.
- **Assigned role**: developer
- **Dependencies**: Step 1

## 3. UI Testing Bootstrap
### SaveTestingBootstrap.cs (New Script)
- Create a script that ensures `SaveSystem` and `SaveManager` exist in the scene and are initialized.
- Add it to the `3_Ui.unity` scene.
- **Assigned role**: developer
- **Dependencies**: None

## 4. Verification & Testing
- **Test A**: Select existing slot -> Click Save -> Timestamp updates.
- **Test B**: Select New Slot -> Click Save -> Slot becomes real (ID/Name/Timestamp).
- **Test C**: Reopen menu -> Saved slot persists.
- **Test D**: Run in scene without managers -> UI shows error message.
- **Assigned role**: developer
- **Dependencies**: All previous steps

# Implementation Steps (Ordered)
1. **Description**: Update `SaveSystem.cs` and `SaveManager.cs` for robust initialization.
   - **Assigned role**: developer
   - **Dependencies**: None
   - **Parallelizable**: Yes
2. **Description**: Create `SaveTestingBootstrap.cs` and add to `3_Ui.unity`.
   - **Assigned role**: developer
   - **Dependencies**: None
   - **Parallelizable**: Yes
3. **Description**: Update `SaveSlotItem.cs` to handle stable IDs.
   - **Assigned role**: developer
   - **Dependencies**: None
   - **Parallelizable**: Yes
4. **Description**: Update `SaveGameMenuController.cs` with preflight checks, status feedback, and stable ID logic.
   - **Assigned role**: developer
   - **Dependencies**: Steps 1, 3
   - **Parallelizable**: No
