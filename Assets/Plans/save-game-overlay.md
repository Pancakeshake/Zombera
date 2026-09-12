# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG in a zombie-infested world.
- Players: Single player.
- Inspiration / Reference Games: survival games with complex save/load systems.
- Tone / Art Direction: Dark, gritty, tactical.
- Target Platform: PC (Windows).
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
The player scavenges, builds bases, and survives. The Save Game system allows players to persist their progress across multiple slots with detailed metadata.

## Controls and Input Methods
Standard mouse-driven UI interaction for menus.

# UI
## Save Game Overlay
An overlay that opens when the "SAVE GAME" button is clicked in the pause menu.
- **Header**: "SAVE GAME" (Large, stylized text).
- **Slot List**: Scrollable list of 5 slots (occupied or "NEW SLOT").
- **Detail Panel**: Shows metadata (Location, Play Time, Day, Difficulty, Progress, Version, Screenshot, Recent Activity) for the selected slot.
- **Buttons**: SAVE GAME (Red), DELETE, RENAME, BACK.

# Key Asset & Context
- `SaveGameMenuController.cs`: Existing script that needs to be wired to a new UI panel.
- `SaveSlotItem.cs`: Existing script for individual slot entries.
- `PauseMenuController.cs`: Needs update to open the new overlay and visually distinguish the Save button.
- `PauseMenuPanel.prefab`: Needs to be modified to include the new overlay.

# Implementation Steps
1. **Prepare SaveSlotItem Prefab**:
    - Create a new prefab `Assets/Shared/Prefabs/UI/Menus/SaveSlotItem.prefab`.
    - Setup hierarchy:
        - Root: `RectTransform` + `Image` (Selection Highlight) + `Button` (Action).
        - Left: `Image` (Screenshot).
        - Center: `TMP_Text` (Slot Name), `TMP_Text` (Stats - Day/Time/Location/Difficulty/Progress).
        - Right: `TMP_Text` (Timestamp), `GameObject` (Current Save Badge).
    - Attach `SaveSlotItem.cs` and wire all references.

2. **Prepare SaveGameMenu Overlay in PauseMenuPanel**:
    - Open `Assets/Shared/Prefabs/UI/Menus/PauseMenuPanel.prefab`.
    - Create a new child `SaveGameOverlay` (RectTransform, stretched to fill).
    - Add a semi-transparent dark background image.
    - Header: "SAVE GAME" (TMP_Text, top-left).
    - ScrollView: For slot items (left side).
        - Content: `VerticalLayoutGroup` + `ContentSizeFitter`.
    - Detail Panel: (right side).
        - Title: "SLOT X DETAILS".
        - Metadata rows: Location, Play Time, Day, Difficulty, Progress, Version.
        - Screenshot: `Image`.
        - Recent Activity: `TMP_Text` in a scrollable area or fixed size.
    - Footer: HorizontalLayoutGroup with buttons:
        - `SaveGameButton`: Red color.
        - `DeleteButton`: Gray.
        - `RenameButton`: Gray.
        - `BackButton`: Gray.
    - Top Right: `CloseButton` (X).
    - Attach `SaveGameMenuController.cs` and wire all references.

3. **Update PauseMenuController Logic**:
    - Modify `Assets/01_Game/08_UI/Scripts/Menus/PauseMenuController.cs`.
    - Add `[SerializeField] private SaveGameMenuController saveGameOverlay;`.
    - Update `Save()` method to call `saveGameOverlay.Show()`.
    - Update `Initialize()` to find and wire the overlay if not assigned.

4. **Styling and Visuals**:
    - Change the `SaveButton` in the main Pause Card to be red (`#D32F2F`) to match the "red save game option" description.
    - Set the `SaveGameButton` in the overlay to the same red.

# Verification & Testing
- Enter Play Mode from `Boot` scene.
- Press Pause to open the pause menu.
- Verify the "SAVE GAME" button is red.
- Click the red "SAVE GAME" button.
- Verify the "SAVE GAME" overlay opens and looks like the sample image.
- Verify slots are populated.
- Select a slot and verify details are updated.
- Test "Save", "Delete", and "Rename" buttons.
- Test "Back" and "X" buttons to return to the pause menu.
