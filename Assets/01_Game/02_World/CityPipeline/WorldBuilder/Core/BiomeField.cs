using System;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Per-cell biome classification and buildability fields.</summary>
    public sealed class BiomeField
    {
        public int Width { get; }
        public int Height { get; }
        public int BiomeCount { get; }
        public string[] BiomeStableIds { get; }
        public float[] Temperature { get; }
        public float[] Moisture { get; }
        public float[] Slope { get; }
        public float[] Buildability { get; }
        public bool[] NoBuild { get; }
        public float[] Weights { get; }
        public int[] DominantBiomeIndex { get; }

        public BiomeField(int width, int height, string[] biomeStableIds)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (biomeStableIds == null || biomeStableIds.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(biomeStableIds));

            Width = width;
            Height = height;
            BiomeCount = biomeStableIds.Length;
            BiomeStableIds = biomeStableIds;

            var cellCount = width * height;
            Temperature = new float[cellCount];
            Moisture = new float[cellCount];
            Slope = new float[cellCount];
            Buildability = new float[cellCount];
            NoBuild = new bool[cellCount];
            Weights = new float[cellCount * BiomeCount];
            DominantBiomeIndex = new int[cellCount];
            for (var i = 0; i < cellCount; i++)
                DominantBiomeIndex[i] = -1;
        }

        public int WeightIndex(int cellIndex, int biomeIndex) =>
            cellIndex * BiomeCount + biomeIndex;

        public bool TryGetDominantStableId(int cellIndex, out string stableId)
        {
            stableId = null;
            if (cellIndex < 0 || cellIndex >= DominantBiomeIndex.Length)
                return false;

            var index = DominantBiomeIndex[cellIndex];
            if (index < 0 || index >= BiomeStableIds.Length)
                return false;

            stableId = BiomeStableIds[index];
            return !string.IsNullOrEmpty(stableId);
        }
    }
}
