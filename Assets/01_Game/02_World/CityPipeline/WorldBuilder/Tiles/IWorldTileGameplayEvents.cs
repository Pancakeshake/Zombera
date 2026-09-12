using System;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>
    ///     City-visible tile-apply events. Implemented by first-party
    ///     <see cref="WorldTileCatalog"/> (ContentReady transitions).
    /// </summary>
    public interface IWorldTileGameplayEvents
    {
        event Action<WorldTileInfo> TileAppliedForGameplay;
    }
}
