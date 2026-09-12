using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Direct regional biome weight injection from macro zone layout (structural zoning).</summary>
    internal static class BiomeMacroZonePriors
    {
        public static void ApplyMacroZoneBiomePriors(
            Rect bounds,
            float worldX,
            float worldZ,
            BiomeField field,
            Dictionary<string, int> biomeIndexById,
            int cell,
            float boostScale)
        {
            if (!BiomeMacroRegionLayout.TrySample(bounds, worldX, worldZ, out var zones))
                return;

            var scale = Mathf.Max(0.5f, boostScale);
            Boost(field, biomeIndexById, cell, zones.NwBadlands * 4f * scale, "Badlands");
            Boost(field, biomeIndexById, cell, zones.SouthDesert * 5f * scale, "Desert");
            Boost(field, biomeIndexById, cell, zones.SouthDesert * zones.SeScrub * 3f * scale, "Dunes");
            Boost(field, biomeIndexById, cell, zones.NeTaiga * 4f * scale, "Taiga");
            Boost(field, biomeIndexById, cell, zones.SeScrub * 3.5f * scale, "Scrubland");
            Boost(field, biomeIndexById, cell, zones.SeScrub * 2.5f * scale, "Savanna");
            Boost(field, biomeIndexById, cell, zones.CenterForest * 4f * scale, "Forest");
            Boost(field, biomeIndexById, cell, zones.CenterForest * 2.5f * scale, "Plains");
        }

        private static void Boost(
            BiomeField field,
            Dictionary<string, int> biomeIndexById,
            int cell,
            float amount,
            string stableId)
        {
            if (amount <= 0.01f)
                return;
            if (!biomeIndexById.TryGetValue(stableId, out var b))
                return;

            field.Weights[field.WeightIndex(cell, b)] += amount;
        }
    }
}
