# Project Overview
- **Game Title**: Zombera
- **Task**: Fix Pause Menu visibility and layout issues.
- **Problem**: 
    1. Pause Menu appears immediately on scene load.
    2. Pause Menu is not fullscreen (no dim background covering the whole screen).

# Game Mechanics
## Core Gameplay Loop
- Survival RPG with pause/resume functionality.
## Controls and Input Methods
- 'Esc' toggles the Pause Menu.

# UI
- **Pause Menu Overlay**: Should be a fullscreen overlay that dims the background and centers the "PauseCard".

# Key Asset & Context
- `PauseMenuController.cs`: Handles visibility.
- `WorldHUDController.cs`: Triggers the menu.
- `Assets/Shared/Prefabs/UI/Menus/PauseMenuPanel.prefab`: The overlay prefab.
- `Assets/Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab`: The HUD container.

# Implementation Steps

## 1. Update PauseMenuPanel Prefab
- **Action**: Fix the RectTransform and add a dim background.
- **Details**:
    - Set `PauseMenuPanel` (root) RectTransform to **Stretch/Stretch** (Anchors 0,0 to 1,1, SizeDelta 0,0).
    - Add/Update an `Image` component on the root `PauseMenuPanel` with a semi-transparent black color (e.g., `RGBA(0, 0, 0, 0.6)`).
    - Ensure `PauseCard` child is centered (Anchors 0.5, 0.5, AnchoredPosition 0,0).
    - Set the `PauseMenuPanel` GameObject to **Inactive** in the prefab.

## 2. Sync Scene Instances
- **Action**: Ensure the instance of the pause menu in the scene matches the prefab or is also set to inactive.

## 3. Verify Script Logic
- **Action**: Ensure `PauseMenuController.Initialize()` correctly hides the panel on start.
- **Verification**: `Initialize()` calls `Hide()`, which sets `panelRoot.SetActive(false)`. This is correct.

# Verification & Testing
- **Immediate Visibility Test**: Run the game and ensure the pause menu does NOT appear automatically.
- **Esc Toggle Test**: Press 'Esc' and verify the pause menu appears as a **fullscreen** dim overlay with the card centered.
- **Resume Test**: Press 'Esc' again or click 'Resume' and verify the overlay disappears and the background is no longer dimmed.
