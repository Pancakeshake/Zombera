using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        // ──────────────────────────────────────────────
        //  Region mode: multiple city sites + inter-city highways.
        //  Step '1. Roads' builds the whole region (all sites + highways);
        //  steps 2-19 operate on the selected active site.
        // ──────────────────────────────────────────────

        [SerializeField, HideInInspector] private CityRegionAsset regionAsset;
        [SerializeField, HideInInspector] private int activeSiteIndex;
        [SerializeField, HideInInspector] private bool regionModeEnabled;

        /// <summary>
        ///     Transient session clone for a pipeline run. Inspector keeps
        ///     <see cref="regionAsset"/> as the authored CityRegion.asset.
        /// </summary>
        [System.NonSerialized] private CityRegionAsset _sessionRegionOverride;

        [Tooltip("0 = normal seed flow. Set > 0 to force the same region layout AND site scatter " +
                 "on every Roads run — repeatable A/B timing tests.")]
        [SerializeField] private int fixedRegionSeedOverride;

        [Tooltip("When enabled, the Roads step reuses the roads already in the scene instead of rebuilding whenever the region seed matches the previous build. Fast iteration through pipeline steps; disable when changing road or layout settings.")]
        [SerializeField] private bool reuseCachedRoadsOnSameSeed = true;

        // Region seed captured by the most recent successful road build, so later
        // pipeline steps derive per-site seeds consistent with the roads on screen.
        private int lastBuiltRegionSeed;

        // Region seed pinned for the current build. ResolveRegionSeed() is called from
        // several independent sites (region build, pad flattening, gizmo footprints) and
        // each call rolled a NEW seed, so pads/highways/gizmos disagreed with the roads
        // from the same run. Serialized so the pinned seed survives the domain reloads
        // that follow a script change.
        [SerializeField, HideInInspector] private int _regionSeedForBuild;

        /// <summary>Authored City Region SO from the Scriptable Objects inspector.</summary>
        public CityRegionAsset RegionAsset
        {
            get => regionAsset;
            set => regionAsset = value;
        }

        /// <summary>
        ///     Region used for builds: session override when present, otherwise the
        ///     authored <see cref="RegionAsset"/>.
        /// </summary>
        public CityRegionAsset ActiveRegionAsset =>
            _sessionRegionOverride != null ? _sessionRegionOverride : regionAsset;

        public bool HasSessionRegionOverride => _sessionRegionOverride != null;

        public void SetSessionRegionOverride(CityRegionAsset sessionRegion)
        {
            if (_sessionRegionOverride != null &&
                _sessionRegionOverride != sessionRegion &&
                _sessionRegionOverride != regionAsset)
            {
                if (Application.isPlaying)
                    Object.Destroy(_sessionRegionOverride);
                else
                    Object.DestroyImmediate(_sessionRegionOverride);
            }

            _sessionRegionOverride = sessionRegion;
            if (sessionRegion != null &&
                (sessionRegion.SiteCount > 0 || sessionRegion.EffectiveScatterSiteCount > 0))
                regionModeEnabled = true;
        }

        /// <summary>Clears the override without destroying it (caller owns lifetime).</summary>
        public void DetachSessionRegionOverride()
        {
            _sessionRegionOverride = null;
        }

        public void ClearSessionRegionOverride()
        {
            if (_sessionRegionOverride != null && _sessionRegionOverride != regionAsset)
            {
                if (Application.isPlaying)
                    Object.Destroy(_sessionRegionOverride);
                else
                    Object.DestroyImmediate(_sessionRegionOverride);
            }

            _sessionRegionOverride = null;
        }

        public bool RegionModeEnabled
        {
            get => regionModeEnabled;
            set => regionModeEnabled = value;
        }

        public int ActiveSiteIndex
        {
            get => ClampActiveSiteIndex(activeSiteIndex);
            set => activeSiteIndex = ClampActiveSiteIndex(value);
        }

        /// <summary>0 = normal seed flow; > 0 forces a fixed region seed for repeatable runs.</summary>
        public int FixedRegionSeedOverride
        {
            get => fixedRegionSeedOverride;
            set => fixedRegionSeedOverride = value;
        }

        /// <summary>When on, the Roads step reuses existing roads for an unchanged region seed.</summary>
        public bool ReuseCachedRoadsOnSameSeed
        {
            get => reuseCachedRoadsOnSameSeed;
            set => reuseCachedRoadsOnSameSeed = value;
        }

        /// <summary>
        ///     True when the next Roads run can reuse the roads already in the scene —
        ///     reuse enabled, a previous build was published, and (region mode) the
        ///     resolved region seed matches the previous build. Single-city keeps the
        ///     legacy "roads already generated" behavior.
        /// </summary>
        public bool CanReuseCachedRoadsForCurrentBuild()
        {
            if (!reuseCachedRoadsOnSameSeed)
                return false;

            // _lastGeneratedRoadNetwork is [NonSerialized], so it is null after every
            // domain reload while the serialized _cachedRoadPolylines survive. Rebuild
            // the runtime view first — otherwise reuse fails and the build destroys the
            // very meshes it was meant to keep.
            EnsureRoadCache();

            if (_lastGeneratedRoadNetwork == null || _cachedRoadPolylines.Count == 0)
                return false;

            return !RegionModeActive ||
                   (lastBuiltRegionSeed != 0 && lastBuiltRegionSeed == ResolveRegionSeed());
        }

        public CityHubSite ActiveSite => ActiveRegionAsset?.GetSite(ActiveSiteIndex);

        public bool RegionModeActive =>
            regionModeEnabled &&
            ActiveRegionAsset != null &&
            (ActiveRegionAsset.SiteCount > 0 || ActiveRegionAsset.EffectiveScatterSiteCount > 0);

        /// <summary>Template layout without any active-site overrides.</summary>
        private CityMathRoadLayout BaseLayout => layoutAsset?.Data ?? layout;

        private int ClampActiveSiteIndex(int index)
        {
            var region = ActiveRegionAsset;
            return region == null || region.SiteCount == 0
                ? 0
                : Mathf.Clamp(index, 0, region.SiteCount - 1);
        }

        private static CityMathRoadLayout ApplySiteToLayout(CityMathRoadLayout template, CityHubSite site) =>
            CityRegionSiteLayoutUtility.ApplySiteToLayout(template, site);

        /// <summary>Layout-resolved flatten footprint for scene gizmos (matches road build bounds).</summary>
        public bool TryResolveSiteFootprintRect(CityHubSite site, int siteIndex, out Rect footprintXZ)
        {
            footprintXZ = default;
            if (Layout == null || site == null)
                return false;

            var siteLayout = ApplySiteToLayout(Layout, site);
            var siteSeed = CityRegionSiteLayoutUtility.ResolveSiteSeed(site, ResolveRegionSeed(), siteIndex);
            siteLayout.GetTerrainFlattenBounds(site.centerXZ, siteSeed, out footprintXZ, out _);
            return footprintXZ.width > 0f && footprintXZ.height > 0f;
        }

        private static int ResolveSiteSeed(CityHubSite site, int regionSeed, int siteIndex) =>
            CityRegionSiteLayoutUtility.ResolveSiteSeed(site, regionSeed, siteIndex);

        /// <summary>
        ///     Region seed for the current build. The first call pins the seed for the
        ///     build (or reuses the pinned one), so every consumer — region layout,
        ///     pad flattening and terrain gizmos — derives from the SAME seed.
        /// </summary>
        public int ResolveRegionSeed()
        {
            if (fixedRegionSeedOverride != 0)
                return fixedRegionSeedOverride;

            if (_regionSeedForBuild != 0)
                return _regionSeedForBuild;

            _regionSeedForBuild = RollRegionSeed();
            return _regionSeedForBuild;
        }

        /// <summary>
        ///     Rolls the seed a fresh build should use and pins it. The roads routine
        ///     calls this on entry so differing settings produce differing layouts.
        /// </summary>
        private int RollRegionSeedForNewBuild()
        {
            _regionSeedForBuild = RollRegionSeed();
            return _regionSeedForBuild;
        }

        private int RollRegionSeed()
        {
            var region = ActiveRegionAsset;
            if (region == null)
                return BaseLayout != null && BaseLayout.layoutSeed != 0 ? BaseLayout.layoutSeed : 12345;

            var configured = region.regionSeed != 0 ? region.regionSeed : BaseLayout.layoutSeed;
            if (configured == 0)
                configured = 12345;

            // Single-city mode rolls a fresh seed per build; mirror that here so
            // region layouts vary unless the asset opts into determinism.
            if (region.randomizeRegionSeedPerBuild)
                return new System.Random().Next(1, int.MaxValue);

            return configured;
        }

        /// <summary>Seed used by the roads already in the scene, when known.</summary>
        private int ResolveBuiltRegionSeed() =>
            lastBuiltRegionSeed != 0 ? lastBuiltRegionSeed : ResolveRegionSeed();

    }
}
