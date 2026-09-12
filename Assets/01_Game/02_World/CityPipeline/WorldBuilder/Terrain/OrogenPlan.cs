using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Compact orogen metadata produced with landforms (ranges + pass corridors).
    /// Not a full structure tensor — used by validation, sites, and road cost bias.
    /// </summary>
    public sealed class OrogenPlan
    {
        public InteriorLandformRelief.MountainRange[] Ranges { get; }
        public Vector2[] PassCentersXZ { get; }

        public OrogenPlan(
            InteriorLandformRelief.MountainRange[] ranges,
            Vector2[] passCentersXZ)
        {
            Ranges = ranges ?? System.Array.Empty<InteriorLandformRelief.MountainRange>();
            PassCentersXZ = passCentersXZ ?? System.Array.Empty<Vector2>();
        }

        public float SampleOrogenCoreMask(float worldX, float worldZ)
        {
            if (Ranges.Length == 0)
                return 0f;

            var best = 0f;
            for (var i = 0; i < Ranges.Length; i++)
            {
                InteriorLandformRelief.SampleRangeMasks(
                    worldX,
                    worldZ,
                    Ranges[i],
                    out var core,
                    out _);
                if (core > best)
                    best = core;
            }

            return best;
        }

        public float SamplePassAttract(float worldX, float worldZ, float halfWidthMeters)
        {
            if (PassCentersXZ.Length == 0 || halfWidthMeters <= 1f)
                return 0f;

            var point = new Vector2(worldX, worldZ);
            var best = 0f;
            var inv = 1f / halfWidthMeters;
            for (var i = 0; i < PassCentersXZ.Length; i++)
            {
                var d = Vector2.Distance(point, PassCentersXZ[i]) * inv;
                var t = 1f - Mathf.Clamp01(d);
                t = t * t * (3f - 2f * t);
                if (t > best)
                    best = t;
            }

            return best;
        }
    }
}
