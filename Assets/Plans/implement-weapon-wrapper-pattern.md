# Project Overview
- **Game Title**: Zombera
- **Goal**: Implement a "Wrapper Prefab" pattern for weapons and automate its creation/wiring using an Editor Tool.
- **Problem**: Individual weapon models have different orientations and scales, leading to messy offset lists in `ItemDefinition` assets.
- **Solution**: 
    - Use "Equip Wrappers" (Empty Root + Mesh Child).
    - Alignment is handled once per prefab (on the child).
    - `ItemDefinition` assets stay clean with zeroed offsets.
    - Automation: A tool to generate these wrappers and wire them to items.

# Game Mechanics
- **Equipment Visuals**: The `EquipmentSystem` instantiates the wrapper.
- **Consistency**: Handle placement is standardized across all weapons.

# Key Assets & Context
- **Tool Script**: `Assets/Editor/WeaponEquipWrapperBatchTool.cs`
- **Wrapper Folder**: `Assets/Prefabs/Weapons/EquipWrappers/`
- **Target Item**: `Axe_DS` (and others).

# Implementation Steps

## 1. Create the Batch Tool
- **Task**: Create `Assets/Editor/WeaponEquipWrapperBatchTool.cs`.
- **Logic**:
    1. Scan `Assets/Models_Weapons` and `Assets/ThirdParty/.../Meshes` for weapon models.
    2. For each model:
        - Check if a wrapper already exists in `Assets/Prefabs/Weapons/EquipWrappers/`.
        - If not, create a new prefab: Empty Root -> Model Child.
        - Find the matching `ItemDefinition` (by name or ID).
        - Assign the wrapper to `item.equippedVisualPrefab`.
        - **Reset Offsets**: Set the item's `equippedVisualLocalPosition/Rotation` to (0,0,0) and `Scale` to (1,1,1).

## 2. Execute for Axe DS
- **Task**: Run the tool to specifically generate and wire the `Axe_DS_Wrapper`.
- **Source**: `Assets/ThirdParty/GameReady3D/Post_Apocalyptic_Asset_Pack/Meshes/SM_Axe.fbx`.

## 3. Manual Alignment (One-time)
- **Task**: Open the newly created `Axe_DS_Wrapper` prefab.
- **Action**: Rotate and position the **child mesh** (the SM_Axe) so its handle sits at the parent's (0,0,0) point.
- **Benefit**: This fix persists globally for any character equipping this axe.

# Verification & Testing
1. **Play Mode**: Equip the Axe DS.
2. **Visual Check**: Ensure the axe appears.
3. **Consolidated Logic**: Verify that the `ItemDefinition` for Axe DS now has zeroed offsets, while the weapon is correctly positioned.
