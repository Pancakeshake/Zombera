# Project Overview
- **Game Title**: Zombera
- **High-Level Concept**: AAA animation and combat polish.
- **Task**: Implement Phase 1 and 2 of the animation tuning plan.

# Phase 1: Lock Graph/Runtime Parity
- Update `PlayerAnimatorSetup.cs` to include `CombatEntryTrigger` parameter.
- Add `CombatEntry`, `TurnLeft`, and `TurnRight` states to the animator controller.
- Wire transitions for these new states.

# Phase 2: Sprint and Locomotion Blend Pass
- Retune locomotion smoothing parameters in `PlayerAnimationController.cs`.
- Tighten transition durations in `PlayerAnimatorSetup.cs` for snappier movement and combat.

# Key Asset & Context
- **Scripts**:
  - `Assets/01_Game/03_Characters/Animation/PlayerAnimationController.cs`
  - `Assets/Editor/AnimationTools/PlayerAnimatorSetup.cs`
- **Controller**: `Assets/Animations/Players/Player_Default.controller`
- **Clips**:
  - `Actions/Armature_Turn90_L`
  - `Actions/Armature_Turn90_R`
  - `Combat/Armature_PunchKick_Enter`

# Implementation Steps
1. **Retune Locomotion Parameters in `PlayerAnimationController.cs`**
   - Update default values for `locomotionSpeedDampTime`, `locomotionVelocityDampTime`, and `locomotionIdleVelocityDeadZone`.
2. **Update `PlayerAnimatorSetup.cs` for Graph Parity and Sprints**
   - Add new clips: `turnLeft`, `turnRight`, `combatEntry`.
   - Add parameter: `CombatEntryTrigger`.
   - Add states: `CombatEntry`, `TurnLeft`, `TurnRight`.
   - Tighten transition durations:
     - Attacks: 0.08s -> 0.06s.
     - Idle/Loco -> SprintEnter: 0.1s -> 0.07s.
     - Sprint -> Loco/SprintExit: 0.08s -> 0.06s.
3. **Execute Rewire Tool**
   - Run the "Tools/Zombera/Animation/Rewire Player Default Controller" menu command to apply changes to the controller asset.

# Verification & Testing
- **Parameter Check**: Verify `CombatEntryTrigger` exists in the `Player_Default` animator.
- **State Check**: Verify `CombatEntry`, `TurnLeft`, and `TurnRight` states exist.
- **Transition Check**: Check transition durations in the Animator window match the new values.
- **Feel Test (Play Mode)**:
  - Toggle sprint: Look for responsive entry without sliding.
  - Attack: Verify Snappiness.
  - Facing changes: Verify turn animations trigger.
