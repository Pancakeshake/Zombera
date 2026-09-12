using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Session-only coastal derivation for city pads (not SettlementState).
    ///     Seaward = direction toward nearest non-reclaimable ocean.
    /// </summary>
    public static partial class CityCoastalPadUtility
    {
        public const float DefaultProbeMeters = 420f;
        public const float QuayFalloffMeters = 48f;

        public static bool TryDeriveCoastal(
            Vector2 centerXZ,
            float halfWidthMeters,
            float halfDepthMeters,
            HydrologyPlan hydrology,
            LandformProfile landforms,
            out Vector2 seawardNormalXZ,
            out float coastExposure01)
        {
            seawardNormalXZ = Vector2.zero;
            coastExposure01 = 0f;
            if (hydrology?.WaterClass == null)
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
                if (!TryFindOceanAlongRay(
                        centerXZ, dir, probe, hydrology, maxReclaim, out var hitDist, out var hitPoint))
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

        public static bool TryDeriveCoastal(
            Rect plateau,
            HydrologyPlan hydrology,
            LandformProfile landforms,
            out Vector2 seawardNormalXZ,
            out float coastExposure01) =>
            TryDeriveCoastal(
                plateau.center,
                plateau.width * 0.5f,
                plateau.height * 0.5f,
                hydrology,
                landforms,
                out seawardNormalXZ,
                out coastExposure01);

        public static bool IsSeawardBearing(Vector2 fromPlateauCenter, Vector2 seawardNormalXZ, float facingDotMin = 0.15f)
        {
            if (seawardNormalXZ.sqrMagnitude < 0.0001f)
                return false;
            var dir = fromPlateauCenter.normalized;
            if (dir.sqrMagnitude < 0.0001f)
                return false;
            return Vector2.Dot(dir, seawardNormalXZ.normalized) >= facingDotMin;
        }

        public static float ResolveQuayFalloffMeters(LandformProfile landforms)
        {
            if (landforms == null)
                return QuayFalloffMeters;
            var beach = Mathf.Max(16f, landforms.OceanBeachWidthMeters * 0.35f);
            return Mathf.Clamp(beach, 24f, 80f);
        }

        private static bool TryFindOceanAlongRay(
            Vector2 origin,
            Vector2 dir,
            float maxDist,
            HydrologyPlan hydrology,
            float maxReclaimDepth,
            out float hitDist,
            out Vector2 hitPoint)
        {
            hitDist = 0f;
            hitPoint = origin;
            var step = Mathf.Max(hydrology.CellSizeMeters, 8f);
            for (var d = step; d <= maxDist; d += step)
            {
                var p = origin + dir * d;
                if (!TrySampleHydrology(hydrology, p, out var waterClass, out var depth))
                    continue;
                if (waterClass != WorldWaterClass.Ocean)
                    continue;
                if (CityPadReclaimPolicy.IsReclaimable(waterClass, depth, maxReclaimDepth))
                    continue;

                hitDist = d;
                hitPoint = p;
                return true;
            }

            return false;
        }

        private static bool TrySampleHydrology(
            HydrologyPlan hydrology,
            Vector2 worldXZ,
            out WorldWaterClass waterClass,
            out float depth)
        {
            waterClass = WorldWaterClass.None;
            depth = 0f;
            var x = Mathf.FloorToInt((worldXZ.x - hydrology.OriginXZ.x) / hydrology.CellSizeMeters);
            var z = Mathf.FloorToInt((worldXZ.y - hydrology.OriginXZ.y) / hydrology.CellSizeMeters);
            if (x < 0 || z < 0 || x >= hydrology.Width || z >= hydrology.Height)
                return false;

            var i = hydrology.Index(x, z);
            waterClass = hydrology.WaterClass[i];
            depth = hydrology.DepthMeters != null && i < hydrology.DepthMeters.Length
                ? hydrology.DepthMeters[i]
                : 0f;
            return true;
        }
    }
}
