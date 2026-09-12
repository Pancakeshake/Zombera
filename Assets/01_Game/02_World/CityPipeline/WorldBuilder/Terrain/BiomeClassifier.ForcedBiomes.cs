using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Post-classification biome boosts for hydrology, elevation, and climate edge cases.</summary>
    public static partial class BiomeClassifier
    {
        private static void ApplyForcedBiomes(in ForcedBiomeBoostArgs args)
        {
            ApplyWetBiomeBoosts(in args);
            ApplyTerrainBiomeBoosts(in args);
            ApplyTemperateBiomeBoosts(in args);
            ApplyAridBiomeBoosts(in args);
            ApplyColdHighBiomeBoosts(in args);
            ApplyAshlandsBoost(in args);
        }

        private static void ApplyWetBiomeBoosts(in ForcedBiomeBoostArgs args)
        {
            var s = args.Scalars;
            var temp = args.Field.Temperature[args.Cell];
            var moist = args.Field.Moisture[args.Cell];

            if (moist > 0.84f && s.WaterDist < s.ShoreBand * 1.1f && temp <= 0.52f)
                BoostBiome(in args, 3f, "Wetland");

            if (moist > 0.78f && temp > 0.45f && s.ElevNorm < 0.25f && s.Slope < 22f)
                BoostBiome(in args, 4f, "Swamp");
        }

        private static void ApplyTerrainBiomeBoosts(in ForcedBiomeBoostArgs args)
        {
            var s = args.Scalars;
            if (s.ElevNorm >= 0.25f && s.ElevNorm <= 0.55f && s.Slope >= 8f && s.Slope <= 25f)
                BoostBiome(in args, 4f, "Hills");

            if (s.ElevNorm > 0.55f || s.Slope > 25f)
                BoostBiome(in args, 5f, "Mountains");
        }

        private static void ApplyTemperateBiomeBoosts(in ForcedBiomeBoostArgs args)
        {
            var s = args.Scalars;
            var temp = args.Field.Temperature[args.Cell];
            var moist = args.Field.Moisture[args.Cell];

            if (s.ElevNorm < 0.48f && s.Slope < 16f &&
                moist > 0.34f && moist < 0.52f &&
                temp > 0.34f && temp < 0.68f)
                BoostBiome(in args, 3f, "Plains");

            if (temp > 0.68f && moist >= 0.18f && moist <= 0.38f &&
                s.ElevNorm < 0.42f && s.Slope < 18f && s.WaterDist > s.ShoreBand * 1.5f)
                BoostBiome(in args, 5f, "Savanna");

            if (temp > 0.55f && moist >= 0.22f && moist <= 0.38f &&
                s.Slope >= 12f && s.Slope <= 28f && s.ElevNorm < 0.55f)
                BoostBiome(in args, 3.5f, "Scrubland");

            if (s.WaterDist > s.ShoreBand * 1.5f &&
                s.ElevNorm < 0.52f &&
                s.Slope < 22f &&
                moist > 0.42f &&
                temp >= 0.36f &&
                temp <= 0.68f)
                BoostBiome(in args, 3f, "Forest");

            if (moist > 0.48f &&
                temp >= 0.38f &&
                temp <= 0.68f &&
                s.Slope < 24f &&
                s.ElevNorm < 0.52f)
                BoostBiome(in args, 2.5f, "Forest");

            if (temp < 0.35f &&
                moist >= 0.35f &&
                moist <= 0.65f &&
                s.ElevNorm < 0.55f &&
                s.Slope < 24f)
                BoostBiome(in args, 4.5f, "Taiga");

            if (moist > 0.55f &&
                temp >= 0.35f &&
                temp <= 0.7f &&
                s.Slope < 22f)
                BoostBiome(in args, 2f, "Forest");
        }

        private static void ApplyAridBiomeBoosts(in ForcedBiomeBoostArgs args)
        {
            var s = args.Scalars;
            var temp = args.Field.Temperature[args.Cell];
            var moist = args.Field.Moisture[args.Cell];

            if (temp <= 0.74f || moist >= 0.2f || s.ElevNorm >= 0.32f ||
                s.WaterDist <= s.ShoreBand * 2.5f)
                return;

            if (s.Slope >= 18f && s.ElevNorm >= 0.28f)
                BoostBiome(in args, 3.5f, "Badlands");
            else if (s.Slope >= 8f && s.Slope < 22f && s.ElevNorm < 0.38f)
                BoostBiome(in args, 3.25f, "Dunes");
            else if (s.Slope < 12f)
                BoostBiome(in args, 3f, "Desert");
        }

        private static void ApplyColdHighBiomeBoosts(in ForcedBiomeBoostArgs args)
        {
            var s = args.Scalars;
            var temp = args.Field.Temperature[args.Cell];

            if (s.Elev <= s.SeaLevel + 480f || s.ElevNorm <= 0.64f)
                return;

            if (s.ElevNorm > 0.74f || (s.Elev > s.SeaLevel + 620f && s.ElevNorm > 0.78f))
            {
                BoostBiome(in args, 7f, "AlpineSnow");
                return;
            }

            if (s.Slope < 24f && s.ElevNorm > 0.66f)
                BoostBiome(in args, 6f, "Snow");

            if (s.Slope > 28f && s.ElevNorm > 0.72f)
                BoostBiome(in args, 5f, "Snow");

            if (temp < 0.2f && s.Elev > s.SeaLevel + 500f && s.Slope > 20f)
                BoostBiome(in args, 4f, "Snow");
        }

        private static void ApplyAshlandsBoost(in ForcedBiomeBoostArgs args)
        {
            var s = args.Scalars;
            var temp = args.Field.Temperature[args.Cell];
            var moist = args.Field.Moisture[args.Cell];

            if (temp < 0.38f || temp > 0.62f)
                return;
            if (moist > 0.28f)
                return;
            if (s.ElevNorm < 0.32f || s.ElevNorm > 0.68f)
                return;
            if (s.Slope < 10f || s.Slope > 36f)
                return;

            BoostBiome(in args, 4f, "Ashlands");
        }

        private static void BoostBiome(in ForcedBiomeBoostArgs args, float amount, string stableId)
        {
            if (!args.BiomeIndexById.TryGetValue(stableId, out var b))
                return;

            args.Field.Weights[args.Field.WeightIndex(args.Cell, b)] +=
                amount * Mathf.Max(0.5f, args.Scalars.BoostScale);
        }
    }
}
