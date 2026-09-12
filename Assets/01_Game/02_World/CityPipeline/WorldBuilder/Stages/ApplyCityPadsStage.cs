using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Debug = UnityEngine.Debug;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>
    ///     Reasserts city-footprint+margin flat cores after highways without mutating hydrology, then bakes terrains.
    ///     Blend aprons come from ReserveCityPads + erosion; this stage only re-flats cores.
    /// </summary>
    public sealed class ApplyCityPadsStage : WorldBuildStageBase
    {
        private readonly List<WorldTileCoord> _tiles = new(64);
        private readonly List<CityFlattenPad> _pads = new(16);
        private readonly List<Rect> _padOuterBounds = new(16);

        public ApplyCityPadsStage() : base(WorldBuildStageId.ApplyCityPads)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts?.Sites == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldSitePlan is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Reasserting city pad cores");

            context.CityBuilder?.ResnapWorldTerrainGridToSession(context.Session);

            ResolvePads(context, _pads);

            var landforms = context.Artifacts.Landforms;
            var wallSw = Stopwatch.StartNew();
            long padApplyMs = 0, highwayCarveMs = 0, bakeMs = 0;

            if (landforms?.WorldHeights != null && _pads.Count > 0)
            {
                RunPadCorePhases(context, landforms, out padApplyMs, out highwayCarveMs);
                yield return null;
            }

            // Hydrology is authoritative. Pad terrain writes already skip water cells;
            // this stage must never clear a selected river or lake to make a pad fit.

            PatchBuildabilityUnderPads(
                context.Artifacts.Biomes,
                context.Artifacts.Landforms,
                context.Artifacts.Hydrology,
                _pads);

            var catalog = context.WorldBuilder?.TileCatalog;
            if (catalog != null && landforms != null && context.Profile?.TerrainGrid != null)
            {
                bakeMs = BakePadTiles(context, catalog, landforms);
                yield return null;
            }

            if (catalog != null)
            {
                WorldTerrainNeighborUtility.StitchAndLink(catalog);
                SyncScopedHeightmaps(catalog, _tiles);
            }

            ResyncOceanIfNeeded(context);

            context.WorldBuilder?.BindArtifactFields(
                context.Artifacts.Landforms,
                context.Artifacts.Biomes,
                context.Artifacts.Hydrology);

            wallSw.Stop();
            Debug.Log(
                $"[ApplyCityPads] pads={_pads.Count} wall={wallSw.ElapsedMilliseconds}ms " +
                $"coreReassert={padApplyMs}ms highwayCarve={highwayCarveMs}ms bake={bakeMs}ms");

            context.Progress?.Report(Descriptor.Id, 1f, $"Reasserted {_pads.Count} city pad cores");
        }

        private void RunPadCorePhases(
            WorldBuildContext context,
            LandformField landforms,
            out long padApplyMs,
            out long highwayCarveMs)
        {
            var seaLevel = context.Profile?.Hydrology != null
                ? context.Profile.Hydrology.SeaLevelWorldY
                : 0f;
            var maxReclaimDepth = CityPadReclaimPolicy.MaxReclaimDepthMeters(context.Profile?.Landforms);

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.4f, "Re-carving highway approaches");
            highwayCarveMs = CarveHighwayApproaches(context, landforms, maxReclaimDepth);

            context.Progress?.Report(Descriptor.Id, 0.55f, "Reasserting flat cores");
            var phaseSw = Stopwatch.StartNew();
            CityPadCoreUtility.StampCoresWithBlend(
                landforms,
                _pads,
                context.Profile?.Landforms,
                context.Profile?.RoadNetworkSettings,
                context.Artifacts.Hydrology,
                seaLevel);
            SyncSitePadHeights(context.Artifacts.Sites, _pads);
            context.Artifacts.SetCityPads(new List<CityFlattenPad>(_pads));
            padApplyMs = phaseSw.ElapsedMilliseconds;
            LogPadDiagnostics(_pads, builderPath: context.CityBuilder != null &&
                context.CityBuilder.RegionModeActive ? "region" : "worldbuilder");
        }

        private long CarveHighwayApproaches(
            WorldBuildContext context,
            LandformField landforms,
            float maxReclaimDepth)
        {
            if (context.Artifacts.Roads?.Roads == null ||
                context.Profile?.RoadNetworkSettings == null)
                return 0;

            // Tunnel-enabled builds carve once in ResolveMountainTunnels (post-chord rewrite).
            if (context.Profile.RoadNetworkSettings.enableMountainTunnels)
                return 0;

            var phaseSw = Stopwatch.StartNew();
            HighwayCorridorCarver.Carve(
                landforms,
                context.Artifacts.Roads.Roads,
                context.Profile.RoadNetworkSettings,
                new HighwayCorridorCarver.HighwayCarveOptions
                {
                    Hydrology = context.Artifacts.Hydrology,
                    PadAnchors = _pads,
                    MaxReclaimDepthMeters = maxReclaimDepth,
                    Tunnels = context.Artifacts.Tunnels
                });
            return phaseSw.ElapsedMilliseconds;
        }

        private long BakePadTiles(
            WorldBuildContext context,
            WorldTileCatalog catalog,
            LandformField landforms)
        {
            if (_pads.Count > 0)
            {
                CollectPadOuterBounds(_pads, _padOuterBounds, relaxReachMeters: 0f);
                WorldBuildScopeUtility.CollectTilesOverlappingRects(
                    _padOuterBounds, context.Session, _tiles);
            }
            else
                WorldBuildScopeUtility.CollectTiles(context.Scope, context.Session, _tiles);

            context.Progress?.Report(Descriptor.Id, 0.7f, $"Baking {_tiles.Count} pad tiles");
            var bakeSw = Stopwatch.StartNew();
            LandformHeightmapBaker.BakeScopedTiles(
                landforms,
                context.Profile.TerrainGrid,
                context.Profile.Hydrology,
                catalog,
                _tiles,
                context.Session.Seed ^ unchecked((int)0xCAD50001),
                applyDetailNoise: false);
            return bakeSw.ElapsedMilliseconds;
        }

        private static void ResyncOceanIfNeeded(WorldBuildContext context)
        {
            var oceanRenderer = context.WorldBuilder?.OceanWaterRenderer;
            if (oceanRenderer == null || context.Profile?.Hydrology == null)
                return;

            oceanRenderer.ResyncOceanToBounds(
                context.Session.WorldBoundsXZ,
                context.Profile.Hydrology.SeaLevelWorldY);
        }

        private static void SyncScopedHeightmaps(WorldTileCatalog catalog, List<WorldTileCoord> tiles)
        {
            for (var i = 0; i < tiles.Count; i++)
            {
                if (!catalog.TryGetTile(tiles[i], out var info) ||
                    !WorldTileInfoUtility.TryGetLiveTerrain(info, out var terrain))
                    continue;
                terrain.terrainData.SyncHeightmap();
            }

            Physics.SyncTransforms();
        }

        private static void ResolvePads(WorldBuildContext context, List<CityFlattenPad> pads)
        {
            pads.Clear();
            var existing = context.Artifacts?.CityPads;
            if (existing != null && existing.Count > 0)
            {
                for (var i = 0; i < existing.Count; i++)
                {
                    if (existing[i] != null)
                        pads.Add(existing[i]);
                }

                if (pads.Count > 0)
                    return;
            }

            var pruneMargin = context.Profile?.Landforms?.CityPadHydrologyPruneMarginMeters ?? 16f;
            CityPadCoreUtility.BuildCityCorePads(
                context.Artifacts?.Sites,
                context.CityBuilder,
                context.Artifacts?.Landforms,
                context.Profile?.Landforms,
                pruneMargin,
                context.Artifacts?.Hydrology,
                context.Profile?.Hydrology?.CityWaterSetbackMeters ?? 64f,
                pads);
        }

        private static void SyncSitePadHeights(WorldSitePlan sites, IReadOnlyList<CityFlattenPad> pads)
        {
            if (sites?.CitySites == null || pads == null)
                return;

            for (var i = 0; i < sites.CitySites.Count && i < pads.Count; i++)
            {
                var site = sites.CitySites[i];
                var pad = pads[i];
                if (site == null || pad == null)
                    continue;
                site.PadHeightWorldY = pad.TargetHeightWorldY;
                if (pad.HasHighwayEntry)
                {
                    site.HighwayEntryXZ = pad.HighwayEntryXZ;
                    site.HighwayEntryHeightWorldY = pad.TargetHeightWorldY;
                    pad.HighwayEntryHeightWorldY = pad.TargetHeightWorldY;
                    if (site.HighwayEntryEdgeLengthMeters < 0.01f)
                        site.HighwayEntryEdgeLengthMeters = 1f;
                }
                else if (site.HasHighwayEntry)
                {
                    site.HighwayEntryHeightWorldY = pad.TargetHeightWorldY;
                }
                if (pad.IsCoastal)
                {
                    site.IsCoastal = true;
                    site.SeawardNormalXZ = pad.SeawardNormalXZ;
                    site.CoastExposure01 = pad.CoastExposure01;
                }
            }
        }

        private static void LogPadDiagnostics(IReadOnlyList<CityFlattenPad> pads, string builderPath)
        {
            if (pads == null || pads.Count == 0)
                return;

            for (var i = 0; i < pads.Count; i++)
            {
                var pad = pads[i];
                if (pad == null) continue;
                Debug.Log(
                    $"[ApplyCityPads:diag] path={builderPath} pad={i} coastal={pad.IsCoastal} " +
                    $"maxDelta={pad.DiagnosticsInlandMaxDelta:F1} falloff={pad.FalloffMeters:F0} " +
                    $"quayFalloff={pad.FalloffMetersSeaward:F0} cone={pad.ConeSlopeRatio:F3} " +
                    $"capExceeded={pad.DiagnosticsExceedsContinuityCap} " +
                    $"padY={pad.TargetHeightWorldY:F1} plateau={pad.PlateauBoundsXZ}");
            }
        }

        private static void PatchBuildabilityUnderPads(
            BiomeField biomes,
            LandformField landforms,
            HydrologyPlan hydrology,
            IReadOnlyList<CityFlattenPad> pads)
        {
            if (biomes == null || landforms == null || pads == null || pads.Count == 0)
                return;
            if (biomes.Width != landforms.Width || biomes.Height != landforms.Height)
                return;

            for (var p = 0; p < pads.Count; p++)
            {
                if (pads[p] == null)
                    continue;
                PatchSinglePadBuildability(biomes, landforms, hydrology, pads[p]);
            }
        }

        private static void PatchSinglePadBuildability(
            BiomeField biomes,
            LandformField landforms,
            HydrologyPlan hydrology,
            CityFlattenPad pad)
        {
            if (!TryResolvePadCellRange(landforms, pad.PlateauBoundsXZ, out var x0, out var x1, out var z0, out var z1))
                return;

            var plateau = pad.PlateauBoundsXZ;
            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    if (!IsCellInsidePlateau(landforms, plateau, x, z))
                        continue;
                    if (IsHydrologyBlocked(hydrology, x, z))
                        continue;

                    var i = landforms.Index(x, z);
                    biomes.NoBuild[i] = false;
                    biomes.Buildability[i] = Mathf.Max(
                        biomes.Buildability[i],
                        CityPadReclaimPolicy.ReclaimedBuildabilityFloor);
                }
            }
        }

        private static bool TryResolvePadCellRange(
            LandformField landforms,
            Rect plateau,
            out int x0,
            out int x1,
            out int z0,
            out int z1)
        {
            x0 = Mathf.Max(0, Mathf.FloorToInt((plateau.xMin - landforms.OriginXZ.x) / landforms.CellSize));
            x1 = Mathf.Min(landforms.Width - 1, Mathf.CeilToInt((plateau.xMax - landforms.OriginXZ.x) / landforms.CellSize));
            z0 = Mathf.Max(0, Mathf.FloorToInt((plateau.yMin - landforms.OriginXZ.y) / landforms.CellSize));
            z1 = Mathf.Min(landforms.Height - 1, Mathf.CeilToInt((plateau.yMax - landforms.OriginXZ.y) / landforms.CellSize));
            return x0 <= x1 && z0 <= z1;
        }

        private static bool IsCellInsidePlateau(LandformField landforms, Rect plateau, int x, int z)
        {
            var center = landforms.CellCenterXZ(x, z);
            return center.x >= plateau.xMin && center.x <= plateau.xMax &&
                   center.y >= plateau.yMin && center.y <= plateau.yMax;
        }

        private static bool IsHydrologyBlocked(HydrologyPlan hydrology, int x, int z)
        {
            if (hydrology == null || x >= hydrology.Width || z >= hydrology.Height)
                return false;
            return hydrology.WaterClass[hydrology.Index(x, z)] != WorldWaterClass.None;
        }

        private static void CollectPadOuterBounds(
            List<CityFlattenPad> pads,
            List<Rect> bounds,
            float relaxReachMeters)
        {
            bounds.Clear();
            for (var p = 0; p < pads.Count; p++)
            {
                var pad = pads[p];
                if (pad == null) continue;
                var outer = pad.OuterBoundsXZ;
                if (relaxReachMeters > 0f)
                {
                    outer = Rect.MinMaxRect(
                        outer.xMin - relaxReachMeters,
                        outer.yMin - relaxReachMeters,
                        outer.xMax + relaxReachMeters,
                        outer.yMax + relaxReachMeters);
                }

                bounds.Add(outer);
            }
        }
    }
}
