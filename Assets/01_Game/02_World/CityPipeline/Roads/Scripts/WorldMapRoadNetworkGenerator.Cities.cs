using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Multi-city world placement: seeds city centers with minimum separation,
    ///     stamps a local grid per city, registers town nodes, and connects cities
    ///     with highway polylines. Fully deterministic from the generator rng.
    /// </summary>
    public static partial class WorldMapRoadNetworkGenerator
    {
        private const int HighwayIdBase = 200000;
        private const int CityGridIdBase = 10000;
        private const int MaxPlacementAttempts = 250;

        private struct CityPlan
        {
            public Vector2 position;
            public float radius;
        }

        private static void PlaceCitiesAndHighways(
            RoadNetworkRuntime network, RoadNetworkSettings settings, System.Random rng, Vector2 center)
        {
            var cities = PlaceCityCenters(network, settings, rng, center);

            for (var i = 0; i < cities.Count; i++)
                AddCityGrid(network, settings, rng, cities[i], CityGridIdBase * (i + 1));

            if (settings.connectCitiesWithHighways)
                ConnectCitiesWithHighways(network, settings, rng, cities);
        }

        // ── City center placement ─────────────────────────────

        private static List<CityPlan> PlaceCityCenters(
            RoadNetworkRuntime network, RoadNetworkSettings settings, System.Random rng, Vector2 center)
        {
            var cities = new List<CityPlan>();
            var cityCount = Mathf.Max(1, settings.cityCount);
            var minSep = Mathf.Max(1f, settings.cityMinSeparationMeters);
            var maxRingRadius = Mathf.Max(100f, settings.globalRingRadius * 0.85f);
            var minPlacementRadius = Mathf.Max(100f, settings.cityGridRadius * 2f);

            var mainRadius = settings.cityGridRadius * RollRadiusScale(settings, rng);
            cities.Add(new CityPlan { position = center, radius = mainRadius });
            if (settings.spawnTownNodes)
                AddTownNode(network, center, mainRadius, TownType.City);

            for (var i = 1; i < cityCount; i++)
            {
                if (!TryPickSeparatedPosition(cities, center, rng, minSep, minPlacementRadius, maxRingRadius, out var position))
                    break; // Region too crowded for another separated city.

                var radius = settings.cityGridRadius * RollRadiusScale(settings, rng);
                cities.Add(new CityPlan { position = position, radius = radius });

                if (!settings.spawnTownNodes)
                    continue;

                var townType = rng.Next(0, 3) == 0 ? TownType.Town : (TownType)rng.Next(0, 2);
                AddTownNode(network, position, radius, townType);
            }

            return cities;
        }

        private static void AddTownNode(RoadNetworkRuntime network, Vector2 position, float radius, TownType type)
        {
            network.AddTown(new TownNode(network.TownNodes.Count, position, type, radius));
        }

        private static bool TryPickSeparatedPosition(
            List<CityPlan> cities, Vector2 center, System.Random rng,
            float minSep, float minRadius, float maxRadius, out Vector2 position)
        {
            position = default;
            var separation = minSep;

            for (var attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                if (attempt > 0 && attempt % 50 == 0)
                    separation *= 0.8f; // Relax constraints after repeated failures.

                var angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                var distance = Mathf.Lerp(minRadius, maxRadius, (float)rng.NextDouble());
                var candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                var valid = true;
                for (var c = 0; c < cities.Count; c++)
                {
                    if (Vector2.Distance(candidate, cities[c].position) >= separation)
                        continue;

                    valid = false;
                    break;
                }

                if (!valid)
                    continue;

                position = candidate;
                return true;
            }

            return false;
        }

        // ── Per-city grid ─────────────────────────────────────

        private static void AddCityGrid(
            RoadNetworkRuntime network, RoadNetworkSettings settings, System.Random rng, CityPlan city, int idBase)
        {
            var spacing = Mathf.Max(10f, settings.cityGridSpacing);
            var radius = Mathf.Max(20f, city.radius);
            var count = Mathf.FloorToInt(radius * 2f / spacing);
            var offset = -radius;
            const float jitterScale = 4f;

            for (var x = 0; x <= count; x++)
            {
                var px = city.position.x + offset + x * spacing;
                var road = new RoadPolyline
                {
                    id = idBase + x,
                    roadClass = RoadClass.Local,
                    widthMeters = settings.localWidth
                };

                for (var z = 0; z <= count; z++)
                {
                    var pz = city.position.y + offset + z * spacing;
                    if (Vector2.Distance(new Vector2(px, pz), city.position) > radius)
                        continue;

                    road.pointsXZ.Add(new Vector2(
                        px + (float)(rng.NextDouble() - 0.5) * jitterScale,
                        pz + (float)(rng.NextDouble() - 0.5) * jitterScale));
                }

                network.AddRoad(road);
            }

            for (var z = 0; z <= count; z++)
            {
                var pz = city.position.y + offset + z * spacing;
                var road = new RoadPolyline
                {
                    id = idBase + 1000 + z,
                    roadClass = RoadClass.Local,
                    widthMeters = settings.localWidth
                };

                for (var x = 0; x <= count; x++)
                {
                    var px = city.position.x + offset + x * spacing;
                    if (Vector2.Distance(new Vector2(px, pz), city.position) > radius)
                        continue;

                    road.pointsXZ.Add(new Vector2(
                        px + (float)(rng.NextDouble() - 0.5) * jitterScale,
                        pz + (float)(rng.NextDouble() - 0.5) * jitterScale));
                }

                network.AddRoad(road);
            }
        }

        // ── Inter-city highways ───────────────────────────────

        private static void ConnectCitiesWithHighways(
            RoadNetworkRuntime network, RoadNetworkSettings settings, System.Random rng, List<CityPlan> cities)
        {
            if (cities.Count < 2)
                return;

            var unconnected = new List<int>(cities.Count - 1);
            for (var i = 1; i < cities.Count; i++)
                unconnected.Add(i);

            var previous = 0;
            var roadId = HighwayIdBase;

            while (unconnected.Count > 0)
            {
                var nearestIndex = FindNearestCityIndex(cities, cities[previous].position, unconnected);
                var next = unconnected[nearestIndex];
                unconnected.RemoveAt(nearestIndex);

                network.AddRoad(new RoadPolyline
                {
                    id = roadId++,
                    roadClass = RoadClass.Highway,
                    widthMeters = settings.highwayWidth,
                    pointsXZ = Subdivide(new List<Vector2>
                    {
                        cities[previous].position,
                        cities[next].position
                    }, 30f)
                });

                previous = next;
            }

            // Occasional loop closure back to the main city.
            if (cities.Count > 2 && rng.NextDouble() < settings.highwayExtraLoopChance)
            {
                network.AddRoad(new RoadPolyline
                {
                    id = roadId,
                    roadClass = RoadClass.Highway,
                    widthMeters = settings.highwayWidth,
                    pointsXZ = Subdivide(new List<Vector2>
                    {
                        cities[previous].position,
                        cities[0].position
                    }, 30f)
                });
            }
        }

        private static int FindNearestCityIndex(List<CityPlan> cities, Vector2 from, List<int> candidates)
        {
            var bestIndex = 0;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < candidates.Count; i++)
            {
                var distance = (cities[candidates[i]].position - from).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static float RollRadiusScale(RoadNetworkSettings settings, System.Random rng)
        {
            var min = Mathf.Max(0.1f, settings.cityRadiusVariationMin);
            var max = Mathf.Max(min, settings.cityRadiusVariationMax);
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }
    }
}
