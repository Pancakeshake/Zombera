# Project Overview
- **Game Title**: Zombera
- **UI Context**: `WorldHUDCanvas` prefab (Pause Menu sub-panels).

# UI Task
- **Objective**: Update the `LoadSavePanel` in `WorldHUDCanvas` to be a styled clone of `SaveGameOverlay`.
- **Constraint**: Maintain the exact layout, colors, and components of `SaveGameOverlay`, but replace all "Save" terminology with "Load".

# Key Asset & Context
- **Prefab**: `Assets/Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab`
- **Parent Object**: `WorldHUDCanvas/PauseMenuPanel`
- **Controllers Involved**: 
  - `SaveGameMenuController` (on the panels)
  - `PauseMenuController` (on `PauseMenuPanel`)

# Implementation Steps

## 1. Refresh LoadSavePanel from SaveGameOverlay
- **Description**: Replace the existing `LoadSavePanel` with a duplicate of the preferred `SaveGameOverlay` style.
  - Open `Assets/Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab`.
  - Delete the current `LoadSavePanel` game object.
  - Duplicate `SaveGameOverlay` and name the clone `LoadSavePanel`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## 2. Update Terminology and Naming
- **Description**: Localization and naming updates for the Load context.
  - In the new `LoadSavePanel`:
    - **Header**: Change text to `[LOAD GAME]`.
    - **Subtitle**: Change text to `[Choose a slot to load your progress.]`.
    - **Footer**: Rename `SaveGameButton` to `LoadGameButton`.
    - **LoadGameButton/Text**: Change text to `[LOAD GAME]`.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: Yes

## 3. Re-Wire Controllers
- **Description**: Update component references to ensure logic points to the new objects.
  - **SaveGameMenuController** (on `LoadSavePanel`):
    - Assign `panelRoot` to the `LoadSavePanel` itself.
    - Assign `saveButton` to the new `LoadGameButton`.
    - Assign `saveButtonLabel` to the text child of `LoadGameButton`.
    - (Ensure other references like `slotContainer`, `headerText`, etc., are correct duplicates).
  - **PauseMenuController** (on `PauseMenuPanel`):
    - Assign `loadSavePanel` property to the new `LoadSavePanel` instance.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

# Verification & Testing
- **Manual Verification**:
  1. Open a scene with `WorldHUDCanvas` (or open the prefab in isolation).
  2. Verify `PauseMenuPanel` hierarchy has both `SaveGameOverlay` and `LoadSavePanel`.
  3. Verify `LoadSavePanel` text and button names are "Load" themed.
  4. Enter Play Mode, pause the game, and click "Load": Verify the new panel appears and looks identical to the Save panel but with "Load" labels.
- **Visual Check**:
  - Compare `SaveGameOverlay` and `LoadSavePanel` side-by-side: they should be identical except for the text strings.
