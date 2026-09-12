# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG set in a zombie-infested world, featuring third-person combat, base building, and inventory management.
- Players: Single player.
- Inspiration / Reference Games: DayZ, Project Zomboid (3D).
- Tone / Art Direction: Gritty, survival.
- Target Platform: PC (Windows).
- Screen Orientation / Resolution: Landscape.
- Render Pipeline: Universal Render Pipeline (URP).

# Game Mechanics
## Core Gameplay Loop
Players scavenge resources, fight zombies, build fortifications, and manage their survival stats (health, stamina, hunger).
## Controls and Input Methods
The game uses the New Input System for movement and combat. UI interactions are handled via UGUI.

# UI
## Main Menu Buttons
The main menu buttons (Continue, Start, Load, Settings, Quit) will be modified to have a pill-shaped "curved" appearance and a flashing highlight effect when hovered.

# Key Asset & Context
- **Sprite**: `Assets/Art/UI/Generated/MainMenuButtonRounded.png`
- **Script**: `Assets/Scripts/UI/Effects/ButtonHoverFlash.cs` (To be created)
- **Prefab**: `Assets/Shared/Prefabs/UI/Menus/MainMenu.prefab`
- **Scene**: `Assets/00_Scenes/MainMenu.unity`

# Implementation Steps
## 1. Adjust Button Shape (Curved/Pill)
- **Description**: Update the `MainMenuButtonRounded` sprite's border settings or the `Image` component properties to achieve a pill-shaped look. 
- **Files**: `Assets/Art/UI/Generated/MainMenuButtonRounded.png`, `Assets/Shared/Prefabs/UI/Menus/MainMenu.prefab`
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Create Button Hover Flash Script
- **Description**: Create a new C# script `ButtonHoverFlash` that implements `IPointerEnterHandler` and `IPointerExitHandler`. It will pulse the brightness or an overlay alpha when the mouse hovers over the button.
- **Files**: `Assets/Scripts/UI/Effects/ButtonHoverFlash.cs`
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 3. Wire Script to Main Menu Buttons
- **Description**: Add the `ButtonHoverFlash` component to the buttons in the `MainMenu.prefab`. Configure the flash color and speed.
- **Files**: `Assets/Shared/Prefabs/UI/Menus/MainMenu.prefab`
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

## 4. Verify in Scene
- **Description**: Ensure the changes reflect in the `MainMenu.unity` scene and work correctly in Play Mode.
- **Files**: `Assets/00_Scenes/MainMenu.unity`
- **Assigned role**: developer
- **Dependencies**: Step 1, Step 3
- **Parallelizable**: No

# Verification & Testing
- **Visual Check**: Open the `MainMenu` scene in the editor. Verify buttons are pill-shaped (fully rounded ends).
- **Interactive Check**: Enter Play Mode. Hover over each button in the stack. Verify a smooth pulsating flash effect triggers on hover and stops when the mouse leaves.
- **Prefab Consistency**: Check that the `CloseButton` in sub-panels also inherits the new style/effect.
