# Project Overview 
- **Game Title**: Zombera
- **Goal**: Implement a sequential "Animation Cycler" that follows a user-defined list of actions to test equipment and locomotion.
- **Problem**: Current random animation logic is insufficient for precise testing and causing script reference errors.

# Game Mechanics 
## Controls and Input Methods
The script provides an automated sequence in Play Mode and manual "Next/Previous" controls in the Inspector.

# Key Asset & Context
- `Assets/Scripts/Testing/AnimationCycler.cs`: New script replacing previous randomizers.

# Implementation Steps

1. **Develop AnimationCycler.cs**:
   - Create the script with a serializable `AnimationStep` class.
   - `AnimationStep` details:
     - `ActionType`: Trigger, SetBool, SetFloat, CrossFade.
     - `ParamName`: Animator parameter or State name.
     - `Value`: The value to set (for bool/float).
     - `WaitTime`: Duration before moving to the next step.
   - Logic: A coroutine that loops through the `steps` list.
   - **Dependency**: None.

2. **Cleanup Redundant Files**:
   - Delete `Assets/Scripts/Testing/AnimationPreviewer.cs`.
   - Delete `Assets/Scripts/Testing/AnimationRandomizer.cs`.
   - Remove these components from the Player in the `ClothingTesting` scene.
   - **Dependency**: Step 1.

3. **Configuration**:
   - Attach `AnimationCycler` to the Player.
   - Add a list of animations as requested (e.g., Idle -> Combat -> Attack -> Crouch).
   - **Dependency**: Step 2.

# Verification & Testing
- **Cycle Test**: Ensure the character moves through the list in the specified order.
- **Manual Control**: Test the ContextMenu/Inspector buttons to manually trigger the next animation.
- **Reliability**: Confirm no `MissingReferenceException` occurs even if certain parameters are missing (the script will perform safety checks).
