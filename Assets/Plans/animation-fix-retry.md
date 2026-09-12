# Project Overview 
- Game Title: Zombera
- Problem: Character is stuck in T-pose because the Animator Controller is missing on the scene instance. Previous testing scripts were missing or broken.

# Key Asset & Context
- `Assets/Animations/Players/Player_Default.controller`: The main animator controller.
- `Assets/Scripts/Testing/AnimationCycler.cs`: New sequence-based testing script.

# Implementation Steps

1. **Re-create AnimationCycler.cs**:
   - Ensure the script is written to `Assets/Scripts/Testing/AnimationCycler.cs`.
   - Content will include the `AnimationStep` list for sequential testing.

2. **Clean up old Testing Scripts**:
   - Manually delete `AnimationPreviewer.cs` and `AnimationRandomizer.cs` to resolve "Missing script" warnings and naming conflicts.

3. **Fix Scene Animator**:
   - Locate the `Player` object in the scene.
   - Find the child with the `Animator`.
   - Assign the `Player_Default` controller to the `Controller` slot.

4. **Setup Animation Cycler**:
   - Add the `AnimationCycler` component to the Animator's GameObject.
   - Configure a list of steps (Idle -> Combat -> Attack) to verify character movement.

# Verification & Testing
- **Visual Check**: Player should transition to Idle on Play.
- **Cycle Check**: Console should log "Step X" as the cycler moves through the list.
