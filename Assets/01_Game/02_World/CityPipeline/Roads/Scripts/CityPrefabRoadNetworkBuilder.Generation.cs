using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        // ──────────────────────────────────────────────
        //  Road generation pipeline
        // ──────────────────────────────────────────────

        private bool TryPrepareRoadGeneration(out RoadNetworkSettings settings)
        {
            if (ResolvedAutoEnsureMinimalRoadStack)
                EnsureMinimalRoadStack();
            else
                RefreshReferences();

            settings = roadNetworkSettings != null
                ? roadNetworkSettings
                : ResolveFallbackRoadNetworkSettings();
            if (settings == null)
            {
                Debug.LogWarning(
                    "[CityPrefabRoadNetworkBuilder] RoadNetworkSettings missing for procedural backend.",
                    this);
                return false;
            }

            return true;
        }

        private CityMathRoadLayout PrepareWorkingLayout(bool clipStreetsToArterialRing)
        {
            // Region mode operates on the template layout; per-site overrides are
            // applied inside TryBuildRegionalNetworkContext. Single-city mode is unchanged.
            var workingLayout = RegionModeActive ? BaseLayout : Layout;
            workingLayout.Normalize();
            if (clipStreetsToArterialRing && workingLayout.generateStreetGrid && workingLayout.generateArterialRing)
                workingLayout.clipLocalStreetsToArterialRing = true;
            if (ResolvedAlignLayoutCenterToTransform && !RegionModeActive)
                workingLayout.centerXZ = new Vector2(transform.position.x, transform.position.z);
            return workingLayout;
        }

        private bool TryBuildRoadNetworkContext(
            CityMathRoadLayout workingLayout,
            RoadNetworkSettings settings,
            bool useTestLayout,
            out CityRoadNetworkBuildContext buildContext)
        {
            buildContext = default;
            if (useTestLayout)
            {
                buildContext = BuildTIntersectionTestContext(workingLayout, settings);
                return ValidateBuiltNetwork(ref buildContext);
            }

            if (RegionModeActive)
                return TryBuildRegionalNetworkContext(workingLayout, settings, out buildContext);

            return TryBuildFullCityNetworkContext(workingLayout, settings, out buildContext);
        }

        private CityRoadNetworkBuildContext BuildTIntersectionTestContext(
            CityMathRoadLayout workingLayout,
            RoadNetworkSettings settings)
        {
            var center = ResolvedAlignLayoutCenterToTransform
                ? new Vector2(transform.position.x, transform.position.z)
                : workingLayout.centerXZ;
            var width = settings.ResolveWidthMeters(RoadClass.Local);
            return new CityRoadNetworkBuildContext
            {
                Network = CityMathRoadLayoutGenerator.GenerateTIntersectionTest(center, ResolvedTTestArmLengthMeters, width),
                Bounds = CityMathRoadLayoutGenerator.ComputeTIntersectionTestBounds(center, ResolvedTTestArmLengthMeters)
            };
        }

        private bool TryBuildFullCityNetworkContext(
            CityMathRoadLayout workingLayout,
            RoadNetworkSettings settings,
            out CityRoadNetworkBuildContext buildContext)
        {
            buildContext = default;
            workingLayout.RollNewSeed();
            var seed = workingLayout.layoutSeed;
            var resolved = workingLayout.Resolve(seed);

            if (workingLayout.generateHighwayExits || _hasPipelineWorldBounds)
                ApplyWorldTerrainBoundsToLayout(workingLayout);

            Debug.Log("[CityPrefabRoadNetworkBuilder] Seed=" + seed +
                ", halfWidth=" + resolved.HalfWidthMeters.ToString("F0") +
                ", halfDepth=" + resolved.HalfDepthMeters.ToString("F0") +
                " (range: " + workingLayout.cityHalfWidthMinMeters + "-" + workingLayout.cityHalfWidthMaxMeters +
                " x " + workingLayout.cityHalfDepthMinMeters + "-" + workingLayout.cityHalfDepthMaxMeters + ")" +
                ", topology=uniform",
                this);

            var network = CityMathRoadLayoutGenerator.Generate(workingLayout, settings, seed);
            if (network.Roads.Count == 0)
            {
                Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Math layout produced zero roads.", this);
                return false;
            }

            if (!ValidateJunctionBuildBudget(workingLayout))
            {
                Debug.LogError(
                    "[CityPrefabRoadNetworkBuilder] Build blocked: estimated " + workingLayout.EstimateSplitSegmentCount() +
                    " split segments exceeds hard limit (" +
                    ProceduralRoadSystem.CityPrefabMaxSplitSegmentsHardBlock +
                    "). Increase block spacing (streetSpacingMeters / blockSpacingMinMeters ≥ 80) " +
                    "or shrink city half-extents to ≤150 m.", this);
                return false;
            }

            var bounds = ExpandBoundsForArterialRing(workingLayout, resolved, workingLayout.ComputeBoundsRect(resolved));
            workingLayout.GetTerrainFlattenBounds(workingLayout.centerXZ, seed, out var innerFlattenRect, out _);
            var outerFlattenRect = CityRegionSiteLayoutUtility.ExpandRect(
                innerFlattenRect,
                TerrainFlattenOuterMarginMeters);
            buildContext = new CityRoadNetworkBuildContext
            {
                Network = network,
                Bounds = bounds,
                InnerFlattenRect = innerFlattenRect,
                OuterFlattenRect = outerFlattenRect,
                HasInnerFlattenRect = true
            };
            return ValidateBuiltNetwork(ref buildContext);
        }

        /// <summary>
        ///     Returns false when the estimated segment count exceeds the hard budget,
        ///     blocking the build with an actionable error message.
        ///     Warns at the soft limit (100) but only blocks at the hard limit (5000 by default).
        /// </summary>
        private bool ValidateJunctionBuildBudget(CityMathRoadLayout workingLayout)
        {
            if (!CreateJunctionConnectors)
                return true;

            var estimated = workingLayout.EstimateSplitSegmentCount();

            if (estimated <= ProceduralRoadSystem.CityPrefabMaxSplitSegmentsWithJunctions)
                return true;

            if (estimated <= ProceduralRoadSystem.CityPrefabMaxSplitSegmentsHardBlock)
            {
                Debug.LogWarning(
                    "[CityPrefabRoadNetworkBuilder] Dense grid (~" + estimated +
                    " split segments). X/T connector build may be slow — consider wider block spacing.",
                    this);
                return true;
            }

            // Hard block: >5000 segments takes too long even with the P0 optimizations.
            return false;
        }

        private static Rect ExpandBoundsForArterialRing(
            CityMathRoadLayout workingLayout,
            CityMathRoadLayoutResolved resolved,
            Rect bounds)
        {
            if (workingLayout.footprintShape != CityFootprintShape.Square || !workingLayout.generateArterialRing)
                return bounds;

            CityMathRoadLayoutGenerator.ResolveEffectiveSquareArterialRingBounds(
                workingLayout,
                resolved,
                workingLayout.centerXZ,
                out var ringX0,
                out var ringX1,
                out var ringZ0,
                out var ringZ1);

            const float ringPad = 12f;
            var xMin = Mathf.Min(bounds.xMin, ringX0 - ringPad);
            var yMin = Mathf.Min(bounds.yMin, ringZ0 - ringPad);
            var xMax = Mathf.Max(bounds.xMax, ringX1 + ringPad);
            var yMax = Mathf.Max(bounds.yMax, ringZ1 + ringPad);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private bool ValidateBuiltNetwork(ref CityRoadNetworkBuildContext buildContext)
        {
            if (!ValidatePipelineHighwayRequirement(buildContext.Network))
                return false;

            if (buildContext.Network.Roads.Count != 0)
                return true;

            Debug.LogWarning("[CityPrefabRoadNetworkBuilder] Layout produced zero roads.", this);
            return false;
        }

        private bool ValidatePipelineHighwayRequirement(RoadNetworkRuntime network)
        {
            var settings = HubRoadNetworkSettings;
            if (settings == null || !settings.connectCitiesWithHighways)
                return true;

            var regionSiteCount = ActiveRegionAsset != null ? ActiveRegionAsset.SiteCount : 0;
            var artifactCityCount = _infrastructureArtifacts?.Sites?.CitySites?.Count ?? 0;
            var townCount = network?.TownNodes?.Count ?? 0;
            var multiCityCount = Mathf.Max(regionSiteCount, Mathf.Max(artifactCityCount, townCount));
            if (multiCityCount < 2)
                return true;
            if (CountHighwayRoads(network) > 0)
                return true;

            Debug.LogWarning(
                "[CityPrefabRoadNetworkBuilder] connectCitiesWithHighways with " + multiCityCount +
                " cities/towns expected at least one Highway road, but none were built.",
                this);
            return false;
        }

        private static int CountHighwayRoads(RoadNetworkRuntime network)
        {
            if (network?.Roads == null)
                return 0;

            var count = 0;
            for (var i = 0; i < network.Roads.Count; i++)
            {
                if (network.Roads[i]?.roadClass == RoadClass.Highway)
                    count++;
            }

            return count;
        }

        [ContextMenu("Roll Layout Seed")]
        public void RollLayoutSeed()
        {
            var workingLayout = Layout;
            workingLayout.RollNewSeed();
            Debug.Log("[CityPrefabRoadNetworkBuilder] Rolled layout seed to " + workingLayout.layoutSeed + ". Click Generate City Roads.", this);
        }

        [ContextMenu("Clear Generated Roads")]
        public void ClearGeneratedRoads()
        {
            RefreshReferences();
            HighwayRoadHeightProfiles.Clear();
            ProceduralCityRoadBuilder.Clear(transform);
            ClearGeneratedInfrastructureContent();

            if (proceduralRoadSystem != null)
                proceduralRoadSystem.ClearPinnedTilePreviewRoads();

            ClearRoadDecals();
            ClearFootpaths();
            ClearAllStreetscapeRoadPhase();
            // Named areas are district output, not road output. Keep them in
            // place while rebuilding roads so the Roads stage can be inspected
            // against the existing city pads and district footprints. The
            // full city reset owns district cleanup, and GenerateNamedAreas
            // replaces them when its pipeline stage runs.
            _lastGeneratedRoadNetwork = null;
            _cachedRoadPolylines.Clear();
            _lastJunctionRegistry.Clear();
            _lastGeneratedBounds = default;
        }

        /// <summary>
        ///     Removes every piece of generated city content from the scene —
        ///     the runtime equivalent of the inspector's 'Clear All' button.
        ///     Pair with <see cref="ResetCityTerrainFootprint()"/> to hard-erase
        ///     city splat paint back to grass (do not MapMagic Refresh — City
        ///     Surfaces would re-stamp concrete/paver).
        /// </summary>
        [ContextMenu("Clear All Generated City Content")]
        public void ClearAllGeneratedCityContent()
        {
            // Bulk Reset: skip per-object Undo (thousands of DestroyImmediate
            // registrations were the dominant cost of the clear half).
            SuppressEditorDestroyUndo = true;
            try
            {
                RefreshReferences();
                // Buildings / lots / parks live under CityNamedAreas — clear
                // their district-owned content before clearing road content.
                ClearPlacedBuildings();
                ClearParks();
                ClearTrees();
                ClearLotDecorations();
                ClearDistrictLotTerrain();
                ClearDistrictFences();
                ClearDistrictLots();
                ClearStreetLamps();
                ClearNamedAreas();
                ClearGeneratedRoads();
                CityPipelineFailureMarkers.Clear();
                LogRemainingGeneratedRoots();
            }
            finally
            {
                SuppressEditorDestroyUndo = false;
            }
        }

        private void LogRemainingGeneratedRoots()
        {
            var areas = transform.Find(CityNamedAreasContainerName);

            // Reads the procedural root. The legacy EasyRoads root has no creator left,
            // so the old lookup reported 0/0 forever and hid real residue.
            var roadNetwork = ProceduralCityRoadBuilder.FindNetworkRoot(transform);
            var roadCount = roadNetwork != null ? roadNetwork.childCount : 0;
            var areaCount = areas != null ? areas.childCount : 0;
            Debug.Log(
                "[CityPrefabRoadNetworkBuilder] Clear All residual: areas=" + areaCount +
                " roadLayers=" + roadCount + ".",
                this);
        }

        /// <summary>
        ///     Rebuilds _lastGeneratedRoadNetwork from serialized _cachedRoadPolylines
        ///     after a domain reload / recompile when the non-serialized field was lost.
        /// </summary>
        private void EnsureRoadCache()
        {
            if (_lastGeneratedRoadNetwork != null)
                return;
            if (_cachedRoadPolylines == null || _cachedRoadPolylines.Count == 0)
                return;

            var network = new RoadNetworkRuntime(layout?.layoutSeed ?? 12345);
            for (var i = 0; i < _cachedRoadPolylines.Count; i++)
                network.AddRoad(_cachedRoadPolylines[i]);
            _lastGeneratedRoadNetwork = network;
        }

        private static RoadNetworkSettings ResolveFallbackRoadNetworkSettings()
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<RoadNetworkSettings>(
                "Assets/02_Shared/ScriptableObjects/World/RoadNetworkSettings.asset");
            if (asset != null)
                return asset;
#endif
            return ScriptableObject.CreateInstance<RoadNetworkSettings>();
        }
    }
}
