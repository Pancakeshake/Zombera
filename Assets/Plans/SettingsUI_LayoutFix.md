# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG UI Layout Fixes.
- Players: Single player
- Target Platform: PC

# Game Mechanics
## UI Layout Fixes
The `SettingsPage` UI has a responsive layout issue where the `RightInfoPanel` is hidden at resolutions below 1200px, causing the `CenterPanel` to expand and appear to "push" the info panel off-screen. This plan lowers the visibility threshold and optimizes panel widths for better balance across resolutions.

# UI Changes
- **SettingsPageController**: Lower the `isWide` threshold from 1200 to 1024.
- **SettingsPage_StyleA Prefab**:
    - **CenterPanel**: Reduce `preferredWidth` from 860 to 700.
    - **RightInfoPanel**: Ensure it is active by default.
    - **BodyRow**: Verify `HorizontalLayoutGroup` settings to ensure proportional shrinking.

# Key Asset & Context
- `SettingsPageController.cs`: Handles responsive UI logic for the settings menu.
- `SettingsPage_StyleA.prefab`: The UI prefab containing the layout panels.

# Implementation Steps

## 1. Update Responsive Logic
- **Description**: Modify `SettingsPageController.cs` to lower the screen width threshold for showing the right info panel.
- **File**: `Assets/01_Game/08_UI/Scripts/Menus/SettingsV2/SettingsPageController.cs`
- **Change**: Set `bool isWide = Screen.width >= 1024;` (was 1200).
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Adjust Panel Layout Elements
- **Description**: Update the `LayoutElement` properties in the `SettingsPage_StyleA` prefab.
- **File**: `Assets/01_Game/08_UI/Prefabs/SettingsPage_StyleA.prefab`
- **Changes**:
    - Select `CenterPanel`. Change `Preferred Width` to **700**.
    - Select `RightInfoPanel`. Ensure `GameObject` is active.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

# Verification & Testing
1. **Resolution Test (1920x1080)**: Open Settings. Verify `LeftNavPanel`, `CenterPanel`, and `RightInfoPanel` are all visible and correctly aligned.
2. **Resolution Test (1280x720)**: Open Settings. Verify the `RightInfoPanel` is now visible (it was likely hidden before).
3. **Resolution Test (< 1024)**: Resize the window to below 1024. Verify the `RightInfoPanel` hides and the `CenterPanel` expands to fill the space smoothly.
4. **Visual Check**: Compare with the user's screenshots to ensure the "too much space" feeling is resolved by having the info panel return or having a more balanced center width.
