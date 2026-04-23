using UnityEngine;

namespace Zombera.World
{
    /// <summary>
    /// Deterministic session contract shared by MapMagic terrain, chunk streaming, and simulation.
    /// Reset when leaving play mode or starting a new world session.
    /// </summary>
    public static class ProceduralWorldSession
    {
        public static bool IsActive { get; private set; }
        public static int WorldSeed { get; private set; }
        public static string GraphVersion { get; private set; } = string.Empty;

        public static void Begin(int worldSeed, string graphVersion)
        {
            WorldSeed = worldSeed;
            GraphVersion = graphVersion ?? string.Empty;
            IsActive = true;
            Random.InitState(worldSeed);
        }

        public static void Clear()
        {
            IsActive = false;
            WorldSeed = 0;
            GraphVersion = string.Empty;
        }

        /// <summary>
        /// Converts world XZ to gameplay chunk indices using the same floor convention as <see cref="ChunkLoader"/>.
        /// </summary>
        public static Vector2Int WorldPositionToChunk(Vector3 worldPosition, int chunkSize)
        {
            int safeChunkSize = Mathf.Max(1, chunkSize);
            int chunkX = Mathf.FloorToInt(worldPosition.x / safeChunkSize);
            int chunkZ = Mathf.FloorToInt(worldPosition.z / safeChunkSize);
            return new Vector2Int(chunkX, chunkZ);
        }
    }
}
