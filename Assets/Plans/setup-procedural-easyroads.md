# Project Overview
- Game Title: Zombera
- High-Level Concept: Procedural world survival with EasyRoads3D and MapMagic integration.
- Goal: Create procedural roads that sync from MapMagic splines into EasyRoads3D and then into the gameplay road network (for AI and squad management).

# Problem Analysis
The current world scene has a `RoadGameplayService` but lacks the necessary "bridge" components to connect MapMagic's procedural generation to the EasyRoads3D runtime. Specifically:
1. `EasyRoadsMapMagicSync`: Missing. This component is responsible for taking spline data from MapMagic tiles and creating EasyRoads `ERRoad` objects at runtime.
2. `EasyRoadsRoadGameplayBridge`: Missing. This component scans the active EasyRoads objects and converts them into a `GameplayRoadGraph` used by the game's AI and services.
3. MapMagic Graph: The primary `ZomberaWorld` graph needs to ensure it is outputting spline data that the sync component can consume.

# Proposed Solution
Use the project's internal tooling to bootstrap the road infrastructure and then manually connect the EasyRoads spawning bridge.

## 1. Automated Infrastructure Setup
Run the `WorldRoadGenerationSetupTool` via the Unity Editor menu. This tool is designed to create the entire "Road Gameplay Stack" including:
- `RoadGameplayService` (if missing or incorrectly parented).
- `GameplayRoadGraph` and `GameplayRoadDerivedData` scriptable objects.
- `MapMagicTileStreamBridge` and `EasyRoadsRoadGameplayBridge` components on the `WorldManager`.
- Disabling any legacy road systems.

## 2. Spawning Bridge Integration
Manually add the `EasyRoadsMapMagicSync` component. This script is the missing link that actually creates the 3D road meshes from MapMagic data.

## 3. MapMagic Spline Configuration
Ensure the MapMagic graph (or its sub-graphs like `City_Area`) contains a `Spline Output` node. This node must be connected to a path generator (e.g., a "Path" node or "Road" generator).

# Key Assets & Context
- **Scripts**: 
    - `EasyRoadsMapMagicSync.cs`: Spawns EasyRoads from MapMagic.
    - `EasyRoadsRoadGameplayBridge.cs`: Syncs EasyRoads to the gameplay graph.
    - `WorldRoadGenerationSetupTool.cs`: Automates the wiring.
- **Scene Objects**: 
    - `ProceduralWorldRuntime`: The central hub for world streaming logic.
    - `WorldManager`: The main manager in the `World` scene.

# Implementation Steps

## Step 1: Run Infrastructure Setup
- **Action**: In the Unity Editor, go to `Tools > 1.Quick Dev Tools > Roads > Setup World Road Generation Stack`.
- **Effect**: This will find your `WorldManager`, add the `MapMagicTileStreamBridge` and `EasyRoadsRoadGameplayBridge`, and create/assign the necessary Road Graph assets in `Assets/ScriptableObjects/World/Roads`.

## Step 2: Add MapMagic to EasyRoads Sync
- **Action**: Select the `ProceduralWorldRuntime` object (or the object with `WorldManager`) in the `World` scene.
- **Action**: Add the `EasyRoadsMapMagicSync` component.
- **Action**: Assign the `tileStreamBridge` reference (the tool in Step 1 created this).
- **Configuration**: Set the `roadWidth` (e.g., 6.0) and ensure `buildRoadMesh` is checked.

## Step 3: Update MapMagic Graph
- **Action**: Open the `ZomberaWorld` graph (or `City_Area` sub-graph).
- **Action**: Add a `Spline Output` node.
- **Action**: Connect a spline generator (like a `Path` node) to the `Spline Output`.
- **Note**: Ensure the output index matches what `EasyRoadsMapMagicSync` expects (default is index 0).

## Step 4: Configure EasyRoads Road Types
- **Action**: Open the EasyRoads3D infrastructure (Road Network object).
- **Action**: Ensure that road types named `Road_CityCore` and `Road_Residential` (or whatever names you set in `EasyRoadsMapMagicSync`) are defined in the EasyRoads Road Type manager so they can be spawned at runtime.

# Verification & Testing
1. **Console Check**: Start the game from the `Boot` scene. Look for `[EasyRoadsMapMagicSync] Created road...` logs.
2. **Visual Check**: Navigate to the world in-game and verify that road meshes are visible on the terrain.
3. **Gameplay Check**: Verify that the `Roads` loading stage in `GameManager` reports `hasRoadData=True`.
4. **AI Check**: Command a squad member to move to a road; they should favor the road if pathfinding is configured to use the `gameplayRoadGraph`.
