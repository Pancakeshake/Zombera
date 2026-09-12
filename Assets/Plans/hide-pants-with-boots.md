# Project Overview 
- Game Title: Zombera
- High-Level Concept: Survival RPG with modular equipment system.
- Problem: Boots and Pants overlap, causing clipping. We need to hide the lower part of the pants (`Pants_Bottom`) when boots are worn.

# Key Asset & Context
- `Assets/Scripts/Inventory/EquipmentSystem.cs`: Manages equipment visibility.
- `Assets/ScriptableObjects/Items/Feet/Desert_Tan_Combat_Boo/Desert_Tan_Combat_Boo.asset`: The "Desert Tan Combat Boo" item definition.
- `Pants_Bottom`: The specific GameObject name within the pants prefab hierarchy to be hidden.

# Implementation Steps

1. **Modify EquipmentSystem.cs**:
   - Update `RefreshBodyVisibility()` to scan all renderers in the hierarchy (including those belonging to equipped items).
   - If a renderer's GameObject name matches any name in the collective `hiddenBodyParts` list of all equipped items, set it to inactive.
   - Maintain logic to manage "Body_" prefixed base body meshes.
   - **Dependency**: None.

2. **Update Boots Item Asset**:
   - Add `"Pants_Bottom"` to the `hiddenBodyParts` array in `Assets/ScriptableObjects/Items/Feet/Desert_Tan_Combat_Boo/Desert_Tan_Combat_Boo.asset`.
   - **Dependency**: Step 1.

# Verification & Testing
- **Setup**: Open a test scene (e.g., `ClothingTesting`) with a player unit.
- **Test 1 (Equip Pants)**: Equip "Camo legs 1". Verify both `Pants_Top` and `Pants_Bottom` are visible.
- **Test 2 (Equip Boots)**: Equip "Desert Tan Combat Boo" while pants are equipped. Verify `Pants_Bottom` is hidden but `Pants_Top` remains visible.
- **Test 3 (Unequip Boots)**: Unequip boots. Verify `Pants_Bottom` becomes visible again.
