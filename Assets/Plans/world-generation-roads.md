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
## World Generation (Step 1 & 2 Focus)
The world is procedurally generated using a tiered approach:
1. **MapMagic 2:** Responsible for the macro-scale terrain, heightmaps, biomes (forests, badlands, etc.), and masks (town candidate areas).
2. **EasyRoads3D Pro:** Responsible for building the physical road network on top of the MapMagic terrain. It handles road meshes, intersections, and terrain flattening.
3. **Easy Build System:** (Future Step) Modular buildings placed alongside the road network.
4. **Gameplay Road Service:** Bridges the procedural geometry into gameplay data for AI pathing, scavenging, and lot placement.

## Core Gameplay Loop
The player explores a vast procedural world, scavenging resources from towns generated alongside roads, defending against zombies, and building bases to survive.

# UI
- **HUD:** Displays health, stamina, and quick-slots.
- **Main Menu:** Handles game settings and world initialization.
- **Loading Screen:** Asynchronous world generation feedback.

# Key Asset & Context
- **MapMagicObject:** The core terrain engine.
- **ProceduralRoadSystem:** Runtime manager that converts road path data into EasyRoads3D objects.
- **RoadNetworkSettings:** Configuration for road widths, materials, and generation flags.
- **RoadGameplayService:** Authoritative source for road sample points used by other systems.
- **EasyRoads3D API (ERRoadNetwork):** Used to programmatically create roads and junctions.

# Implementation Steps

## 1. Refine RoadNetworkSettings Configuration
Update the `RoadNetworkSettings` to support the hybrid MapMagic + EasyRoads3D workflow.
- **File:** `Assets/Scripts/World/Roads/RoadNetworkSettings.cs`
- **Change:** Ensure `useMapMagicSplineOutput` and `applyTerrainDeformationAndPaint` are correctly prioritized for the procedural system. Add any missing tuning parameters for spline sampling density.

## 2. Implement MapMagic Spline Extraction in ProceduralRoadSystem
Enable `ProceduralRoadSystem` to pull spline data directly from MapMagic's tile generation payload.
- **File:** `Assets/Scripts/World/Roads/ProceduralRoadSystem.cs`
- **Dependencies:** MapMagic 2 `TileData` and `SplineOutput200`.
- **Logic:**
  - In `HandleTileApplied`, check if `settings.useMapMagicSplineOutput` is true.
  - Extract `SplineSys` from `data.ApplyOfType<SplineOutput200.ApplyData>()`.
  - Convert `SplineSys` lines into `RoadPolyline` objects (world-space).
  - Use these as the source for `ERRoad` creation instead of the hardcoded global ring/spokes.

## 3. Update ProceduralRoadSystem to Use EasyRoads3D for Spline Roads
Modify the tile processing logic to handle MapMagic-sourced roads.
- **File:** `Assets/Scripts/World/Roads/ProceduralRoadSystem.cs`
- **Logic:**
  - Clip extracted MapMagic splines to the current tile (with margin).
  - Call `_roadNetwork.CreateRoad` for each clipped segment.
  - Enable `road.SnapToTerrain(true)` and ensure EasyRoads3D's terrain flattening is triggered.
  - Register junctions where splines overlap or end near each other.

## 4. Integrate "Flat-ish Town Candidate Area" Mask (Foundation for Step 3)
Add support for identifying town areas to guide road and future building placement.
- **MapMagic Graph:** Configure a "Town Mask" output (using slope and biome nodes).
- **ProceduralRoadSystem:** Optionally query this mask to decide road class (e.g., Arterial vs Local).
- **RoadGameplayService:** Ensure `RoadSamplePoint.roadsideLotEligible` is set correctly based on proximity to these areas.

## 5. Verify Road Network & Terrain Sync
Ensure the transition between MapMagic terrain generation and EasyRoads3D road application is seamless.
- **Logic:** Verify that `ProceduralRoadSystem` waits for the `TileAppliedForGameplay` event (which ensures heights are final) before building roads.
- **Logic:** Verify that EasyRoads3D successfully flattens the terrain under the roads.

# Verification & Testing
1. **Scene Test:** Open `Assets/Scenes/Testing/WorldRoad_Generation_Testing.unity`.
2. **Editor Validation:** Ensure `ProceduralRoadSystem` resolves EasyRoads3D connection prefabs (T/X Crossings).
3. **Play Mode Test:**
   - Observe MapMagic generating terrain tiles.
   - Verify EasyRoads3D roads appear along the paths defined in the MapMagic graph (or global fallback).
   - Check the Unity Console for `[ProceduralRoadSystem]` diagnostics regarding road and junction creation.
   - Visually inspect road-terrain blending and flattening.
4. **Roadside Lot Check:** Verify `RoadGameplayService` gizmos show valid roadside lot samples (green spheres) along the newly created roads.
