# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG in a zombie-infested world.
- Players: Single player
- Render Pipeline: URP

# Game Mechanics
- Core Gameplay Loop: Scavenge, Combat, Build, Survive.
- Controls and Input Methods: Mouse & Keyboard, Controller.

# UI
## Settings Panel Redesign
- **Visual Style**: Survival/Grunge theme (consistent with SaveGameOverlay).
  - Background: Dark (#080808) with alpha (~230).
  - Layers: BaseFill, RedGlowBorder (#7A1111), NoiseOverlay, InnerStroke.
  - Accent Color: Apocalypse Red (#D32F2F).
- **Layout**:
  - **Header**: "ZOMBERA SETTINGS" in large bold high-contrast font. Biohazard separator below.
  - **Tab Bar**: Horizontal buttons [ GAME ], [ GRAPHICS ], [ AUDIO ], etc.
  - **Scroll Area**: A ScrollRect containing all settings categories separated by lines.
  - **Footer**: Buttons [ APPLY ], [ RESET ], [ BACK ].

# Key Asset & Context
- `Assets/Shared/Prefabs/UI/Menus/PauseMenuPanel.prefab`: The main prefab to modify.
- `SettingsPanel`: The child panel to be restyled and restructured.
- `SettingsMenuController.cs`: Existing script handling settings logic.

# Implementation Steps
1. **Visual Layer Update**:
   - Update `SettingsPanel/Backdrop` and `SettingsPanel/SettingsCard` to use the layered grunge style (BaseFill, RedGlowBorder, NoiseOverlay, InnerStroke).
   - assigned role: developer
2. **Header & Separator**:
   - Update Title to "ZOMBERA SETTINGS".
   - Add the Biohazard separator line from `PauseCard` or create a new one.
   - assigned role: developer
3. **Tab Bar Implementation**:
   - Create a horizontal layout for the category tabs [ GAME | GRAPHICS | AUDIO | ... ].
   - Style buttons with the "Survival" aesthetic (red glow highlights).
   - assigned role: developer
4. **Scrollable Content Area**:
   - Add a `ScrollRect` (SettingsList) to the middle of the panel.
   - Inside the Content, create sections for each category (Graphics, Audio, Controls, etc.) separated by horizontal lines.
   - assigned role: developer
5. **Settings Rows Creation**:
   - Create a reusable "SettingsRow" structure (Label on left, Control on right).
   - Implement Sliders, Dropdowns, and Toggles (Buttons) in the grunge style.
   - assigned role: developer
6. **Footer Update**:
   - Add [ APPLY ], [ RESET ], [ BACK ] buttons to the bottom footer area.
   - Style them as primary red/dark buttons.
   - assigned role: developer
7. **Script Binding**:
   - Ensure `SettingsMenuController` is updated to reference the new UI elements if necessary (or at least the close/back button).
   - assigned role: developer

# Verification & Testing
- Open `PauseMenuPanel.prefab` in the editor.
- Verify the `SettingsPanel` visuals match the `SaveGameOverlay`.
- Verify the scrollable area functions correctly.
- Check that all categories from the request are present.
- Ensure the Close/Back buttons trigger the `Hide()` method on the controller.
