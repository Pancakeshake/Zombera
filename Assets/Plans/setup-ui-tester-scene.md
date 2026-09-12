# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with squad management and UI-heavy gameplay.
- Tester Scene: `Assets/00_Scenes/SystemDevelopment/3_Ui.unity`.
- Goal: Create a tester scene that mimics the main game's setup (camera, lighting, HUD) but uses placeholder units for UI testing.

# UI
- The scene will include the `WorldHUDCanvas`, which manages the main gameplay HUD, squad panels, inventory, and pause menus.

# Key Asset & Context
- **Prefabs**:
  - `Assets/Shared/Prefabs/GameManager/[GameManager].prefab`
  - `Assets/Shared/Prefabs/Environment/Environment.prefab`
  - `Assets/Shared/Prefabs/UI/HUD/WorldHUDCanvas.prefab`
  - `Assets/Shared/Prefabs/Player/SquadMember_Runtime.prefab`
- **Scripts**:
  - `Assets/01_Game/99_Development/Scripts/UITesterBootstrapper.cs` (New)

# Implementation Steps

## 1. Scene Setup
- **Description**: Prepare the `3_Ui.unity` scene by removing defaults and adding core systems.
- **Assigned role**: developer
- **Files**: `Assets/00_Scenes/SystemDevelopment/3_Ui.unity`
- **Implementation**:
  - Open the scene.
  - Delete `Main Camera` and `Directional Light`.
  - Instantiate `[GameManager]`, `Environment`, and `WorldHUDCanvas` prefabs.

## 2. Unit Instantiation
- **Description**: Add placeholder units to the scene.
- **Assigned role**: developer
- **Files**: `Assets/00_Scenes/SystemDevelopment/3_Ui.unity`
- **Implementation**:
  - Instantiate 3 instances of `SquadMember_Runtime`.
  - Name them `Unit_Alpha`, `Unit_Bravo`, and `Unit_Charlie`.
  - Position them at `(0,0,0)`, `(3,0,0)`, and `(-3,0,0)`.

## 3. Create Bootstrapper Script
- **Description**: Create a script to handle runtime initialization for the tester scene.
- **Assigned role**: developer
- **Files**: `Assets/01_Game/99_Development/Scripts/UITesterBootstrapper.cs`
- **Implementation**:
  - Write a script that sets the `GameManager` state to `Playing` on `Start()`.
  - Ensure the first unit is selected in the `SquadManager` to populate the HUD.

## 4. Scene Finalization
- **Description**: Add the bootstrapper to the scene and verify.
- **Assigned role**: developer
- **Files**: `Assets/00_Scenes/SystemDevelopment/3_Ui.unity`
- **Implementation**:
  - Create a GameObject named `[UITester]` and attach `UITesterBootstrapper`.
  - Save the scene.

# Verification & Testing
- Enter Play Mode in the `3_Ui.unity` scene.
- Verify that the World HUD appears automatically.
- Check that three portraits appear in the bottom squad strip.
- Verify that pressing `Tab` opens the squad/inventory panels.
- Verify that pressing `Escape` opens the Pause menu and that Save/Settings/Quit buttons are functional.
