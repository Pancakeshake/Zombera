# Project Overview
- Goal: Fix duplicate road spawning via point clipping, implement advanced valley-seeking path refinement, and integrate road generation progress into the loading bar.

# Problem Analysis
1. **Duplicate Road Bug**: Tiles with overlapping bounds currently spawn the entire global road segment, leading to N perfectly overlapping road meshes.
2. **Lag in Gameplay**: Road and NavMesh generation occur during movement, causing spikes.
3. **Terrain Flow**: Roads are still creating vertical terrain artifacts due to insufficient path adaptation and shoulder blending.

# Proposed Design

## 1. Local Segment Clipping (Anti-Duplicate Logic)
- **Bounding-Box Intersection**: Before processing a global road, check if its segments pass through the current tile's `WorldRect`.
- **Precise Point Extraction**: I will implement a segment-clipping algorithm to extract only the polyline segments that are inside the tile (plus a 5% margin to ensure mesh connectivity).
- **Unique Segments**: This ensures each physical part of a road is owned by exactly one tile, eliminating duplicate meshes and Z-fighting.

## 2. Gradient-Descent Path Refinement (Winding)
- **Valley Seeking**: For every road point, the system will perform 3 passes of height sampling in a 20m radius. It will "slide" points toward local height minima, causing roads to naturally wind through valleys.
- **Curvature Constraint**: To prevent the road from becoming too "wiggly," a Laplacian smoothing pass will follow the height nudge to maintain a drivable radius.
- **Vertical Grading**: The vertical smoothing pass will be increased to 8 iterations to ensure the grade changes are invisible to the player.

## 3. Loading Phase Synchronization
- **Road Stage Readiness**: 
    - `ProceduralRoadSystem` will count the number of tiles that have completed their road generation.
    - `WorldManager` will be updated to track the "initial loading volume" of roads. 
    - The loading screen will remain active until the roads (and subsequent NavMesh) for the player's vicinity are fully built and baked.
- **Order Enforcement**: Terrain -> Road -> NavMesh sequence will be strictly enforced via the new `RoadsApplied` event.

# Implementation Steps

## Step 1: Implement Polyline Clipping in ProceduralRoadSystem.cs
- Add `List<List<Vector3>> ExtractClippedSegments(RoadPolyline poly, Rect rect)` method.
- Update `HandleTileApplied` to iterate over extracted segments instead of the whole polyline.

## Step 2: Implement Physics-Lite Path Relaxation
- Enhance `RefineRoadPoints` with "Gradient Descent" logic (sampling height around points and nudging towards lower ground).
- Increase Laplacian vertical smoothing iterations.

## Step 3: Link ProceduralRoadSystem to Loading Sequence
- Add `ProcessedInitialTileCount` to `ProceduralRoadSystem`.
- Update `WorldManager.IsRoadStageReady` to check the progress of the new system.
- Update `GameManager` to provide road-specific status strings to the loading screen.

## Step 4: Tune Settings
- Update `RoadNetworkSettings` default values: `terrainShoulderMeters = 8f`, `terrainRefinementStrength = 0.65f`.

# Verification & Testing
1. **Visual Profile**: Fly near a mountain; roads should curve around the base instead of climbing the peak.
2. **Inspector Audit**: Verify that `ERRoad` object counts per tile are low and localized to the tile footprint.
3. **Loading Sequence**: Check the loading bar; it should show "Generating Roads" followed by "Baking NavMesh" before entering the game.
