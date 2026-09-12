# Project Overview
- Game Title: Zombera
- Goal: Fix the animation testing flow in `ClothingTesting` scene.
- Issue: The character is stuck in Idle because:
  1. The `RobustTester` component is missing (Null/Missing Script) on the Player.
  2. The production scripts (`PlayerAnimationController`, `UnitController`) are overriding animator parameters.
  3. The scene might be starting in a paused state.

# Game Mechanics
The character should automatically cycle through different gameplay states (Walking, Combat, Crouching) and perform actions (Attacks, Hits).

# Key Asset & Context
- **Player**: The UMA character prefab instance.
- **RobustTester.cs**: The script driving the test logic.
- **Time.timeScale**: Must be 1.0.

# Implementation Steps
1. **Clean Player Components**:
   - Use `GameObjectUtility.RemoveMonoBehavioursWithMissingScript` to clean the Player object.
2. **Assign RobustTester**:
   - Re-attach the `RobustTester` component to the Player object.
   - Set the `interval` to 2.0 seconds.
3. **Disable Production Scripts**:
   - Ensure `PlayerAnimationController` and `UnitController` are disabled on the Player instance.
4. **Force Unpause**:
   - Set `Time.timeScale = 1.0`.
5. **Verify Play Mode**:
   - Press Play.
   - Verify parameters are changing in the Animator window.
   - Verify the character is moving.

# Verification & Testing
- **Visual**: Confirm the character's pose changes every few seconds.
- **Console**: Check for "[RobustTester] Mode Change" logs.
