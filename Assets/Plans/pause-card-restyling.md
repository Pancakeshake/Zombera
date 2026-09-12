# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG in a zombie-infested world.
- Players: Single player.
- Target Platform: PC (Windows).
- Render Pipeline: URP.
- UI System: uGUI.

# Game Mechanics
## Core Gameplay Loop
The game features a pause menu that allows players to resume, change settings, or exit. The user wants to improve its styling to match a gritty, survival-themed reference image.

# UI
## Pause Card Improvements
The current `PauseCard.prefab` will be updated to match the reference image's aesthetic:
- **Background**: Use `Assets/Art/UI/pause_menu.png` as the main panel background.
- **Title**: Large, bold "PAUSED" text.
- **Separator**: A thin line with a biohazard icon in the center.
- **Menu Items**: A list of buttons with icons, left-aligned text, and a red "rough" border for the selected item.
- **Styling**: Gritty textures, dark semi-transparent backgrounds for items, and thin separator lines.

# Key Asset & Context
- **Prefab**: `Assets/01_Game/08_UI/PauseCard.prefab`
- **Background Sprite**: `Assets/Art/UI/pause_menu.png`
- **Icons**:
    - Biohazard: `Packages/com.unity.dt.app-ui/PackageResources/Icons/Regular/Biohazard.png`
    - Resume: `Packages/com.unity.dt.app-ui/PackageResources/Icons/Regular/Play.png`
    - Settings: `Packages/com.unity.dt.app-ui/PackageResources/Icons/Regular/Gear.png`
    - Inventory: `Packages/com.unity.dt.app-ui/PackageResources/Icons/Regular/Backpack.png`
    - Map: `Packages/com.unity.dt.app-ui/PackageResources/Icons/Regular/MapTrifold.png`
    - Journal: `Packages/com.unity.dt.app-ui/PackageResources/Icons/Regular/BookOpenText.png`
    - Missions: `Packages/com.unity.dt.app-ui/PackageResources/Icons/Regular/Article.png`
    - Save Game: `Packages/com.unity.dt.app-ui/PackageResources/Icons/Regular/User.png`
    - Exit: `Packages/com.unity.dt.app-ui/PackageResources/Icons/Regular/SignOut.png`
- **Highlight Sprite**: A "rough" red border will be generated to match the reference.

# Implementation Steps
1. **Generate Highlight Asset**:
    - Use AI to generate a rough, hand-drawn red rectangular border sprite.
    - Save as `Assets/01_Game/08_UI/Sprites/UI_Selection_Rough.png`.
2. **Modify PauseCard Prefab Hierarchy**:
    - **Root**: 
        - Apply `pause_menu.png` to the `Image` component.
        - Ensure it's correctly sliced if it's a 9-slice sprite.
    - **Header**:
        - Adjust `Title` (TMP) font size to 48 and style to Bold.
        - Add `Separator` object below title containing a thin white line and the `Biohazard` icon.
    - **Button List**:
        - Modify the existing `VerticalLayoutGroup` to remove spacing and add padding.
        - For each Button:
            - Set height to 50.
            - Add a `HorizontalLayoutGroup` for Icon and Label.
            - Add an `Image` for the Icon (left).
            - Set `Label` (TMP) to size 24, left-aligned.
            - Add a `BottomLine` (thin Image) for separation.
            - Add a `Highlight` object (Red rough border Image) that is disabled by default.
3. **Add Missing Buttons**:
    - Duplicate existing buttons to create INVENTORY, MAP, JOURNAL, MISSIONS, and SAVE GAME placeholders to match the reference.
4. **Update Button Logic**:
    - Ensure `PauseMenuController` references the updated button list if needed.
5. **Animation/Interactivity**:
    - Configure the `Button` components to enable/disable the `Highlight` object on Selection or Hover (via `OnSelect`/`OnDeselect` events or a simple script).

# Verification & Testing
1. **Manual Check**: Inspect the `PauseCard.prefab` in the scene view to ensure styling matches the reference.
2. **Runtime Test**: Enter Play Mode, pause the game, and verify button navigation and highlights.
