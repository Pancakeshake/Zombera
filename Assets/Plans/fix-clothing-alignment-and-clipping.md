# Project Overview
- Game Title: Zombera
- Goal: Fix clothing alignment and clipping issues to achieve "100% correct" visual equipment.
- Issue: Clothing is currently parented to sockets, causing offsets for skinned meshes. Character skin clips through clothing.

# Game Mechanics
## High-Fidelity Clothing (Refined)
The system distinguishes between **Static Props** (hats, tools) and **Skinned Clothing** (shirts, vests, pants). 
- **Skinned Clothing**: Instantiated at the character root and "stitched" to the armature. This prevents offsets and ensures perfect movement.
- **Static Props**: Socketed to specific bones as before.

## Body Part Hiding
Items can now define which parts of the base character body they should hide (e.g., a "Parka" hides the "Chest" and "UpperArms"). This eliminates clipping.

# Key Asset & Context
- `Assets/Scripts/Inventory/EquipmentSystem.cs`: Refactor instantiation logic and add body visibility management.
- `Assets/Scripts/Inventory/ItemDefinition.cs`: Add `hiddenBodyParts` metadata.

# Implementation Steps

### 1. Update ItemDefinition
- Add `public string[] hiddenBodyParts;` to the `ItemDefinition` class.
- This allows artists to specify which body meshes to disable (e.g., "Body_Chest", "Body_Legs").

### 2. Update EquipmentSystem: Instantiation Logic
- Modify `ApplyVisualAttachment`:
    - If the item has a `SkinnedMeshRenderer`, instantiate it as a direct child of `transform` (the character root) with `localPosition = Vector3.zero` and `localRotation = Quaternion.identity`.
    - Else (static mesh), continue parenting to the socket.
- Call `RefreshBodyVisibility()` after any change.

### 3. Update EquipmentSystem: Body Visibility
- Implement `RefreshBodyVisibility()`:
    - First, enable all body parts (meshes under a "Body" container or tagged accordingly).
    - Second, iterate through all equipped items and collect their `hiddenBodyParts`.
    - Third, disable any GameObjects on the character that match these names.

### 4. Verification & Testing
- **Alignment**: Equip the "Green Vest" and verify it sits correctly on the torso, not floating or offset.
- **Clipping**: Equip a full suit; verify the skin beneath it is hidden.
- **Animation**: Ensure the vest deforms correctly during the run animation.
