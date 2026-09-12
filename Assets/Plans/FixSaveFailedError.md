# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with building, scavenging, and combat.
- Players: Single player
- Target Platform: PC (Windows)
- Render Pipeline: URP

# Game Mechanics
## Save System Fix
The user is experiencing "Save failed" errors during normal game flow. The investigation revealed a redundant and incorrectly configured `Save Manager` GameObject in core prefabs, which conflicts with the functional `SaveManager`.

# UI Fixes
- None directly in the UI layout, but the status text "Save failed!" will be resolved.

# Key Asset & Context
- **Scripts**:
    - `Assets/01_Game/09_SaveSystem/Core/SaveManager.cs`: The central save orchestrator.
- **Prefabs**:
    - `Assets/Shared/Prefabs/GameManager/[GameManager].prefab`: Core manager container.
    - `Assets/Shared/Prefabs/UI/Menus/CoreRoot.prefab`: Another core manager container used in UI scenes.

# Implementation Steps

## 1. Add Defensive Logging to SaveManager.cs
- **Description**: Add a `Debug.LogError` call to `SaveGame` if the `saveSystem` reference is missing. This prevents silent failures and helps diagnose issues if they recur.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Remove Redundant 'Save Manager' from [GameManager] Prefab
- **Description**: Open `Assets/Shared/Prefabs/GameManager/[GameManager].prefab` and delete the child named `Save Manager` (the one with the space). Ensure the correctly configured `SaveManager` (no space) remains.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 3. Remove Redundant 'Save Manager' from CoreRoot Prefab
- **Description**: Open `Assets/Shared/Prefabs/UI/Menus/CoreRoot.prefab` and delete the child named `Save Manager` (the one with the space).
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

# Verification & Testing
1. **Inspector Check**: Open `[GameManager]` and `CoreRoot` prefabs and verify only one `SaveManager` child exists.
2. **Play Mode Check**: Run the game, open the pause menu, and click "Save". Verify it now says "Save successful!".
3. **Console Check**: Verify no "SaveManager skipped: SaveSystem reference is missing" errors appear in the console.
