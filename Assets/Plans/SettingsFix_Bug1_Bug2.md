# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG settings UI fixes.
- Players: Single player
- Target Platform: PC

# Game Mechanics
## Bug Fixes
- Fix settings panel auto-opening or failing to reopen.
- Fix "Reset To Defaults" button alignment in the navigation panel.

# UI
- Settings Panel (SettingsPage_StyleA prefab)
- Left Navigation Panel

# Key Asset & Context
- `SettingsPageController.cs`: Controls settings panel lifecycle and animations.
- `SettingsPage_StyleA.prefab`: The UI prefab for the settings menu.

# Implementation Steps
1. **Modify SettingsPageController.cs**
    - **Description**: Remove the `OnEnable` override to prevent auto-opening. Update the `Show()` method to call `gameObject.SetActive(true)` before starting the opening animation.
    - **Assigned role**: developer
    - **Dependencies**: None
    - **Parallelizable**: Yes

2. **Reorder Children in SettingsPage_StyleA.prefab**
    - **Description**: In the `LeftNavPanel`, move the `ResetButton` to be positioned directly after the `ACCESSIBILITY_Button` and before the `Spacer`. This ensures the button is flush with the navigation items while the spacer pushes remaining space to the bottom.
    - **Assigned role**: developer
    - **Dependencies**: None
    - **Parallelizable**: Yes

# Verification & Testing
1. **Play Mode Test**: Press ESC to pause. Verify the settings panel is NOT visible.
2. **Open Settings**: Click the Settings button. Verify the panel opens with animation.
3. **Close and Reopen**: Close the settings panel, then click the Settings button again. Verify it reopens correctly (original bug fix).
4. **Layout Check**: Verify the "RESET TO DEFAULTS" button appears directly below the "ACCESSIBILITY" navigation item.
