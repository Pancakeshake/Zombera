# Project Overview
- Game Title: Zombera
- Goal: Enable World HUD in the animation testing scene.
- Current Issue: `WorldHUDController` destroys itself in non-World scenes, and the test scene setup is missing HUD and essential managers for full interactivity.

# Implementation Steps

## 1. Allow HUD in Test Scenes
Modify `WorldHUDController.Awake` to prevent self-destruction if the scene name contains "Test".
- **File**: `Assets/Scripts/UI/HUD/WorldHUDController.cs`
- **Action**: Update the scene name check logic to include "Test".

## 2. Enhance Test Scene Generator
Update `TestAnimationBlendTreesTool.cs` to include the World HUD prefab and ensure all necessary managers are present for a "real-game" feel (clicking buttons, status bars).
- **File**: `Assets/Editor/AnimationTools/TestAnimationBlendTreesTool.cs`
- **Changes**:
    - Add `Assets/Prefabs/UI/HUD/WorldHUDCanvas.prefab` to the scene.
    - Ensure `GameManager` and `TimeSystem` are present if they are missing.
    - Organize into a more robust systems hierarchy.

# Key Assets & Context
- **HUD Prefab**: `Assets/Prefabs/UI/HUD/WorldHUDCanvas.prefab`
- **Script**: `Zombera.UI.WorldHUDController`

# Verification & Testing
1. **Tool Check**: Run `Tools/5. Animation Tools/Create Animation Test Scene`.
2. **Hierarchy Check**: Verify `WorldHUDCanvas` is in the scene.
3. **Play Mode**: 
    - Verify the HUD appears.
    - Verify buttons (like inventory or squad tabs) can be clicked.
    - Verify the self-destruction warning no longer appears in the console.
