# Project Overview
- Game Title: Zombera
- Road System: EasyRoads3D + MapMagic + Zombera Road Gameplay Service.
- Goal: Fix road spawning and AI integration.

# Problem Analysis
The "Road Gameplay Stack" is currently fragmented and redundant, causing the road spawning sequence to fail.
1. **Redundant Bridges**: There are three `MapMagicTileStreamBridge` instances in the scene (`WorldManager`, `RoadGameplayService`, and `ProceduralWorldRuntime`).
2. **Missing Binding**: The `EasyRoadsMapMagicSync` component on the `RoadGameplayService` object is likely using the local bridge instance which is **not bound** to the `MapMagic` object. Consequently, it never receives the `TileApplied` events required to spawn roads.
3. **Empty Syncs**: The `EasyRoadsRoadGameplayBridge` (on `WorldManager`) is reporting "ProcessedSync=True" but finding 0 roads because the spawner (Sync) never ran.
4. **UMA Errors**: A secondary issue exists where UMA avatars are failing to build because they can't find the `UMA_GLIB` (Context/Generator) after it was moved to the `Boot` scene.

# Proposed Solution

## 1. Hierarchy Consolidation (Roads)
Consolidate all road-related logic onto the `RoadGameplayService` object to ensure they all share the same event source and data references.

### Target Configuration for `RoadGameplayService` Object:
- **`MapMagicTileStreamBridge`**: The **ONLY** active bridge in the scene. It must be bound to the `MapMagic` object.
- **`EasyRoadsMapMagicSync`**: Pointed to the bridge above.
- **`EasyRoadsRoadGameplayBridge`**: Pointed to the bridge above.
- **`RoadGameplayService`**: Pointed to the `EasyRoadsRoadGameplayBridge`.

## 2. UMA Centralization Fix
Update the UMA scripts to support the global `UMA_GLIB` in the `Boot` scene.

# Implementation Steps

## Step 1: Cleanup Scene Redundancy
- **Action**: Locate the `World` scene.
- **Action**: Disable or remove the `MapMagicTileStreamBridge` and `EasyRoadsRoadGameplayBridge` components from the `WorldManager` object.
- **Action**: Disable or remove the `MapMagicTileStreamBridge` component from the `ProceduralWorldRuntime` object.

## Step 2: Fix Road Stack Binding
- **Action**: Select the `RoadGameplayService` object.
- **Action**: Ensure `MapMagicTileStreamBridge` is enabled.
- **Action**: Ensure `EasyRoadsMapMagicSync` and `EasyRoadsRoadGameplayBridge` have their `tileStreamBridge` fields assigned to the bridge on the **same** object.
- **Action**: Ensure the `WorldManager`'s `tileStreamBridge` field is also pointed to this same bridge on `RoadGameplayService`.

## Step 3: Script Upgrades (UMA & Roads)
- **File**: `Assets/Scripts/UI/Menus/CharacterCreatorController.cs`
- **Change**: Modify `FindUmaContextInCurrentScene` and `FindUmaGeneratorInCurrentScene` to fall back to `UMAContextBase.Instance` and global generator search if scene-local instances are missing.
- **File**: `Assets/Scripts/Characters/UmaSpawnStylingService.cs`
- **Change**: Update `RebindAvatarToOwnerScene` to preserve existing references if no local `UMA_GLIB` is found.

## Step 4: Re-run Setup Tool (Optional but Recommended)
- **Action**: Use `Tools > 1.Quick Dev Tools > Roads > Setup World Road Generation Stack` after the hierarchy cleanup to re-verify the internal links.

# Verification & Testing
1. **Console Check**: Enter Play Mode from `Boot`.
2. **Road Spawning**: Verify `[EasyRoadsRoadGameplayBridge] Synced X EasyRoads roads...` appears in the console.
3. **Road Data**: Verify `GameManager` reports `hasRoadData=True` during the Roads loading stage.
4. **UMA Check**: Verify the Character Creator avatar appears correctly and the player character spawns with the correct appearance.
5. **AI Check**: Verify squad members can walk on roads.
