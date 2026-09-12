# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with building, scavenging, and combat.
- Players: Single player
- Target Platform: PC (Windows)
- Render Pipeline: URP

# Game Mechanics
## Settings UI Integration
The goal is to replace the legacy settings panel in the `WorldHUDCanvas` with the newly created `SettingsPage_StyleA` UI, ensuring it follows the AAA cinematic glass style and supports proper layout for "Apply" and "Reset Defaults" buttons.

# UI Changes
- **Settings Layout**: Fix the orientation of button groups from vertical to horizontal.
- **HUD Integration**: Replace the old `SettingsPanel` inside `WorldHUDCanvas` with the new system.
- **Pause Menu Logic**: Update `PauseMenuController` to interact with the new `SettingsPageController`.

# Key Assets & Context
- **Scripts**:
    - `Assets/01_Game/08_UI/Scripts/Menus/SettingsV2/SettingsPageController.cs` (Modify to add compatibility methods/events)
    - `Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs` (Modify to update reference type)
- **Prefabs**:
    - `Assets/01_Game/08_UI/Prefabs/SettingsPage_StyleA.prefab` (Fix layouts)
    - `Assets/Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab` (Replace settings panel)

# Implementation Steps

## 1. Script Compatibility Update
- **Description**: Add the `OnHide` event, `IsVisible` property, and `Show()`/`Hide()` methods to `SettingsPageController` to match the interface used by `PauseMenuController`. Update `PauseMenuController` to use `SettingsPageController` instead of `SettingsMenuController`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Fix Prefab Layouts (SettingsPage_StyleA)
- **Description**: 
    - In `SettingsPage_StyleA.prefab`, find the `BottomBar/ButtonContainer`. Ensure it uses a `HorizontalLayoutGroup` with proper spacing (12px) and no force expand.
    - Find the `ResetButton` in the `LeftNavPanel`. Add a `HorizontalLayoutGroup` to it to ensure its internal elements (Border, Text) are laid out horizontally. Set its `LayoutElement` preferred height to 42px.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 3. Integration into WorldHUDCanvas
- **Description**: 
    - Open `WorldHUDCanvas.prefab`.
    - Under `PauseMenuPanel`, remove the existing `SettingsPanel`.
    - Extract the `SettingsRoot` hierarchy from `SettingsPage_StyleA.prefab` (excluding the outer Canvas/Scaler) and place it under `PauseMenuPanel`.
    - Rename the object to `SettingsPanel` for consistency.
    - Update the `PauseMenuController` reference for `settingsPanel` to point to the new object.
- **Assigned role**: developer
- **Dependencies**: Step 1 & 2
- **Parallelizable**: No

# Verification & Testing
- Open the `WorldHUDCanvas` in the scene.
- Ensure the `PauseMenuController` has no missing references.
- Test the "Apply" and "Back" buttons in the prefab editor to ensure they are horizontal.
- Verify the "Reset Defaults" button layout.
- (Manual) Run the game (from Boot), pause, and open settings to ensure the new UI appears and functions correctly.
