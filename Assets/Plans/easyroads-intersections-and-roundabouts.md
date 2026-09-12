# Project Overview
- Goal: Fix road joins using EasyRoads3D intersections and roundabouts. Normalize city heights and prevent building overlap.

# Problem Analysis
1. **Broken Joins**: Roads just overlap at intersections instead of forming a unified mesh.
2. **Uneven Intersections**: City roads cross at different heights.
3. **Building Clipping**: Buildings still spawn too close to roads.

# Proposed Design

## 1. EasyRoads Runtime Intersections
- **Connection API**: Use `ERRoadNetwork.InstantiateConnection` to spawn intersection prefabs (X, T, Roundabout) at snapped marker points.
- **Node Joining**: When two road markers snap, instead of just sharing a coordinate, they will both connect to the same `ERConnection` object.
- **Roundabouts**: Detect when two `Arterial` or `Highway` segments meet at an angle and spawn a roundabout prefab if available.

## 2. City Height Normalization (Perfectly Flat Junctions)
- **Intersection Plane**: When a junction is created, all connecting markers will have their Y-coordinate snapped to the junction's center height.
- **Plateau Stamp**: The `RoadTerrainStamper` will ensure the area around junctions is flattened.

## 3. Dynamic Building Setbacks
- **Width-Aware Padding**: Buildings in `StreamedMapMagicCityBuilder` will calculate setback as `(RoadWidth / 2) + Max(3m, RoadShoulder)`.

# Implementation Steps

## Step 1: Refactor ProceduralRoadSystem.cs
- Add `ERConnection` tracking.
- Search for "Roundabout", "X Crossing", "T Crossing" in `_roadNetwork.GetConnectionPrefabs()`.
- Implement `CreateJunctionAtPoint(Vector3 position, List<ERRoad> connectingRoads)`.

## Step 2: Update Intersection Logic in HandleTileApplied
- After clipping segments, check `_markerCache` for shared points.
- Trigger junction creation for points with 3+ roads.

## Step 3: Refine Building Spawner
- Update `SpawnLotsAlongRoad` in `StreamedMapMagicCityBuilder.cs` to use the new setback formula.

# Verification & Testing
1. **Visual Mesh**: Verify intersections have smooth mesh transitions (no Z-fighting).
2. **Roundabouts**: Verify roundabouts appear at major highway junctions.
3. **Flatness**: Verify city crossroads are 100% level.
