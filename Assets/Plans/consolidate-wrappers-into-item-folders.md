# Consolidate Wrappers into Item Folders

The project structure is being refined to keep "Wrappers" (equipment offset prefabs) directly within their respective item folders (e.g., `Assets/Systems/Clothing/Back/Black_MOLLE_Tactical/`). This plan updates the batch tools to support this decentralized structure and adopts a `Wrapper_` prefix naming convention.

## Project Overview
- **Goal:** Decentralize wrapper storage and standardize naming.
- **Naming Convention:** `Wrapper_` + `[ItemName]`.
- **Target Folder:** The same folder containing the model and item definition.

## Implementation Steps

### 1. Update Clothing Batch Tool
Modify `Assets/Editor/ClothingPrefabAndWrapperBatchTool.cs`.
- **Changes:**
    - Replace `AppendWrapperSuffix` with `PrependWrapperPrefix` (naming change to `Wrapper_[Name]`).
    - Update `BatchBuildWrappersInternal` to save new wrappers into the same directory as the source prefab using `PrefabRoot`.
    - Update `CreateItemDefinitionsFromPrefabsInternal` to look for wrappers in the item's own directory.
    - Update the skip logic to ignore prefabs that already start with `Wrapper_` to prevent recursive wrapping.
    - Keep `LegacyWrapperRoot` checks for migration.

### 2. Update Weapon Batch Tools
Modify `Assets/Editor/WeaponEquipWrapperBatchTool.cs` and `Assets/Editor/WeaponCleanPrefabAndWrapperBatchTool.cs`.
- **Changes:**
    - Replace `AppendWrapperSuffix` with `PrependWrapperPrefix`.
    - Update paths to use the item's own folder instead of a centralized `_Wrappers` folder.
    - Standardize the `Wrapper_` prefix naming.

### 3. Migration
Running these tools will automatically move legacy wrappers from `_Wrappers` or `1.EquipWrappers` into the correct item folders and rename them to follow the new convention.

## Verification & Testing
1. **Clothing Test:** Run "Build Wrappers from Clothing Prefabs" and verify a `Wrapper_...` prefab is created next to the source model.
2. **Weapon Test:** Run "Batch Wire Weapon Equipment" and verify the `equippedVisualPrefab` in the `ItemDefinition` points to a `Wrapper_...` prefab in the weapon's own folder.
3. **Hierarchy Check:** Verify that no new `_Wrappers` folders are created.
