# Project Overview 
- **Game Title**: Zombera
- **Goal**: Expand the weapon system to support multiple holster/equip slots with per-slot visual offsets for testing placement.
- **Architecture Change**: Move weapon visual management to the `WeaponSystem` component for better integration with combat state.

# Game Mechanics 
## Controls and Input Methods
The system allows designers to test weapon placement on the body (back, hip, chest, hands) directly in the Unity Inspector by assigning `ItemDefinition` assets to specific slots.

# Key Asset & Context
- `Assets/Scripts/Combat/WeaponSystem.cs`: Will be updated to manage weapon visuals in various holster and hand slots.
- `Assets/Scripts/Inventory/ItemDefinition.cs`: Will be updated to store an array of per-slot offsets.
- `Assets/Scripts/Inventory/EquipmentSystem.cs`: Will be updated to delegate hand-slot visuals to the weapon system.

# Implementation Steps

1. **Update ItemDefinition.cs**:
   - Add a `WeaponSlot` enum (or use the one from WeaponSystem).
   - Add a `WeaponVisualOffset` serializable struct containing `WeaponSlot`, `Position`, `Rotation`, and `Scale`.
   - Add `public WeaponVisualOffset[] slotOffsets;` to `ItemDefinition`.
   - **Dependency**: None.

2. **Update WeaponSystem.cs**:
   - Add `WeaponSlot` enum: `Back`, `Hip_R`, `Hip_L`, `Chest`, `Hand_R`, `Hand_L`.
   - Add serializable fields for testing: `backSlotItem`, `hipRSlotItem`, `hipLSlotItem`, `chestSlotItem`, `handRSlotItem`, `handLSlotItem`.
   - Implement `RebuildVisuals()`:
     - Clear existing instances with `_equippedVisuals` dictionary.
     - Instantiate `equippedVisualPrefab` from each item.
     - Resolve sockets (Back -> UpperChest, Hip -> UpperLeg, etc.).
     - Apply offsets: Look for a match in the item's `slotOffsets`; fallback to default offsets if not found.
   - Add `OnValidate` with `EditorApplication.delayCall` to support real-time inspector updates.
   - **Dependency**: Step 1.

3. **Update EquipmentSystem.cs**:
   - Modify `RebuildEquippedVisuals` to skip `LeftHand` and `RightHand` slots, allowing `WeaponSystem` to manage them exclusively.
   - This prevents double-instantiation when testing weapons.
   - **Dependency**: Step 2.

4. **Testing and Validation**:
   - Open `ClothingTesting` scene.
   - Assign a weapon (e.g., Katana or Fire Axe) to different slots on the Player's `WeaponSystem`.
   - Configure offsets in the weapon's `ItemDefinition` asset to ensure it sits correctly in holsters.

# Verification & Testing
- **Visual Consistency**: Ensure weapons appear in the correct physical locations (e.g., Back slot attaches to the spine).
- **Offset Precision**: Verify that changing offsets in the `ItemDefinition` asset immediately updates all units using that item in the scene.
- **Conflict Check**: Confirm that equipping an item in `EquipmentSystem` and `WeaponSystem` hand slots doesn't create two visual objects.
