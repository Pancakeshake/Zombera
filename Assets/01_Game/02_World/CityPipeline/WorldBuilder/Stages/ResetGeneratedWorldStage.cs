using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Clears scoped generated content according to <see cref="WorldResetMode"/>.</summary>
    public sealed class ResetGeneratedWorldStage : WorldBuildStageBase
    {
        private const string TerrainGridRootName = "WorldTerrainGrid";
        /// <summary>Retired mesh-water root; Crest owns water now. Sweep leftovers on reset.</summary>
        private const string LegacyHydrologySurfacesRootName = "HydrologySurfaces";
        /// <summary>
        ///     Retired editor viz root for city site footprints (ScatterSiteFill meshes).
        ///     Spawn code removed; sweep leftovers on reset.
        /// </summary>
        private const string LegacyCityScatterFootprintsRootName = "CityScatterFootprints";
        private readonly List<WorldTileCoord> _tiles = new(64);

        public ResetGeneratedWorldStage() : base(WorldBuildStageId.ResetGeneratedWorld)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context == null)
                throw new WorldBuildStageException(Descriptor.Id, "Context is null.");

            context.Cancellation.ThrowIfRequested();
            var mode = context.Options != null ? context.Options.ResetMode : WorldResetMode.ContentOnly;
            context.Progress?.Report(Descriptor.Id, 0.05f, $"Reset ({mode})");
            PublishWorldStateReset(context, mode);

            var catalog = context.WorldBuilder?.TileCatalog;
            WorldBuildScopeUtility.CollectTiles(context.Scope, context.Session, _tiles);

            if (catalog != null)
            {
                for (var i = 0; i < _tiles.Count; i++)
                    catalog.Invalidate(_tiles[i]);
            }

            yield return null;
            context.Cancellation.ThrowIfRequested();

            ClearBoundContent(context, mode);
            DestroyLegacyHydrologySurfaces(context.WorldBuilder);
            DestroyLegacyCityScatterFootprints(context.WorldBuilder);
            if (mode == WorldResetMode.ContentOnly)
            {
                ResetSurfaces(context, catalog);
                ClearTunnelHoles(context, catalog);
            }

            ClearArtifacts(context.Artifacts, mode);
            TunnelRuntimeRegistry.Clear();
            MountainTunnelBuildCache.Clear();
            context.WorldBuilder?.ClearSessionCityPlan();
            // AlignLayout may bind a scatter override without ApplyCitySitePlan ownership.
            context.CityBuilder?.ClearSessionRegionOverride();
            context.CityBuilder?.ClearAllGeneratedCityContent();

            if (mode == WorldResetMode.TerrainAndContent)
                DestroyAllocatedTerrains(context.WorldBuilder, catalog, _tiles);

            var reset = context.WorldBuilder?.ResetScope(context.Scope, mode);
            if (reset != null)
            {
                while (reset.MoveNext())
                    yield return reset.Current;
            }

            context.Progress?.Report(Descriptor.Id, 1f, $"Reset complete ({_tiles.Count} tiles)");
        }

        private void PublishWorldStateReset(WorldBuildContext context, WorldResetMode mode)
        {
            if (WorldStateStageCaptureUtility.TryGetActiveStage(context, Descriptor.Id, out var stage))
                stage.ClearGeneratedDomains(mode);
        }

        private static void ClearBoundContent(WorldBuildContext context, WorldResetMode mode)
        {
            var builder = context.WorldBuilder;
            if (builder == null) return;

            if (mode == WorldResetMode.TerrainAndContent)
            {
                builder.WaterRenderer?.TearDown();
                builder.OceanWaterRenderer?.TearDownOcean();
            }
            else
            {
                builder.WaterRenderer?.Clear(context.Scope);
                builder.OceanWaterRenderer?.ClearOcean(context.Scope);
            }

            builder.NaturePlacer?.Clear(context.Scope);
            builder.PoiSink?.Clear(context.Scope);
        }

        /// <summary>
        /// Removes retired mesh-water roots left from pre-Crest hydrology. No live code
        /// creates these anymore; they only persist as scene orphans after reset.
        /// </summary>
        private static void DestroyLegacyHydrologySurfaces(WorldBuilderService service)
        {
            DestroyLegacyNamedRoots(service, LegacyHydrologySurfacesRootName);
        }

        /// <summary>
        /// Removes retired CityScatterFootprints / ScatterSiteFill editor viz left from
        /// WorldBiomeScatterMarkerService. No live code creates these anymore.
        /// </summary>
        private static void DestroyLegacyCityScatterFootprints(WorldBuilderService service)
        {
            DestroyLegacyNamedRoots(service, LegacyCityScatterFootprintsRootName);
            DestroyOrphanScatterSiteFills();
        }

        private static void DestroyOrphanScatterSiteFills()
        {
            const string fillName = "ScatterSiteFill";
            var destroyed = 0;
            var transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t.name != fillName)
                    continue;

                // Prefer destroying the Site_XX parent so empty site shells don't linger.
                var target = t.parent != null && t.parent.name.StartsWith("Site_")
                    ? t.parent.gameObject
                    : t.gameObject;
                DestroyHierarchy(target);
                destroyed++;
            }

            if (destroyed > 0)
            {
                Debug.Log(
                    "[ResetGeneratedWorldStage] Destroyed orphan " +
                    fillName + " hierarchies=" + destroyed + ".");
            }
        }

        private static void DestroyLegacyNamedRoots(WorldBuilderService service, string rootName)
        {
            var destroyed = 0;
            if (service != null)
            {
                var rooted = service.transform.Find(rootName);
                if (rooted != null)
                {
                    DestroyHierarchy(rooted.gameObject);
                    destroyed++;
                }
            }

            var transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t.name != rootName)
                    continue;
                if (service != null && t.IsChildOf(service.transform))
                    continue;

                DestroyHierarchy(t.gameObject);
                destroyed++;
            }

            if (destroyed > 0)
            {
                Debug.Log(
                    "[ResetGeneratedWorldStage] Destroyed legacy " +
                    rootName + " roots=" + destroyed + ".");
            }
        }

        private static void ResetSurfaces(WorldBuildContext context, WorldTileCatalog catalog)
        {
            if (catalog == null) return;
            var backend = context.WorldBuilder?.SurfacePainter?.SyncBackend;
            if (backend == null) return;

            var tiles = new List<WorldTileCoord>(64);
            WorldBuildScopeUtility.CollectTiles(context.Scope, context.Session, tiles);
            for (var i = 0; i < tiles.Count; i++)
            {
                if (!catalog.TryGetTile(tiles[i], out var info) ||
                    !WorldTileInfoUtility.TryGetLiveTerrain(info, out var terrain))
                    continue;

                var data = terrain.terrainData;
                backend.ResetToBase(
                    terrain,
                    new RectInt(0, 0, data.alphamapWidth, data.alphamapHeight),
                    0);
            }
        }

        private static void ClearTunnelHoles(WorldBuildContext context, WorldTileCatalog catalog)
        {
            if (catalog == null)
                return;

            var tiles = new List<WorldTileCoord>(64);
            WorldBuildScopeUtility.CollectTiles(context.Scope, context.Session, tiles);
            TunnelTerrainHoleApplicator.ClearHolesOnScopedTerrains(tiles, catalog);
        }

        private static void ClearArtifacts(WorldBuildArtifacts artifacts, WorldResetMode mode)
        {
            if (artifacts == null) return;
            artifacts.SetLandforms(null);
            artifacts.SetOrogen(null);
            artifacts.SetHydrology(null);
            artifacts.SetBiomes(null);
            artifacts.SetSites(null);
            artifacts.SetCityPads(null);
            artifacts.SetRoads(null);
            artifacts.SetCrossings(null);
            artifacts.SetTunnels(null);
            artifacts.SetCityGeneratedRoads(null);
            if (mode == WorldResetMode.TerrainAndContent)
                artifacts.SetPlan(null);
        }

        private static void DestroyAllocatedTerrains(
            WorldBuilderService service,
            WorldTileCatalog catalog,
            List<WorldTileCoord> tiles)
        {
            var destroyed = 0;
            if (catalog != null && tiles != null)
            {
                for (var i = 0; i < tiles.Count; i++)
                {
                    if (!catalog.TryGetTile(tiles[i], out var info) ||
                        !WorldTileInfoUtility.TryGetLiveTerrain(info, out var terrain))
                        continue;

                    DestroyTerrainObject(terrain);
                    catalog.SetTerrain(tiles[i], null);
                    catalog.Invalidate(tiles[i]);
                    destroyed++;
                }
            }

            // Orphans: catalog lost refs after session reconfigure, but meshes remain.
            destroyed += DestroyTerrainGridRoot(service);
            destroyed += DestroyOrphanTerrainGrids(service);

            Debug.Log(
                "[ResetGeneratedWorldStage] Destroyed WorldTerrainGrid tiles=" + destroyed + ".");
        }

        private static int DestroyTerrainGridRoot(WorldBuilderService service)
        {
            if (service == null)
                return 0;

            var root = service.transform.Find(TerrainGridRootName);
            if (root == null)
                return 0;

            var count = root.childCount;
            DestroyHierarchy(root.gameObject);
            return count;
        }

        private static int DestroyOrphanTerrainGrids(WorldBuilderService service)
        {
            var destroyed = 0;
            var transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t.name != TerrainGridRootName)
                    continue;
                if (service != null && t.IsChildOf(service.transform))
                    continue; // already handled

                destroyed += t.childCount;
                DestroyHierarchy(t.gameObject);
            }

            return destroyed;
        }

        private static void DestroyTerrainObject(Terrain terrain)
        {
            if (terrain == null)
                return;
            DestroyHierarchy(terrain.gameObject);
        }

        private static void DestroyHierarchy(GameObject target)
        {
            if (target == null)
                return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(target);
                return;
            }
#endif
            Object.Destroy(target);
        }
    }
}
