using System.Collections.Generic;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Derives independent subsystem seeds via <see cref="StableHash64"/>.</summary>
    public static class WorldSubsystemSeeds
    {
        public const string Landforms = "landforms";
        public const string Hydrology = "hydrology";
        public const string Biomes = "biomes";
        public const string Surfaces = "surfaces";
        public const string Sites = "city-sites";
        public const string Roads = "roads";
        public const string Nature = "nature";
        public const string Pois = "pois";
        public const string Weather = "weather";

        private static readonly string[] DefaultNames =
        {
            Landforms, Hydrology, Biomes, Surfaces, Sites, Roads, Nature, Pois, Weather
        };

        public static Dictionary<string, int> Create(int worldSeed, int profileVersion)
        {
            var seeds = new Dictionary<string, int>(DefaultNames.Length);
            for (var i = 0; i < DefaultNames.Length; i++)
            {
                var name = DefaultNames[i];
                seeds[name] = Derive(worldSeed, profileVersion, name);
            }

            return seeds;
        }

        public static int Derive(int worldSeed, int profileVersion, string subsystemName)
        {
            var hasher = new StableHash64();
            hasher.Append(worldSeed);
            hasher.Append(profileVersion);
            hasher.Append(subsystemName ?? string.Empty);
            return unchecked((int)hasher.Finalize());
        }

        public static ulong Fingerprint(
            WorldMapSession session,
            float cellSizeMeters,
            int width,
            int height,
            IReadOnlyDictionary<string, int> seeds)
        {
            var hasher = new StableHash64();
            hasher.Append(session.Seed);
            hasher.Append(session.ProfileVersion);
            hasher.Append((int)session.Tier);
            hasher.Append(session.TilesPerSide);
            hasher.Append(session.TileSizeMeters);
            hasher.Append(session.WorldOriginXZ.x);
            hasher.Append(session.WorldOriginXZ.y);
            hasher.Append(session.WorldBoundsXZ.width);
            hasher.Append(session.WorldBoundsXZ.height);
            hasher.Append(cellSizeMeters);
            hasher.Append(width);
            hasher.Append(height);

            if (seeds != null)
            {
                foreach (var pair in seeds)
                {
                    hasher.Append(pair.Key);
                    hasher.Append(pair.Value);
                }
            }

            return hasher.Finalize();
        }
    }
}
