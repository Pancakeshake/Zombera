# Project Overview
- Game Title: Zombera
- High-Level Concept: Fixing redundant nodes and floating markers in the procedural road network.
- Goal: Align with "ER Modular Base Script" logic by allowing EasyRoads3D to handle snapping during the Build phase, and eliminating redundant connections.

# Implementation Steps

## 1. Consolidate Connection Logic
Remove the redundant `ConnectRoadMarkersAtSharedPoints` method which was creating overlapping connections (Flex Connectors on top of Junctions).
- **Files**: `ProceduralRoadSystem.cs`
- **Action**: Delete the method and its call in `HandleTileApplied`.

## 2. Enhance Junction Port Selection
Use the incoming road direction to select the most appropriate connection port. This prevents roads from "twisting" into the wrong side of a junction.
- **Files**: `ProceduralRoadSystem.cs`
- **Action**: Update `CreateJunctionsInTile` to calculate a `directionPoint` (the second-to-last or second marker) and pass it to `FindConnectionPortIndex`.

## 3. Improve Junction Positioning
Calculate the junction center as the average of all markers in the bucket, rather than just the first one.
- **Files**: `ProceduralRoadSystem.cs`
- **Action**: Sum and divide marker positions in `CreateJunctionsInTile`.

## 4. Enable EasyRoads Snapping
Align with the "Modular Base" logic by enabling automatic terrain snapping on all generated roads.
- **Files**: `ProceduralRoadSystem.cs`
- **Action**: Call `road.SnapToTerrain(true)` immediately after `road.SetWidth`.

## 5. Simplify Degree 2 Stitching
Use the high-level `ERRoadNetwork.ConnectRoads` for stitching segments (Degree 2) across tiles instead of manually instantiating Flex Connectors.
- **Files**: `ProceduralRoadSystem.cs`
- **Action**: Update the Degree 2 block in `CreateJunctionsInTile`.

# Verification & Testing
1. **Node Visual Check**: Verify in the Editor that only ONE junction handle exists at crossings (no overlapping markers).
2. **Height Check**: Ensure roads are correctly snapped to the terrain without gaps ("floating").
3. **Port Check**: Verify that roads enter junctions from the correct side without "U-turns" or twists.
4. **Diagnostics**: Ensure `Diagnostics` still report correct creation counts.
