# Project Overview
- Game Title: Zombera
- Goal: Add a "LOAD GAME" button to the pause menu below "SAVE GAME".
- Target Platform: PC (Windows)

# UI
- The pause menu current state has Resume, Save Game, Settings, and Exit.
- New Button: "LOAD GAME" with identical styling, placed between "SAVE GAME" and "SETTINGS".
- Icon: Using the "Download" icon from the existing icon library to match the set.

# Key Asset & Context
- `Assets/01_Game/08_UI/PauseCard.prefab`: The prefab where buttons are defined.
- `Assets/Shared/Prefabs/UI/Menus/PauseMenuPanel.prefab`: The prefab containing the `PauseMenuController` component.
- `PauseMenuController.cs`: Handles the logic for the Load button (already has a `Load()` method and `loadButton` field).

# Implementation Steps
1. **Update PauseCard.prefab**:
   - Duplicate `SaveButton` and rename to `LoadButton`.
   - Change label text to "LOAD GAME".
   - Change Icon sprite to `Download` (from `Packages/com.unity.dt.app-ui/PackageResources/Icons/Regular/Download.png`).
   - Move `LoadButton` between `SaveButton` and `SettingsButton` in the hierarchy.
   - Ensure it has the same `PauseMenuButtonHighlight` component setup (with `highlightObject` null as per previous task).

2. **Update PauseMenuPanel.prefab**:
   - Find the `PauseMenuController` component.
   - Assign the newly created `LoadButton` (within the `PauseCard` child) to the `loadButton` field.

3. **Verify wiring**:
   - Ensure `PauseMenuController.Initialize()` correctly wires the `Load` method to the `onClick` event of the new button.

# Verification & Testing
- Enter Play Mode.
- Open pause menu.
- Verify "LOAD GAME" appears below "SAVE GAME" with correct styling.
- Verify hover effect (text turns red, no box).
- Click "LOAD GAME" and verify it opens the Load menu (LoadSavePanel in Load mode).
