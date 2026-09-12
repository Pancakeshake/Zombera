using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Builds a seed road graph from selected city sites using WorldCostField pathfinding.</summary>
    public sealed class WorldRoadPlanner
    {
        private const int LocalIdBase = 10000;

        public RoadNetworkRuntime Plan(
            WorldMapSession session,
            WorldSitePlan sites,
            WorldGenerationProfile profile,
            IWorldTerrainQuery terrainQuery,
            RoadNetworkRuntime existingNetwork = null)
        {
            var settings = profile?.RoadNetworkSettings;
            var network = existingNetwork ?? new RoadNetworkRuntime(session.Seed);
            if (settings == null || sites?.CitySites == null || sites.CitySites.Count == 0)
                return network;

            RegisterTowns(network, sites);
            AddLocalCrossArms(network, sites, settings);

            if (settings.connectCitiesWithHighways &&
                sites.CitySites.Count >= 2 &&
                !HasHighwayRoads(network))
            {
                Debug.LogWarning(
                    "[WorldRoadPlanner] Inter-city highways missing; locals/cross-arms only. " +
                    "WB sole author is PlanInterCityHighways.");
            }

            return network;
        }

        private static bool HasHighwayRoads(RoadNetworkRuntime network)
        {
            if (network?.Roads == null)
                return false;

            for (var i = 0; i < network.Roads.Count; i++)
            {
                if (network.Roads[i]?.roadClass == RoadClass.Highway)
                    return true;
            }

            return false;
        }

        private static void RegisterTowns(RoadNetworkRuntime network, WorldSitePlan sites)
        {
            for (var i = 0; i < sites.CitySites.Count; i++)
            {
                var site = sites.CitySites[i];
                if (site == null) continue;
                var radius = Mathf.Max(40f, Mathf.Min(site.HalfWidthMeters, site.HalfDepthMeters));
                network.AddTown(new TownNode(i, site.CenterXZ, TownType.City, radius));
            }
        }

        private static void AddLocalCrossArms(
            RoadNetworkRuntime network,
            WorldSitePlan sites,
            RoadNetworkSettings settings)
        {
            var arm = Mathf.Max(40f, settings.cityGridRadius * 0.55f);
            for (var i = 0; i < sites.CitySites.Count; i++)
            {
                var site = sites.CitySites[i];
                if (site == null) continue;
                var c = site.CenterXZ;
                network.AddRoad(MakeRoad(
                    LocalIdBase + i * 2,
                    RoadClass.Arterial,
                    settings.ResolveWidthMeters(RoadClass.Arterial),
                    c + new Vector2(-arm, 0f),
                    c + new Vector2(arm, 0f)));
                network.AddRoad(MakeRoad(
                    LocalIdBase + i * 2 + 1,
                    RoadClass.Arterial,
                    settings.ResolveWidthMeters(RoadClass.Arterial),
                    c + new Vector2(0f, -arm),
                    c + new Vector2(0f, arm)));
            }
        }

        private static RoadPolyline MakeRoad(
            int id,
            RoadClass roadClass,
            float width,
            Vector2 a,
            Vector2 b)
        {
            return new RoadPolyline
            {
                id = id,
                roadClass = roadClass,
                widthMeters = width,
                pointsXZ = new List<Vector2> { a, b }
            };
        }
    }
}
