# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG set in a zombie-infested world, featuring base building, third-person combat, and procedural world exploration.
- Players: Single player
- Inspiration / Reference Games: Project Zomboid, State of Decay, 7 Days to Die
- Tone / Art Direction: Realistic, gritty, post-apocalyptic
- Target Platform: PC (Windows)
- Screen Orientation / Resolution: Landscape 1920x1080
- Render Pipeline: Universal Render Pipeline (URP)

# Game Mechanics
## Custom World Generation (Redefined)
The world generation follows a tiered sequence where our own logic determines placement based on MapMagic's terrain data:
1. **MapMagic 2 (Foundation):** Generates terrain geometry, biomes, and masks (e.g., `TownMask`, `BuildabilityMask`).
2. **Custom Road Placement System (Logic):** Instead of using MapMagic splines, this system analyzes the `TownMask` to identify centers of population and calculates the best paths (roads) to connect them, considering terrain cost (slopes, water).
3. **EasyRoads3D Pro (Physical):** Takes the calculated paths and generates the physical road meshes, flattens the terrain, and handles intersections.
4. **Streamed City Builder (Buildings):** Places buildings along the EasyRoads3D network using the existing plot system.

# UI
- **HUD:** Displays survival stats and navigation.
- **Main Menu:** World seed selection and generation start.
- **Loading Screen:** Feedback during procedural world setup.

# Key Asset & Context
- **MapMagicObject:** Provides the raw height and mask data.
- **RoadNetworkSettings:** Configuration for road widths, materials, and generation cost weights (e.g., "Avoid steep slopes").
- **CustomRoadPlacementSystem (New):** The core logic for deciding *where* roads go.
- **ProceduralRoadSystem:** The tile-based worker that instantiates the roads using EasyRoads3D.

# Implementation Steps

## 1. Implement Town Candidate Resolver
Create a utility to sample MapMagic masks across the world grid to find clusters of "Town" potential.
- **File:** `Assets/Scripts/World/Roads/TownCandidateResolver.cs` (New)
- **Logic:** Iterate through world coordinates (using a coarse grid) and sample the `TownMask`. Store the highest-value clusters as "Town Centers".

## 2. Implement Seed-Based Road Graph Generator
Implement a deterministic generator that connects Town Centers into a graph.
- **File:** `Assets/Scripts/World/Roads/CustomRoadPlacementSystem.cs` (New)
- **Logic:** 
  - Retrieve Town Centers from Step 1.
  - Connect them using a Minimum Spanning Tree (MST) or a Delaunay Triangulation to form a network.
  - Apply pathfinding (e.g., A*) between nodes, where the "cost" is increased by terrain slope and "liquid" biomes.

## 3. Update RoadNetworkSettings for Custom Logic
Add parameters to control the custom placement logic.
- **File:** `Assets/Scripts/World/Roads/RoadNetworkSettings.cs`
- **Change:** Add `townMaskName`, `slopeCostWeight`, `minTownDistance`, and `pathfindingResolution`.

## 4. Integrate Custom Placement into ProceduralRoadSystem
Modify the `ProceduralRoadSystem` to use the new `CustomRoadPlacementSystem`.
- **File:** `Assets/Scripts/World/Roads/ProceduralRoadSystem.cs`
- **Logic:** 
  - Instead of `ProceduralRoadGenerator.Generate` (the ring/spoke model), call `CustomRoadPlacementSystem.GetRoadsInRect(tileRect)`.
  - The system will provide the pre-calculated, terrain-aware road segments.
  - Feed these segments into EasyRoads3D as before.

## 5. Local Road Grid Generation (Town Expansion)
Add logic to generate local street grids (small residential roads) within the radius of identified Town Centers.
- **Logic:** Within a town's influence area, generate a grid-like or organic sub-network that connects to the main Arterial roads.

## 6. Verification of Terrain Flattening & Intersections
Ensure that as custom roads are placed, EasyRoads3D correctly flattens the MapMagic terrain and generates junctions.
- **Logic:** Use `ERRoad.SnapToTerrain(true)` and ensure the procedural junction logic (T/X Crossings) is correctly triggered for the custom graph nodes.

# Verification & Testing
1. **Debug Visuals:** Implement Gizmo drawing in `CustomRoadPlacementSystem` to show Town Centers and the calculated Graph in the Scene view.
2. **Consistency Test:** Verify that using the same seed produces the same road network across multiple runs.
3. **Slope Test:** Verify that roads favor flatter areas and "hill climb" realistically rather than going straight up cliffs.
4. **Town Spawning:** Verify that `StreamedMapMagicCityBuilder` picks up the new roads and spawns buildings correctly along them.
