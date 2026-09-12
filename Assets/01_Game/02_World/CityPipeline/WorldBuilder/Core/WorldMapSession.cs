using UnityEngine;
using Zombera.Core;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Construction args for <see cref="WorldMapSession"/> (keeps the ctor under Sonar S107).</summary>
    public readonly struct WorldMapSessionArgs
    {
        public WorldMapSizeTier Tier { get; init; }
        public int Seed { get; init; }
        public int ProfileVersion { get; init; }
        public Vector2 WorldOriginXZ { get; init; }
        public int TilesPerSide { get; init; }
        public float TileSizeMeters { get; init; }
        public Rect WorldBoundsXZ { get; init; }
        public Rect CoreWorldBoundsXZ { get; init; }
        public int OceanRingTiles { get; init; }
    }

    /// <summary>Immutable identity of a generated world map session.</summary>
    public readonly struct WorldMapSession
    {
        public WorldMapSizeTier Tier { get; }
        public int Seed { get; }
        public int ProfileVersion { get; }
        public Vector2 WorldOriginXZ { get; }
        public int TilesPerSide { get; }
        public float TileSizeMeters { get; }
        public Rect WorldBoundsXZ { get; }

        /// <summary>
        ///     Playable / settlement interior. Equals <see cref="WorldBoundsXZ"/> when no ocean ring.
        /// </summary>
        public Rect CoreWorldBoundsXZ { get; }

        public int OceanRingTiles { get; }

        public WorldMapSession(in WorldMapSessionArgs args)
        {
            Tier = args.Tier;
            Seed = args.Seed;
            ProfileVersion = args.ProfileVersion;
            WorldOriginXZ = args.WorldOriginXZ;
            TilesPerSide = args.TilesPerSide;
            TileSizeMeters = args.TileSizeMeters;
            WorldBoundsXZ = args.WorldBoundsXZ;
            OceanRingTiles = Mathf.Max(0, args.OceanRingTiles);
            CoreWorldBoundsXZ = args.CoreWorldBoundsXZ.width > 0f && args.CoreWorldBoundsXZ.height > 0f
                ? args.CoreWorldBoundsXZ
                : args.WorldBoundsXZ;
        }

        public static WorldMapSession Create(
            WorldMapSizeTier tier,
            int seed,
            int profileVersion,
            Vector2 originXZ,
            int tilesPerSide,
            float tileSizeMeters)
        {
            var sideMeters = tilesPerSide * tileSizeMeters;
            var bounds = new Rect(originXZ.x, originXZ.y, sideMeters, sideMeters);
            return new WorldMapSession(new WorldMapSessionArgs
            {
                Tier = tier,
                Seed = seed,
                ProfileVersion = profileVersion,
                WorldOriginXZ = originXZ,
                TilesPerSide = tilesPerSide,
                TileSizeMeters = tileSizeMeters,
                WorldBoundsXZ = bounds,
                CoreWorldBoundsXZ = bounds,
                OceanRingTiles = 0
            });
        }

        /// <summary>
        ///     Builds a session whose full grid includes an ocean-ring shelf around a core playable square.
        /// </summary>
        public static WorldMapSession CreateWithOceanRing(
            WorldMapSizeTier tier,
            int seed,
            int profileVersion,
            Vector2 coreOriginXZ,
            int coreTilesPerSide,
            int oceanRingTiles,
            float tileSizeMeters)
        {
            var coreTiles = Mathf.Max(1, coreTilesPerSide);
            var ring = Mathf.Max(0, oceanRingTiles);
            var tileSize = tileSizeMeters > 0f ? tileSizeMeters : WorldMapSizeSettings.TileSizeMeters;
            var fullTiles = coreTiles + ring * 2;
            var origin = new Vector2(
                coreOriginXZ.x - ring * tileSize,
                coreOriginXZ.y - ring * tileSize);
            var fullSide = fullTiles * tileSize;
            var coreSide = coreTiles * tileSize;
            var fullBounds = new Rect(origin.x, origin.y, fullSide, fullSide);
            var coreBounds = new Rect(coreOriginXZ.x, coreOriginXZ.y, coreSide, coreSide);
            return new WorldMapSession(new WorldMapSessionArgs
            {
                Tier = tier,
                Seed = seed,
                ProfileVersion = profileVersion,
                WorldOriginXZ = origin,
                TilesPerSide = fullTiles,
                TileSizeMeters = tileSize,
                WorldBoundsXZ = fullBounds,
                CoreWorldBoundsXZ = coreBounds,
                OceanRingTiles = ring
            });
        }
    }
}
