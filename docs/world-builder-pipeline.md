# WorldBuilder pipeline chart (AI reference)

**SoT (DAG):** `Assets/01_Game/02_World/CityPipeline/WorldBuilder/Core/WorldBuildStageRegistry.cs` (`BuildDefaultDescriptors`).  
Trust the registry for order, prereqs, outputs, Invalidates. This file is routing only.

## Agent flow → STOP

1. Read invariants + ownership below.  
2. Use the task table → open ≤3–4 files (SigMap before folder walks).  
3. Need deps/order? Open the **registry** — never invent stage order from this doc.  
4. **STOP.**

Unlisted task → registry (+ SigMap) → STOP.

## Invariants

- Pads / layout / biomes before natural paint; Reserve ≠ Apply (aprons vs cores — read those stages).  
- Stamp / tunnel holes before procedural road meshes (`CityPrefabRoadNetworkBuilder` under `CityPipeline/Roads/`).  
- Dependency edges = registry `HardPrerequisites` (not enum order, not `Invalidates` — Assets never call `Runner.Invalidate`).  
- Prefer `WorldTileStreamSource` / `IWorldTileGameplayEvents`; do not wire MapMagic `TerrainTile` events for WB.  
- No world gen in `MainMenu`.

## Ownership

| Owner | Responsibility |
|-------|----------------|
| `WorldBuilderService` | Facade, session, artifacts |
| `WorldBuildPipelineRunner` | Stage execution + hard prereqs |
| `WorldBuildStageRegistry` | DAG truth |
| `Stages/*` | Thin orchestration only |
| `Terrain/` `Hydrology/` `Roads/` (WB) | Landforms, pads, hydro, highways, tunnels |
| `CityPrefabRoadNetworkBuilder` | City graph, lots/buildings/street dress, road meshes |
| `Surfaces/` | Alphamap / MicroSplat (`WorldSurfacePainter*`) |
| `Sites/` `Content/` `Profiles/` | Sites, nature/POI/water, profiles |
| Editor | Provisioner, hub, `CityPipelineRunnerWindow` |

Change the **owner**; don’t thicken stages.

## Task → open → STOP

Paths under `…/CityPipeline/WorldBuilder/` unless noted.

| Task | Open | Optional second |
|------|------|-----------------|
| Order / prereqs / outputs | `Core/WorldBuildStageRegistry.cs` | — |
| Reserve / apply pads | `Stages/ReserveCityPadsStage.cs` or `ApplyCityPadsStage.cs` | `Terrain/CityPad*` |
| Natural paint | `Stages/PaintNaturalSurfacesStage.cs` | `Surfaces/WorldSurfacePainter.cs` |
| Hydro / carve | `Stages/SolveHydrologyStage.cs` | `Stages/CarveWaterFeaturesStage.cs` |
| Tunnels / highways | `Stages/ResolveMountainTunnelsStage.cs` | `Stages/PlanInterCityHighwaysStage.cs` |
| Road meshes | `Stages/BuildEasyRoadsMeshesStage.cs` | `../Roads/Scripts/CityPrefabRoadNetworkBuilder.cs` |
| Wilderness | `Stages/PlaceWildernessNatureStage.cs` | SigMap → `WorldNaturePlacer` |
| Layout / lots | `Stages/GenerateDistrictLotsStage.cs` | `Stages/GenerateNamedAreasStage.cs` |
| Runtime / hub run | `WorldBuilderService.RuntimeSession.cs` | `Assets/Editor/CityPipeline/CityPipelineRunnerWindow.Steps.cs` |
| Stack / profile | `Core/WorldBuilderStackBootstrap.cs` | `Profiles/WorldGenerationProfile.cs` |

**STOP.**
