# Project Overview
- Game Title: Zombera
- Goal: Fix road spawning from MapMagic splines into EasyRoads3D.

# Problem Analysis
1.  **Spline Output Limitation**: The current `EasyRoadsMapMagicSync` script only processes the first `SplineOutput` it finds in a tile's data. If the graph uses multiple outputs or sub-graphs with separate outputs, most roads are ignored.
2.  **Visibility**: There is no logging when a tile is processed but found to have no road data, making it hard to diagnose why spawning fails.
3.  **Hierarchy**: Redundant bridges in the scene were disabled, but we need to ensure the remaining bridge is correctly receiving events.

# Proposed Changes

## 1. EasyRoadsMapMagicSync.cs
- **Iterate All Outputs**: Change `ApplyOfType` to a loop that iterates over all `SplineOutput200.ApplyData` entries in the `TileData`.
- **Exhaustive Logging**: Add `Debug.Log` calls for:
    - `HandleTileApplied` entry.
    - Total spline outputs found.
    - Number of lines in each spline output.
    - Reasons for skipping lines or points.
    - Successful road creation.
- **Null Safety**: Improve checks for `tile.ActiveTerrain` and `_roadNetwork`.

## 2. MapMagic Graph Check
- Verify that the `Spline Output` nodes in sub-graphs (like `Badlands`) are properly connected to the graph's execution flow.

# Implementation Steps

## Step 1: Update EasyRoadsMapMagicSync.cs
- **File**: `Assets/Scripts/World/Roads/EasyRoadsMapMagicSync.cs`
- **Changes**:
    - Update `HandleTileApplied` to log its execution.
    - Use a loop to process all `SplineOutput200` data.
    - Add detailed logging for spline counts and line counts.

## Step 2: Verification
- Enter Play Mode and check console for `[EasyRoadsMapMagicSync]` logs.
- If "0 Spline Outputs found" appears, we know the issue is in the MapMagic graph configuration.
- If "Created road" appears but nothing is visible, we check the `points` values and EasyRoads road types.

# Verification & Testing
1.  **Console Logs**: Verify tile applied events are received.
2.  **Visual Check**: Verify road meshes appear on the terrain in the Scene view.
3.  **AI Check**: Verify squad members can use the roads (navmesh check).
