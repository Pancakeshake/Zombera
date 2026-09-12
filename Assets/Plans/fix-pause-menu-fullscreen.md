# Project Overview
- Game Title: Zombera
- Goal: Make the World HUD pause menu fully cover the screen and scale correctly across resolutions.
- Target Platform: PC (Windows)
- Screen Orientation: Landscape 1920x1080 (Reference)

# Game Mechanics
- Core Gameplay Loop: Scavenge, Combat, Build, Survive.
- Controls: New Input System.

# UI
- The pause menu current state has a sidebar layout that doesn't fill the screen with its background image.
- Goal: Ensure the menu background (street image) fills the entire screen while keeping the interactive content (buttons) in a readable sidebar area.

# Key Asset & Context
- `Assets/01_Game/08_UI/PauseCard.prefab`: The prefab representing the main pause menu content.
- `Assets/Shared/Prefabs/UI/Menus/PauseMenuPanel.prefab`: The root container for the pause menu system.
- `PauseMenuController.cs`: The script managing the menu logic.

# Implementation Steps
1. **Move Background Image in PauseCard.prefab**:
   - Open `Assets/01_Game/08_UI/PauseCard.prefab` in edit mode.
   - Move the `UnityEngine.UI.Image` component from the `Content` child to the `PauseCard` root object.
   - Configure the root `Image`:
     - Set `Image Type` to `Simple` (or `Sliced` if it has borders, but `Simple` with `Preserve Aspect` off is likely desired for a full-screen street backdrop).
     - Ensure `Raycast Target` is enabled.
   - Ensure the `Content` child (the sidebar) no longer has the street image.
   - (Optional) Add a semi-transparent dark `Image` to `Content` if the text becomes unreadable against the street backdrop. Based on the screenshot, the text is white and might need a darker backing in the sidebar area.

2. **Refine Sidebar (Content) Layout**:
   - Keep the `Content` anchors as they are (approx 0.05 to 0.35 width) to maintain the sidebar positioning.
   - Ensure `VerticalLayoutGroup` remains on `Content`.

3. **Verify Parent Prefab**:
   - Check `Assets/Shared/Prefabs/UI/Menus/PauseMenuPanel.prefab`.
   - Ensure the `PauseCard` instance remains at full stretch (0,0 - 1,1) with no overrides.

4. **Verify Scene Instances**:
   - Re-check `Assets/00_Scenes/SystemDevelopment/3_Ui.unity` to ensure the `PauseCard` instance there hasn't regained any fixed-size overrides.

# Verification & Testing
- Enter Play Mode or use Scene View at different resolutions (1920x1080, 2560x1440, Ultrawide).
- Confirm the street background image fills the entire viewport.
- Confirm the "PAUSED" text and buttons stay on the left side within the sidebar area.
- Confirm all buttons are still clickable.
