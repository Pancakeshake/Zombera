using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Shared water rejection for city sites and district lot building placement.
    /// Reclaimable shallow water (pads) passes; deep / non-reclaimable water rejects.
    /// </summary>
    public static class WorldWaterPlacementGate
    {
        public const float DefaultDeepWaterDepthMeters = 0.3f;
        public const float DefaultMinDistanceToWaterMeters = 2f;

        public static bool PassesWaterSample(
            in WorldWaterSample water,
            float deepWaterDepth,
            out string rejectCode) =>
            PassesWaterSample(
                water,
                deepWaterDepth,
                minDistanceToWaterMeters: 0f,
                maxReclaimDepthMeters: 0f,
                out rejectCode);

        public static bool PassesWaterSample(
            in WorldWaterSample water,
            float deepWaterDepth,
            float minDistanceToWaterMeters,
            out string rejectCode) =>
            PassesWaterSample(
                water,
                deepWaterDepth,
                minDistanceToWaterMeters,
                maxReclaimDepthMeters: 0f,
                out rejectCode);

        public static bool PassesWaterSample(
            in WorldWaterSample water,
            float deepWaterDepth,
            float minDistanceToWaterMeters,
            float maxReclaimDepthMeters,
            out string rejectCode)
        {
            if (maxReclaimDepthMeters > 0f &&
                CityPadReclaimPolicy.IsReclaimable(water.Class, water.DepthMeters, maxReclaimDepthMeters))
            {
                rejectCode = null;
                return true;
            }

            if (water.Class == WorldWaterClass.Ocean ||
                water.DepthMeters > deepWaterDepth)
            {
                rejectCode = "reject_water";
                return false;
            }

            if (minDistanceToWaterMeters > 0f &&
                water.DistanceMeters < minDistanceToWaterMeters)
            {
                rejectCode = "reject_water_margin";
                return false;
            }

            rejectCode = null;
            return true;
        }

        /// <summary>
        /// Samples footprint center + corners. Returns false when any probe fails the water gate.
        /// When <paramref name="terrainQuery"/> is null: accepts if <paramref name="requireQuery"/> is false
        /// (standalone city lab); rejects if requireQuery is true (full hub build).
        /// </summary>
        public static bool FootprintIsDry(
            Rect footprintXZ,
            IWorldTerrainQuery terrainQuery,
            float deepWaterDepth,
            float minDistanceToWaterMeters,
            out string rejectCode) =>
            FootprintIsDry(
                footprintXZ,
                terrainQuery,
                deepWaterDepth,
                minDistanceToWaterMeters,
                maxReclaimDepthMeters: 0f,
                requireQuery: false,
                out rejectCode);

        public static bool FootprintIsDry(
            Rect footprintXZ,
            IWorldTerrainQuery terrainQuery,
            float deepWaterDepth,
            float minDistanceToWaterMeters,
            float maxReclaimDepthMeters,
            bool requireQuery,
            out string rejectCode)
        {
            rejectCode = null;
            if (terrainQuery == null)
            {
                if (!requireQuery)
                    return true;
                rejectCode = "reject_water_query_missing";
                return false;
            }

            if (!TryProbe(
                    footprintXZ.center,
                    terrainQuery,
                    deepWaterDepth,
                    minDistanceToWaterMeters,
                    maxReclaimDepthMeters,
                    requireQuery,
                    out rejectCode))
                return false;

            var halfW = footprintXZ.width * 0.5f;
            var halfD = footprintXZ.height * 0.5f;
            var cx = footprintXZ.center.x;
            var cz = footprintXZ.center.y;

            return TryProbe(new Vector2(cx - halfW, cz - halfD), terrainQuery, deepWaterDepth,
                       minDistanceToWaterMeters, maxReclaimDepthMeters, requireQuery, out rejectCode) &&
                   TryProbe(new Vector2(cx + halfW, cz - halfD), terrainQuery, deepWaterDepth,
                       minDistanceToWaterMeters, maxReclaimDepthMeters, requireQuery, out rejectCode) &&
                   TryProbe(new Vector2(cx - halfW, cz + halfD), terrainQuery, deepWaterDepth,
                       minDistanceToWaterMeters, maxReclaimDepthMeters, requireQuery, out rejectCode) &&
                   TryProbe(new Vector2(cx + halfW, cz + halfD), terrainQuery, deepWaterDepth,
                       minDistanceToWaterMeters, maxReclaimDepthMeters, requireQuery, out rejectCode);
        }

        private static bool TryProbe(
            Vector2 worldXZ,
            IWorldTerrainQuery terrainQuery,
            float deepWaterDepth,
            float minDistanceToWaterMeters,
            float maxReclaimDepthMeters,
            bool requireQuery,
            out string rejectCode)
        {
            rejectCode = null;
            if (!terrainQuery.TrySampleWater(worldXZ, out var water))
            {
                if (!requireQuery)
                    return true;
                rejectCode = "reject_water_sample_miss";
                return false;
            }

            return PassesWaterSample(
                water,
                deepWaterDepth,
                minDistanceToWaterMeters,
                maxReclaimDepthMeters,
                out rejectCode);
        }

        public static float ResolveDeepWaterDepth(WorldGenerationProfile profile)
        {
            var hydro = profile != null ? profile.Hydrology : null;
            if (hydro == null) return DefaultDeepWaterDepthMeters;
            return Mathf.Max(DefaultDeepWaterDepthMeters, hydro.FordMaxDepthMeters);
        }
    }
}
