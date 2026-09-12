using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Detects road/ocean intersections and assigns Ford/Causeway/Bridge policy.
    ///     Ocean-only: Lake/River/None are skipped for crossing detection.
    /// </summary>
    public static class WaterCrossingScanner
    {
        private const float SampleStepMeters = 8f;
        private const float WetDepthMeters = 0.05f;

        private struct CrossingScanState
        {
            public bool HasPrevious;
            public bool InWater;
            public Vector2 Previous;
            public float PreviousDepth;
            public Vector2 Entry;
            public float MaximumDepth;
            public float BankSlope;
        }

        public static List<WaterCrossing> Scan(
            RoadNetworkRuntime roads,
            IWorldTerrainQuery terrainQuery,
            IWorldHydrologyQuery hydrologyQuery,
            HydrologyProfile profile)
        {
            var results = new List<WaterCrossing>(16);
            if (roads?.Roads == null || terrainQuery == null || profile == null)
                return results;

            for (var r = 0; r < roads.Roads.Count; r++)
            {
                var road = roads.Roads[r];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2) continue;
                ScanRoad(road, terrainQuery, hydrologyQuery, profile, results);
            }

            return results;
        }

        private static void ScanRoad(
            RoadPolyline road,
            IWorldTerrainQuery terrainQuery,
            IWorldHydrologyQuery hydrologyQuery,
            HydrologyProfile profile,
            List<WaterCrossing> results)
        {
            var state = new CrossingScanState();

            for (var i = 0; i < road.pointsXZ.Count - 1; i++)
            {
                var a = road.pointsXZ[i];
                var b = road.pointsXZ[i + 1];
                var dist = Vector2.Distance(a, b);
                var steps = Mathf.Max(1, Mathf.CeilToInt(dist / SampleStepMeters));

                for (var s = 0; s <= steps; s++)
                {
                    var t = s / (float)steps;
                    var p = Vector2.Lerp(a, b, t);
                    AdvanceSample(
                        p,
                        road,
                        terrainQuery,
                        hydrologyQuery,
                        profile,
                        results,
                        ref state);
                }
            }

            if (!state.InWater)
                return;

            AddCrossing(
                results,
                state.Entry,
                state.Previous,
                state.MaximumDepth,
                state.BankSlope,
                road,
                profile,
                terrainQuery);
        }

        private static void AdvanceSample(
            Vector2 point,
            RoadPolyline road,
            IWorldTerrainQuery terrainQuery,
            IWorldHydrologyQuery hydrologyQuery,
            HydrologyProfile profile,
            List<WaterCrossing> results,
            ref CrossingScanState state)
        {
            var wet = TrySampleOceanWet(point, terrainQuery, hydrologyQuery, out var depth);
            if (!state.HasPrevious)
            {
                state.HasPrevious = true;
                state.Previous = point;
                state.PreviousDepth = depth;
                if (wet)
                    BeginCrossing(point, depth, terrainQuery, ref state);
                return;
            }

            if (wet && !state.InWater)
            {
                var entry = ResolveWaterline(
                    state.Previous,
                    point,
                    state.PreviousDepth,
                    depth);
                BeginCrossing(entry, depth, terrainQuery, ref state);
            }
            else if (wet)
            {
                state.MaximumDepth = Mathf.Max(state.MaximumDepth, depth);
            }
            else if (state.InWater)
            {
                var exit = ResolveWaterline(
                    state.Previous,
                    point,
                    state.PreviousDepth,
                    depth);
                AddCrossing(
                    results,
                    state.Entry,
                    exit,
                    state.MaximumDepth,
                    state.BankSlope,
                    road,
                    profile,
                    terrainQuery);
                state.InWater = false;
                state.MaximumDepth = 0f;
            }

            state.Previous = point;
            state.PreviousDepth = depth;
        }

        private static void BeginCrossing(
            Vector2 entry,
            float depth,
            IWorldTerrainQuery terrainQuery,
            ref CrossingScanState state)
        {
            state.InWater = true;
            state.Entry = entry;
            state.MaximumDepth = depth;
            terrainQuery.TrySampleSlope(entry, out state.BankSlope);
        }

        private static Vector2 ResolveWaterline(
            Vector2 a,
            Vector2 b,
            float depthA,
            float depthB)
        {
            var delta = depthB - depthA;
            if (Mathf.Abs(delta) < 0.0001f)
                return (a + b) * 0.5f;

            var t = (WetDepthMeters - depthA) / delta;
            if (t <= 0.001f || t >= 0.999f)
                return (a + b) * 0.5f;
            return Vector2.Lerp(a, b, t);
        }

        /// <summary>
        ///     Prefers hydrology depth; wet only for Ocean class when class is available.
        /// </summary>
        private static bool TrySampleOceanWet(
            Vector2 worldXZ,
            IWorldTerrainQuery terrainQuery,
            IWorldHydrologyQuery hydrologyQuery,
            out float depth)
        {
            depth = SampleDepth(worldXZ, terrainQuery, hydrologyQuery);
            if (depth <= WetDepthMeters)
                return false;

            if (!TryResolveWaterClass(worldXZ, terrainQuery, hydrologyQuery, out var waterClass))
            {
                // Depth without class: not ocean-confirmed in an ocean-only gate.
                return false;
            }

            return waterClass == WorldWaterClass.Ocean;
        }

        private static float SampleDepth(
            Vector2 worldXZ,
            IWorldTerrainQuery terrainQuery,
            IWorldHydrologyQuery hydrologyQuery)
        {
            if (hydrologyQuery != null)
            {
                var depth = hydrologyQuery.SampleWaterDepth(worldXZ);
                if (depth > 0f) return depth;
            }

            if (terrainQuery != null && terrainQuery.TrySampleWater(worldXZ, out var water))
                return water.DepthMeters;
            return 0f;
        }

        private static bool TryResolveWaterClass(
            Vector2 worldXZ,
            IWorldTerrainQuery terrainQuery,
            IWorldHydrologyQuery hydrologyQuery,
            out WorldWaterClass waterClass)
        {
            waterClass = WorldWaterClass.None;

            if (hydrologyQuery != null &&
                hydrologyQuery.TrySampleWater(worldXZ, out var hydroWater))
            {
                waterClass = hydroWater.Class;
                return true;
            }

            if (terrainQuery != null && terrainQuery.TrySampleWater(worldXZ, out var terrainWater))
            {
                waterClass = terrainWater.Class;
                return true;
            }

            return false;
        }

        private static void AddCrossing(
            List<WaterCrossing> results,
            Vector2 entry,
            Vector2 exit,
            float maxDepth,
            float bankSlope,
            RoadPolyline road,
            HydrologyProfile profile,
            IWorldTerrainQuery terrainQuery)
        {
            var width = Vector2.Distance(entry, exit);
            if (width < 1f && maxDepth < WetDepthMeters) return;

            var policy = WaterCrossingPolicyResolver.Resolve(
                profile,
                road.roadClass,
                maxDepth,
                width,
                bankSlope);

            if (policy == WaterCrossingPolicy.Avoid)
                return;

            var hasher = new StableHash64();
            hasher.Append(road.id);
            hasher.Append(entry.x);
            hasher.Append(entry.y);
            hasher.Append(exit.x);
            hasher.Append(exit.y);

            var deckY = ResolveDeckWorldY(entry, exit, terrainQuery, profile);
            results.Add(new WaterCrossing(
                hasher.Finalize(),
                road.id,
                entry,
                exit,
                width,
                maxDepth,
                deckY,
                road.roadClass,
                policy));
        }

        private static float ResolveDeckWorldY(
            Vector2 entry,
            Vector2 exit,
            IWorldTerrainQuery terrainQuery,
            HydrologyProfile profile)
        {
            if (terrainQuery == null)
                return 0f;

            terrainQuery.TrySampleHeight(entry, out var entryY);
            terrainQuery.TrySampleHeight(exit, out var exitY);
            var clearance = profile != null ? profile.BridgeDeckClearanceMeters : 0f;
            return Mathf.Max(entryY, exitY) + clearance;
        }
    }
}
