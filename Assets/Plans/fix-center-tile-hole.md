# Project Overview 
- Game Title: Zombera
- Problem: The center tile (0,0) appears blank in high-altitude overviews (y=2000).
- Root Cause: MapMagic 2 uses 3D distance for LOD switching. At height 2000 (with 1000m tiles), the center tile is considered "1.0 to 2.0 tiles away". With `mainRange = 1`, MapMagic disables high-resolution "Main Terrain" for the center tile and only keeps "Draft Terrain" active. Since `draftsInPlaymode` was false, the draft terrain was also hidden, leaving a blank hole.

# Implementation Steps

## 1. Adjust MapMagic Streaming Ranges
- **Modify `Assets/Scripts/World/WorldManager.cs`**:
    - Increase `mapMagic.mainRange` to **5**. This allows tiles to remain at high-resolution even when viewed from a high-altitude camera.
    - Set `mapMagic.draftsInPlaymode` to **true**. This ensures that terrain is visible immediately at low resolution while high resolution processes, preventing "blank holes".
    - In overview mode (`radius == 0`), explicitly set `genAroundCoordinates = true` and add `Coord(0,0)` to the pinned list. This ensures the center is always the generation priority.

## 2. Restore Camera Far Clip
- **Scene Calibration**:
    - Ensure the Main Camera in `1_World_Generation` has a Far Clip Plane of at least 5000 to see the terrain below. (Currently at 2000, which is exactly the camera height—might clip the floor).

# Verification & Testing
1. **Runtime Audit**: Run the scene and verify that `Tile 0,0` now has `Main Terrain` active in the hierarchy.
2. **Visual Check**: Confirm the center tile is no longer blank and shows full detail.
3. **Distance Check**: Verify that `distance` for tile (0,0) is less than `mainRange`.
