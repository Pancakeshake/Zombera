# Project Overview 
- **Game Title**: Zombera
- **Goal**: Resolve the issue where `PlayerAnimationController` and other character scripts override the `AnimationCycler`, preventing testing of non-idle poses.
- **Root Cause**: `PlayerAnimationController.Update()` sets animator parameters (Speed, Velocity, Posture) every frame based on the unit's state. Since the unit is stationary in the test scene, it constanty forces the animator back to the "Idle" state.

# Key Asset & Context
- `Assets/Scripts/Characters/PlayerAnimationController.cs`: The script overriding the animator.
- `Assets/Scripts/Testing/AnimationCycler.cs`: The script that needs to take control.

# Implementation Steps

1. **Update AnimationCycler.cs**:
   - Add logic to find and disable the `PlayerAnimationController` component on `Start()` if `autoCycle` or a new `overrideRegularLogic` flag is enabled.
   - Ensure it re-enables the script on `OnDisable()` or when testing stops.
   - Add a safety check to ensure `Animator.speed` is set to 1.
   - **File**: `Assets/Scripts/Testing/AnimationCycler.cs`

2. **Update Player Instance**:
   - Verify that disabling `PlayerAnimationController` allows the cycler to function.
   - If `UnitController` is still interfering (e.g., via Root Motion or physics), disable it as well during testing.

# Verification & Testing
- **Visual Check**: Enter Play Mode and confirm the character successfully leaves the Idle pose and enters the specified states (Attack, Sprint, etc.).
- **Consistency Check**: Verify that the character stays in each state for the full 3 seconds without "jittering" back to Idle.
