# Refactor Clothing, Weapon, and Meshy Importer Tools to Assets/Systems Structure

The project is moving to a feature-based organization structure under `Assets/Systems/`. This plan refactors the automated batch tools to align with this structure, ensuring that models, prefabs, and ScriptableObjects are kept together within their respective system folders.

## Project Overview
- **Game Title:** Zombera
- **Target Platform:** Standalone Windows 64
- **New Structure Goal:** `Assets/Systems/[Feature]/[ItemName]/` contains all related assets.

## Implementation Steps

### 1. Refactor Clothing Tool
Modify `Assets/Editor/ClothingPrefabAndWrapperBatchTool.cs` to use the new root path and consolidate asset outputs.
- **File:** `Assets/Editor/ClothingPrefabAndWrapperBatchTool.cs`
- **Changes:**
    - Update `ModelRoot` to `Assets/Systems/Clothing`.
    - Update `PrefabRoot` to `Assets/Systems/Clothing`.
    - Update `ItemRoot` to `Assets/Systems/Clothing`.
    - Update `ClothesAssetRoot` to `Assets/Systems/Clothing`.
    - Update `WrapperRoot` to `Assets/Systems/Clothing/_Wrappers` (keeping non-item specific wrappers in a categorized subfolder).
- **Impact:** When building a clothing item like "Tactical Vest" in the "Chest" slot, assets will now be created in `Assets/Systems/Clothing/Chest/TacticalVest/`.

### 2. Refactor Weapon Tools
Modify the weapon pipeline scripts to consolidate outputs under `Assets/Systems/Weapons/`.
- **Files:** 
    - `Assets/Editor/WeaponCleanPrefabAndWrapperBatchTool.cs`
    - `Assets/Editor/WeaponEquipmentBatchTool.cs`
    - `Assets/Editor/WeaponEquipWrapperBatchTool.cs`
- **Changes:**
    - Update `ModelRoot`, `PrefabRoot`, `VisualRoot`, `WeaponDataRoot`, and `ItemRoot` to `Assets/Systems/Weapons`.
    - Update `WrapperRoot` to `Assets/Systems/Weapons/_Wrappers`.
- **Impact:** Weapons and their data will now be grouped together (e.g., `Assets/Systems/Weapons/Guns/AK47/`).

### 3. Refactor Meshy Importer
Update the Meshy Importer tool to point its destination roots to the new system folders.
- **File:** `Assets/Editor/MeshyImportPipelineTool.cs`
- **Changes:**
    - Update `WeaponsModelRoot` to `Assets/Systems/Weapons`.
    - Update `ClothingModelRoot` to `Assets/Systems/Clothing`.
- **Impact:** New imports from Meshy will automatically be placed in the correct `Assets/Systems/` subfolders.

### 4. Folder Creation
Ensure the new root folders exist.
- **Folders to Create:**
    - `Assets/Systems/Clothing`
    - `Assets/Systems/Weapons`

## Verification & Testing
1. **Manual Check:** Run the "Meshy Importer" with a test asset and verify it lands in `Assets/Systems/Weapons/` or `Assets/Systems/Clothing/`.
2. **Clothing Pipeline:** Run the "Clean Names + Create Prefabs..." tool for a clothing item and verify the `.prefab`, `.asset` (ItemDefinition), and `.asset` (ArmorData) are all in the same folder as the model.
3. **Weapon Pipeline:** Run the weapon batch tool and verify the `WeaponData` and `ItemDefinition` are created alongside the weapon prefab.
4. **Reference Check:** Ensure that existing items still work (the tools are designed to "Move" assets if they find them in legacy locations, so this refactor acts as a migration path).
