# Project Overview
- **Game Title**: Zombera
- **High-Level Concept**: Survival RPG with procedural world elements.
- **Task**: Disable procedural building spawning (City Builder) in the main game flow temporarily.

# Game Mechanics
## Core Gameplay Loop
The game involves exploring a procedural world, scavenge, combat, and building. Procedural buildings are currently causing loading timeouts.

# Key Asset & Context
- **Script**: `Assets/01_Game/02_World/Streaming/WorldManager.cs`
- **Component**: `WorldManager`
- **Field**: `enableStreamedCityBuilder`
- **Scene**: `Assets/00_Scenes/World.unity`

# Implementation Steps

## 1. Modify WorldManager.cs
- **Description**: Set the default value of `enableStreamedCityBuilder` to `false` in the script. This ensures that any new instances or instances using defaults will have building spawning disabled.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Update WorldManager in World Scene
- **Description**: Open the `World` scene and set `enableStreamedCityBuilder` to `false` on the `WorldManager` GameObject. This specifically targets the main gameplay scene.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

# Verification & Testing
- **Loading Flow**: Start from `Boot.unity`. Verify the "Buildings" loading stage completes instantly (should be logged as `0.0s`).
- **Visual Check**: Enter the `World` scene and verify that no procedural buildings (spawning from `StreamedMapMagicCityBuilder`) are present on the terrain.
- **Logs**: Check console for `[GameManager] Loading stage 'Buildings' completed in 0.00s`.
