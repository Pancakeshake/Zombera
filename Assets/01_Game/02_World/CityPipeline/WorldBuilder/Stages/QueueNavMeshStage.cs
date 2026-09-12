using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>
    /// Yield until scoped ContentReady tiles reach NavigationReady (or timeout).
    /// NavMesh service bakes asynchronously after ContentReady; this stage waits and reports.
    /// </summary>
    public sealed class QueueNavMeshStage : WorldBuildStageBase
    {
        private const float TimeoutSeconds = 120f;

        public QueueNavMeshStage() : base(WorldBuildStageId.QueueNavMesh)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.WorldBuilder?.TileCatalog == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldTileCatalog is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Waiting for NavMesh readiness");

            var catalog = context.WorldBuilder.TileCatalog;
            var required = new List<WorldTileCoord>(32);
            CollectRequired(catalog, context.Scope.BoundsXZ, required);

            if (required.Count == 0)
            {
                context.Progress?.Report(Descriptor.Id, 1f, "No tiles require NavMesh");
                yield break;
            }

            var start = Time.realtimeSinceStartup;
            while (true)
            {
                context.Cancellation.ThrowIfRequested();
                var ready = CountAtOrAbove(catalog, required, WorldTileState.NavigationReady);
                var progress = Mathf.Clamp01(ready / (float)required.Count);
                context.Progress?.Report(Descriptor.Id, progress, $"NavMesh {ready}/{required.Count}");

                if (ready >= required.Count)
                    break;

                if (Time.realtimeSinceStartup - start > TimeoutSeconds)
                {
                    throw new WorldBuildStageException(
                        Descriptor.Id,
                        $"NavMesh timeout: {ready}/{required.Count} tiles NavigationReady.");
                }

                yield return null;
            }

            context.Progress?.Report(Descriptor.Id, 1f, "NavMesh ready");
        }

        private static void CollectRequired(
            WorldTileCatalog catalog,
            Rect boundsXZ,
            List<WorldTileCoord> required)
        {
            var buffer = new List<WorldTileInfo>(64);
            catalog.CopyTilesIntersecting(boundsXZ, buffer);
            for (var i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].State >= WorldTileState.ContentReady)
                    required.Add(buffer[i].Coord);
            }
        }

        private static int CountAtOrAbove(
            WorldTileCatalog catalog,
            List<WorldTileCoord> coords,
            WorldTileState minimum)
        {
            var count = 0;
            for (var i = 0; i < coords.Count; i++)
            {
                if (!catalog.TryGetTile(coords[i], out var info)) continue;
                if (info.State >= minimum)
                    count++;
            }

            return count;
        }
    }
}
