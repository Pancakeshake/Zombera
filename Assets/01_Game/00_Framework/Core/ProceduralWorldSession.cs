using UnityEngine;
using Zombera.Core;

namespace Zombera.World
{
    /// <summary>
    ///     Deterministic session contract for finite World Builder (and Legacy) worlds.
    ///     Lives where Core/World consumers can read seed without cycles.
    /// </summary>
    public static class ProceduralWorldSession
    {
        public const string SingleTileStressGraphVersion = "SingleTileStress";
        public const string FirstPartyGraphVersion = "WorldBuilder";

        public static bool IsActive { get; private set; }
        public static int WorldSeed { get; private set; }
        public static string GraphVersion { get; private set; } = string.Empty;
        public static WorldMapSizeTier MapSizeTier { get; private set; } = WorldMapSizeTier.Medium;
        public static int TilesPerSide { get; private set; }
        public static int OriginTileX { get; private set; }
        public static int OriginTileZ { get; private set; }
        public static int ProfileVersion { get; private set; } = 1;
        public static ulong PlanFingerprint { get; private set; }

        public static void Begin(int worldSeed, string graphVersion)
        {
            Begin(worldSeed, graphVersion, WorldMapSizeTier.Medium, 8, 0, 0, 1, 0);
        }

        public static void Begin(
            int worldSeed,
            string graphVersion,
            WorldMapSizeTier tier,
            int tilesPerSide,
            int originTileX,
            int originTileZ,
            int profileVersion,
            ulong planFingerprint)
        {
            WorldSeed = worldSeed;
            GraphVersion = graphVersion ?? string.Empty;
            MapSizeTier = tier;
            TilesPerSide = tilesPerSide;
            OriginTileX = originTileX;
            OriginTileZ = originTileZ;
            ProfileVersion = profileVersion;
            PlanFingerprint = planFingerprint;
            IsActive = true;
            Random.InitState(worldSeed);
        }

        public static void Clear()
        {
            IsActive = false;
            WorldSeed = 0;
            GraphVersion = string.Empty;
            MapSizeTier = WorldMapSizeTier.Medium;
            TilesPerSide = 0;
            OriginTileX = 0;
            OriginTileZ = 0;
            ProfileVersion = 1;
            PlanFingerprint = 0;
        }

        public static void UpdatePlanFingerprint(ulong planFingerprint)
        {
            PlanFingerprint = planFingerprint;
        }

        public static bool IsFirstPartySession() =>
            IsActive && string.Equals(GraphVersion, FirstPartyGraphVersion, System.StringComparison.Ordinal);

        public static bool IsSingleTileStressSession()
        {
            return IsActive
                   && string.Equals(GraphVersion, SingleTileStressGraphVersion,
                       System.StringComparison.OrdinalIgnoreCase);
        }

        public static Vector2Int WorldPositionToChunk(Vector3 worldPosition, int chunkSize)
        {
            var safeChunkSize = Mathf.Max(1, chunkSize);
            var chunkX = Mathf.FloorToInt(worldPosition.x / safeChunkSize);
            var chunkZ = Mathf.FloorToInt(worldPosition.z / safeChunkSize);
            return new Vector2Int(chunkX, chunkZ);
        }
    }
}
