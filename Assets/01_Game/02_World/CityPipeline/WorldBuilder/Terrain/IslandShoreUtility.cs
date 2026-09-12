using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Local beach / shallow-shelf shaping for island and archipelago pads
    /// (map-edge <see cref="WorldMapBoundaryUtility"/> coasts do not follow island contours).
    /// </summary>
    public static class IslandShoreUtility
    {
        /// <summary>
        ///     When <paramref name="islandMask"/> dominates, reshape heights into a short beach
        ///     ramp then a mild offshore shelf. Does not apply map-edge trench depths.
        /// </summary>
        public static float ApplyIslandShore(
            float height,
            float seaLevel,
            float landMask,
            float islandMask,
            float mainlandMask,
            float beachWidthInfluence,
            float beachMaxElevationMeters,
            float worldX = 0f,
            float worldZ = 0f,
            float shelfReefNoiseScaleMeters = 180f,
            float shelfReefNoiseAmplitudeMeters = 0f,
            float shelfReefCoverage = 0.32f,
            DeterministicNoise2D reefNoise = null)
        {
            // Mainland owns the cell — map-edge coast handles beaches there.
            if (islandMask < 0.05f || mainlandMask > 0.55f)
                return height;

            var blend = Mathf.Clamp01(islandMask * (1f - mainlandMask));
            if (blend < 0.01f)
                return height;

            var beachMax = Mathf.Max(1f, beachMaxElevationMeters);
            // landMask acts as a cheap SDF stand-in: 0 wet → 1 dry pad.
            // Beach band occupies the upper transition; shelf the lower.
            var beachLo = 0.28f;
            var beachHi = Mathf.Lerp(0.55f, 0.72f, Mathf.Clamp01(beachWidthInfluence / 120f));

            float shaped;
            if (landMask >= beachHi)
            {
                // Dry island pad: keep height but enforce a soft dry shoulder.
                shaped = Mathf.Max(height, seaLevel + beachMax * 0.65f);
            }
            else if (landMask >= beachLo)
            {
                var t = Smooth01(Mathf.InverseLerp(beachLo, beachHi, landMask));
                var beachH = Mathf.Lerp(seaLevel, seaLevel + beachMax, t);
                // Cap tall noise on the beach face; allow inland pads to stay taller once dry.
                shaped = Mathf.Lerp(Mathf.Min(height, beachH + 2f), beachH, 1f - t * 0.35f);
                shaped = Mathf.Lerp(height, shaped, 0.85f);
            }
            else
            {
                // Mild shelf around islands (not deep trench).
                var wetT = 1f - Mathf.Clamp01(landMask / beachLo);
                var shelf = seaLevel - Mathf.Lerp(1.5f, 14f, wetT);
                shaped = Mathf.Lerp(height, shelf, Mathf.Clamp01(wetT * 0.92f));
                shaped = CoastalShelfReefUtility.ApplyDeepen(
                    shaped,
                    seaLevel,
                    worldX,
                    worldZ,
                    shelfReefNoiseScaleMeters,
                    shelfReefNoiseAmplitudeMeters,
                    shelfReefCoverage,
                    reefNoise);
            }

            return Mathf.Lerp(height, shaped, blend);
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
