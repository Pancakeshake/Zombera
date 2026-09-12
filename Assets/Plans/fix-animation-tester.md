# Project Overview
- Game Title: Zombera
- Goal: Fix the animation testing flow in `ClothingTesting` scene.
- Issue: Character is stuck in an infinite idle loop because forced states via `CrossFade` are being overridden by animator transitions since parameters (`IsInCombat`, `Speed`, etc.) are not being updated.

# Game Mechanics
The character should automatically cycle through different gameplay states (Walking, Combat, Crouching) and perform actions (Attacks, Hits) to test clothing mesh deformation.

# Key Asset & Context
- **Player**: The UMA character prefab instance in the scene.
- **Animator Parameters**: `Speed`, `IsInCombat`, `IsCrouching`, `AttackTrigger`, `HitTrigger`.
- **Conflicts**: `PlayerAnimationController` and `UnitController` reset these parameters to 0/false by default.

# Implementation Steps
1. **Create Animation Test Driver**:
   - Create a new script `Assets/Scripts/Testing/AnimationTestDriver.cs`.
   - This script will:
     - Explicitly disable `PlayerAnimationController` and `UnitController` on `Start`.
     - Cycle through "test profiles" every few seconds.
     - Profile 1: Normal Idle (`Speed=0`, `IsInCombat=false`).
     - Profile 2: Combat Stance (`Speed=0`, `IsInCombat=true`).
     - Profile 3: Walking (`Speed=0.5`).
     - Profile 4: Crouching (`IsCrouching=true`).
     - Randomly fire `AttackTrigger` and `HitTrigger`.
     - Log activity to the console for verification.
2. **Setup Scene**:
   - Remove the existing `RobustTester` script from the `Player` object.
   - Attach the new `AnimationTestDriver` script to the `Player` object.
3. **Verify**:
   - Enter Play Mode.
   - Confirm parameters are changing in the Animator window.
   - Confirm character transitions out of Idle.

# Verification & Testing
- **Animator Parameters**: Check `Speed`, `IsInCombat`, and `IsCrouching` in the Inspector/Animator window while playing.
- **Console Logs**: Look for "[TestDriver] Profile Selected" messages.
- **Visual**: Verify the character's pose and movement change.
