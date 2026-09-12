# Project Overview 
- Problem: The center tile (0,0) is missing/blank in high-altitude overviews, while neighbors (roads/structures) appear.
- Analysis: 
    1.  **LOD Conflict**: MapMagic 2 uses 3D distance for LOD switching. At height 2000, the distance to (0,0) is $\ge 1$, which is the threshold for Draft resolution.
    2.  **Preview Hijacking**: The center tile (0,0) is currently flagged as `preview=True` at runtime, which often excludes it from standard "All Complete" signals and high-resolution application in certain MapMagic configurations.
    3.  **Draft Suppression**: `draftsInPlaymode` was disabled, so the Draft terrain was hidden, leaving a hole.

# Implementation Steps

## 1. Force Production Generation State
- **Modify `Assets/Scripts/World/WorldManager.cs`**:
    - Increase `mapMagic.mainRange` to **5**. This forces a 11x11 tile area (including the 3x3 view) to stay in high-resolution "Main" mode, even when viewed from height 2000.
    - Set `mapMagic.draftsInPlaymode = true`. This ensures terrain is always visible, even if high-res is still calculating.
    - Explicitly call `mapMagic.ClearPreviewTile()` during session start to un-hijack the center tile.

## 2. Explicitly Pin 3x3 Grid
- In `ApplyMapMagicProceduralSessionStreaming`, if `radius == 0` (Overview mode):
    - Explicitly set `tiles.genAroundCoordinates = true`.
    - Add `Coord(0,0)` and its immediate 8 neighbors to `tiles.genCoordinates`.
    - This ensures MapMagic attempts to generate the entire 3x3 block regardless of where it thinks the camera "focus" is at high altitude.

# Verification & Testing
1. **Hierarchy Check**: Run the scene and verify that `Tile 0,0` now has `Main Terrain` child **Active**.
2. **Metric Check**: Verify `MapMagicTileAppliedEvents` reaches at least 9 (the full 3x3 grid).
3. **Visual Check**: Confirm the center tile is no longer a hole.
