using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public enum WorldMapBoundaryKind
    {
        Ocean = 0,
        Mountains = 1
    }

    public enum WorldMapEdgeSide
    {
        West = 0,
        East = 1,
        South = 2,
        North = 3
    }

    /// <summary>
    /// Map-edge boundary kinds. When <see cref="LandformProfile.ForceOceanOnAllEdges"/> is false,
    /// each edge independently rolls Ocean vs Mountains from the world seed.
    /// Unless <see cref="LandformProfile.AllowLandlockedMaps"/> is true, at least one ocean edge is forced.
    /// </summary>
    public readonly struct WorldMapBoundaryLayout
    {
        public WorldMapBoundaryKind West { get; }
        public WorldMapBoundaryKind East { get; }
        public WorldMapBoundaryKind South { get; }
        public WorldMapBoundaryKind North { get; }

        public WorldMapBoundaryLayout(
            WorldMapBoundaryKind west,
            WorldMapBoundaryKind east,
            WorldMapBoundaryKind south,
            WorldMapBoundaryKind north)
        {
            West = west;
            East = east;
            South = south;
            North = north;
        }

        public WorldMapBoundaryKind Get(WorldMapEdgeSide side) => side switch
        {
            WorldMapEdgeSide.West => West,
            WorldMapEdgeSide.East => East,
            WorldMapEdgeSide.South => South,
            _ => North
        };

        public int OceanEdgeCount
        {
            get
            {
                var n = 0;
                if (West == WorldMapBoundaryKind.Ocean) n++;
                if (East == WorldMapBoundaryKind.Ocean) n++;
                if (South == WorldMapBoundaryKind.Ocean) n++;
                if (North == WorldMapBoundaryKind.Ocean) n++;
                return n;
            }
        }

        public static WorldMapBoundaryLayout AllOcean { get; } = new(
            WorldMapBoundaryKind.Ocean,
            WorldMapBoundaryKind.Ocean,
            WorldMapBoundaryKind.Ocean,
            WorldMapBoundaryKind.Ocean);

        public static WorldMapBoundaryLayout AllMountains { get; } = new(
            WorldMapBoundaryKind.Mountains,
            WorldMapBoundaryKind.Mountains,
            WorldMapBoundaryKind.Mountains,
            WorldMapBoundaryKind.Mountains);

        /// <summary>
        /// Resolves per-edge ocean/mountain kinds from profile + session seed.
        /// </summary>
        public static WorldMapBoundaryLayout Resolve(WorldMapSession session, LandformProfile landformProfile)
        {
            if (landformProfile == null || landformProfile.ForceOceanOnAllEdges)
                return AllOcean;

            var seed = session.Seed;
            var chance = Mathf.Clamp01(landformProfile.OceanEdgeChance);
            var layout = new WorldMapBoundaryLayout(
                RollEdge(seed, WorldMapEdgeSide.West, chance),
                RollEdge(seed, WorldMapEdgeSide.East, chance),
                RollEdge(seed, WorldMapEdgeSide.South, chance),
                RollEdge(seed, WorldMapEdgeSide.North, chance));

            if (landformProfile.AllowLandlockedMaps || layout.OceanEdgeCount > 0)
                return layout;

            return ForceOneOceanEdge(seed, layout);
        }

        private static WorldMapBoundaryLayout ForceOneOceanEdge(int worldSeed, WorldMapBoundaryLayout layout)
        {
            var side = (WorldMapEdgeSide)(Mathf.Abs(new DeterministicRng(worldSeed ^ unchecked(0x0CEA71D)).NextInt()) % 4);
            return side switch
            {
                WorldMapEdgeSide.West => new WorldMapBoundaryLayout(
                    WorldMapBoundaryKind.Ocean, layout.East, layout.South, layout.North),
                WorldMapEdgeSide.East => new WorldMapBoundaryLayout(
                    layout.West, WorldMapBoundaryKind.Ocean, layout.South, layout.North),
                WorldMapEdgeSide.South => new WorldMapBoundaryLayout(
                    layout.West, layout.East, WorldMapBoundaryKind.Ocean, layout.North),
                _ => new WorldMapBoundaryLayout(
                    layout.West, layout.East, layout.South, WorldMapBoundaryKind.Ocean)
            };
        }

        private static WorldMapBoundaryKind RollEdge(int worldSeed, WorldMapEdgeSide side, float oceanChance)
        {
            if (oceanChance <= 0f)
                return WorldMapBoundaryKind.Mountains;
            if (oceanChance >= 1f)
                return WorldMapBoundaryKind.Ocean;

            var rng = new DeterministicRng(worldSeed ^ unchecked(0x0CEA7E00 + (int)side * 7919));
            return rng.NextFloat01() < oceanChance
                ? WorldMapBoundaryKind.Ocean
                : WorldMapBoundaryKind.Mountains;
        }
    }
}
