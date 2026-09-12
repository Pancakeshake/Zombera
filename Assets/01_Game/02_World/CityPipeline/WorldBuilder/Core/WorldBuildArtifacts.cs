using System.Collections.Generic;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Mutable bag of whole-artifact replacements produced by pipeline stages.</summary>
    public sealed class WorldBuildArtifacts
    {
        public WorldPlan Plan { get; private set; }
        public LandformField Landforms { get; private set; }
        public OrogenPlan Orogen { get; private set; }
        public HydrologyPlan Hydrology { get; private set; }
        public BiomeField Biomes { get; private set; }
        public WorldSitePlan Sites { get; private set; }
        /// <summary>Arterial+margin flat cores reserved during planning-field generation.</summary>
        public IReadOnlyList<CityFlattenPad> CityPads { get; private set; }
        public RoadNetworkRuntime Roads { get; private set; }
        public IReadOnlyList<WaterCrossing> Crossings { get; private set; }
        public IReadOnlyList<MountainTunnel> Tunnels { get; private set; }

        public IReadOnlyList<RoadPolyline> CityGeneratedRoads { get; private set; }

        /// <summary>
        ///     True after Refine (or equivalent) carved+baked highway corridors to Unity heightmaps.
        ///     Used to fail-closed Roads-stage heightmap write skips.
        /// </summary>
        public bool HighwayCorridorsBakedPostRefine { get; private set; }

        /// <summary>
        ///     True after PaintNaturalSurfaces composed district/lot urban stamps
        ///     so GenerateLotTerrain can early-out the alphamap overwrite.
        /// </summary>
        public bool LotTerrainComposedDuringPaint { get; private set; }

        public void SetPlan(WorldPlan plan) => Plan = plan;
        public void SetLandforms(LandformField landforms)
        {
            var isRegen = !ReferenceEquals(Landforms, landforms);
            Landforms = landforms;
            HighwayCorridorsBakedPostRefine = false;
            LotTerrainComposedDuringPaint = false;
            SetTunnels(null);
            // Full landform regen invalidates stamped cores; in-place mutate keeps them.
            if (isRegen)
                SetCityPads(null);
        }
        public void SetOrogen(OrogenPlan orogen) => Orogen = orogen;
        public void SetHydrology(HydrologyPlan hydrology) => Hydrology = hydrology;
        public void SetBiomes(BiomeField biomes) => Biomes = biomes;
        public void SetSites(WorldSitePlan sites) => Sites = sites;
        public void SetCityPads(IReadOnlyList<CityFlattenPad> pads) => CityPads = pads;
        public void SetRoads(RoadNetworkRuntime roads)
        {
            Roads = roads;
            HighwayCorridorsBakedPostRefine = false;
            SetTunnels(null);
        }
        public void SetCrossings(IReadOnlyList<WaterCrossing> crossings)
        {
            Crossings = crossings;
            WaterCrossingBuildCache.Set(crossings);
        }

        public void SetTunnels(IReadOnlyList<MountainTunnel> tunnels)
        {
            Tunnels = tunnels;
            MountainTunnelBuildCache.Set(tunnels);
            if (tunnels == null || tunnels.Count == 0)
                TunnelRuntimeRegistry.Clear();
        }
        public void SetCityGeneratedRoads(IReadOnlyList<RoadPolyline> roads) => CityGeneratedRoads = roads;
        public void SetHighwayCorridorsBakedPostRefine(bool value) => HighwayCorridorsBakedPostRefine = value;
        public void SetLotTerrainComposedDuringPaint(bool value) => LotTerrainComposedDuringPaint = value;
    }
}
