using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>
    ///     Vendor-agnostic pinned-tile queries for editor/runtime authoring.
    ///     MapMagic pinned-tile implementation lives in Legacy.
    /// </summary>
    public interface IWorldPinnedTileQuery
    {
        bool TryGetPinnedTerrains(out List<Terrain> terrains);
        bool TryGetPinnedTileCenterXZ(out Vector2 centerXZ);
        bool TryGetPinnedTilesWorldBounds(out Rect worldBoundsXZ, out int tileCount);
    }
}
