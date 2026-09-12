# Project Overview
- **Game Title**: Zombera
- **High-Level Concept**: A survival RPG set in a zombie-infested world, featuring tactical combat, base building, and character progression.
- **Players**: Single player (PC).
- **Inspiration**: AAA-style cinematic settings interfaces.
- **Tone / Art Direction**: Dark, cinematic, survival-themed with high-contrast gold accents and "glass" style UI.
- **Target Platform**: PC (Windows).
- **Screen Orientation**: Landscape 1920x1080 (Reference).
- **Render Pipeline**: Universal Render Pipeline (URP).

# Game Mechanics
## Core Gameplay Loop
The settings UI allows players to configure their experience, including graphics quality, control bindings, and audio levels, without breaking the cinematic immersion.

## Controls and Input Methods
- **Mouse**: Primary interaction for clicking buttons, adjusting sliders, and selecting dropdown items.
- **Keyboard**: Navigation (Esc to close/back) and confirming changes (Enter).

# UI
## Layout Overview
- **Full Screen Overlay**: The Settings UI occupies the entire screen with a blurred background.
- **Left Navigation Rail**: Fixed width (290px), vertical list of categories (GENERAL, GRAPHICS, etc.).
- **Center Content Panel**: Flexible width, scrollable area containing categorized settings rows.
- **Right Description Panel**: Fixed width (340px), contextual information about the selected category or setting.
- **Bottom Action Bar**: Pinned to the bottom right with "APPLY" and "BACK" buttons.

## Visual Tokens
- **Base Background**: #0A1018 (alpha 0.88).
- **Accent Gold**: #D5B45B.
- **Text Primary**: #E6E7E8.
- **Text Secondary**: #A7B0B7.

# Key Asset & Context
- **Prefab**: `Assets/01_Game/08_UI/Prefabs/SettingsPage_StyleA.prefab`
- **Controller**: `Assets/01_Game/08_UI/Scripts/Menus/SettingsV2/SettingsPageController.cs`
- **Factory**: `Assets/01_Game/08_UI/Scripts/Menus/SettingsV2/SettingsRowFactory.cs`
- **Theme**: `Assets/01_Game/08_UI/Scripts/Menus/SettingsV2/SettingsThemeTokens.cs`
- **Data Model**: `Assets/01_Game/08_UI/Scripts/Menus/SettingsV2/SettingsDataModels.cs`

# Implementation Steps
## Step 1: Script Foundation
- **Description**: Create the directory structure and the four C# scripts with basic stubs.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## Step 2: Canvas & Root Hierarchy
- **Description**: Create the `SettingsCanvas` (Screen Space Overlay, Canvas Scaler) and the root hierarchy: `SettingsRoot`, `BackgroundImage`, `VignetteOverlay`, and `MainFrame`.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## Step 3: Top Bar & Bottom Bar
- **Description**: Build the `TopBar` with the "SETTINGS" title and close button. Build the `BottomBar` with "APPLY" and "BACK" buttons. Apply the theme colors (gold accents).
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

## Step 4: Body Layout (Columns)
- **Description**: Set up the `BodyRow` with a Horizontal Layout Group. Create the three columns: `LeftNavPanel`, `CenterPanel`, and `RightInfoPanel` with their specified widths.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

## Step 5: Left Navigation Panel
- **Description**: Implement the vertical list of category buttons. Add the "Reset To Defaults" button at the bottom. Implement the selection visual state (accent strip, gold text).
- **Assigned role**: developer
- **Dependencies**: Step 4
- **Parallelizable**: No

## Step 6: Center Settings Panel & Templates
- **Description**: Set up the `ScrollRect` in the `CenterPanel`. Create templates for Section Headers and Settings Rows (Dropdown, Toggle, Slider, Segmented Selector). Use `SettingsRowFactory` to manage these templates.
- **Assigned role**: developer
- **Dependencies**: Step 4
- **Parallelizable**: No

## Step 7: Controller Logic & Wiring
- **Description**: Implement `SettingsPageController` to handle category switching, updating the right info panel, and monitoring changes to enable the "APPLY" button. Add responsive logic to hide the right panel if screen width < 1200px.
- **Assigned role**: developer
- **Dependencies**: Step 5, Step 6
- **Parallelizable**: No

## Step 8: Animation & Polish
- **Description**: Add `CanvasGroup` to the root for fade-in (0.2s). Implement the slide-up animation for `MainFrame`. Add hover/pressed states for all interactive elements.
- **Assigned role**: developer
- **Dependencies**: Step 7
- **Parallelizable**: Yes

# Verification & Testing
- **Visual Match**: Compare the built prefab with the reference image in 1920x1080.
- **Responsiveness**: Test resolution switching (1280x720, 2560x1440, 3440x1440) and verify panel hiding at < 1200px.
- **Functionality**:
    - Switching categories updates the center list and right description.
    - Changing a setting enables the "APPLY" button.
    - "BACK" and "CLOSE" trigger a confirmation if changes are unsaved.
    - "RESET TO DEFAULTS" resets current category values.
- **Console**: Ensure no errors or warnings are logged during interactions.
