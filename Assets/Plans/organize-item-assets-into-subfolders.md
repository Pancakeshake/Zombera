# Organize Item Assets into Subfolders (Refined)

This plan refines the `Assets/Systems/` structure by organizing individual item assets into standardized subfolders (`Model`, `Material`, `Wrappers`, `Data`, `UMA_[ItemName]`) and ensures that migration overwrites existing assets in subfolders instead of leaving duplicates in the item root. It also adds automated UMA asset generation.

## Project Overview
- **Goal:** Improve asset organization and ensure clean migration/overwriting of models and prefabs.
- **Item Folder Structure:**
    - `Assets/Systems/[System]/[Slot]/[ItemName]/`
        - `Model/` - Contains the source FBX.
        - `Material/` - Contains Materials and Textures.
        - `Wrappers/` - Contains the `Wrapper_` prefabs.
        - `Data/` - Contains the `ItemDefinition` asset and Data assets (`ArmorData`/`WeaponData`).
        - `UMA_[ItemName]/` - Contains `SlotDataAsset`, `OverlayDataAsset`, and `UMAWardrobeRecipe`.
        - (Root) - Contains the main `.prefab`.

## Implementation Steps

### 1. Enhance Migration Utility
Modify `TryMoveAssetIfNeeded` in all batch tools.
- **Change:** If the destination asset already exists and is different from the source path, delete the destination asset before moving to ensure the "newest" file overwrites the one in the subfolder.

### 2. Implement UMA Generation in Clothing Tool
Modify `Assets/Editor/ClothingPrefabAndWrapperBatchTool.cs`.
- **Changes:**
    - Add `CreateUmaAssetsFromPrefabsInternal` to generate the UMA folder and its assets (`Slot`, `Overlay`, `Recipe`) with names like `UMA_[ItemName]_Recipe`.
    - Update `ApplyItemDefinitionDefaults` to automatically find and link the `UMA_[ItemName]_Recipe` to the `ItemDefinition`.
    - Ensure `UMAWardrobeRecipe` is assigned the correct `wardrobeSlot` based on the item category.

### 3. Verify Weapon and Meshy Tools
- Ensure same overwrite/move logic is applied to maintain consistency.

## Verification & Testing
1. **Migration Test:** Run the tool on an existing item and verify it reorganizes the files correctly (Model, Material, Wrappers, Data, UMA).
2. **UMA Creation Test:** Run the tool and verify that `UMA_[ItemName]` folder is created with the three UMA assets, and they are linked to the `ItemDefinition`.
3. **Hierarchy Check:** Verify `T-Shirt1` and similar items follow the structure:
    - `T-Shirt1/T-Shirt1.prefab` (Root)
    - `T-Shirt1/Model/T-Shirt1.fbx`
    - `T-Shirt1/Wrappers/Wrapper_T-Shirt1.prefab`
    - `T-Shirt1/Data/T-Shirt1.asset`
    - `T-Shirt1/UMA_T-Shirt1/UMA_T-Shirt1_Recipe.asset`
