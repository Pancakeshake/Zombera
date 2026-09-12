# Project Overview
- Goal: Implement terrain-aware road winding, ensure cities spawn only in flat areas, and improve terrain smoothing under roads.

# Problem Analysis
1. **Wall-like Roads**: The current road segments are too long and don't adjust their path to the terrain, resulting in "straight-line" roads that ignore slopes.
2. **Mountain Cities**: City grids are generated purely by math without checking if the terrain is suitable for buildings.
3. **Terrain Integration**: The terrain under roads needs to be flattened and smoothed to avoid sharp drops or "wall" effects at the road edges.

# Proposed Design

## 1. High-Resolution Pathing
- **Generator Update**: Subdivide all global road segments (Ring, Spokes, Grids) into smaller segments (e.g., every 15-20 meters). This gives the system more "joints" to bend around mountains.
- **Organic Jitter**: Add a deterministic noise layer to the global paths to make them look less like perfect geometric shapes.

## 2. Terrain-Aware Refinement (Runtime)
- **Point Nudging**: When a tile is processed, the system will sample the terrain around each road point. If a point is on a steep slope, it will be "nudged" toward the nearest flatter or lower area.
- **Vertical Smoothing**: Apply a Laplacian smoothing pass to the points' Y-coordinates to ensure the road's grade changes smoothly, avoiding sharp vertical bumps.
- **EasyRoads Deformation**: Explicitly enable EasyRoads' terrain deformation to flatten the roadbed and create smooth shoulders.

## 3. Flatland City Filter
- **Slope Validation**: Before spawning a "Local" (City) road, calculate the average slope along its segment. 
- **Thresholding**: If the slope exceeds 12-15 degrees, the segment is discarded or downgraded to a "Dirt Track" to prevent cities from being built on cliffs.

# Implementation Steps

## Step 1: Enhance ProceduralRoadGenerator
- **File**: `Assets/Scripts/World/Roads/ProceduralRoadGenerator.cs`
- **Action**: Update the generation loops to subdivide segments. Add a `Subdivide` helper method.

## Step 2: Implement Refinement in ProceduralRoadSystem
- **File**: `Assets/Scripts/World/Roads/ProceduralRoadSystem.cs`
- **Action**: Add `RefineRoadPoints` and `CalculateAverageSlope` methods.
- **Action**: Update `HandleTileApplied` to call these before spawning `ERRoad`.
- **Action**: Call `road.SetTerrainDeformation(true)` and configure shoulder widths.

## Step 3: Update Settings
- **File**: `Assets/Scripts/World/Roads/RoadNetworkSettings.cs`
- **Action**: Add serialized fields for `maxCitySlope`, `refinementStrength`, and `smoothingIterations`.

# Verification & Testing
1. **Visual Pathing**: Observe in Scene View that roads "snake" through valleys instead of going over peaks.
2. **City Layout**: Verify that the city grid is denser in flat areas and "breaks" where the terrain becomes too steep.
3. **Terrain Quality**: Use the Scene View to verify the terrain under the road is flat and blends into the surrounding area without "wall" artifacts.
