# Project Overview
- Game Title: Zombera
- High-Level Concept: Animation Blend Tree testing scene and tool organization.
- Goal: Move existing animation tools to a new menu and create a dedicated animation testing scene generator.

# Implementation Steps

## 1. Reorganize Animation Tools Menu
Update the `MenuItem` paths in existing scripts to reside under `Tools/5. Animation Tools/`.
- **Files**:
    - `Assets/Editor/PlayerAnimatorSetup.cs`
    - `Assets/Editor/ZomberaAnimatorBatchTools.cs`
    - `Assets/Editor/ZombieAnimatorSetup.cs`
- **Action**: Rename "Tools/Zombera/Animation/" to "Tools/5. Animation Tools/".

## 2. Create Animation Test Controller Component
Implement a runtime component that provides a debug UI to control player and zombie animations.
- **File**: `Assets/Scripts/Animation/AnimationTestControllerUI.cs`
- **Features**:
    - IMGUI-based overlay (to avoid prefab dependencies in simple test scenes).
    - Sliders: Speed, Direction (VelocityX/Z).
    - Toggles: Combat Mode, Crouching, Sprinting.
    - Buttons: Force Hit, Force Death, Attack.
    - Spawning: Buttons to spawn 1, 10, or 50 zombies using the `ZombieSpawner`.

## 3. Create Test Scene Generator Tool
Create an editor tool that automates the setup of the "Test_AnimationBlendTrees" scene.
- **File**: `Assets/Editor/TestAnimationBlendTreesTool.cs`
- **Menu Path**: `Tools/5. Animation Tools/Create Animation Test Scene`
- **Scene Setup**:
    - New Scene: `Assets/Scenes/Testing/Test_AnimationBlendTrees.unity`.
    - Basic environment: Directional Light, Floor Plane (100x100).
    - Bootstrap: GameObject with `TestScenePlayerBootstrap` configured with the Player prefab.
    - AI Setup: GameObject with `ZombieSpawner` configured with the Zombie prefab.
    - UI Setup: GameObject with `AnimationTestControllerUI`.

# Key Assets & Context
- **Player Prefab**: `Assets/Prefabs/Player/Player.prefab`
- **Zombie Prefab**: `Assets/Prefabs/Zombies/Zombie Type1.prefab`
- **Scripts**:
    - `Zombera.Testing.TestScenePlayerBootstrap` (handles player spawn/camera wiring).
    - `Zombera.AI.ZombieSpawner` (handles zombie spawning).

# Verification & Testing
1. **Menu Check**: Verify that `Tools/5. Animation Tools/` contains all four tools (Rewire Player, Rewire Zombie, Apply Performance, Create Test Scene).
2. **Scene Creation**: Run the tool and verify the scene loads with a floor, light, and setup objects.
3. **Runtime Interaction**:
    - Press Play.
    - Use the UI sliders to move the player.
    - Use the Spawn buttons to add zombies.
    - Use the Force Hit/Death buttons on the player and zombies.
    - Verify blend tree transitions (walking -> running -> sprinting).
