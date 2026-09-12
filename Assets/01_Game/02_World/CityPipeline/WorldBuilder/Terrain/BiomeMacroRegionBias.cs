using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Macro-zone climate offsets derived from soft regional weights.</summary>
    internal static class BiomeMacroRegionBias
    {
        public static void ApplyMacroClimateOffsets(
            Rect bounds,
            float worldX,
            float worldZ,
            out float tempOffset,
            out float moistOffset)
        {
            tempOffset = 0f;
            moistOffset = 0f;
            if (!BiomeMacroRegionLayout.TrySample(bounds, worldX, worldZ, out var zones))
                return;

            tempOffset += zones.NwBadlands * 0.22f;
            moistOffset -= zones.NwBadlands * 0.55f;

            tempOffset += zones.SouthDesert * 0.48f;
            moistOffset -= zones.SouthDesert * 0.72f;

            moistOffset += zones.NeTaiga * 0.58f;
            tempOffset -= zones.NeTaiga * 0.32f;

            tempOffset += zones.SeScrub * 0.18f;
            moistOffset -= zones.SeScrub * 0.42f;

            moistOffset += zones.CenterForest * 0.35f;
            tempOffset -= zones.CenterForest * 0.08f;
        }
    }
}
