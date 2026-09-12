# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with zombie combat, base building, and inventory management.
- Players: Single player (based on current scene setup).
- Target Platform: PC (Windows).
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
The user is focusing on the "Clothing and Equipment" aspect of the character system. They want to be able to preview equipment changes and animations in a dedicated testing scene.

## Controls and Input Methods
- Editor-based interaction: Dragging and dropping items into slots in the Inspector.
- Testing controls: Buttons or automated scripts to trigger animations.

# UI
- No new runtime UI is requested. The focus is on the **Unity Editor Inspector** experience for the `EquipmentSystem` and `WeaponSystem`.

# Key Asset & Context
- `Assets/Scripts/Inventory/EquipmentSystem.cs`: The primary script for managing equipped items and their visuals.
- `Assets/Scripts/Combat/WeaponSystem.cs`: Manages weapon data and firing logic.
- `Assets/Scripts/Characters/PlayerAnimationController.cs`: Handles character animations and variants.
- `Assets/Scripts/Testing/AnimationPreviewer.cs` (New): A utility to play animations in the test scene.

# Implementation Steps

## 1. Enhance EquipmentSystem for Editor Interaction
- Modify `EquipmentSystem.cs` to support visual updates in Edit Mode.
- **Dependencies**: None.
- **Changes**:
    - Add `[ExecuteAlways]` attribute to the class.
    - Implement `OnValidate` to call `RebuildEquippedVisuals()` when values change in the inspector.
    - Add explicit `ItemDefinition` fields for common slots (Head, Chest, Legs, Feet, Hands) to allow easy "dropping" of items into specific slots without navigating the `equippedItems` list.
    - Add logic to sync these explicit fields with the `equippedItems` list.

## 2. Enhance WeaponSystem for Slot Management
- Modify `WeaponSystem.cs` to support a secondary weapon slot.
- **Dependencies**: None.
- **Changes**:
    - Add a `secondaryWeapon` field (WeaponData).
    - Add a method/button to switch between `equippedWeapon` and `secondaryWeapon`.
    - Ensure `EquipWeapon` logic is compatible with editor-time previews.

## 3. Create Animation Preview Utility
- Create a new script `AnimationPreviewer.cs` in `Assets/Scripts/Testing/`.
- **Dependencies**: `PlayerAnimationController`.
- **Changes**:
    - Script will reference the `PlayerAnimationController` and `Animator`.
    - Expose a button in the Inspector to "Play Random Animation".
    - Logic will pick a random clip from `PlayerAnimationController.playerFolderClips` or trigger common triggers (Attack, Dodge, Hit).
    - Add a "Auto-Cycle" mode to automatically play a new animation every few seconds.

## 4. Update ClothingTesting Scene
- Add the `AnimationPreviewer` component to the `Player` object in the `ClothingTesting` scene.
- Ensure the `Player` object's `EquipmentSystem` and `WeaponSystem` are correctly configured for the new slots.

# Verification & Testing
- **Editor Visuals**: Verify that changing an item in the `EquipmentSystem` inspector immediately updates the character's visual model in the Scene View (Edit Mode).
- **Slot Drop**: Verify that dragging an `ItemDefinition` into the "Head" slot correctly equips it and replaces any existing head item.
- **Animation Testing**: Verify that clicking the "Play Random Animation" button triggers a valid animation on the player model.
- **Weapon Switching**: Verify that switching between primary and secondary weapons updates the `WeaponSystem` state.
