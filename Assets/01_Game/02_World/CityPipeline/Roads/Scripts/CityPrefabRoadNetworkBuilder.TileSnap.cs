using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        /// <summary>
        ///     Pipeline-owned world footprint. When set, road layout centers and
        ///     highway-exit clamps use this instead of MapMagic pins / activeTerrain.
        /// </summary>
        [System.NonSerialized] private Rect _pipelineWorldBoundsXZ;
        [System.NonSerialized] private bool _hasPipelineWorldBounds;

        /// <summary>
        ///     Snaps layout into <paramref name="session"/> terrain bounds.
        ///     When a City Region SO is wired, enables multi-city and scatters sites
        ///     onto WorldTerrainGrid (authored MapMagic-era coords are remapped via
        ///     a non-serialized session override — inspector keeps CityRegion.asset).
        /// </summary>
        public void AlignLayoutToWorldSession(
            WorldMapSession session,
            WorldSitePlan sites = null,
            WorldTileCatalog tileCatalog = null)
        {
            // Hub moves drag WorldTerrainGrid children; snap tiles before reading live bounds.
            AlignHubTransformToWorldOrigin(session);

            var bounds = ResolvePipelineWorldBounds(session, tileCatalog);
            if (bounds.width <= 0f || bounds.height <= 0f)
                return;

            _pipelineWorldBoundsXZ = bounds;
            _hasPipelineWorldBounds = true;
            ApplyWorldBoundsToLayoutFields(Layout, bounds);
            if (BaseLayout != null)
                ApplyWorldBoundsToLayoutFields(BaseLayout, bounds);

            if (EnsureAuthoredRegionOnWorldTerrain(bounds, sites, session))
            {
                _lastGeneratedRoadNetwork = null;
                _cachedRoadPolylines.Clear();
                _lastGeneratedBounds = default;
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Region mode on world terrain — sites=" +
                    (ActiveRegionAsset != null ? ActiveRegionAsset.SiteCount : 0) +
                    " (scatter target " +
                    (ActiveRegionAsset != null ? ActiveRegionAsset.EffectiveScatterSiteCount : 0) +
                    ") bounds=" + bounds + ".",
                    this);
                return;
            }

            var halfExtent = Mathf.Max(
                Layout.cityHalfWidthMaxMeters,
                Layout.cityHalfDepthMaxMeters,
                50f);
            var center = bounds.center;
            if (sites?.CitySites != null && sites.CitySites.Count > 0 && sites.CitySites[0] != null)
                center = sites.CitySites[0].CenterXZ;

            center.x = Mathf.Clamp(center.x, bounds.xMin + halfExtent, bounds.xMax - halfExtent);
            center.y = Mathf.Clamp(center.y, bounds.yMin + halfExtent, bounds.yMax - halfExtent);

            Layout.centerXZ = center;
            if (ResolvedAlignLayoutCenterToTransform)
            {
                var pos = transform.position;
                transform.position = new Vector3(center.x, pos.y, center.y);
                // Single-city hub may leave world origin; tiles must not ride along.
                ResnapWorldTerrainGridToSession(session);
            }

            // Cached hub roads were authored outside WorldTerrainGrid — force rebuild.
            _lastGeneratedRoadNetwork = null;
            _cachedRoadPolylines.Clear();
            _lastGeneratedBounds = default;

            Debug.Log(
                "[CityPrefabRoadNetworkBuilder] Aligned city layout to world terrain center=" +
                center + " bounds=" + bounds + ".",
                this);
        }

        /// <summary>
        ///     Activates multi-city from the authored <see cref="RegionAsset"/> and,
        ///     when sites are missing or outside <paramref name="worldBoundsXZ"/>,
        ///     binds a session clone configured to scatter inside the world grid.
        /// </summary>
        /// <returns>True when region mode is active after this call.</returns>
        public bool EnsureAuthoredRegionOnWorldTerrain(
            Rect worldBoundsXZ,
            WorldSitePlan plan = null,
            WorldMapSession session = default)
        {
            if (worldBoundsXZ.width <= 0f || worldBoundsXZ.height <= 0f)
                return RegionModeActive;

            var authored = RegionAsset;
            if (authored == null)
                return RegionModeActive;

            var mapSizeSettings = ResolveMapSizeSettings();
            var landformProfile = ResolveLandformProfileFromStack();
            var active = ActiveRegionAsset;
            var hasSession = session.WorldBoundsXZ.width > 0f;

            void SyncScatter(CityRegionAsset region)
            {
                if (hasSession)
                    region.ConfigureScatterForSession(session, mapSizeSettings, landformProfile);
                else
                    region.ConfigureScatterForWorldBounds(worldBoundsXZ);
            }

            bool SitesNeedRescatter(CityRegionAsset region) =>
                !region.AllSitesInsideScatterZone(worldBoundsXZ) ||
                !region.AllSiteFootprintsInside(worldBoundsXZ);

            // Keep an existing session override (pipeline plan or prior scatter clone).
            if (HasSessionRegionOverride && active != null)
            {
                SyncScatter(active);
                if (SitesNeedRescatter(active))
                    active.autoScatterOnBuild = true;

                regionModeEnabled = true;
                return true;
            }

            if (active != null &&
                active.SiteCount > 0 &&
                active.AllSitesInsideScatterZone(worldBoundsXZ) &&
                active.AllSiteFootprintsInside(worldBoundsXZ))
            {
                SyncScatter(active);
                regionModeEnabled = true;
                return true;
            }

            // Authored sites sit on old MapMagic coords — clone, clear stale positions,
            // retarget scatter to WorldTerrainGrid, leave inspector SO untouched.
            var sessionRegion = Object.Instantiate(authored);
            sessionRegion.name = authored.name + "_WorldScatter";
            sessionRegion.hideFlags = HideFlags.HideAndDontSave;
            sessionRegion.sites = new System.Collections.Generic.List<CityHubSite>();
            SyncScatter(sessionRegion);
            sessionRegion.autoScatterOnBuild = true;
            sessionRegion.randomizeRegionSeedPerBuild = false;
            if (sessionRegion.scatterSiteCount < 1)
                sessionRegion.scatterSiteCount = Mathf.Max(1, sessionRegion.SiteCount);

            SetSessionRegionOverride(sessionRegion);
            regionModeEnabled = true;
            return true;
        }

        /// <summary>
        ///     Locks region scatter + site positions to the session terrain grid so
        ///     city pads, terrain flatten, and road meshes share the same footprints.
        /// </summary>
        public void FinalizeRegionSitesForWorldBuild(
            WorldMapSession session,
            WorldTileCatalog tileCatalog = null)
        {
            // Snap drifted tiles before resolving scatter bounds from live terrains.
            AlignHubTransformToWorldOrigin(session);

            var bounds = ResolvePipelineWorldBounds(session, tileCatalog);
            if (bounds.width <= 0f || bounds.height <= 0f)
                return;

            _pipelineWorldBoundsXZ = bounds;
            _hasPipelineWorldBounds = true;
            ApplyWorldBoundsToLayoutFields(Layout, bounds);
            if (BaseLayout != null)
                ApplyWorldBoundsToLayoutFields(BaseLayout, bounds);

            EnsureAuthoredRegionOnWorldTerrain(bounds, null, session);

            var region = ActiveRegionAsset;
            if (region == null)
                return;

            var regionSeed = ResolveRegionSeed();
            var scatterSeedOverride = region.randomizeRegionSeedPerBuild || fixedRegionSeedOverride != 0
                ? regionSeed
                : 0;
            PrepareActiveRegionSites(region, scatterSeedOverride, regionSeed);
        }

        private WorldMapSizeSettings ResolveMapSizeSettings()
        {
            var stack = transform.Find("WorldBuilderStack");
            var service = stack != null ? stack.GetComponent<WorldBuilderService>() : null;
            return service != null ? service.Profile?.MapSizeSettings : null;
        }

        private LandformProfile ResolveLandformProfileFromStack()
        {
            var stack = transform.Find("WorldBuilderStack");
            var service = stack != null ? stack.GetComponent<WorldBuilderService>() : null;
            return service != null ? service.Profile?.Landforms : null;
        }

        /// <summary>
        ///     Procedural road meshes bake world-space XZ into mesh local coordinates.
        ///     The hub transform must sit on the session world origin or cities render offset.
        ///     WorldTerrainGrid is parented under the hub stack, so any hub move must re-snap
        ///     tile world XZ to the session grid or city pads / heightmaps drift off-map.
        /// </summary>
        private void AlignHubTransformToWorldOrigin(WorldMapSession session)
        {
            var origin = session.WorldOriginXZ;
            var current = transform.position;
            if (!Mathf.Approximately(current.x, origin.x) || !Mathf.Approximately(current.z, origin.y))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.RecordObject(transform, "Align Hub To World Origin");
#endif
                transform.position = new Vector3(origin.x, current.y, origin.y);
                _lastGeneratedRoadNetwork = null;
                _cachedRoadPolylines.Clear();
                _lastGeneratedBounds = default;

                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Aligned hub transform to world origin " + origin +
                    " so procedural meshes match terrain/site world coordinates.",
                    this);
            }

            ResnapWorldTerrainGridToSession(session);
        }

        /// <summary>
        ///     Restores each WorldTerrainGrid tile to session world XZ so hub transform
        ///     moves do not drag terrains away from landforms / city sites.
        /// </summary>
        /// <returns>Number of tiles whose world XZ was corrected.</returns>
        public int ResnapWorldTerrainGridToSession(WorldMapSession session)
        {
            if (session.TileSizeMeters <= 0f || session.TilesPerSide < 1)
                return 0;

            var grid = FindWorldTerrainGridRoot();
            if (grid == null)
                return 0;

            var origin = session.WorldOriginXZ;
            var corrected = ResnapGridChildrenToSession(grid, session, origin);

            if (corrected > 0)
            {
                Debug.Log(
                    "[CityPrefabRoadNetworkBuilder] Re-snapped " + corrected +
                    " WorldTerrainGrid tile(s) to session origin " + origin +
                    " after hub transform change.",
                    this);
            }

            return corrected;
        }

        private static int ResnapGridChildrenToSession(
            Transform grid,
            WorldMapSession session,
            Vector2 origin)
        {
            var tileSize = session.TileSizeMeters;
            var corrected = 0;

            for (var i = 0; i < grid.childCount; i++)
            {
                var child = grid.GetChild(i);
                if (!TryGetInBoundsTileChild(child, session, out var coord))
                    continue;

                if (TryResnapTileChild(child, origin, coord, tileSize))
                    corrected++;
            }

            return corrected;
        }

        private static bool TryGetInBoundsTileChild(
            Transform child,
            WorldMapSession session,
            out WorldTileCoord coord)
        {
            coord = default;
            if (child == null || !TryParseTerrainTileCoord(child.name, out coord))
                return false;

            return coord.X >= 0 && coord.Z >= 0 &&
                   coord.X < session.TilesPerSide && coord.Z < session.TilesPerSide;
        }

        private static bool TryResnapTileChild(
            Transform child,
            Vector2 origin,
            WorldTileCoord coord,
            float tileSize)
        {
            var pos = child.position;
            var expectedX = origin.x + coord.X * tileSize;
            var expectedZ = origin.y + coord.Z * tileSize;
            if (Mathf.Approximately(pos.x, expectedX) && Mathf.Approximately(pos.z, expectedZ))
                return false;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.RecordObject(child, "Resnap Terrain To Session");
#endif
            child.position = new Vector3(expectedX, pos.y, expectedZ);
            return true;
        }

        private static Rect ResolvePipelineWorldBounds(
            WorldMapSession session,
            WorldTileCatalog tileCatalog)
        {
            if (tileCatalog == null)
                return session.WorldBoundsXZ;

            Transform hubRoot = null;
            if (tileCatalog.transform.parent != null)
                hubRoot = tileCatalog.transform.root;

            return WorldTerrainBoundsResolver.Resolve(tileCatalog, session, hubRoot);
        }

        private Transform FindWorldTerrainGridRoot()
        {
            var stack = transform.Find("WorldBuilderStack");
            if (stack != null)
            {
                var underStack = stack.Find("WorldTerrainGrid");
                if (underStack != null)
                    return underStack;
            }

            return transform.Find("WorldTerrainGrid");
        }

        private static bool TryParseTerrainTileCoord(string terrainName, out WorldTileCoord coord)
        {
            coord = default;
            if (string.IsNullOrEmpty(terrainName) || !terrainName.StartsWith("Terrain_", System.StringComparison.Ordinal))
                return false;

            var parts = terrainName.Split('_');
            if (parts.Length < 3)
                return false;
            if (!int.TryParse(parts[1], out var x) || !int.TryParse(parts[2], out var z))
                return false;

            coord = new WorldTileCoord(x, z);
            return true;
        }

        public void ClearPipelineWorldBounds()
        {
            _hasPipelineWorldBounds = false;
            _pipelineWorldBoundsXZ = default;
        }

        [ContextMenu("Snap Layout To Pinned Tile Center")]
        public void SnapLayoutToPinnedTileCenter()
        {
            if (!TrySnapLayoutToPinnedTileCenter(regenerateRoads: false, out var summary))
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] " + summary, this);
            else
                Debug.Log("[CityPrefabRoadNetworkBuilder] " + summary, this);
        }

        [ContextMenu("Snap To Pinned Tile Center And Regenerate Roads")]
        public void SnapLayoutToPinnedTileCenterAndRegenerate()
        {
            if (!TrySnapLayoutToPinnedTileCenter(regenerateRoads: true, out var summary))
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] " + summary, this);
            else
                Debug.Log("[CityPrefabRoadNetworkBuilder] " + summary, this);
        }

        public bool TrySnapLayoutToPinnedTileCenter(bool regenerateRoads, out string summary)
        {
            summary = string.Empty;

            var query = WorldTileInfoUtility.FindPinnedTileQuery();
            if (query == null || !query.TryGetPinnedTileCenterXZ(out var centerXZ))
            {
                summary = "No pinned-tile query available (assign Legacy MapMagicPinnedTileRoadAuthoring).";
                return false;
            }

#if UNITY_EDITOR
            Undo.RecordObject(this, "Snap Layout To Pinned Tile");
#endif
            Layout.centerXZ = centerXZ;
            summary = "Snapped layout center to pinned tile (" +
                      centerXZ.x.ToString("F1") + "," + centerXZ.y.ToString("F1") + ").";

            if (regenerateRoads)
                RegenerateRoadsFromLayout();

            return true;
        }

        private void RegenerateRoadsFromLayout()
        {
            GenerateCityRoadNetwork(useTestLayout: false);
        }

        private void ApplyWorldTerrainBoundsToLayout(CityMathRoadLayout targetLayout)
        {
            if (targetLayout == null) return;

            if (_hasPipelineWorldBounds &&
                _pipelineWorldBoundsXZ.width > 0f &&
                _pipelineWorldBoundsXZ.height > 0f)
            {
                ApplyWorldBoundsToLayoutFields(targetLayout, _pipelineWorldBoundsXZ);
                return;
            }

            if (TryGetWorldTerrainGridBounds(out var gridBounds))
            {
                ApplyWorldBoundsToLayoutFields(targetLayout, gridBounds);
                return;
            }

            if (TryGetPinnedWorldBounds(out var pinnedBounds, out _))
            {
                ApplyWorldBoundsToLayoutFields(targetLayout, pinnedBounds);
                return;
            }

            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null) return;

            var tPos = terrain.transform.position;
            var size = terrain.terrainData.size;
            ApplyWorldBoundsToLayoutFields(
                targetLayout,
                new Rect(tPos.x, tPos.z, size.x, size.z));
        }

        private static void ApplyWorldBoundsToLayoutFields(CityMathRoadLayout targetLayout, Rect bounds)
        {
            targetLayout.worldTerrainXMin = bounds.xMin;
            targetLayout.worldTerrainXMax = bounds.xMax;
            targetLayout.worldTerrainZMin = bounds.yMin;
            targetLayout.worldTerrainZMax = bounds.yMax;
        }

        private static bool TryGetWorldTerrainGridBounds(out Rect boundsXZ) =>
            WorldTileInfoUtility.TryGetWorldTerrainGridBounds(out boundsXZ);

        public static bool TryGetPinnedWorldBounds(out Rect worldBoundsXZ, out int tileCount)
        {
            worldBoundsXZ = default;
            tileCount = 0;
            var query = WorldTileInfoUtility.FindPinnedTileQuery();
            return query != null && query.TryGetPinnedTilesWorldBounds(out worldBoundsXZ, out tileCount);
        }
    }
}
