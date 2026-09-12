using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Args for <see cref="WorldSitePlanner.Plan"/> (Sonar S107).</summary>
    public readonly struct WorldSitePlanArgs
    {
        public WorldMapSession Session { get; init; }
        public WorldGenerationProfile Profile { get; init; }
        public IWorldTerrainQuery TerrainQuery { get; init; }
        public DeterministicRng Rng { get; init; }
        public Rect? WorldBoundsOverride { get; init; }
        public WorldTileCatalog TileCatalog { get; init; }
        public OrogenPlan Orogen { get; init; }
        public bool ProvisionalPlacement { get; init; }
    }

    /// <summary>Args for <see cref="WorldSitePlanner.ImproveHighwayConnectivity"/> (Sonar S107).</summary>
    public readonly struct ImproveHighwayConnectivityArgs
    {
        public IReadOnlyList<WorldCitySite> Cities { get; init; }
        public WorldMapSession Session { get; init; }
        public WorldGenerationProfile Profile { get; init; }
        public IWorldTerrainQuery TerrainQuery { get; init; }
        public DeterministicRng Rng { get; init; }
        public Rect Bounds { get; init; }
        public WorldTileCatalog TileCatalog { get; init; }
        public OrogenPlan Orogen { get; init; }
    }

    /// <summary>Parameter bundles for site placement / acceptance (Sonar S107).</summary>
    public sealed partial class WorldSitePlanner
    {
        private readonly struct PlaceSitesByHierarchyArgs
        {
            public List<WorldCitySite> Sites { get; init; }
            public IReadOnlyList<CitySiteType> OrderedTypes { get; init; }
            public Rect Bounds { get; init; }
            public float RoadPadMeters { get; init; }
            public float FalloffGuardMeters { get; init; }
            public IWorldTerrainQuery TerrainQuery { get; init; }
            public DeterministicRng LocalRng { get; init; }
            public int Seed { get; init; }
            public float MinBuildability { get; init; }
            public float MaxSlopeDegrees { get; init; }
            public float DeepWaterDepth { get; init; }
            public float MaxReclaimDepth { get; init; }
            public float MinPadY { get; init; }
            public bool RequireFootprint { get; init; }
            public List<WorldCitySite> Existing { get; init; }
            public WorldTileCatalog TileCatalog { get; init; }
            public bool EnforceFootprintBuildability { get; init; }
            public bool EnforceContinuityGate { get; init; }
        }

        private readonly struct PlaceSitesArgs
        {
            public List<WorldCitySite> Sites { get; init; }
            public int Target { get; init; }
            public Rect Bounds { get; init; }
            public float Clearance { get; init; }
            public float FootprintRadius { get; init; }
            public IWorldTerrainQuery TerrainQuery { get; init; }
            public DeterministicRng LocalRng { get; init; }
            public int Seed { get; init; }
            public string NamePrefix { get; init; }
            public float MinBuildability { get; init; }
            public float MaxSlopeDegrees { get; init; }
            public float DeepWaterDepth { get; init; }
            public float MaxReclaimDepth { get; init; }
            public float MinPadY { get; init; }
            public bool RequireFootprint { get; init; }
            public List<WorldCitySite> Existing { get; init; }
            public WorldTileCatalog TileCatalog { get; init; }
            public CitySiteType SiteType { get; init; }
            public int DisplayIndex { get; init; }
            public bool EnforceFootprintBuildability { get; init; }
            public bool EnforceContinuityGate { get; init; }
            public float RoadPadMeters { get; init; }
            public float FalloffGuardMeters { get; init; }
        }

        private readonly struct TryAcceptSiteArgs
        {
            public Vector2 Center { get; init; }
            public IWorldTerrainQuery TerrainQuery { get; init; }
            public float MinBuildability { get; init; }
            public float MaxSlopeDegrees { get; init; }
            public float DeepWaterDepth { get; init; }
            public float MaxReclaimDepth { get; init; }
            public bool RequireFootprint { get; init; }
            public WorldTileCatalog TileCatalog { get; init; }
            public float FootprintRadius { get; init; }
            public bool EnforceFootprintBuildability { get; init; }
            public bool EnforceContinuityGate { get; init; }
        }

        private readonly struct FootprintIsBuildableArgs
        {
            public Vector2 Center { get; init; }
            public IWorldTerrainQuery TerrainQuery { get; init; }
            public float MinBuildability { get; init; }
            public float MaxSlopeDegrees { get; init; }
            public float DeepWaterDepth { get; init; }
            public float MaxReclaimDepth { get; init; }
            public WorldTileCatalog TileCatalog { get; init; }
            public float FootprintRadius { get; init; }
        }

        private readonly struct TryRerollCitySiteArgs
        {
            public IReadOnlyList<WorldCitySite> Cities { get; init; }
            public int IndexToReroll { get; init; }
            public Rect Bounds { get; init; }
            public float Clearance { get; init; }
            public float FootprintRadius { get; init; }
            public float RoadPadMeters { get; init; }
            public float FalloffGuardMeters { get; init; }
            public IWorldTerrainQuery TerrainQuery { get; init; }
            public DeterministicRng LocalRng { get; init; }
            public int Seed { get; init; }
            public float DeepWaterDepth { get; init; }
            public float MaxReclaimDepth { get; init; }
            public WorldTileCatalog TileCatalog { get; init; }
        }

        private sealed class HighwayImproveContext
        {
            public RoadNetworkSettings RoadSettings { get; init; }
            public TerrainRoadCostField CostField { get; set; }
            public float RoadPad { get; init; }
            public float FalloffGuard { get; init; }
            public float DeepWater { get; init; }
            public float MaxReclaimDepth { get; init; }
            public float MinPadY { get; init; }
            public int MaxAttemptsPerCity { get; init; }
            public DeterministicRng LocalRng { get; init; }
        }
    }
}
