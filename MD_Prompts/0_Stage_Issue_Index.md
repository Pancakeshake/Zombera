# Build pipeline stage issue logs

Per-stage living logs of issues, wrong paths, and broken behavior.
Registry order matches [`WorldBuildStageRegistry`](../Assets/01_Game/02_World/CityPipeline/WorldBuilder/Core/WorldBuildStageRegistry.cs).
Parent reference: [`Build_Pipeline_Reference_Prompt.md`](Build_Pipeline_Reference_Prompt.md).

| # | File | StageId | Section |
|---|------|---------|---------|
| 1 | [1_Reset.md](1_Reset.md) | `ResetGeneratedWorld` | Setup |
| 2 | [2_EnsureStack.md](2_EnsureStack.md) | `EnsureWorldBuilderStack` | Setup |
| 3 | [3_ValidateProfile.md](3_ValidateProfile.md) | `ValidateWorldProfile` | Setup |
| 4 | [4_CreatePlan.md](4_CreatePlan.md) | `CreateGlobalWorldPlan` | Setup |
| 5 | [5_AllocateTerrain.md](5_AllocateTerrain.md) | `AllocateTerrainGrid` | Terrain |
| 6 | [6_BaseLandforms.md](6_BaseLandforms.md) | `GenerateBaseLandforms` | Planning Field |
| 6b | [15b_ReserveCityPads.md](15b_ReserveCityPads.md) | `ReserveCityPads` | Planning Field |
| 7 | [7_ErodeLandforms.md](7_ErodeLandforms.md) | `ErodeLandforms` | Planning Field |
| 8 | [8_SolveHydrology.md](8_SolveHydrology.md) | `SolveHydrology` | Planning Field |
| 9 | [9_CarveWater.md](9_CarveWater.md) | `CarveWaterFeatures` | Planning Field |
| 10 | [10_ClassifyBiomes.md](10_ClassifyBiomes.md) | `ClassifyBiomesAndBuildability` | Planning Field |
| 11 | [22_SkyWeather.md](22_SkyWeather.md) | `ConfigureSkyAndWeather` | Environment |
| 12 | [23_InitEnvironment.md](23_InitEnvironment.md) | `InitializeEnvironmentState` | Environment |
| 13 | [24_BindWeather.md](24_BindWeather.md) | `BindWeatherConsumers` | Environment |
| 14 | [11_OceanSurfaces.md](11_OceanSurfaces.md) | `BuildOceanSurfaces` | Water |
| 15 | [12_WaterSurfaces.md](12_WaterSurfaces.md) | `BuildWaterSurfaces` | Water |
| 16 | [13_SelectSites.md](13_SelectSites.md) | `SelectCitySitesAndLandmarks` | Sites |
| 17 | [14_InterCityHighways.md](14_InterCityHighways.md) | `PlanInterCityHighways` | Sites |
| 18 | [15_ApplyCityPads.md](15_ApplyCityPads.md) | `ApplyCityPads` | Sites |
| 19 | [16_NamedAreas.md](16_NamedAreas.md) | `GenerateNamedAreas` | Layout |
| 20 | [17_Footpaths.md](17_Footpaths.md) | `GenerateCityFootpaths` | Layout |
| 21 | [18_DistrictLots.md](18_DistrictLots.md) | `GenerateDistrictLots` | Layout |
| 22 | [19_PaintSurfaces.md](19_PaintSurfaces.md) | `PaintNaturalSurfaces` | Surfaces |
| 23 | [25_PlanRoads.md](25_PlanRoads.md) | `PlanRoadsAndHighways` | Roads |
| 24 | [26_RefineRoads.md](26_RefineRoads.md) | `RefineRoadsAgainstTerrain` | Roads |
| 25 | [27_WaterCrossings.md](27_WaterCrossings.md) | `ResolveWaterCrossings` | Roads |
| 26 | [28_MountainTunnels.md](28_MountainTunnels.md) | `ResolveMountainTunnels` | Roads |
| 27 | [29_StampInfrastructure.md](29_StampInfrastructure.md) | `StampInfrastructureTerrain` | Roads |
| 28 | [30_RoadMeshes.md](30_RoadMeshes.md) | `BuildEasyRoadsMeshes` | Roads |
| 29 | [31_SyncInfraSurfaces.md](31_SyncInfraSurfaces.md) | `SyncInfrastructureSurfaces` | Roads |
| 30 | [20_WildernessPois.md](20_WildernessPois.md) | `PlaceWildernessPois` | Wilderness |
| 31 | [21_WildernessNature.md](21_WildernessNature.md) | `PlaceWildernessNature` | Wilderness |
| 32 | [32_PlaceBuildings.md](32_PlaceBuildings.md) | `PlaceBuildings` | City |
| 33 | [33_LotTerrain.md](33_LotTerrain.md) | `GenerateLotTerrain` | City |
| 34 | [34_Fences.md](34_Fences.md) | `GenerateFences` | City |
| 35 | [35_Parks.md](35_Parks.md) | `BuildParks` | City |
| 36 | [36_CityTrees.md](36_CityTrees.md) | `PlaceCityTrees` | City |
| 37 | [37_RoadMarkings.md](37_RoadMarkings.md) | `GenerateRoadMarkings` | StreetDressing |
| 38 | [38_TrafficLights.md](38_TrafficLights.md) | `GenerateTrafficLights` | StreetDressing |
| 39 | [39_StreetSigns.md](39_StreetSigns.md) | `GenerateStreetSigns` | StreetDressing |
| 40 | [40_StreetLamps.md](40_StreetLamps.md) | `GenerateStreetLamps` | StreetDressing |
| 41 | [41_PowerLines.md](41_PowerLines.md) | `GeneratePowerLines` | StreetDressing |
| 42 | [42_StreetFurniture.md](42_StreetFurniture.md) | `GenerateStreetFurniture` | StreetDressing |
| 43 | [43_ParkedCars.md](43_ParkedCars.md) | `GenerateParkedCars` | StreetDressing |
| 44 | [44_FinalizeTiles.md](44_FinalizeTiles.md) | `FinalizeTerrainTiles` | Finalize |
| 45 | [45_QueueNavMesh.md](45_QueueNavMesh.md) | `QueueNavMesh` | Finalize |
| 46 | [46_ValidatePublish.md](46_ValidatePublish.md) | `ValidateAndPublish` | Finalize |
