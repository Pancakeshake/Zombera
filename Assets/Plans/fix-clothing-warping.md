# Project Overview 
- **Game Title**: Zombera
- **High-Level Concept**: Survival RPG with UMA-based character customization and modular equipment.
- **Problem**: Clothing items warp and collapse when equipped because the system incorrectly applies a 180-degree rotation to limb bone mappings.

# Game Mechanics 
## Core Gameplay Loop
The player manages an inventory of clothing and weapons. The `EquipmentSystem` handles attaching these items to the character's animated skeleton at runtime.

# Key Asset & Context
- `Assets/Scripts/Inventory/EquipmentSystem.cs`: The core script responsible for `SkinnedMeshRenderer` remapping and bone assignment.

# Implementation Steps

1. **Remove Mirrored Rig Compensation**:
   - In `Assets/Scripts/Inventory/EquipmentSystem.cs`, delete the logic that calculates `isNonMirroredRig` using foot dot products.
   - Remove the section that creates `CorrectionProxy_180` game objects and applies them to the `newBones` array.
   - Simplify the loop to assign `newBones[i] = targetBone;` directly.
   - **Dependency**: None.

2. **Add Cleanup for Legacy Proxies**:
   - Update `ClearAllEquippedVisuals()` to search for and destroy any existing children named `CorrectionProxy_180` on the character. This ensures that the fix applies immediately even if the character already has these objects from previous executions.
   - **Dependency**: Step 1.

3. **Validation**:
   - Trigger a rebuild of the equipment (e.g., by toggling an item in the Inspector) and verify that the warping is resolved.

# Verification & Testing
- **Manual Check**: Verify in the `ClothingTesting` scene that the `Player` character's limb bones no longer have `CorrectionProxy_180` children.
- **Visual Check**: Confirm that the T-shirt mesh follows the arm movements correctly without twisting.
- **Code Check**: Ensure that `RemapSkinnedMesh` no longer contains the `boneName.Contains("Left") || boneName.Contains("Right")` conditional block.
