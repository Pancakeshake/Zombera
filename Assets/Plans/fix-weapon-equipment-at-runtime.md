# Project Overview
- **Game Title**: Zombera
- **Goal**: Fix weapons not equipping visually when using the inventory system in the `Test_AnimationBlendTrees` scene.
- **Current Issue**: 
    - The `Player` and `SquadMember_Runtime` prefabs are missing the `EquipmentSystem` and `WeaponSystem` components.
    - The `InventoryPanelController` attempts to add these components at runtime, but they are not pre-configured, and the runtime initialization might be failing to find the correct hand sockets on UMA characters.
    - No visual feedback (instantiation of `_Equipped` objects) is occurring during play.

# Game Mechanics
- **Inventory/Equipment**: Users can click "Equip" on a weapon in the inventory to attach it to the unit's hand and update their combat stats.

# Key Assets & Context
- **Prefabs**:
    - `Assets/Prefabs/Player/Player.prefab`
    - `Assets/Prefabs/Player/SquadMember_Runtime.prefab`
- **Scripts**:
    - `Assets/Scripts/Inventory/EquipmentSystem.cs`
    - `Assets/Scripts/Combat/WeaponSystem.cs`
- **Data**: `ItemDefinition` assets in `Assets/ScriptableObjects/Items/`.

# Implementation Steps

## 1. Update Player and Squadmate Prefabs
- **Task**: Add `EquipmentSystem` and `WeaponSystem` components to the base prefabs so they are correctly initialized and configured at spawn time.
- **Files**:
    - `Assets/Prefabs/Player/Player.prefab`
    - `Assets/Prefabs/Player/SquadMember_Runtime.prefab`
- **Dependency**: None.

## 2. Enhance EquipmentSystem Logging
- **Task**: Add diagnostic logging to `EquipmentSystem.ApplyVisualAttachment` and `ResolveSocket` to identify why a socket or prefab might be failing to load.
- **File**: `Assets/Scripts/Inventory/EquipmentSystem.cs`
- **Dependency**: None.

## 3. Configure ItemDefinition Defaults
- **Task**: Ensure that standard weapons (Axes, Swords, Guns) have `enforceSpecificEquipSlot` enabled and `forcedEquipSlot` set to `RightHand` to prevent them from defaulting to the `Belt` slot if the item type is ambiguous.
- **File**: Various `.asset` files in `Assets/ScriptableObjects/Items/`.
- **Dependency**: Step 1.

## 4. Verify Socket Names
- **Task**: Check if adding a specific `Socket_RightHand` transform to the UMA character root helps the `EquipmentSystem` find the hand reliably, even if the Animator's bone transform is delayed during UMA generation.
- **Dependency**: Step 1.

# Verification & Testing
1. **Play Mode**: Start the `Test_AnimationBlendTrees` scene.
2. **Equip Check**: Open the inventory and equip a weapon (e.g., Axe or Rifle).
3. **Console Check**: Monitor the console for "Equipped [Weapon] to [Socket]" or any "Could not resolve socket" warnings.
4. **Hierarchy Check**: Verify that a child object named `[WeaponName]_Equipped` appears under the player's `RightHand` bone in the hierarchy.
