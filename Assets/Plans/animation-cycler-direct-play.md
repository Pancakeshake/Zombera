# Project Overview 
- **Game Title**: Zombera
- **Goal**: Update the `AnimationCycler` to play animations directly by state name for a fixed duration, bypassing complex trigger logic for more reliable testing.
- **Current Issue**: Only the "Idle" animation plays; other actions like attacks aren't appearing because they rely on specific animator transitions that might not be met.

# Key Asset & Context
- `Assets/Scripts/Testing/AnimationCycler.cs`: The script managing the sequence.
- `Player_Default` Animator Controller: Contains states like `Attack_Jab`, `CrouchLocomotion`, `Sprint`, etc.

# Implementation Steps

1. **Update AnimationCycler.cs**:
   - Ensure `ExecuteCurrentStep` uses `CrossFadeInFixedTime` for the `CrossFade` action type to jump directly to any state by name.
   - Add extra logging to confirm which state is being entered.
   - **File**: `Assets/Scripts/Testing/AnimationCycler.cs`

2. **Configure Direct Sequence**:
   - Update the `Player` instance in the scene to use direct state names instead of parameters.
   - New sequence:
     1. `Idle` (3s)
     2. `CombatIdle` (3s)
     3. `Attack_Jab` (3s)
     4. `Attack_Hook` (3s)
     5. `CrouchLocomotion` (3s)
     6. `Sprint` (3s)
   - **Dependency**: Step 1.

# Verification & Testing
- **Play Mode Test**: Enter Play Mode and confirm the character transitions through all 6 states in the order listed.
- **Loop Check**: Verify that once the list finishes, it returns to `Idle` and starts over.
