# Project Overview
- Game Title: Zombera
- Goal: Fix World HUD visibility and interactivity in the `Test_AnimationBlendTrees` scene.
- Issues:
    1.  The HUD might still be self-destructing due to strict scene name matching.
    2.  The HUD canvas is not being enabled if the `GameManager` is already in the `Playing` state when the HUD initializes.

# Implementation Steps

## 1. Robust HUD Persistence
Update the scene check in `WorldHUDController.Awake` to be more lenient and correctly identify the test scene.
- **File**: `Assets/Scripts/UI/HUD/WorldHUDController.cs`
- **Action**: Use `IndexOf("Test_AnimationBlendTrees", StringComparison.OrdinalIgnoreCase)` instead of direct equality.

## 2. HUD State Sync on Start
Ensure the HUD canvas correctly reflects the current `GameManager` state during initialization.
- **File**: `Assets/Scripts/UI/HUD/WorldHUDController.cs`
- **Action**: In `Start()`, check `GameManager.Instance.CurrentState` and enable the canvas if it's `Playing` or `Paused`.

# Key Assets & Context
- **Script**: `Zombera.UI.WorldHUDController`

# Verification & Testing
1. **Play Mode**: Run the `Test_AnimationBlendTrees` scene.
2. **Console Check**: Verify no "destroying to avoid menu interference" warning appears.
3. **UI Check**: Verify the HUD (top bar, bottom bar) is visible.
4. **Interactivity Check**: Press F1-F4 and verify tabs open.
