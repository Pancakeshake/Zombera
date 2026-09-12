namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>Optional disk/memory cache keyed by seed, profile and tile coord.</summary>
    public interface IWorldTileCache
    {
        bool TryLoad(WorldTileCoord coord, ulong fingerprint, out byte[] payload);
        void Store(WorldTileCoord coord, ulong fingerprint, byte[] payload);
        void Invalidate(WorldTileCoord coord);
        void Clear();
    }
}
