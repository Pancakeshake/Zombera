# Project Overview 
- Game Title: Zombera
- Goal: Rewire the character equipment system to use the new split body parts for hiding meshes.
- New Body Parts: `Body_Chest`, `Body_Head`, `Body_Hips`, `Body_LeftArm`, `Body_LeftFoot`, `Body_LeftLeg`, `Body_RightArm`, `Body_RightFoot`, `Body_RightLeg`, `Body_Waist`.

# Key Asset & Context
- `Assets/Scripts/Inventory/EquipmentSystem.cs`: Automatically shows all meshes starting with `Body_` and hides those listed in an item's `hiddenBodyParts`.
- `Assets/Prefabs/Player/Player.prefab`: Already updated with the new split meshes.
- Existing `ItemDefinition` assets: Need updated `hiddenBodyParts` to match the new naming convention.

# Implementation Steps

1. **Update ItemDefinition: T-Shirt 1**:
   - Update `hiddenBodyParts` to hide the chest and waist.
   - New list: `["Body_Chest", "Body_Waist"]`.
   - **File**: `Assets/ScriptableObjects/Items/Chest/T-Shirt 1/T-Shirt 1.asset`

2. **Update ItemDefinition: Camo legs 1**:
   - Update `hiddenBodyParts` to hide the hips and legs.
   - New list: `["Body_Hips", "Body_LeftLeg", "Body_RightLeg"]`.
   - **File**: `Assets/ScriptableObjects/Items/Legs/Camo_legs 1/Camo_legs 1.asset`

3. **Update ItemDefinition: Twin Camouflage Comba (Boots)**:
   - Update `hiddenBodyParts` to hide the feet and the bottom of the pants (for clipping).
   - New list: `["Body_LeftFoot", "Body_RightFoot", "Pants_Bottom"]`.
   - **File**: `Assets/ScriptableObjects/Items/Feet/Twin_Camouflage_Comba/Twin_Camouflage_Comba.asset`

4. **Verify System Integrity**:
   - Confirm that the `EquipmentSystem.cs` logic handles these names correctly (it already supports `Body_` prefix and explicit hiding).

# Verification & Testing
- **Setup**: Open the `ClothingTesting` scene.
- **Equip Test**: Equip the updated Shirt, Pants, and Boots on the Player.
- **Visual Check**: Use the scene view to ensure the corresponding body parts are disabled and no "base skin" is clipping through the clothes.
- **Unequip Test**: Unequip all items and ensure the character returns to full visibility (all `Body_` meshes active).
