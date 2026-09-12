# Project Overview
- Game Title: Zombera
- Goal: Prevent World HUD from self-destructing in the animation test scene.

# Implementation Steps

## 1. Update Scene Name Check
Modify the `WorldHUDController` logic to include an exception for the `Test_AnimationBlendTrees` scene.
- **File**: `Assets/Scripts/UI/HUD/WorldHUDController.cs`
- **Action**: Update the `inWorldScene` condition in `Awake`.

# Key Assets & Context
- **Script**: `Assets/Scripts/UI/HUD/WorldHUDController.cs`

# Verification & Testing
1. **Play Mode**: Run the `Test_AnimationBlendTrees` scene.
2. **Console Check**: Verify the HUD self-destruction warning no longer appears.
3. **UI Check**: Verify the World HUD remains in the scene and is interactive.
