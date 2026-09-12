using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>
    /// Sync heightmaps/colliders after stitching neighbors, flush MicroSplat, then mark scoped tiles ContentReady.
    /// </summary>
    public sealed class FinalizeTerrainTilesStage : WorldBuildStageBase
    {
        public FinalizeTerrainTilesStage() : base(WorldBuildStageId.FinalizeTerrainTiles)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.WorldBuilder == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldBuilderService is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.1f, "Finalizing terrain tiles");

            var catalog = context.WorldBuilder.TileCatalog;
            if (catalog == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldTileCatalog is required.");

            var buffer = new List<WorldTileInfo>(64);
            catalog.CopyTilesIntersecting(context.Scope.BoundsXZ, buffer);

            // Stitch before flush so LOD/GPU pick up matched edges (not divergent pre-stitch heights).
            WorldTerrainNeighborUtility.StitchAndLink(catalog);

            for (var i = 0; i < buffer.Count; i++)
            {
                context.Cancellation.ThrowIfRequested();
                FlushTerrain(buffer[i].Terrain);
                if (i % 4 == 0)
                    yield return null;
            }

            if (context.WorldBuilder.SurfacePainter != null)
            {
                var sync = context.WorldBuilder.SurfacePainter.SyncDirtyTiles();
                while (sync.MoveNext())
                    yield return sync.Current;
            }

            Physics.SyncTransforms();
            context.Progress?.Report(Descriptor.Id, 0.85f, "Advancing tiles to ContentReady");
            AdvanceToContentReady(catalog, buffer);

            var oceanRenderer = context.WorldBuilder.OceanWaterRenderer;
            if (oceanRenderer != null && context.Profile?.Hydrology != null)
            {
                var terrainBounds = WorldTerrainBoundsResolver.Resolve(
                    catalog,
                    context.Session);
                if (terrainBounds.width <= 0f || terrainBounds.height <= 0f)
                    terrainBounds = context.Session.WorldBoundsXZ;
                oceanRenderer.ResyncOceanToBounds(
                    terrainBounds,
                    context.Profile.Hydrology.SeaLevelWorldY);
            }

            context.Progress?.Report(Descriptor.Id, 1f, "Terrain tiles finalized");
        }

        private static void FlushTerrain(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null) return;
            terrain.terrainData.SyncHeightmap();
            terrain.Flush();
        }

        private static void AdvanceToContentReady(WorldTileCatalog catalog, List<WorldTileInfo> buffer)
        {
            for (var i = 0; i < buffer.Count; i++)
            {
                var info = buffer[i];
                if (info.State == WorldTileState.InfrastructureReady)
                    catalog.TryTransition(info.Coord, WorldTileState.InfrastructureReady, WorldTileState.ContentReady);
                else if (info.State == WorldTileState.TerrainReady)
                    catalog.TryTransition(info.Coord, WorldTileState.TerrainReady, WorldTileState.ContentReady);
            }
        }
    }
}
