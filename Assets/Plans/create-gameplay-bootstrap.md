# Project Overview
- Game Title: Zombera
- Goal: Create a robust bootstrap script to satisfy gameplay logic and allow the World HUD to spawn in the `Test_AnimationBlendTrees` scene.
- Core Feedback: The current path is wrong; a dedicated bootstrap script is needed to handle GameManager initialization and HUD spawning.

# Implementation Steps

## 1. Create Test Gameplay Bootstrap Script
Create a script that ensures the `GameManager` is fully initialized in a "Playing" state and that the `WorldHUD` is spawned.
- **File**: `Assets/Scripts/Testing/TestGameplayBootstrap.cs`
- **Logic**:
    - Instantiate the `[GameManager]` prefab if missing.
    - Call `GameManager.Instance.InitializeSystems()`.
    - Set `GameManager.Instance.SetGameState(GameState.Playing)`.
    - Instantiate the `HUD` prefab (containing `WorldHUD`) if missing.
    - This script should run in `Awake` to ensure managers are ready before other scripts' `Start` methods.

## 2. Update HUD Persistence
Allow the HUD to persist in the test scene.
- **File**: `Assets/Scripts/UI/HUD/WorldHUDController.cs`
- **Action**: Update `inWorldScene` condition in `Awake` to include `Test_AnimationBlendTrees`.

## 3. Update Test Scene Generator Tool
Update the tool to use the new bootstrap script and the official prefabs.
- **File**: `Assets/Editor/AnimationTools/TestAnimationBlendTreesTool.cs`
- **Changes**:
    - Use `Assets/Prefabs/GameManager/[GameManager].prefab`.
    - Use `Assets/Prefabs/UI/HUD/HUD.prefab`.
    - Add the `TestGameplayBootstrap` component to a root object and wire the prefabs to it.
    - Remove the manual creation of individual manager objects (AIManager, UnitManager, etc.) as the `[GameManager]` prefab handles them.

# Key Assets & Context
- **Scripts**: `TestGameplayBootstrap.cs`, `WorldHUDController.cs`, `TestAnimationBlendTreesTool.cs`
- **Prefabs**: `Assets/Prefabs/GameManager/[GameManager].prefab`, `Assets/Prefabs/UI/HUD/HUD.prefab`

# Verification & Testing
1. **Tool Run**: Run `Tools/5. Animation Tools/Create Animation Test Scene`.
2. **Hierarchy Check**: Verify a single `[GameManager]` root and a `HUD` object exist.
3. **Play Mode**:
    - Verify the World HUD appears and is interactive (F1-F5).
    - Verify no self-destruction warnings in the console.
    - Verify `GameManager.Instance.CurrentState` is `Playing`.
