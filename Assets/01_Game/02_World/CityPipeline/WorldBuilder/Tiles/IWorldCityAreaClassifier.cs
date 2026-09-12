using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>
    ///     Classifies whether a world tile should receive city-area generation.
    ///     MapMagic biome-mask implementation lives in Legacy.
    /// </summary>
    public interface IWorldCityAreaClassifier
    {
        bool TryGetTileCenterXZ(WorldTileInfo tile, out Vector2 centerXZ);

        bool IsCityAreaTile(
            WorldTileInfo tile,
            float dominanceThreshold,
            out float dominantMask,
            out string diagnosticSummary);
    }
}
