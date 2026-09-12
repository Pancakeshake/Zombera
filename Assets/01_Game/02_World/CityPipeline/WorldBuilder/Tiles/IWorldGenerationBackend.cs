using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>Backend that owns generation session and tile streaming.</summary>
    public interface IWorldGenerationBackend
    {
        WorldTileStreamSource TileStream { get; }
        bool IsGenerating { get; }
        float Progress01 { get; }
        WorldMapSession Session { get; }

        IEnumerator InitializeSession(WorldMapSession session, WorldBuildScope initialScope);
        IEnumerator ResetScope(WorldBuildScope scope, WorldResetMode mode);
        void RequestTiles(IReadOnlyList<WorldTileCoord> coords);
        void ReleaseTiles(IReadOnlyList<WorldTileCoord> coords);
        void ReportNavigationResult(WorldTileCoord coord, bool succeeded);
        void ShutdownSession();
    }
}
