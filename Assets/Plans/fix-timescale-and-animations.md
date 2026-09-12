# Project Overview 
- **Game Title**: Zombera
- **Problem**: The `ClothingTesting` scene is currently paused (`Time.timeScale = 0`), which prevents the `AnimationCycler` from progressing, the `Animator` from playing clips, and the `ClothingTestCameraController` from moving the camera.

# Key Asset & Context
- `Assets/Scripts/Testing/AnimationCycler.cs`: Needs to ensure the game is unpaused for testing.
- `Assets/Scripts/Testing/ClothingTestCameraController.cs`: Affected by `Time.timeScale`.

# Implementation Steps

1. **Update AnimationCycler.cs**:
   - Add `Time.timeScale = 1.0f;` to the `Start()` or `OnEnable()` method to ensure the game is running.
   - Use `yield return new WaitForSecondsRealtime(interval);` in the coroutine instead of `WaitForSeconds` to make it immune to pausing.
   - Force `_animator.updateMode = AnimatorUpdateMode.UnscaledTime;` on start for robustness.
   - **File**: `Assets/Scripts/Testing/AnimationCycler.cs`

2. **Update ClothingTestCameraController.cs**:
   - Change `Time.deltaTime` to `Time.unscaledDeltaTime` in all movement and rotation calculations.
   - This ensures the camera remains responsive even if the game is paused for a specific frame or pose.
   - **File**: `Assets/Scripts/Testing/ClothingTestCameraController.cs`

# Verification & Testing
- **Visual Check**: Enter Play Mode. The character should immediately start playing the Idle animation (moving/swaying) and transition to the next state after 3 seconds.
- **Input Check**: Camera movement (WASD) and rotation (Right-click Drag) should be smooth and responsive.
- **Log Check**: Verify that "Step X" logs appear every 3 seconds in the console.
