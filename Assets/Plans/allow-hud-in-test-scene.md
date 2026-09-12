# Project Overview
- Game Title: Zombera
- Goal: Allow the World HUD to function in the `Test_AnimationBlendTrees` scene.
- Current Issue: The HUD self-destructs if it detects it's not in a scene with "World" in the name.

# Implementation Steps

## 1. Update HUD Self-Destruction Logic
Modify the `WorldHUDController` to include an exception for the `Test_AnimationBlendTrees` scene.
- **File**: `Assets/Scripts/UI/HUD/WorldHUDController.cs`
- **Action**: Update the scene name check in `Awake`.

# Key Assets & Context
- **Script**: `Zombera.UI.WorldHUDController`

# Verification & Testing
1. **Play Mode**: Run the `Test_AnimationBlendTrees` scene.
2. **Console Check**: Verify the warning about destroying the HUD no longer appears.
3. **UI Check**: Verify the HUD is visible and interactive.
