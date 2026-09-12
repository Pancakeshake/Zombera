using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Core (0–12) and extended natural (17–31) alphamap layer helpers.</summary>
    public sealed partial class WorldSurfacePainter
    {
        private const int CoreNaturalLayerCount = 13;

        private static bool IsNaturalPaintLayer(int layer) =>
            layer >= 0 &&
            (layer < WorldSurfacePalette.InfrastructureMinIndex ||
             (layer >= WorldSurfacePalette.ExtendedNaturalMinIndex &&
              layer <= WorldSurfacePalette.ExtendedNaturalMaxIndex));

        private static void ClearNatural(float[,,] map, int z, int x, int layers)
        {
            var coreMax = Mathf.Min(layers, CoreNaturalLayerCount);
            for (var l = 0; l < coreMax; l++)
                map[z, x, l] = 0f;

            var extMax = Mathf.Min(layers - 1, WorldSurfacePalette.ExtendedNaturalMaxIndex);
            for (var l = WorldSurfacePalette.ExtendedNaturalMinIndex; l <= extMax; l++)
                map[z, x, l] = 0f;
        }

        private static void NormalizeNatural(float[,,] map, int z, int x, int layers)
        {
            var sum = SumNaturalLayers(map, z, x, layers);
            if (sum <= 1e-5f)
            {
                if (layers > 0)
                    map[z, x, 0] = 1f;
                return;
            }

            var inv = 1f / sum;
            var coreMax = Mathf.Min(layers, CoreNaturalLayerCount);
            for (var l = 0; l < coreMax; l++)
                map[z, x, l] *= inv;

            var extMax = Mathf.Min(layers - 1, WorldSurfacePalette.ExtendedNaturalMaxIndex);
            for (var l = WorldSurfacePalette.ExtendedNaturalMinIndex; l <= extMax; l++)
                map[z, x, l] *= inv;
        }

        private static float SumNaturalLayers(float[,,] map, int z, int x, int layers)
        {
            var sum = 0f;
            var coreMax = Mathf.Min(layers, CoreNaturalLayerCount);
            for (var l = 0; l < coreMax; l++)
                sum += map[z, x, l];

            var extMax = Mathf.Min(layers - 1, WorldSurfacePalette.ExtendedNaturalMaxIndex);
            for (var l = WorldSurfacePalette.ExtendedNaturalMinIndex; l <= extMax; l++)
                sum += map[z, x, l];
            return sum;
        }

        private static void ScaleNaturalExceptLayers(
            float[,,] map,
            int z,
            int x,
            int layers,
            float keep,
            int exceptA,
            int exceptB,
            int exceptC)
        {
            var coreMax = Mathf.Min(layers, CoreNaturalLayerCount);
            for (var l = 0; l < coreMax; l++)
            {
                if (l == exceptA || l == exceptB || l == exceptC)
                    continue;
                map[z, x, l] *= keep;
            }

            var extMax = Mathf.Min(layers - 1, WorldSurfacePalette.ExtendedNaturalMaxIndex);
            for (var l = WorldSurfacePalette.ExtendedNaturalMinIndex; l <= extMax; l++)
            {
                if (l == exceptA || l == exceptB || l == exceptC)
                    continue;
                map[z, x, l] *= keep;
            }
        }
    }
}
