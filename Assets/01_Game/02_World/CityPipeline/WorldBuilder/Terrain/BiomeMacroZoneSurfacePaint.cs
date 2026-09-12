using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Paints MicroSplat semantics directly from macro zone weights (visual layer, not classifier).</summary>
    internal static class BiomeMacroZoneSurfacePaint
    {
        private static readonly (string semantic, float weight)[] NwBadlands =
        {
            ("CliffRed", 0.4f),
            ("BlackDirt", 0.35f),
            ("CliffDark", 0.25f)
        };

        private static readonly (string semantic, float weight)[] SouthDesert =
        {
            ("DesertSand", 0.55f),
            ("Sand", 0.25f),
            ("SandCracks", 0.2f)
        };

        private static readonly (string semantic, float weight)[] NeTaiga =
        {
            ("DryForestFloor", 0.55f),
            ("SparseGrass", 0.25f),
            ("SnowRock", 0.2f)
        };

        private static readonly (string semantic, float weight)[] SeScrub =
        {
            ("MeadowGrass", 0.35f),
            ("SparseGrass", 0.3f),
            ("BlackDirt", 0.35f)
        };

        private static readonly (string semantic, float weight)[] CenterForest =
        {
            ("JungleFloor", 0.45f),
            ("GrassGreen", 0.35f),
            ("Dirt", 0.2f)
        };

        public static bool TryAccumulate(
            Rect bounds,
            float worldX,
            float worldZ,
            WorldSurfacePainter painter,
            float[,,] map,
            int z,
            int x,
            int layers)
        {
            if (painter == null || map == null)
                return false;

            if (!BiomeMacroRegionLayout.TrySample(bounds, worldX, worldZ, out var zones))
                return false;

            var total = zones.NwBadlands + zones.SouthDesert + zones.NeTaiga +
                        zones.SeScrub + zones.CenterForest;
            if (total < 0.04f)
                return false;

            var inv = 1f / total;
            AccumulateStack(painter, map, z, x, layers, zones.NwBadlands * inv, NwBadlands);
            AccumulateStack(painter, map, z, x, layers, zones.SouthDesert * inv, SouthDesert);
            AccumulateStack(painter, map, z, x, layers, zones.NeTaiga * inv, NeTaiga);
            AccumulateStack(painter, map, z, x, layers, zones.SeScrub * inv, SeScrub);
            AccumulateStack(painter, map, z, x, layers, zones.CenterForest * inv, CenterForest);
            return true;
        }

        private static void AccumulateStack(
            WorldSurfacePainter painter,
            float[,,] map,
            int z,
            int x,
            int layers,
            float zoneStrength,
            (string semantic, float weight)[] stack)
        {
            if (zoneStrength <= 0.001f)
                return;

            for (var i = 0; i < stack.Length; i++)
            {
                var entry = stack[i];
                painter.AddLayerForMacro(map, z, x, layers, entry.semantic, zoneStrength * entry.weight);
            }
        }
    }
}
