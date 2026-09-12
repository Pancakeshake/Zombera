# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with third-person combat and base building.
- Players: Single player.
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
The player explores, builds, and fights zombies. The `ClothingTesting` scene is used to verify clothing assets on the UMA character during various animations.

# UI
The scene uses the `AnimationTestControllerUI` (if added) or just randomized scripts to drive the character for inspection.

# Key Asset & Context
- **Player**: The core character with UMA components.
- **Interfering Scripts**: `PlayerAnimationController` and `UnitController` are production scripts that attempt to drive the animator based on actual gameplay input and physics. In a testing scene, they conflict with randomized animation scripts.

# Implementation Steps
1. **Remove Conflicting Test Scripts**:
   - The current `AnimationRandomizer` and `AnimationCycler` might be conflicting or holding stale references to the Animator (which UMA sometimes resets during character building).
   - I will remove these components from the `Player` GameObject.
2. **Setup Robust Animation Testing**:
   - Add the `AnimationPreviewer` script to the `Player` GameObject.
   - `AnimationPreviewer` is more robust as it re-fetches the Animator if it becomes null and supports playing random clips from the `FolderClips` collection defined in `PlayerAnimationController`.
   - Set `autoCycle = true` and `cycleInterval = 2.0` on the `AnimationPreviewer`.
3. **Disable Production Controllers**:
   - Ensure `PlayerAnimationController`, `UnitController`, and `PlayerInputController` are disabled on the `Player` object. This prevents them from resetting `Speed` or `IsInCombat` parameters to 0 every frame.
4. **Final Verification**:
   - Enter Play Mode.
   - Monitor the Animator parameters to ensure `AttackTrigger`, `HitTrigger`, and posture booleans are being set by the `AnimationPreviewer`.
   - Verify the character transitions out of the "Idle" state.

# Verification & Testing
- **Visual Check**: Does the character perform attacks, hits, and posture changes?
- **Parameter Check**: In the Animator window, do the triggers highlight when the script fires?
- **UMA Ready**: Ensure animations only start or are visible once the UMA mesh is built.
