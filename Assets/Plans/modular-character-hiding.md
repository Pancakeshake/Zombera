# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with clothing/equipment system.
- Players: Single player.
- Target Platform: PC (Windows).

# Game Mechanics
## Modular Character Hiding
The user has split the player body mesh into separate GameObjects (e.g., `Body_Chest`, `Body_Legs`). We need to update the `EquipmentSystem` to correctly identify and toggle these parts based on the `hiddenBodyParts` list defined on equipped items.

# Key Asset & Context
- `Assets/Scripts/Inventory/EquipmentSystem.cs`: The core logic for handling equipment and visuals.
- `ItemDefinition`: ScriptableObject that contains the `hiddenBodyParts` string array.

# Implementation Steps

## 1. Refine EquipmentSystem.RefreshBodyVisibility
- Update the logic to explicitly identify the new modular body parts.
- Ensure the system correctly toggles `SetActive` on these parts when items are equipped or unequipped.
- Add a safety check to ensure we don't accidentally hide the clothing itself if its name contains "Body".
- **Files**: `Assets/Scripts/Inventory/EquipmentSystem.cs`

## 2. Configure ItemDefinitions (Testing)
- Update the `hiddenBodyParts` array for `T-Shirt 1` and `Camo_legs 1` to match the new GameObject names.
- This will be done via a one-time editor script for immediate verification.
- **Names to hide**:
    - **T-Shirt**: `Body_Chest`, `Body_Waist`, `Body_UpperArms`.
    - **Camo Legs**: `Body_Thighs`, `Body_Knees`, `Body_Shins`, `Body_Hips`.

# Verification & Testing
- **Visual Check**: In the `ClothingTesting` scene, equip the T-Shirt and verify that the `Body_Chest` part of the player mesh disappears, preventing any clipping.
- **Equip/Unequip**: Verify that removing the item makes the body part reappear.
- **Overlap**: Verify that if two items hide the same part (e.g., an over-vest and a shirt), it stays hidden and reappears only when both are removed.
