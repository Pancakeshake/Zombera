# Project Overview
- Game Title: Zombera
- High-Level Concept: A procedural survival game with a focus on exploration and city building.
- Players: Single player.
- Inspiration / Reference Games: 7 Days to Die, Project Zomboid.
- Tone / Art Direction: Realistic / Gritty.
- Target Platform: PC (Windows).
- Screen Orientation / Resolution: Landscape 1920x1080.
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
Players explore a procedurally generated world, gather resources, build bases, and defend against zombies. The road network is crucial for navigation and city layout.
## Controls and Input Methods
Standard FPS/Third-Person controls via Keyboard/Mouse. New Input System is active.

# UI
The road network is a background system and doesn't have a direct UI, but its generation affects building placement and navigation.

# Key Asset & Context
- `ProceduralRoadSystem.cs`: The core logic for generating EasyRoads3D objects from a procedural graph.
- `RoadNetworkSettings.cs`: ScriptableObject containing generation parameters.
- `ERRoadNetwork`, `ERRoad`, `ERConnection`: EasyRoads3D API classes.
- `Connection Objects` & `Road Objects`: Scene containers within the "Road Network" GameObject.

# Implementation Steps
The goal is to align the `ProceduralRoadSystem` with the architecture found in "Demo Scene v3.2+", specifically utilizing `ERConnection` objects (Connectors) more effectively.

## 1. Improve Connection Prefab Discovery
Currently, only 3 types are found. We need to expand this to include `I Connector` and `Flex Connector`.
- **Files**: `ProceduralRoadSystem.cs`
- **Action**: Update `FindConnectionPrefabs()` to search for "I Connector" and "Flex Connector" from the road network's available connections.

## 2. Implement Connector-Based Road Termination
All road ends that are not junctions (degree 1) should be terminated with an `I Connector`.
- **Files**: `ProceduralRoadSystem.cs`
- **Action**: In `CreateJunctionsInTile`, handle degree 1 endpoints by instantiating an `I Connector` and attaching it to the road end using `marker.road.ConnectToStart/End`.

## 3. Implement Flexible Stitching at Tile Boundaries (Degree 2)
Instead of manual marker snapping, use `Flex Connector` or `ConnectRoads` to stitch road segments that cross tile boundaries.
- **Files**: `ProceduralRoadSystem.cs`
- **Action**: Update `ConnectRoadMarkersAtSharedPoints` to use `ERRoadNetwork.ConnectRoads` or instantiate a `Flex Connector` at the shared points. This allows EasyRoads to handle the geometry blending.

## 4. Align with Scene Hierarchy
Ensure all procedurally generated roads and connections are correctly parented.
- **Files**: `ProceduralRoadSystem.cs`
- **Action**: Explicitly find "Connection Objects" and "Road Objects" children of the "Road Network" and ensure generated GameObjects are children of these. (EasyRoads usually does this automatically if configured, but we should verify).

## 5. Refine Junction Logic
Ensure junctions (degree >= 3) use the best-fit prefab based on the road classes and angles.
- **Files**: `ProceduralRoadSystem.cs`
- **Action**: Improve `CreateJunctionsInTile` to better select between X, T, and Roundabout based on `arterialCount` and angle analysis.

# Verification & Testing
1.  **Scene Inspection**: Run the procedural generation and verify that the "Road Network" hierarchy contains "Connection Objects" and "Road Objects" with all generated parts.
2.  **Visual Check**: Verify that road ends have "I Connector" caps.
3.  **Boundary Check**: Verify there are no gaps or Z-fighting at tile boundaries where roads were stitched.
4.  **Logging**: Check the "RoadGenerationDiagnostics" log to ensure successful junction and auto-connect counts.
