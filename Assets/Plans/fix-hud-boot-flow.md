# Project Overview
- Game Title: Zombera
- Goal: Fix World HUD visibility and functionality in the `Test_AnimationBlendTrees` scene.
- Core Issues:
    1.  `GameManager.Instance` is null when the bootstrap script tries to set the game state, causing the HUD to stay hidden.
    2.  `WorldHUDController` disables its canvas in `Awake` and misses the state change event.
    3.  `GameManager`'s `SyncGameplayUiVisibility` doesn't find the test scene's HUD because it looks for the `WorldHUD` component, which is missing.

# Implementation Steps

## 1. Update WorldHUDController
Ensure the HUD persists in test scenes and synchronizes its visibility on start.
- **File**: `Assets/Scripts/UI/HUD/WorldHUDController.cs`
- **Changes**:
    - Update `inWorldScene` check to use `IndexOf` for the test scene name.
    - Add logic in `Start()` to enable the canvas if `GameManager.Instance.CurrentState` is already `Playing` or `Paused`.

## 2. Update TestScenePlayerBootstrap
Improve the `GameManager` state forcing logic.
- **File**: `Assets/Scripts/Testing/TestScenePlayerBootstrap.cs`
- **Changes**:
    - If `GameManager.Instance` is null, attempt to find any `GameManager` in the scene and call `InitializeSystems()` on it to ensure the singleton is set.

## 3. Update TestAnimationBlendTreesTool
Use the official `GameManager` prefab for better stability and dependency resolution.
- **File**: `Assets/Editor/AnimationTools/TestAnimationBlendTreesTool.cs`
- **Changes**:
    - Use `Assets/Prefabs/GameManager/[GameManager].prefab` instead of manually adding manager components.
    - Remove the manual addition of `UnitManager`, `ZombieManager`, etc., as the prefab handles them.

# Key Assets & Context
- **Scripts**: `WorldHUDController.cs`, `TestScenePlayerBootstrap.cs`, `TestAnimationBlendTreesTool.cs`
- **Prefab**: `Assets/Prefabs/GameManager/[GameManager].prefab`

# Verification & Testing
1. **Tool Run**: Run `Tools/5. Animation Tools/Create Animation Test Scene`.
2. **Play Mode**:
    - Verify World HUD (top/bottom bars) appears immediately.
    - Verify HUD tabs (F1-F5) are interactive.
    - Verify `Player_Default` controller is assigned to the player.
3. **Console**: No self-destruction warnings or null reference errors.
