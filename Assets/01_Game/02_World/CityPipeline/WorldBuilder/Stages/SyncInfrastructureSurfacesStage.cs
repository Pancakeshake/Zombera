using System.Collections;
using System.Collections.Generic;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Paints infrastructure alphamap overlays and flushes surface sync backends.</summary>
    public sealed class SyncInfrastructureSurfacesStage : WorldBuildStageBase
    {
        private readonly List<WorldTileCoord> _tiles = new(64);

        public SyncInfrastructureSurfacesStage() : base(WorldBuildStageId.SyncInfrastructureSurfaces)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            context?.Cancellation?.ThrowIfRequested();
            context?.Progress?.Report(Descriptor.Id, 0.05f, "Syncing infrastructure surfaces");

            var painter = context?.WorldBuilder?.SurfacePainter;
            var catalog = context?.WorldBuilder?.TileCatalog;
            if (painter == null || catalog == null)
            {
                context?.Progress?.Report(Descriptor.Id, 1f, "Surface sync skipped");
                yield break;
            }

            WorldBuildScopeUtility.CollectTiles(context.Scope, context.Session, _tiles);
            for (var i = 0; i < _tiles.Count; i++)
            {
                context.Cancellation.ThrowIfRequested();
                if (!catalog.TryGetTile(_tiles[i], out var info) || info.Terrain == null)
                    continue;

                if (context.Profile?.Surfaces != null)
                    painter.BindTerrain(info.Terrain, context.Profile.Surfaces);

                painter.PaintInfrastructure(info, context.Artifacts);

                if (i % 2 == 0)
                {
                    context.Progress?.Report(
                        Descriptor.Id,
                        0.1f + 0.7f * ((i + 1f) / _tiles.Count),
                        $"Painted infra {i + 1}/{_tiles.Count}");
                    yield return null;
                }
            }

            var sync = painter.SyncDirtyTiles();
            while (sync.MoveNext())
                yield return sync.Current;

            context.Progress?.Report(Descriptor.Id, 1f, "Infrastructure surfaces synced");
        }
    }
}
