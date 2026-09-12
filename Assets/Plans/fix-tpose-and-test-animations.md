# Project Overview 
- Game Title: Zombera
- High-Level Concept: Survival RPG with modular equipment system.
- Problem: The Player character is in a T-pose because the `Animator` component is missing an `Avatar` asset. The character model (`Player.fbx`) has been updated to a Humanoid rig with a `PlayerAvatar`, but this avatar has not yet been assigned to the Animator.

# Game Mechanics 
## Controls and Input Methods
This task implements an automated "Animation Randomizer" to test character movement and clothing fit without requiring manual input.

# Key Asset & Context
- `Assets/Models/Player/Player Average/Player.fbx`: The source model containing the newly created `PlayerAvatar`.
- `Assets/Prefabs/Player/Player.prefab`: The player character prefab.
- `Assets/Animations/Players/Player_Default.controller`: The animator controller defining states and parameters.

# Implementation Steps

1. **Assign Player Avatar**:
   - Open `Assets/Prefabs/Player/Player.prefab`.
   - Find the `Animator` component on the `Player` child object.
   - Assign the `PlayerAvatar` (from `Assets/Models/Player/Player Average/Player.fbx`) to the **Avatar** slot.
   - Also verify and update the `Animator` on the `Player` object in the `ClothingTesting` scene.
   - **Dependency**: None.

2. **Create Animation Randomizer Script**:
   - Create `Assets/Scripts/Testing/AnimationRandomizer.cs`.
   - Implement logic to:
     - Randomly set float parameters (`Speed`, `VelocityX`, `VelocityZ`) to simulate movement.
     - Periodically fire triggers (`AttackJabTrigger`, `AttackCrossTrigger`, `HitTrigger`, etc.) at defined intervals.
     - Toggle boolean parameters (`IsCrouching`, `IsInCombat`) to test posture changes.
   - Add a "Test Mode" boolean to easily enable/disable randomization in the Inspector.
   - **Dependency**: None.

3. **Attach to Scene Player**:
   - In the `ClothingTesting` scene, find the `Player` instance.
   - Attach the `AnimationRandomizer` script to the object with the `Animator`.
   - **Dependency**: Step 1 & 2.

# Verification & Testing
- **Play Mode Test**: Enter Play Mode in the `ClothingTesting` scene.
- **Verification**: Ensure the character transitions from T-pose to an Idle state immediately and performs random actions over time.
- **Visual Validation**: Confirm that split body parts and equipped clothing follow the animations correctly without clipping.
