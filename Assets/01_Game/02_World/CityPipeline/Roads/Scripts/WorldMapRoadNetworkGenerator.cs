using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Builds the authoritative seed-based road graph used for both editor preview and runtime tile builds.
    /// </summary>
    public static partial class WorldMapRoadNetworkGenerator
    {
        public static RoadNetworkRuntime Generate(int seed, RoadNetworkSettings settings)
        {
            settings ??= ScriptableObject.CreateInstance<RoadNetworkSettings>();

            var network = new RoadNetworkRuntime(seed);
            var rng = new System.Random(seed);
            var center = Vector2.zero;

            if (settings.generateGlobalRing)
            {
                var ring = new RoadPolyline
                {
                    id = 100,
                    roadClass = RoadClass.Highway,
                    widthMeters = settings.highwayWidth
                };

                const int ringPoints = 144;
                for (var i = 0; i <= ringPoints; i++)
                {
                    var t = i / (float)ringPoints;
                    var a = t * Mathf.PI * 2f;
                    var radius = settings.globalRingRadius + (float)(rng.NextDouble() - 0.5) * 50f;
                    ring.pointsXZ.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }

                network.AddRoad(ring);
            }

            for (var i = 0; i < settings.globalSpokeCount; i++)
            {
                var angle = i / (float)settings.globalSpokeCount * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                var start = center + dir * settings.cityGridRadius;
                var end = center + dir * settings.globalRingRadius;

                var exitClass = settings.cityExitRoadClass;
                var spoke = new RoadPolyline
                {
                    id = 1000 + i,
                    roadClass = exitClass,
                    widthMeters = settings.ResolveWidthMeters(exitClass)
                };

                List<Vector2> rawPoints;
                if (settings.cityExitUseDirectSpokes)
                {
                    rawPoints = new List<Vector2> { start, end };
                }
                else
                {
                    rawPoints = new List<Vector2> { start };
                    var mid = Vector2.Lerp(start, end, 0.5f);
                    var perp = new Vector2(-dir.y, dir.x);
                    mid += perp * (float)(rng.NextDouble() - 0.5) * 200f;
                    rawPoints.Add(mid);
                    rawPoints.Add(end);
                }

                spoke.pointsXZ = Subdivide(rawPoints, 20f);
                network.AddRoad(spoke);
            }

            if (settings.generateCityGrids)
                PlaceCitiesAndHighways(network, settings, rng, center);

            Debug.Log(
                "[WorldMapRoadNetworkGenerator] Built world-map network seed=" + seed +
                " roads=" + network.Roads.Count + ".");

            return network;
        }

        public static void RefinePlanForTerrain(RoadNetworkRuntime network, RoadNetworkSettings settings) =>
            RefinePlanForTerrain(network, settings, (Func<RoadPolyline, bool>)null);

        /// <summary>
        ///     Reroutes inter-city highways through terrain valleys before hub terrain flatten.
        ///     City grid roads are left untouched.
        /// </summary>
        public static void RefineHighwaysForTerrain(
            RoadNetworkRuntime network,
            RoadNetworkSettings settings,
            bool useHighwayCellSize = false) =>
            RefinePlanForTerrain(network, settings, ShouldRerouteHubHighway, useHighwayCellSize);

        private static bool ShouldRerouteHubHighway(RoadPolyline road) =>
            road != null && road.roadClass == RoadClass.Highway && !road.preserveWorldPath;

        private static void RefinePlanForTerrain(
            RoadNetworkRuntime network,
            RoadNetworkSettings settings,
            Func<RoadPolyline, bool> roadFilter,
            bool useHighwayCellSize = false)
        {
            if (network == null || settings == null) return;

            var terrains = Terrain.activeTerrains;
            if (terrains == null || terrains.Length == 0) return;

            if (settings.rerouteRoadsWithTerrainPathfinding && settings.terrainRefinementStrength > 0.01f)
                RerouteRoadsThroughTerrain(network, settings, terrains, roadFilter, useHighwayCellSize);

            FinalizeRoadGeometry(network, settings, terrains, roadFilter);
        }

        /// <summary>Refines roads using a prebuilt cost field (WorldCostField wrap or terrain sample).</summary>
        public static void RefinePlanForTerrain(
            RoadNetworkRuntime network,
            RoadNetworkSettings settings,
            TerrainRoadCostField costField)
        {
            if (network == null || settings == null) return;

            if (costField != null &&
                costField.IsValid &&
                settings.rerouteRoadsWithTerrainPathfinding &&
                settings.terrainRefinementStrength > 0.01f)
            {
                RerouteRoadsThroughCostField(network, settings, costField);
            }

            FinalizeRoadGeometry(network, settings, Terrain.activeTerrains);
        }

        private static bool MatchesRoadFilter(RoadPolyline road, Func<RoadPolyline, bool> roadFilter) =>
            roadFilter == null || roadFilter(road);

        private static void FinalizeRoadGeometry(
            RoadNetworkRuntime network,
            RoadNetworkSettings settings,
            Terrain[] terrains,
            Func<RoadPolyline, bool> roadFilter = null)
        {
            var roadsToRemove = new List<RoadPolyline>();

            foreach (var road in network.Roads)
            {
                if (!MatchesRoadFilter(road, roadFilter))
                    continue;

                if (road.preserveWorldPath)
                    continue;

                if (settings.filterSteepRoadPointsAfterRefinement && terrains != null && terrains.Length > 0)
                    FilterSteepRoadPoints(road, terrains, settings);

                if (road.pointsXZ == null || road.pointsXZ.Count < 2)
                    roadsToRemove.Add(road);
            }

            foreach (var road in roadsToRemove)
                network.RemoveRoad(road);

            if (terrains == null || terrains.Length == 0) return;

            foreach (var road in network.Roads)
            {
                if (!MatchesRoadFilter(road, roadFilter))
                    continue;

                if (road.preserveWorldPath)
                    continue;

                TrimPolylineEndsForSlope(road, terrains, settings);
            }
        }

    }
}
