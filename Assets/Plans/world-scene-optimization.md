# Project Overview
- Game Title: Zombera
- Goal: Optimize and organize the `World` scene hierarchy and fix broken road authoring components.

# Key Asset & Context
- `Assets/Scenes/World.unity`: The main gameplay scene.
- `[Road Gameplay Stack]`: Currently contains objects with missing scripts.
- `UMA_GLIB`: Contains redundant preview objects in the World scene.

# Implementation Steps

## 1. Road Gameplay Stack Resolution
- **Observation:** The `Road Gameplay Stack` has objects (`Node_B`, `Segment_Main`, `Spawn_Ambient_01`) with missing scripts. This prevents authoring road metadata in the scene.
- **Action:**
    - If the goal is to author roads in-scene: Re-add `RoadGameplayAuthoringNode` to `Node_B`, `RoadGameplayAuthoringSegment` to `Segment_Main`, and `RoadGameplayAuthoringSpawnPoint` to `Spawn_Ambient_01`.
    - If the road data is already baked: Remove the `Road Gameplay Stack` and only keep the `RoadGameplayService` (on a system object) which holds the reference to the baked data. 
    - **Recommendation:** Remove the authoring children and keep only the service, as the service currently points to a valid baked graph (`GameplayRoadGraph`).

## 2. Global Organization (The Folder Pattern)
Create empty GameObjects at the root of the `World` scene to act as folders (Position 0,0,0, Scale 1,1,1).
- **[SYSTEMS]**: Move `PlayerSpawner`, `RTS Control Stack`, `ProceduralWorldRuntime`, `Building Manager`, `UIEventSystem`, and `RoadGameplayService`.
- **[ENVIRONMENT]**: Move `Environment`, `Crest Ocean`, `Crest Water Body`, and `MapMagic`.
- **[GAMEPLAY]**: Move `Player` and `Player_Buildings`.

## 3. UMA World Scene Cleanup
- **Action:** In the `World` scene's `UMA_GLIB` object, delete the `UMAPreviewAvatar` and `UMAPreviewCamera`.
- **Reason:** These are used for character customization in the Main Menu. In the World scene, they cause unnecessary overhead and create a second `AudioListener` conflict.

## 4. Enviro 3 Optimization
- **Action:** Remove the `Rigidbody` and `BoxCollider` components from the `Enviro 3` root object.
- **Reason:** As a global weather manager, it does not require physics or trigger volumes unless specifically configured for regional weather (which isn't the case for the root manager).

## 5. Building System Redundancy Stripping
- **Action:** Remove the following components from `MapMagic` and `Main Camera` objects in the `World` scene:
    - `BuildingInput`
    - `BuildingController`
    - `FirstPersonBuildingView`
    - `PlacementBuildingState`
    - `AdjustmentBuildingState`
    - `DestructionBuildingState`
    - `UpgradeBuildingState`
- **Reason:** These components are already present on the `Player` object, which is the correct owner for building interaction. Having them on the Camera or Terrain leads to duplicate input processing.

# Verification & Testing
1. **NavMesh & Roads:** Verify that zombies still spawn and follow road paths (using the `RoadGameplayService` data).
2. **Input Consistency:** Ensure building only works when the player is active and doesn't trigger twice.
3. **Audio Check:** Confirm only one `AudioListener` is reported in the console.
4. **Visual Check:** Ensure weather and ocean systems are still rendering correctly after reorganization.
