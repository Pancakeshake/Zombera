# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with zombies, base building, and squad management.
- Players: Single player
- Inspiration / Reference Games: Kenshi, Project Zomboid, State of Decay
- Tone / Art Direction: Gritty, survival-focused
- Target Platform: PC (StandaloneWindows64)
- Screen Orientation / Resolution: Landscape
- Render Pipeline: URP

# UI
The focus is on the `WorldHUDCanvas` in the UI development scene. This canvas contains overlay panels (Squad, Inventory, etc.) and is managed by `WorldHUDController`.

# Key Asset & Context
- **Scene**: `Assets/00_Scenes/SystemDevelopment/3_Ui.unity`
- **GameObject**: `WorldHUDCanvas` (Root GameObject for the world overlay UI)
- **Script**: `WorldHUDController.Lifecycle.cs` (Handles Awake/Start/Enable logic for the HUD)
- **Current Issue**: `WorldHUDController.Awake` explicitly disables the `Canvas` component on startup to prevent it showing over the main menu. In the `3_Ui` testing scene, this results in the HUD being invisible by default until game state changes are triggered, which is inconvenient for UI development.

# Implementation Steps
1. **Modify `WorldHUDController.Lifecycle.cs`**:
   - Update the `Awake` method to skip disabling the `Canvas` component if the current scene is a designated testing or UI scene (e.g., `3_Ui` or contains "Tester").
   - This ensures that in development scenes, the UI is visible immediately without waiting for `GameManager` state transitions.
2. **Update `3_Ui.unity` Scene**:
   - Enable the `Canvas` component on the `WorldHUDCanvas` GameObject.
   - Ensure the `WorldHUDCanvas` GameObject itself is active.
   - This allows the UI to be visible as soon as the scene is loaded in the editor and on play start.

# Verification & Testing
1. **Manual Verification**:
   - Open the scene `Assets/00_Scenes/SystemDevelopment/3_Ui.unity`.
   - Verify `WorldHUDCanvas` and its `Canvas` component are enabled.
   - Enter Play Mode.
   - Confirm `WorldHUDCanvas` remains enabled and visible.
2. **Regression Testing**:
   - Open a "World" scene or the `Boot` scene.
   - Enter Play Mode and go through the normal flow.
   - Confirm that the HUD still behaves correctly (stays hidden until gameplay starts).
