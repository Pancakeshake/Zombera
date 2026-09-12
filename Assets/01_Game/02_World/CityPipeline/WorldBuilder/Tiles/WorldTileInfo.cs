using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>Immutable snapshot of a single world tile.</summary>
    public readonly struct WorldTileInfo
    {
        public WorldTileInfo(WorldTileCoord coord, Rect worldRectXZ, Terrain terrain, WorldTileState state)
        {
            Coord = coord;
            WorldRectXZ = worldRectXZ;
            Terrain = terrain;
            State = state;
        }

        public WorldTileCoord Coord { get; }
        public Rect WorldRectXZ { get; }
        public Terrain Terrain { get; }
        public WorldTileState State { get; }

        /// <summary>Alias used by older callers expecting BoundsXZ.</summary>
        public Rect BoundsXZ => WorldRectXZ;

        public WorldTileInfo WithState(WorldTileState state) =>
            new(Coord, WorldRectXZ, Terrain, state);

        public WorldTileInfo WithTerrain(Terrain terrain) =>
            new(Coord, WorldRectXZ, terrain, State);
    }
}
