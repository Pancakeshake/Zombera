# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with procedural world generation.
- Implementation Goal: Transition from fully procedural building placement to a "Premade Town" system where handcrafted town prefabs are spawned at major road intersections (spline nodes) and connected by EasyRoads.

# Game Mechanics
- Procedural roads generated from MapMagic splines.
- Handcrafted town prefabs spawned at high-valence spline nodes.
- EasyRoads connecting town entrances to the global road network.

# Key Asset & Context
- `Assets/Scripts/World/Roads/RoadNetworkRuntime.cs`: Stores the global road plan.
- `Assets/Scripts/World/Roads/ProceduralRoadGenerator.cs`: Generates the global road plan.
- `Assets/Scripts/World/Roads/ProceduralRoadSystem.cs`: Builds EasyRoads meshes per tile.
- `Assets/Scripts/World/City/StreamedMapMagicCityBuilder.cs`: Spawns individual buildings (to be inhibited inside towns).
- `Assets/Scripts/World/WorldGenerationManager.cs`: Orchestrates the generation pipeline.

# Implementation Steps

## 1. Data Model Updates
- **Update `RoadNetworkRuntime.cs`**:
    - Define `TownNode` class with `Position`, `Type` (Enum: Hamlet, Village, Town, City), and `Radius`.
    - Add `List<TownNode> TownNodes` to `RoadNetworkRuntime`.
- **Update `RoadTypes.cs`**:
    - Add `TownType` enum.

## 2. Plan Generation (MapMagic Splines to Global Plan)
- **Modify `ProceduralRoadGenerator.cs`**:
    - Implement `GenerateFromMapMagic(MapMagicObject mapMagic, RoadNetworkSettings settings)`.
    - Extract splines from MapMagic's internal products.
    - Analyze spline connectivity: nodes with degree > 2 are marked as `TownNode` candidates.
    - Filter candidates based on `townAreaMaskName` from settings.
- **Update `ProceduralRoadSystem.cs`**:
    - In `Awake` or `Start`, if `settings.useMapMagicSplineOutput` is true, call the new MapMagic generator.

## 3. Town Spawning System
- **Create `TownSpawner.cs`**:
    - Component to be placed on the `WorldManager` object.
    - Listens for `MapMagicTileStreamBridge.TileAppliedForGameplay`.
    - Checks the `RoadNetworkRuntime.TownNodes` for any nodes within the tile's bounds.
    - Spawns a town prefab (selected by `TownType`) at the node's position.
- **Update `WorldGenerationManager.cs`**:
    - Add a `Stage 2.5: Town Spawning`.
    - Ensure it runs after MapMagic but before (or during) Road generation.

## 4. Road Entrance Snapping
- **Town Prefab Convention**:
    - Town prefabs should contain children named `RoadEntrance_N`, `RoadEntrance_S`, etc., with an `ERMarkerSnap` component or just a specific tag.
- **Modify `ProceduralRoadSystem.cs`**:
    - In `GenerateRoadsForTile`, before creating an `ERRoad`, check if its endpoints are within a small radius of a `RoadEntrance` in the tile.
    - If a match is found, snap the road marker to the entrance's position and rotation.

## 5. Building Inhibition
- **Modify `StreamedMapMagicCityBuilder.cs`**:
    - Add a check in `TrySpawnOnLot` to see if the lot center is within the radius of any `TownNode`.
    - If so, skip spawning the individual building.

# Verification & Testing
1. **Spline Analysis**: Verify that `ProceduralRoadGenerator` correctly identifies intersections as `TownNode` candidates in the logs.
2. **Town Spawning**: Confirm town prefabs spawn at the correct locations in `WorldGenTest.unity`.
3. **Connection**: Check that EasyRoads meshes start/end exactly at the town entrances.
4. **Inhibition**: Ensure no procedural buildings are overlapping with the premade town structures.
