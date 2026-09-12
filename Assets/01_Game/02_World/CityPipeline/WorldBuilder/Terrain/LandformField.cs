using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Global planning-grid landform heights in world meters.</summary>
    public sealed class LandformField
    {
        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }
        public Vector2 OriginXZ { get; }
        public float[] WorldHeights { get; }

        /// <summary>Alias for older stub callers.</summary>
        public float[] Heights => WorldHeights;

        public LandformField(int width, int height)
            : this(width, height, 16f, Vector2.zero)
        {
        }

        public LandformField(int width, int height, float cellSize, Vector2 originXZ)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            CellSize = Mathf.Max(0.01f, cellSize);
            OriginXZ = originXZ;
            WorldHeights = new float[Width * Height];
        }

        public int Index(int x, int z) => z * Width + x;

        public bool TryGetHeight(int x, int z, out float worldY)
        {
            worldY = 0f;
            if (x < 0 || z < 0 || x >= Width || z >= Height) return false;
            worldY = WorldHeights[Index(x, z)];
            return true;
        }

        public void SetHeight(int x, int z, float worldY)
        {
            if (x < 0 || z < 0 || x >= Width || z >= Height) return;
            WorldHeights[Index(x, z)] = worldY;
        }

        public Vector2 CellCenterXZ(int x, int z) =>
            OriginXZ + new Vector2((x + 0.5f) * CellSize, (z + 0.5f) * CellSize);
    }
}
