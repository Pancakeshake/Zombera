# Project Overview
- Game Title: Zombera
- High-Level Concept: Zombie survival game with character customization and base building.
- Players: Single player (based on current scene setup).
- Target Platform: Standalone Windows 64.
- Render Pipeline: Universal Render Pipeline (URP).

# Game Mechanics
## Core Gameplay Loop
The game involves character movement, combat, and interaction in a zombie-infested world. Testing animations is critical for ensuring smooth transitions between movement, combat, and other actions.
## Controls and Input Methods
Uses the New Input System. Testing requires a way to manually trigger and blend animations.

# UI
- **Animation Tester UI**: An IMGUI-based debug overlay (using `AnimationTestControllerUI`) to control player and zombie animations at runtime.
- **HUD**: Standard gameplay HUD to verify binding and display.

# Key Asset & Context
- **Scene**: `Assets/Scenes/Testing/Animation_Testing.unity`
- **Bootstrap Scripts**:
    - `TestScenePlayerBootstrap`: Spawns the player prefab and applies styling.
    - `TestGameplayBootstrap`: Ensures `GameManager` and `HUD` exist and initializes the game state.
- **Prefabs**:
    - Player: `Assets/Prefabs/Player/Player.prefab`
    - GameManager: `Assets/Prefabs/GameManager/[GameManager].prefab`
    - HUD: `Assets/Prefabs/UI/HUD/HUD.prefab`
- **Debug UI**: `AnimationTestControllerUI.cs`

# Implementation Steps
1. **Prepare Scene**:
    - Open `Assets/Scenes/Testing/Animation_Testing.unity`.
    - Delete the legacy manually-placed `Player` GameObject.
    - Delete the legacy `PlayerSpawner` GameObject.
    - *Dependency*: None.
2. **Configure Bootstrap**:
    - Select the `Bootstrap` GameObject.
    - Ensure `TestScenePlayerBootstrap` is present and configured with:
        - `Player Prefab`: `Assets/Prefabs/Player/Player.prefab`
        - `Randomise Appearance`: `true` (for better testing coverage of variants).
    - Add the `TestGameplayBootstrap` component to the `Bootstrap` GameObject.
    - Configure `TestGameplayBootstrap` with:
        - `Game Manager Prefab`: `Assets/Prefabs/GameManager/[GameManager].prefab`
        - `Hud Prefab`: `Assets/Prefabs/UI/HUD/HUD.prefab`
        - `Set Playing State On Awake`: `true`
    - *Dependency*: Step 1.
3. **Add Animation Testing UI**:
    - Add the `AnimationTestControllerUI` component to the `Bootstrap` GameObject (or create a dedicated `AnimationTester` object). This script provides the IMGUI controls needed for quick testing.
    - *Dependency*: Step 2.
4. **Final Scene Polish**:
    - Ensure there is a `MainCamera` in the scene or let the bootstrap spawn a runtime camera.
    - Ensure the scene has a ground/floor for the player to spawn on (the current scene has a floor).
    - *Dependency*: Step 3.

# Verification & Testing
1. **Enter Play Mode**: The player should spawn automatically at the `Bootstrap` location (or assigned spawn point).
2. **Game State**: Check the Console for "[TestGameplayBootstrap] Forcing GameState to Playing."
3. **UI Appearance**: The IMGUI "Zombera Animation Tester" should appear in the top-left.
4. **Animation Control**: Use the sliders and buttons in the Animation Tester UI to verify the player's animation states (Speed, Velocity, Combat, etc.).
5. **HUD Binding**: Verify that the HUD correctly displays player information.
