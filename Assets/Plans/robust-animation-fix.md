# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with character customization (UMA).
- Goal: Fix the animation testing flow in `ClothingTesting` scene.

# Game Mechanics
The character should play a variety of animations (Idles, Attacks, Movement, Postures) automatically so the user can inspect clothing fit and deformation.

# Key Asset & Context
- **Player**: The UMA character.
- **Animator Controller**: `Player_Default`.
- **Issue**: Previous test script (`AnimationPreviewer`) was crashing due to null references in `FolderClips` and failing to transition out of `Idle`.

# Implementation Steps
1. **Create Robust Test Script**:
   - Create `Assets/Scripts/Testing/RobustAnimationTester.cs`.
   - This script will:
     - Automatically find the `Animator`.
     - Cycle through valid Animator States (`Idle`, `CombatIdle`, `Attack_Jab`, `CrouchLocomotion`, etc.) using `CrossFade`.
     - Randomly toggle parameters like `IsInCombat`, `IsCrouching`, and `Speed`.
     - Use a configurable timer.
2. **Apply Script to Player**:
   - Remove the broken `AnimationPreviewer` from the `Player` object.
   - Attach `RobustAnimationTester` to the `Player` object.
3. **Ensure Clean State**:
   - Keep `PlayerAnimationController` and `UnitController` disabled to avoid interference.
4. **Final Check**:
   - Verify character plays multiple animations in Play Mode.

# Verification & Testing
- **State Transition**: Confirm the character moves from `Idle` to other states in the Animator window.
- **Visual**: Confirm the character's pose changes every few seconds.
