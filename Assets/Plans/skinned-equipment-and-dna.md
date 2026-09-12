# Project Overview
- Game Title: Zombera
- Issue: Clothing items (static meshes) do not match character body proportions (DNA).
- Solution: Transition to a Skinned Mesh Equipment system and implement Bone-based DNA scaling.

# Implementation Details

## 1. AppearanceProfileService: Bone-Based DNA
Proportions like "Muscle" or "Belly" are now implemented by scaling specific bones in the Humanoid armature.
- **upperMuscle**: Scales UpperChest and Arms.
- **lowerMuscle**: Scales Thighs.
- **belly**: Scales Spine.
- **headSize**: Scales Head.
- **feetSize**: Scales Feet.

## 2. EquipmentSystem: Skinned Mesh Support
The system now detects if an equipment prefab has a `SkinnedMeshRenderer`.
- If skinned, it "stitches" the item to the character's skeleton.
- Once stitched, the clothing automatically scales with the character's muscles/bones.

## 3. Asset Requirements
For clothing to correctly match a changing body, the asset must be a **Skinned Mesh Renderer** rigged to a standard Unity Humanoid skeleton. Static meshes will continue to work for socketed items (hats, backpacks) but will not deform with the body.

# Implementation Steps

### 1. Update AppearanceProfileService
Update `ApplyScale` and `CaptureScale` to handle bone-specific scaling for DNA attributes.

### 2. Update EquipmentSystem
Implement `RemapSkinnedMesh` logic in `ApplyVisualAttachment` to support high-fidelity clothing.

# Verification & Testing
- Adjust "Upper Muscle" in the Character Creator and confirm equipped shirts expand with the character's chest.
- Verify that "Height" still scales the entire unit correctly.
- Ensure static items like hats still socket correctly to the head.
