# Project Overview
- Game Title: Zombera
- High-Level Concept: An open-world zombie survival RPG with base building, character customization (UMA), and squad mechanics.
- Players: Single player (with AI squads)
- Inspiration / Reference Games: Project Zomboid, State of Decay
- Tone / Art Direction: Realistic / Gritty
- Target Platform: PC (StandaloneWindows64)
- Screen Orientation / Resolution: Landscape 1920x1080
- Render Pipeline: URP

# Game Mechanics
## Core Gameplay Loop
Exploration, scavenging, base building, and defending against zombie hordes. Players manage a squad and grow their character's skills (Toughness, Strength, Melee, etc.) through action.

## Controls and Input Methods
Standard keyboard and mouse controls for movement, combat, and interaction. Uses the New Input System.

# UI
- Main Menu for game start and character creation.
- In-game HUD for health, inventory, and interaction prompts.
- Loading screens for world transitions.

# Key Asset & Context
- `Assets/Scenes/World.unity`: The main gameplay scene where most issues persist.
- `UMA.UMAContext`: Missing singleton required for character rendering.
- `UnityEngine.EventSystems.EventSystem`: Missing component required for UI interaction.
- `Zombera.BuildingSystem.ThirdPartyDoorBridge`: Newly implemented utility for door interactions.

# Implementation Steps
## 1. Scene Repair & Hierarchy Cleanup
- **Add EventSystem**: Create a new GameObject with `EventSystem` and `StandaloneInputModule` (or the Input System equivalent) in the `World` scene. This satisfies the `StartupReadinessValidator`.
- **Add UMA Context**: Add a prefab containing `UMAContext` to the `World` scene. This will resolve the "Object reference not set" errors during UMA recipe loading.
- **Remove Missing Scripts**: Use an editor script to strip null component references from `[Node_B]`, `[Segment_Main]`, and `[Spawn_Ambient_01]` in the `World` scene.

## 2. NavMesh Optimization
- **Audit StreamingNavMeshTileService**: Review the configuration in `Assets/Scripts/World/StreamingNavMeshTileService.cs` and its instance in the scene. 
- **Reduce Bake Complexity**: If bakes are consistently over 500ms, consider increasing `voxelSize` or decreasing the `maxSlope` / `detailSampleDistance` to speed up runtime generation.

## 3. Log Management
- **Suppress Verbose XP Logs**: The console is currently flooded with `[XP DEBUG]` logs from `UnitStats.cs`. These should be disabled for standard development unless specifically debugging skills.
- **Dependency**: Check `UnitStats.cs` for a debug flag.

# Verification & Testing
## Automated Checks
- Run the `StartupReadinessValidator` again (it runs on Awake/Start in `World`).
- Verify console for "Validation passed with no issues."

## Manual Checks
- Confirm UMA characters (Player, Zombies) render correctly without console errors.
- Test UI interactions (ESC menu, inventory) to ensure `EventSystem` is working.
- Check NavMesh visualization in the editor to ensure tiles are generating correctly near the player.
- Verify door interaction using the new `ThirdPartyDoorBridge`.
