using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Terrain-query coastal derivation when HydrologyPlan is unavailable (site accept).</summary>
    public static partial class CityCoastalPadUtility
    {
        public static bool TryDeriveCoastal(
            Vector2 centerXZ,
            float halfWidthMeters,
            float halfDepthMeters,
            IWorldTerrainQuery terrainQuery,
            LandformProfile landforms,
            out Vector2 seawardNormalXZ,
            out float coastExposure01)
        {
            seawardNormalXZ = Vector2.zero;
            coastExposure01 = 0f;
            if (terrainQuery == null)
                return false;

            var maxReclaim = CityPadReclaimPolicy.MaxReclaimDepthMeters(landforms);
            var probe = Mathf.Max(
                DefaultProbeMeters,
                Mathf.Max(halfWidthMeters, halfDepthMeters) + 80f);
            var bestDist = float.MaxValue;
            var oceanSum = Vector2.zero;
            var oceanHits = 0;

            const int spokes = 16;
            for (var i = 0; i < spokes; i++)
            {
                var angle = (Mathf.PI * 2f * i) / spokes;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (!TryFindOceanAlongRayQuery(
                        centerXZ, dir, probe, terrainQuery, maxReclaim, out var hitDist, out var hitPoint))
                    continue;

                oceanHits++;
                oceanSum += (hitPoint - centerXZ).normalized;
                if (hitDist < bestDist)
                    bestDist = hitDist;
            }

            if (oceanHits == 0)
                return false;

            seawardNormalXZ = oceanSum.normalized;
            if (seawardNormalXZ.sqrMagnitude < 0.0001f)
                seawardNormalXZ = Vector2.down;

            var footprint = Mathf.Max(halfWidthMeters, halfDepthMeters);
            coastExposure01 = Mathf.Clamp01(1f - (bestDist / Mathf.Max(1f, footprint + probe * 0.35f)));
            return coastExposure01 > 0.08f;
        }

        private static bool TryFindOceanAlongRayQuery(
            Vector2 origin,
            Vector2 dir,
            float maxDist,
            IWorldTerrainQuery terrainQuery,
            float maxReclaimDepth,
            out float hitDist,
            out Vector2 hitPoint)
        {
            hitDist = 0f;
            hitPoint = origin;
            const float step = 16f;
            for (var d = step; d <= maxDist; d += step)
            {
                var p = origin + dir * d;
                if (!terrainQuery.TrySampleWater(p, out var water))
                    continue;
                if (water.Class != WorldWaterClass.Ocean)
                    continue;
                if (CityPadReclaimPolicy.IsReclaimable(water.Class, water.DepthMeters, maxReclaimDepth))
                    continue;

                hitDist = d;
                hitPoint = p;
                return true;
            }

            return false;
        }
    }
}
