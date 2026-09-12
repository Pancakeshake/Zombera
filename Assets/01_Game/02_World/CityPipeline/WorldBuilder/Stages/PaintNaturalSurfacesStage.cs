using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Debug = UnityEngine.Debug;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Paints natural alphamap channels and transitions Allocated → TerrainReady.</summary>
    public sealed class PaintNaturalSurfacesStage : WorldBuildStageBase
    {
        private readonly List<WorldTileCoord> _tiles = new(64);

        public PaintNaturalSurfacesStage() : base(WorldBuildStageId.PaintNaturalSurfaces)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            ValidateContext(context);
            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Painting natural surfaces");
            var painter = ResolvePainter(context);
            var catalog = ResolveCatalog(context);
            var paintMode = ConfigurePainter(context, painter);
            WorldBuildScopeUtility.CollectTiles(context.Scope, context.Session, _tiles);
            painter.PreparePaintSession(
                context.Artifacts.Landforms,
                context.Artifacts.Hydrology,
                context.Artifacts.Biomes);
            painter.ResetPaintTimingAccumulators();

            context.Artifacts?.SetLotTerrainComposedDuringPaint(false);

            var bindWatch = Stopwatch.StartNew();
            PrebindAllocatedTiles(painter, catalog);
            bindWatch.Stop();
            painter.AddBindTimingMs(bindWatch.ElapsedMilliseconds);

            var paint = PaintAllocatedTiles(context, painter, catalog, paintMode);
            while (paint.MoveNext())
                yield return paint.Current;

            context.Progress?.Report(Descriptor.Id, 0.88f, "Stamping coastal quay rock strips");
            var quayWatch = Stopwatch.StartNew();
            var quayTiles = StampCoastalQuaySurfaces(context, painter, catalog);
            quayWatch.Stop();

            var flush = FlushSyncAndLog(painter, quayWatch.ElapsedMilliseconds, quayTiles);
            while (flush.MoveNext())
                yield return flush.Current;

            LogUnboundTiles(context, catalog);
            TransitionAllocatedTiles(catalog);
            context.Progress?.Report(Descriptor.Id, 1f, "Natural surfaces flushed");
        }

        private static void ValidateContext(WorldBuildContext context)
        {
            if (context?.Artifacts?.Landforms == null || context.Artifacts.Biomes == null)
                throw new WorldBuildStageException(
                    WorldBuildStageId.PaintNaturalSurfaces, "Landforms and Biomes are required.");
            if (context.Profile?.Surfaces == null)
                throw new WorldBuildStageException(
                    WorldBuildStageId.PaintNaturalSurfaces, "WorldSurfacePalette is required.");
        }

        private WorldSurfacePainter ResolvePainter(WorldBuildContext context)
        {
            var painter = context.WorldBuilder?.SurfacePainter;
            if (painter == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldSurfacePainter is required.");
            return painter;
        }

        private WorldTileCatalog ResolveCatalog(WorldBuildContext context)
        {
            var catalog = context.WorldBuilder.TileCatalog;
            if (catalog == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldTileCatalog is required.");
            return catalog;
        }

        private static SurfacePaintQualityMode ConfigurePainter(
            WorldBuildContext context,
            WorldSurfacePainter painter)
        {
            var mode = context.Options != null
                ? context.Options.SurfacePaintQuality
                : SurfacePaintQualityMode.Quality;
            painter.ConfigurePaintIteration(mode);
            if (mode != SurfacePaintQualityMode.Fast)
            {
                Debug.Log(
                    "[PaintNaturalSurfacesStage] SurfacePaintQuality=" + mode +
                    " — soft natural alphamap. Urban lot stamps run later in GenerateLotTerrain. " +
                    "Quay rock strips stamp after soft paint (hard edges expected on pad seaward faces; " +
                    "open-beach SoftRadius acceptance should crop away quay).");
            }

            painter.BindPaintContext(context.Session, context.Profile.Landforms);
            painter.BindBiomePalette(context.Profile.Biomes);
            painter.PreparePalette(context.Profile.Surfaces);
            return mode;
        }

        private void PrebindAllocatedTiles(
            WorldSurfacePainter painter,
            WorldTileCatalog catalog)
        {
            for (var i = 0; i < _tiles.Count; i++)
            {
                if (!catalog.TryGetTile(_tiles[i], out var info) ||
                    !WorldTileInfoUtility.TryGetLiveTerrain(info, out var terrain))
                    continue;
                painter.EnsureMicroSplatTemplate(terrain);
            }
        }

        private IEnumerator PaintAllocatedTiles(
            WorldBuildContext context,
            WorldSurfacePainter painter,
            WorldTileCatalog catalog,
            SurfacePaintQualityMode paintMode)
        {
            // Fast stays serial (already cheap). Quality/Balanced: batch-parallel CPU paint.
            if (paintMode == SurfacePaintQualityMode.Fast)
            {
                PaintAllocatedTilesSerial(context, painter, catalog);
                yield break;
            }

            var jobs = CollectNaturalSurfaceJobs(context, painter, catalog);

            painter.EnsureNaturalRecordIndicesForParallel(context.Artifacts.Biomes);
            if (jobs.Count == 0)
                yield break;

            // Cap batch size for peak alphamap memory (~33MB/tile @512×layers).
            var degree = Mathf.Clamp(System.Environment.ProcessorCount / 2, 2, 6);
            var sample = jobs[0];
            var maps = new float[degree][,,];
            for (var b = 0; b < degree; b++)
                maps[b] = new float[sample.Height, sample.Width, sample.Layers];

            var pending = new WorldSurfacePainter.NaturalSurfacePendingApply[degree];
            for (var offset = 0; offset < jobs.Count; offset += degree)
            {
                context.Cancellation.ThrowIfRequested();
                var batch = Mathf.Min(degree, jobs.Count - offset);
                System.Threading.Tasks.Parallel.For(0, batch, b =>
                {
                    pending[b] = painter.PaintNaturalSurfaceCpu(jobs[offset + b], maps[b]);
                });

                for (var b = 0; b < batch; b++)
                {
                    painter.ApplyNaturalSurfaceAlphamap(pending[b]);
                    pending[b] = default;
                }

                context.Progress?.Report(
                    Descriptor.Id,
                    0.1f + 0.7f * ((offset + batch) / (float)jobs.Count),
                    $"Painted {offset + batch}/{jobs.Count}");
                yield return null;
            }
        }

        /// <summary>Fast iteration keeps painting serial — it is already cheap per tile.</summary>
        private void PaintAllocatedTilesSerial(
            WorldBuildContext context,
            WorldSurfacePainter painter,
            WorldTileCatalog catalog)
        {
            for (var i = 0; i < _tiles.Count; i++)
            {
                context.Cancellation.ThrowIfRequested();
                if (!catalog.TryGetTile(_tiles[i], out var info) ||
                    !WorldTileInfoUtility.TryGetLiveTerrain(info, out var terrain))
                    continue;
                painter.PaintNaturalSurface(
                    info.WithTerrain(terrain),
                    context.Artifacts.Landforms,
                    context.Artifacts.Hydrology,
                    context.Artifacts.Biomes);
            }
        }

        private List<WorldSurfacePainter.NaturalSurfaceCpuJob> CollectNaturalSurfaceJobs(
            WorldBuildContext context,
            WorldSurfacePainter painter,
            WorldTileCatalog catalog)
        {
            var jobs = new List<WorldSurfacePainter.NaturalSurfaceCpuJob>(_tiles.Count);
            for (var i = 0; i < _tiles.Count; i++)
            {
                context.Cancellation.ThrowIfRequested();
                if (!catalog.TryGetTile(_tiles[i], out var info) ||
                    !WorldTileInfoUtility.TryGetLiveTerrain(info, out _))
                    continue;
                if (!painter.TryCreateNaturalSurfaceCpuJob(
                        info,
                        context.Artifacts.Landforms,
                        context.Artifacts.Hydrology,
                        context.Artifacts.Biomes,
                        out var job))
                    continue;
                jobs.Add(job);
            }

            return jobs;
        }

        private IEnumerator FlushSyncAndLog(WorldSurfacePainter painter, long quayMs, int quayTiles)
        {
            var syncWatch = Stopwatch.StartNew();
            var sync = painter.SyncDirtyTiles();
            while (sync.MoveNext())
                yield return sync.Current;
            syncWatch.Stop();
            painter.LogPaintTimingSummary(Descriptor.Id.ToString());
            Debug.Log(
                "[PaintNaturalSurfacesStage] syncFlush=" + syncWatch.ElapsedMilliseconds +
                "ms quay=" + quayMs + "ms quayTiles=" + quayTiles +
                " tiles=" + _tiles.Count +
                " lotCompose=skipped(natural-only)");
        }

        private void LogUnboundTiles(WorldBuildContext context, WorldTileCatalog catalog)
        {
            var unbound = 0;
            for (var i = 0; i < _tiles.Count; i++)
            {
                if (!catalog.TryGetTile(_tiles[i], out var info) ||
                    !WorldTileInfoUtility.TryGetLiveTerrain(info, out var terrain))
                    continue;
                if (!MicroSplatTerrainBinder.IsBound(terrain, context.Profile.Surfaces))
                    unbound++;
            }

            if (unbound > 0)
            {
                Debug.LogWarning(
                    "[PaintNaturalSurfacesStage] " + unbound + "/" + _tiles.Count +
                    " tile(s) still not MicroSplat-bound after paint (no rebound re-paint).");
            }
        }

        private void TransitionAllocatedTiles(WorldTileCatalog catalog)
        {
            for (var i = 0; i < _tiles.Count; i++)
            {
                if (!catalog.TryGetTile(_tiles[i], out var info))
                    continue;
                if (info.State == WorldTileState.Allocated)
                    catalog.TryTransition(_tiles[i], WorldTileState.Allocated, WorldTileState.TerrainReady);
            }
        }

        private int StampCoastalQuaySurfaces(
            WorldBuildContext context,
            WorldSurfacePainter painter,
            WorldTileCatalog catalog)
        {
            var sites = context.Artifacts?.Sites?.CitySites;
            if (sites == null || sites.Count == 0)
                return 0;
            var pads = new List<CityFlattenPad>(sites.Count);
            var quay = CityCoastalPadUtility.ResolveQuayFalloffMeters(context.Profile?.Landforms);
            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null || !site.IsCoastal)
                    continue;
                var halfW = Mathf.Max(40f, site.HalfWidthMeters);
                var halfD = Mathf.Max(40f, site.HalfDepthMeters);
                var bounds = Rect.MinMaxRect(
                    site.CenterXZ.x - halfW,
                    site.CenterXZ.y - halfD,
                    site.CenterXZ.x + halfW,
                    site.CenterXZ.y + halfD);
                pads.Add(new CityFlattenPad(bounds, site.PadHeightWorldY)
                {
                    IsCoastal = true,
                    SeawardNormalXZ = site.SeawardNormalXZ,
                    CoastExposure01 = site.CoastExposure01,
                    FalloffMetersSeaward = quay
                });
            }

            if (pads.Count == 0)
                return 0;

            var stamped = CityPadSeawardSurfaceStamp.Apply(pads, catalog, _tiles, painter);
            if (stamped > 0)
            {
                Debug.Log(
                    "[PaintNaturalSurfacesStage] Seaward rock strips stamped on " + stamped +
                    " tile(s) for " + pads.Count + " coastal pad(s).");
            }

            return stamped;
        }
    }
}
