# Project Overview
- Game Title: Zombera
- Goal: Fix the HUD not loading in the `Test_AnimationBlendTrees` scene and ensure no objects are deleted from the scene.
- Current Issue: 
    - `WorldHUDController` has a self-destruct mechanism in `Awake` that destroys the GameObject if it's in a non-World scene. 
    - `WorldHUDController` and `HUDManager` only show their canvases on a `GameStateChangedEvent`. If they are spawned after the state has already changed to `Playing`, they stay hidden.
    - `TestGameplayBootstrap` checks for `WorldHUD` but the prefab uses `WorldHUDController`, leading to duplicate instantiation or confusion.

# Game Mechanics
- Core Gameplay Loop: Animation testing in a sandbox environment.
- Controls and Input Methods: Standard gameplay controls, but requiring HUD feedback (status bars, tabs).

# Key Assets & Context
- **Scripts**: 
    - `Assets/Scripts/UI/HUD/WorldHUDController.cs`
    - `Assets/Scripts/UI/HUD/HUDManager.cs`
    - `Assets/Scripts/Testing/TestGameplayBootstrap.cs`
- **Prefabs**: 
    - `Assets/Prefabs/UI/HUD/WorldHUDCanvas.prefab`
    - `Assets/Prefabs/UI/HUD/HUD.prefab`

# Implementation Steps

## 1. Modify WorldHUDController.cs (Visibility & Safety)
- **Problem**: Self-destructs in non-World scenes and hides canvas by default without checking current state.
- **Changes**:
    - Update the scene check in `Awake` to be more permissive (allow any scene containing "Test" or "World").
    - **Remove `Destroy(gameObject)`**. If the scene is invalid, just log a warning and disable the script, but do not delete the object.
    - In `Start`, check the current `GameManager` state. If it's already `Playing` or `Paused`, enable the canvas immediately.

## 2. Modify HUDManager.cs (Visibility)
- **Problem**: Hides HUD by default and only shows on event.
- **Changes**:
    - In `Start` (or `BindGameplayEvents`), check the current state and call `SetVisible(true)` if the game is already playing.

## 3. Modify TestGameplayBootstrap.cs (Duplicate Prevention)
- **Problem**: Only looks for `WorldHUD`, causing it to spawn another HUD if `WorldHUDController` is already present.
- **Changes**:
    - Update the "Ensure HUD" logic to look for `WorldHUD`, `WorldHUDController`, or `HUDManager`.

# Verification & Testing
1. **Load Scene**: Open `Test_AnimationBlendTrees`.
2. **Play Mode**: 
    - Verify the HUD (WorldHUDCanvas) appears immediately.
    - Verify no "destroying" warnings appear in the console.
    - Verify that no objects are deleted from the hierarchy (specifically the `WorldHUDCanvas` if it was pre-placed).
3. **Tab Check**: Press `F1`-`F5` to ensure the tabs in `WorldHUDController` are functional.
