using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Seed-stable macro zone weights with soft radial falloff (reference-map style regions).</summary>
    internal static class BiomeMacroRegionLayout
    {
        internal struct ZoneWeights
        {
            public float NwBadlands;
            public float SouthDesert;
            public float NeTaiga;
            public float SeScrub;
            public float CenterForest;
        }

        public static bool TrySample(Rect bounds, float worldX, float worldZ, out ZoneWeights weights)
        {
            weights = default;
            if (bounds.width <= 1f || bounds.height <= 1f)
                return false;

            var u = Mathf.Clamp01((worldX - bounds.xMin) / bounds.width);
            var v = Mathf.Clamp01((worldZ - bounds.yMin) / bounds.height);

            weights.NwBadlands = RadialZone(u, v, 0.18f, 0.72f, 0.34f);
            weights.SouthDesert = RadialZone(u, v, 0.5f, 0.14f, 0.38f);
            weights.NeTaiga = RadialZone(u, v, 0.78f, 0.68f, 0.32f);
            weights.SeScrub = RadialZone(u, v, 0.74f, 0.22f, 0.3f);
            weights.CenterForest = RadialZone(u, v, 0.46f, 0.46f, 0.36f);
            return true;
        }

        private static float RadialZone(float u, float v, float centerU, float centerV, float radius)
        {
            var du = (u - centerU) / Mathf.Max(0.08f, radius);
            var dv = (v - centerV) / Mathf.Max(0.08f, radius);
            var dist = Mathf.Sqrt(du * du + dv * dv);
            return 1f - Mathf.SmoothStep(0.35f, 1f, dist);
        }
    }
}
