# Project Overview
- Game Title: Zombera
- Goal: Fix the pause menu layout to be centered, responsive, and aesthetically pleasing.
- Target Platform: PC (Windows)

# Game Mechanics
- Core Gameplay Loop: Scavenge, Combat, Build, Survive.
- Controls: New Input System.

# UI
- The pause menu current state has a sidebar layout that doesn't fill the screen correctly and causes text wrapping issues.
- Goal: Center all menu content (Title, Buttons) over the full-screen background. Use a full-screen dark overlay for consistency.

# Key Asset & Context
- `Assets/01_Game/08_UI/PauseCard.prefab`: The prefab representing the main pause menu content.
- `PauseMenuController.cs`: The script managing the menu logic.

# Implementation Steps
1. **Center Content in PauseCard.prefab**:
   - Open `Assets/01_Game/08_UI/PauseCard.prefab` in edit mode.
   - **`Content` Container**:
     - Change anchors to (0, 0) - (1, 1) to cover the full screen with the dim overlay.
     - Update `VerticalLayoutGroup`: Set `Child Alignment` to `MiddleCenter`. Increase `Spacing` to 30.
   - **`Title` Text**:
     - Set TMP alignment to `Center`.
     - Disable Word Wrapping and set Overflow to `Truncate` or `Overflow` to prevent "PAUSE D".
     - Ensure the RectTransform width is large enough (e.g., 1000) or controlled by VLG.
   - **Buttons** (`ResumeButton`, `SaveButton`, `SettingsButton`, `QuitButton`):
     - For each button, add a `HorizontalLayoutGroup`:
       - `Child Alignment`: `MiddleCenter`.
       - `Spacing`: 20.
       - `Child Control Width/Height`: False.
       - `Child Force Expand Width/Height`: False.
     - Set the `Label` child to use `Alignment`: `Left` (to stay next to icon) but the container is centered.
     - Reduce `Label` RectTransform width to `400` or use a `ContentSizeFitter` (Preferred Width) to avoid wide empty clickable areas.
   - **Separator**:
     - Ensure the `Separator` container is centered and has a reasonable width.

2. **Verify Layout in Development Scene**:
   - Open `Assets/00_Scenes/SystemDevelopment/3_Ui.unity`.
   - Ensure no scene overrides are breaking the centered layout.

# Verification & Testing
- Enter Play Mode or use Scene View at different resolutions.
- Confirm "PAUSED" text is centered and not wrapping.
- Confirm all buttons are centered and icons are next to labels.
- Confirm the full-screen dim covers the street background.
