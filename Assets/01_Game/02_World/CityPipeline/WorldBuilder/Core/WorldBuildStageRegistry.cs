using System;
using System.Collections.Generic;
using Zombera.World.CityPipeline.WorldBuilder.Stages;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Canonical stage descriptors and stage factory for the world-build pipeline.</summary>
    public sealed class WorldBuildStageRegistry
    {
        private static readonly WorldBuildScopeKind[] AllScopes =
        {
            WorldBuildScopeKind.FullMap,
            WorldBuildScopeKind.InitialPlayArea,
            WorldBuildScopeKind.Bounds,
            WorldBuildScopeKind.TileSet
        };

        private static readonly WorldBuildScopeKind[] FullMapOnly =
        {
            WorldBuildScopeKind.FullMap
        };

        private static WorldBuildStageRegistry _default;

        private readonly Dictionary<WorldBuildStageId, WorldBuildStageDescriptor> _byId;
        private readonly Dictionary<WorldBuildStageId, Func<IWorldBuildStage>> _factories;

        public IReadOnlyList<WorldBuildStageDescriptor> Stages { get; }

        private WorldBuildStageRegistry(
            IReadOnlyList<WorldBuildStageDescriptor> stages,
            Dictionary<WorldBuildStageId, Func<IWorldBuildStage>> factories)
        {
            Stages = stages;
            _byId = new Dictionary<WorldBuildStageId, WorldBuildStageDescriptor>(stages.Count);
            for (var i = 0; i < stages.Count; i++)
                _byId[stages[i].Id] = stages[i];
            _factories = factories;
        }

        public static WorldBuildStageRegistry Default => _default ??= CreateDefault();

        public static WorldBuildStageRegistry CreateDefault()
        {
            var stages = BuildDefaultDescriptors();
            var factories = BuildDefaultFactories();
            return new WorldBuildStageRegistry(stages, factories);
        }

        public WorldBuildStageDescriptor GetDescriptor(WorldBuildStageId id)
        {
            if (_byId.TryGetValue(id, out var descriptor))
                return descriptor;
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown world-build stage.");
        }

        public static WorldBuildStageDescriptor Describe(WorldBuildStageId id) =>
            Default.GetDescriptor(id);

        public IWorldBuildStage CreateStage(WorldBuildStageId id)
        {
            if (_factories.TryGetValue(id, out var factory))
                return factory();
            throw new ArgumentOutOfRangeException(nameof(id), id, "No factory for world-build stage.");
        }

        /// <summary>Flags + artifact metadata packed for <see cref="StageDescriptorArgs"/>.</summary>
        private readonly struct StageDescriptorOptions
        {
            public readonly bool GlobalArtifact;
            public readonly bool CanCancel;
            public readonly string[] Outputs;
            public readonly WorldBuildStageId[] Invalidates;
            public readonly WorldBuildScopeKind[] Scopes;

            public StageDescriptorOptions(
                bool globalArtifact,
                bool canCancel,
                string[] outputs,
                WorldBuildStageId[] invalidates,
                WorldBuildScopeKind[] scopes = null)
            {
                GlobalArtifact = globalArtifact;
                CanCancel = canCancel;
                Outputs = outputs;
                Invalidates = invalidates;
                Scopes = scopes;
            }
        }

        /// <summary>Parameter object for <see cref="D"/> (Sonar S107).</summary>
        private readonly struct StageDescriptorArgs
        {
            public readonly WorldBuildStageId Id;
            public readonly string Section;
            public readonly string DisplayName;
            public readonly WorldBuildStageId[] Prereqs;
            public readonly StageDescriptorOptions Options;

            public StageDescriptorArgs(
                WorldBuildStageId id,
                string section,
                string displayName,
                WorldBuildStageId[] prereqs,
                StageDescriptorOptions options)
            {
                Id = id;
                Section = section;
                DisplayName = displayName;
                Prereqs = prereqs;
                Options = options;
            }
        }

        private static WorldBuildStageDescriptor D(
            WorldBuildStageId id,
            string section,
            string displayName,
            WorldBuildStageId[] prereqs,
            StageDescriptorOptions options)
            => D(new StageDescriptorArgs(id, section, displayName, prereqs, options));

        private static WorldBuildStageDescriptor D(in StageDescriptorArgs args)
        {
            var options = args.Options;
            return new WorldBuildStageDescriptor(
                args.Id,
                args.Section,
                args.DisplayName,
                args.Prereqs ?? Array.Empty<WorldBuildStageId>(),
                options.Scopes ?? AllScopes,
                options.GlobalArtifact,
                options.CanCancel,
                options.Outputs ?? Array.Empty<string>(),
                options.Invalidates ?? Array.Empty<WorldBuildStageId>());
        }

        private static IReadOnlyList<WorldBuildStageDescriptor> BuildDefaultDescriptors()
        {
            var none = Array.Empty<WorldBuildStageId>();
            return new WorldBuildStageDescriptor[]
            {
                D(WorldBuildStageId.ResetGeneratedWorld, "Setup", "Reset Generated World",
                    none, new StageDescriptorOptions(false, true, Array.Empty<string>(), none, FullMapOnly)),
                D(WorldBuildStageId.EnsureWorldBuilderStack, "Setup", "Ensure World Builder Stack",
                    none, new StageDescriptorOptions(false, false, Array.Empty<string>(), none)),
                D(WorldBuildStageId.ValidateWorldProfile, "Setup", "Validate World Profile",
                    new[] { WorldBuildStageId.EnsureWorldBuilderStack }, new StageDescriptorOptions(false, false, Array.Empty<string>(), none, FullMapOnly)),
                D(WorldBuildStageId.CreateGlobalWorldPlan, "Setup", "Create Global World Plan",
                    new[] { WorldBuildStageId.ValidateWorldProfile }, new StageDescriptorOptions(true, false, new[] { "plan" }, none, FullMapOnly)),
                D(WorldBuildStageId.AllocateTerrainGrid, "Terrain", "Allocate Terrain Grid",
                    new[] { WorldBuildStageId.CreateGlobalWorldPlan }, new StageDescriptorOptions(true, false, new[] { "terrainGrid" }, none, FullMapOnly)),
                // Contiguous Planning Field block — Run Section must stay unbroken.
                D(WorldBuildStageId.GenerateBaseLandforms, "Planning Field", "Generate Base Landforms",
                    new[] { WorldBuildStageId.AllocateTerrainGrid }, new StageDescriptorOptions(true, true, new[] { "landforms" }, none)),
                // Pads are frozen flats, so they must exist before the terrain around them is sculpted:
                // erosion freezes the cores+aprons and hydrology solves over the already-stamped flat,
                // and CarveWaterFeatures re-asserts the cores after digging. With the pads resolved any
                // later, all three pad-aware paths are dead on a fresh build (Artifacts.CityPads is
                // nulled by ResetGeneratedWorld) and pads are flattened onto eroded, carved terrain.
                D(WorldBuildStageId.ReserveCityPads, "Planning Field", "Reserve City Pads",
                    new[] { WorldBuildStageId.GenerateBaseLandforms }, new StageDescriptorOptions(true, true, new[] { "sites", "cityPads", "landforms" }, none)),
                D(WorldBuildStageId.ErodeLandforms, "Planning Field", "Erode Landforms",
                    new[] { WorldBuildStageId.GenerateBaseLandforms, WorldBuildStageId.ReserveCityPads }, new StageDescriptorOptions(true, true, new[] { "landforms" }, none)),
                D(WorldBuildStageId.SolveHydrology, "Planning Field", "Solve Hydrology",
                    new[] { WorldBuildStageId.ErodeLandforms, WorldBuildStageId.ReserveCityPads }, new StageDescriptorOptions(true, true, new[] { "hydrology" }, none)),
                D(WorldBuildStageId.CarveWaterFeatures, "Planning Field", "Carve Water Features",
                    new[]
                    {
                        WorldBuildStageId.SolveHydrology,
                        WorldBuildStageId.AllocateTerrainGrid,
                        WorldBuildStageId.ReserveCityPads
                    }, new StageDescriptorOptions(true, true, new[] { "hydrology", "landforms" }, none)),
                D(WorldBuildStageId.ClassifyBiomesAndBuildability, "Planning Field", "Classify Biomes And Buildability",
                    new[] { WorldBuildStageId.CarveWaterFeatures, WorldBuildStageId.ReserveCityPads }, new StageDescriptorOptions(true, true, new[] { "biomes" }, none)),
                // Contiguous Environment block — sky/weather right after Planning Field (Run Section must stay unbroken).
                D(WorldBuildStageId.ConfigureSkyAndWeather, "Environment", "Configure Sky And Weather",
                    new[] { WorldBuildStageId.ValidateWorldProfile }, new StageDescriptorOptions(true, false, new[] { "skyWeather" }, none, FullMapOnly)),
                D(WorldBuildStageId.InitializeEnvironmentState, "Environment", "Initialize Environment State",
                    new[] { WorldBuildStageId.ConfigureSkyAndWeather }, new StageDescriptorOptions(true, false, new[] { "environment" }, none, FullMapOnly)),
                D(WorldBuildStageId.BindWeatherConsumers, "Environment", "Bind Weather Consumers",
                    new[] { WorldBuildStageId.InitializeEnvironmentState }, new StageDescriptorOptions(true, false, new[] { "weatherBindings" }, none, FullMapOnly)),
                D(WorldBuildStageId.BuildOceanSurfaces, "Water", "Build Ocean Surfaces",
                    new[] { WorldBuildStageId.CarveWaterFeatures }, new StageDescriptorOptions(true, true, new[] { "oceanSurfaces" }, none, FullMapOnly)),
                D(WorldBuildStageId.BuildWaterSurfaces, "Water", "Build Water Surfaces",
                    new[] { WorldBuildStageId.BuildOceanSurfaces }, new StageDescriptorOptions(true, true, new[] { "waterSurfaces" }, none, FullMapOnly)),
                // Contiguous Sites block — confirm sites + highways + pad reassert (Run Section contiguous).
                D(WorldBuildStageId.SelectCitySitesAndLandmarks, "Sites", "Select City Sites And Landmarks",
                    new[] { WorldBuildStageId.ReserveCityPads, WorldBuildStageId.ClassifyBiomesAndBuildability }, new StageDescriptorOptions(true, true, new[] { "sites" }, none)),
                D(WorldBuildStageId.PlanInterCityHighways, "Sites", "Plan Inter-City Highways",
                    new[]
                    {
                        WorldBuildStageId.SelectCitySitesAndLandmarks,
                        WorldBuildStageId.ClassifyBiomesAndBuildability
                    }, new StageDescriptorOptions(true, true, new[] { "roads", "landforms" }, none)),
                D(WorldBuildStageId.ApplyCityPads, "Sites", "Apply City Pads",
                    new[]
                    {
                        WorldBuildStageId.ReserveCityPads,
                        WorldBuildStageId.PlanInterCityHighways,
                        WorldBuildStageId.CarveWaterFeatures
                    }, new StageDescriptorOptions(true, true, new[] { "landforms" }, new[]
                    {
                        WorldBuildStageId.RefineRoadsAgainstTerrain,
                        WorldBuildStageId.StampInfrastructureTerrain,
                        WorldBuildStageId.GenerateNamedAreas,
                        WorldBuildStageId.GenerateCityFootpaths,
                        WorldBuildStageId.GenerateDistrictLots,
                        WorldBuildStageId.PaintNaturalSurfaces,
                        WorldBuildStageId.PlaceWildernessPois,
                        WorldBuildStageId.PlaceWildernessNature
                    })),
                // Contiguous Layout block — pre-paint plan masks for Surfaces (Run Section must stay unbroken).
                D(WorldBuildStageId.GenerateNamedAreas, "Layout", "Generate Named Areas",
                    new[] { WorldBuildStageId.ApplyCityPads }, new StageDescriptorOptions(false, true, new[] { "namedAreas" }, none)),
                D(WorldBuildStageId.GenerateCityFootpaths, "Layout", "Generate City Footpaths",
                    new[] { WorldBuildStageId.GenerateNamedAreas }, new StageDescriptorOptions(false, true, new[] { "footpaths" }, none)),
                D(WorldBuildStageId.GenerateDistrictLots, "Layout", "Generate District Lots",
                    new[] { WorldBuildStageId.GenerateCityFootpaths }, new StageDescriptorOptions(false, true, new[] { "lots" }, none)),
                D(WorldBuildStageId.PaintNaturalSurfaces, "Surfaces", "Paint Natural Surfaces",
                    new[]
                    {
                        WorldBuildStageId.ApplyCityPads,
                        WorldBuildStageId.ClassifyBiomesAndBuildability,
                        WorldBuildStageId.GenerateDistrictLots
                    }, new StageDescriptorOptions(false, true, new[] { "surfaces" }, none)),
                D(WorldBuildStageId.PlanRoadsAndHighways, "Roads", "Plan Roads And Highways",
                    new[] { WorldBuildStageId.SelectCitySitesAndLandmarks, WorldBuildStageId.PlanInterCityHighways }, new StageDescriptorOptions(true, true, new[] { "roads" }, none)),
                D(WorldBuildStageId.RefineRoadsAgainstTerrain, "Roads", "Refine Roads Against Terrain",
                    new[] { WorldBuildStageId.PlanRoadsAndHighways, WorldBuildStageId.ApplyCityPads }, new StageDescriptorOptions(false, true, new[] { "roads" }, none)),
                D(WorldBuildStageId.ResolveWaterCrossings, "Roads", "Resolve Water Crossings",
                    new[] { WorldBuildStageId.RefineRoadsAgainstTerrain, WorldBuildStageId.SolveHydrology }, new StageDescriptorOptions(false, true, new[] { "crossings" }, none)),
                D(WorldBuildStageId.ResolveMountainTunnels, "Roads", "Resolve Mountain Tunnels",
                    new[] { WorldBuildStageId.ResolveWaterCrossings, WorldBuildStageId.ApplyCityPads }, new StageDescriptorOptions(false, true, new[] { "tunnels", "landforms" }, none)),
                D(WorldBuildStageId.StampInfrastructureTerrain, "Roads", "Stamp Infrastructure Terrain",
                    new[] { WorldBuildStageId.ResolveMountainTunnels, WorldBuildStageId.PaintNaturalSurfaces }, new StageDescriptorOptions(false, true, new[] { "landforms", "surfaces" }, none)),
                D(WorldBuildStageId.BuildEasyRoadsMeshes, "Roads", "Build Procedural Road Meshes",
                    new[] { WorldBuildStageId.StampInfrastructureTerrain }, new StageDescriptorOptions(false, true, new[] { "roadMeshes" }, none)),
                D(WorldBuildStageId.SyncInfrastructureSurfaces, "Roads", "Sync Infrastructure Surfaces",
                    new[] { WorldBuildStageId.BuildEasyRoadsMeshes }, new StageDescriptorOptions(false, true, new[] { "surfaces" }, none)),
                // Contiguous Wilderness block — after roads so nature/POIs see stamped corridors.
                D(WorldBuildStageId.PlaceWildernessPois, "Wilderness", "Place Wilderness POIs",
                    new[]
                    {
                        WorldBuildStageId.PaintNaturalSurfaces,
                        WorldBuildStageId.ClassifyBiomesAndBuildability,
                        WorldBuildStageId.SelectCitySitesAndLandmarks,
                        WorldBuildStageId.SyncInfrastructureSurfaces
                    }, new StageDescriptorOptions(false, true, new[] { "wildernessPois" }, none)),
                D(WorldBuildStageId.PlaceWildernessNature, "Wilderness", "Place Wilderness Nature",
                    new[] { WorldBuildStageId.PlaceWildernessPois }, new StageDescriptorOptions(false, true, new[] { "wildernessNature" }, none)),
                // Contiguous City block — volume content before StreetDressing (Run Section must stay unbroken).
                D(WorldBuildStageId.PlaceBuildings, "City", "Place Buildings",
                    new[] { WorldBuildStageId.GenerateDistrictLots, WorldBuildStageId.BuildEasyRoadsMeshes }, new StageDescriptorOptions(false, true, new[] { "buildings" }, none)),
                D(WorldBuildStageId.GenerateLotTerrain, "City", "Generate Lot Terrain",
                    new[] { WorldBuildStageId.PaintNaturalSurfaces, WorldBuildStageId.PlaceBuildings }, new StageDescriptorOptions(false, true, new[] { "lotTerrain" }, none)),
                D(WorldBuildStageId.GenerateFences, "City", "Generate Fences",
                    new[] { WorldBuildStageId.PlaceBuildings }, new StageDescriptorOptions(false, true, new[] { "fences" }, none)),
                D(WorldBuildStageId.BuildParks, "City", "Build Parks",
                    new[] { WorldBuildStageId.GenerateDistrictLots, WorldBuildStageId.PlaceBuildings }, new StageDescriptorOptions(false, true, new[] { "parks" }, none)),
                D(WorldBuildStageId.PlaceCityTrees, "City", "Place City Trees",
                    new[] { WorldBuildStageId.GenerateFences }, new StageDescriptorOptions(false, true, new[] { "cityTrees" }, none)),
                // Contiguous StreetDressing block — props after city volume (Run Section must stay unbroken).
                D(WorldBuildStageId.GenerateRoadMarkings, "StreetDressing", "Generate Road Markings",
                    new[] { WorldBuildStageId.BuildEasyRoadsMeshes }, new StageDescriptorOptions(false, true, new[] { "roadMarkings" }, none)),
                D(WorldBuildStageId.GenerateTrafficLights, "StreetDressing", "Generate Traffic Lights",
                    new[] { WorldBuildStageId.BuildEasyRoadsMeshes }, new StageDescriptorOptions(false, true, new[] { "trafficLights" }, none)),
                D(WorldBuildStageId.GenerateStreetSigns, "StreetDressing", "Generate Street Signs",
                    new[] { WorldBuildStageId.BuildEasyRoadsMeshes }, new StageDescriptorOptions(false, true, new[] { "streetSigns" }, none)),
                D(WorldBuildStageId.GenerateStreetLamps, "StreetDressing", "Generate Street Lamps",
                    new[] { WorldBuildStageId.BuildEasyRoadsMeshes }, new StageDescriptorOptions(false, true, new[] { "streetLamps" }, none)),
                D(WorldBuildStageId.GeneratePowerLines, "StreetDressing", "Generate Power Lines",
                    new[] { WorldBuildStageId.PlaceBuildings, WorldBuildStageId.GenerateStreetLamps }, new StageDescriptorOptions(false, true, new[] { "powerLines" }, none)),
                D(WorldBuildStageId.GenerateStreetFurniture, "StreetDressing", "Generate Street Furniture",
                    new[] { WorldBuildStageId.GenerateFences, WorldBuildStageId.BuildEasyRoadsMeshes }, new StageDescriptorOptions(false, true, new[] { "streetFurniture" }, none)),
                D(WorldBuildStageId.GenerateParkedCars, "StreetDressing", "Generate Parked Cars",
                    new[] { WorldBuildStageId.PlaceBuildings, WorldBuildStageId.BuildEasyRoadsMeshes }, new StageDescriptorOptions(false, true, new[] { "parkedCars" }, none)),
                D(WorldBuildStageId.FinalizeTerrainTiles, "Finalize", "Finalize Terrain Tiles",
                    new[] { WorldBuildStageId.BindWeatherConsumers, WorldBuildStageId.SyncInfrastructureSurfaces }, new StageDescriptorOptions(false, true, new[] { "terrainTiles" }, none)),
                D(WorldBuildStageId.QueueNavMesh, "Finalize", "Queue NavMesh",
                    new[] { WorldBuildStageId.FinalizeTerrainTiles }, new StageDescriptorOptions(false, true, new[] { "navMesh" }, none)),
                D(WorldBuildStageId.ValidateAndPublish, "Finalize", "Validate And Publish",
                    new[] { WorldBuildStageId.QueueNavMesh }, new StageDescriptorOptions(false, false, new[] { "published" }, none, FullMapOnly)),
            };
        }

        private static Dictionary<WorldBuildStageId, Func<IWorldBuildStage>> BuildDefaultFactories()
        {
            return new Dictionary<WorldBuildStageId, Func<IWorldBuildStage>>
            {
                { WorldBuildStageId.ResetGeneratedWorld, () => new ResetGeneratedWorldStage() },
                { WorldBuildStageId.EnsureWorldBuilderStack, () => new EnsureWorldBuilderStackStage() },
                { WorldBuildStageId.ValidateWorldProfile, () => new ValidateWorldProfileStage() },
                { WorldBuildStageId.CreateGlobalWorldPlan, () => new CreateGlobalWorldPlanStage() },
                { WorldBuildStageId.AllocateTerrainGrid, () => new AllocateTerrainGridStage() },
                { WorldBuildStageId.GenerateBaseLandforms, () => new GenerateBaseLandformsStage() },
                { WorldBuildStageId.ReserveCityPads, () => new ReserveCityPadsStage() },
                { WorldBuildStageId.ErodeLandforms, () => new ErodeLandformsStage() },
                { WorldBuildStageId.SolveHydrology, () => new SolveHydrologyStage() },
                { WorldBuildStageId.CarveWaterFeatures, () => new CarveWaterFeaturesStage() },
                { WorldBuildStageId.BuildOceanSurfaces, () => new BuildOceanSurfacesStage() },
                { WorldBuildStageId.ClassifyBiomesAndBuildability, () => new ClassifyBiomesAndBuildabilityStage() },
                { WorldBuildStageId.SelectCitySitesAndLandmarks, () => new SelectCitySitesAndLandmarksStage() },
                { WorldBuildStageId.PlanInterCityHighways, () => new PlanInterCityHighwaysStage() },
                { WorldBuildStageId.ApplyCityPads, () => new ApplyCityPadsStage() },
                { WorldBuildStageId.PaintNaturalSurfaces, () => new PaintNaturalSurfacesStage() },
                { WorldBuildStageId.PlanRoadsAndHighways, () => new PlanRoadsAndHighwaysStage() },
                { WorldBuildStageId.RefineRoadsAgainstTerrain, () => new RefineRoadsAgainstTerrainStage() },
                { WorldBuildStageId.ResolveWaterCrossings, () => new ResolveWaterCrossingsStage() },
                { WorldBuildStageId.ResolveMountainTunnels, () => new ResolveMountainTunnelsStage() },
                { WorldBuildStageId.StampInfrastructureTerrain, () => new StampInfrastructureTerrainStage() },
                { WorldBuildStageId.BuildEasyRoadsMeshes, () => new BuildEasyRoadsMeshesStage() },
                { WorldBuildStageId.SyncInfrastructureSurfaces, () => new SyncInfrastructureSurfacesStage() },
                { WorldBuildStageId.GenerateNamedAreas, () => new GenerateNamedAreasStage() },
                { WorldBuildStageId.GenerateCityFootpaths, () => new GenerateCityFootpathsStage() },
                { WorldBuildStageId.GenerateDistrictLots, () => new GenerateDistrictLotsStage() },
                { WorldBuildStageId.PlaceBuildings, () => new PlaceBuildingsStage() },
                { WorldBuildStageId.GenerateLotTerrain, () => new GenerateLotTerrainStage() },
                { WorldBuildStageId.GenerateFences, () => new GenerateFencesStage() },
                { WorldBuildStageId.GenerateRoadMarkings, () => new GenerateRoadMarkingsStage() },
                { WorldBuildStageId.GenerateTrafficLights, () => new GenerateTrafficLightsStage() },
                { WorldBuildStageId.GenerateStreetSigns, () => new GenerateStreetSignsStage() },
                { WorldBuildStageId.BuildParks, () => new BuildParksStage() },
                { WorldBuildStageId.GenerateStreetLamps, () => new GenerateStreetLampsStage() },
                { WorldBuildStageId.GeneratePowerLines, () => new GeneratePowerLinesStage() },
                { WorldBuildStageId.BuildWaterSurfaces, () => new BuildWaterSurfacesStage() },
                { WorldBuildStageId.PlaceWildernessPois, () => new PlaceWildernessPoisStage() },
                { WorldBuildStageId.PlaceWildernessNature, () => new PlaceWildernessNatureStage() },
                { WorldBuildStageId.PlaceCityTrees, () => new PlaceCityTreesStage() },
                { WorldBuildStageId.GenerateStreetFurniture, () => new GenerateStreetFurnitureStage() },
                { WorldBuildStageId.GenerateParkedCars, () => new GenerateParkedCarsStage() },
                { WorldBuildStageId.ConfigureSkyAndWeather, () => new ConfigureSkyAndWeatherStage() },
                { WorldBuildStageId.InitializeEnvironmentState, () => new InitializeEnvironmentStateStage() },
                { WorldBuildStageId.BindWeatherConsumers, () => new BindWeatherConsumersStage() },
                { WorldBuildStageId.FinalizeTerrainTiles, () => new FinalizeTerrainTilesStage() },
                { WorldBuildStageId.QueueNavMesh, () => new QueueNavMeshStage() },
                { WorldBuildStageId.ValidateAndPublish, () => new ValidateAndPublishStage() },
            };
        }
    }
}
