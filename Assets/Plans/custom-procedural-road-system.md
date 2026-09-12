# Project Overview
- Game Title: Zombera
- Goal: Move away from MapMagic's built-in spline system and implement a custom, integrated procedural road generator that works with MapMagic tiles and EasyRoads3D.

# Problem Analysis
The current road system is heavily dependent on MapMagic's `SplineOutput` nodes. This limits flexibility and creates synchronization issues between procedural terrain and gameplay-aware roads. The user wants a custom procedural generator that defines the "Road Network" independently of the MapMagic graph nodes, while still being able to "spawn" roads into MapMagic tiles at runtime.

# Proposed Design: "Integrated Procedural Road System"

## 1. Global Road Graph (`RoadNetworkRuntime`)
We will use a persistent, global road graph generated from a world seed. This graph defines the high-level layout (Highways, Arterials, Local roads) across the entire world, ensuring connectivity across tile boundaries.

## 2. Procedural Road Generator (`ProceduralRoadGenerator`)
A new static or service-based generator that replaces the legacy `RoadNetworkGenerator`. It will support:
- **Global Trunk Roads**: Major highways connecting distant regions.
- **Local Grids**: Generated on-demand or pre-calculated for urban zones.
- **Terrain Awareness**: Adjusting road paths based on terrain slopes and water (by sampling the MapMagic heightmap).

## 3. Authoritative Controller (`ProceduralRoadSystem`)
This component will replace `RoadNetworkSystem` and `EasyRoadsMapMagicSync`.
- **Tile-Driven Spawning**: It listens to MapMagic's `TileApplied` events.
- **Clipping & Spawning**: For each applied tile, it identifies overlapping roads in the Global Graph, clips them to the tile's bounds, and triggers EasyRoads3D to build the meshes.
- **Data Provider**: It populates the `RoadGameplayService` with runtime samples, ensuring the building spawner (`StreamedMapMagicCityBuilder`) and AI can "see" the roads.

# Implementation Steps

## Step 1: Enhance the Procedural Generator
- **Action**: Refactor `RoadNetworkGenerator.cs` into a more robust `ProceduralRoadGenerator`.
- **Feature**: Add logic for grid generation in "City" regions and organic pathing for rural areas.
- **Output**: Returns a `RoadNetworkRuntime` containing `RoadPolyline` segments.

## Step 2: Create the Procedural Road System
- **Action**: Implement `ProceduralRoadSystem.cs` (or refactor `RoadNetworkSystem.cs`).
- **Logic**:
    - On `Awake`, generate the global `RoadNetworkRuntime`.
    - On `TileApplied`, find all `RoadPolyline` segments intersecting the tile's world rect.
    - Use `EasyRoads3Dv3` APIs to create `ERRoad` objects from these segments.
    - Build the EasyRoads network for the tile.
- **Dependency**: EasyRoads3D Runtime API.

## Step 3: Link to Building and AI Systems
- **Action**: Ensure `ProceduralRoadSystem` registers its generated roads with `RoadGameplayService`.
- **Action**: Update `StreamedMapMagicCityBuilder.cs` to prioritize samples from `RoadGameplayService` over MapMagic splines.
- **Dependency**: `RoadGameplayService`.

## Step 4: Scene Setup and Migration
- **Action**: Open the `World` scene.
- **Action**: Remove legacy `EasyRoadsMapMagicSync` and `MapMagic` spline output nodes.
- **Action**: Add the new `ProceduralRoadSystem` and configure it with `RoadNetworkSettings`.

# Verification & Testing
1. **Connectivity**: Verify that roads correctly connect across tile boundaries by observing them in the Scene view while streaming.
2. **Terrain Alignment**: Verify that roads follow the terrain heights correctly.
3. **Building Spawning**: Verify that buildings spawn along the procedural roads via the `StreamedMapMagicCityBuilder`.
4. **AI Navigation**: Verify that zombies and squad members can pathfind using the procedural road network.
