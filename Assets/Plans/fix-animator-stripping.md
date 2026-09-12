# Project Overview
- Game Title: Zombera
- Goal: Fix Animator Controller stripping issue in TestScenePlayerBootstrap.
- Issue: Player spawns without the `Player_Default` controller, likely due to UMA build events or incorrect capture order.

# Implementation Steps

## 1. Robust Animator Controller Preservation
Update `TestScenePlayerBootstrap.cs` to ensure the correct Animator Controller is captured and restored.
- **File**: `Assets/Scripts/Testing/TestScenePlayerBootstrap.cs`
- **Changes**:
    - Improve `preservedAnimatorController` capture: check Animator, then DCA, then the Prefab itself.
    - Register UMA build listeners *before* triggering the UMA build process.
    - Call `RestoreMissingAnimatorController` at the end of the spawn routine as a final guard.
    - Ensure `RestoreMissingAnimatorController` uses a more aggressive update to force the controller into the internal UMA rig if necessary.

# Key Assets & Context
- **Player Prefab**: `Assets/Prefabs/Player/Player.prefab`
- **Animator Controller**: `Player_Default`

# Verification & Testing
1. **Play Mode**: Run the `Test_AnimationBlendTrees` scene.
2. **Inspector Check**: Select the spawned Player and verify the `Animator` component has `Player_Default` assigned.
3. **Parameter Check**: Verify that parameters like `HasWeapon` exist and are controllable via the debug UI.
4. **Log Check**: Look for "[TestScenePlayerBootstrap] Restored AnimatorController" in the console.
