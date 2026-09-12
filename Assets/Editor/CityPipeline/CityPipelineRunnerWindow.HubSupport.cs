#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Views;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>
    ///     Shared Development Hub resolution, parsing, and report path helpers.
    /// </summary>
    internal sealed partial class CityPipelineRunnerWindow
    {
        internal const string ReportsFolderName = "WorldBuilderReports";
        internal const string TestPayloadFileName = "world-state-test-payload.json";

        internal WorldTestRunReport LatestTestReport => _latestTestReport;

        internal WorldDevelopmentTestOrchestrator TestOrchestrator =>
            _testOrchestrator ??= new WorldDevelopmentTestOrchestrator(this);

        internal static string ReportsDirectory
        {
            get
            {
                var path = Path.Combine(Application.dataPath, "..", "Library", ReportsFolderName);
                Directory.CreateDirectory(path);
                return Path.GetFullPath(path);
            }
        }

        internal static string TestPayloadPath =>
            Path.Combine(ReportsDirectory, TestPayloadFileName);

        internal WorldBuilderService TryGetWorldBuilderService()
        {
            if (!TryResolveBuilder())
                return null;

            return WorldBuilderStackProvisioner.TryGetForBuilder(builder);
        }

        internal WorldBuilderService ResolveWorldBuilderService()
        {
            if (!TryResolveBuilder())
                return null;

            return WorldBuilderStackProvisioner.EnsureForBuilder(builder);
        }

        internal WorldStateManager ResolveStateManager() =>
            ResolveWorldBuilderService()?.StateManager;

        internal WorldBuildingMaterializer ResolveMaterializer()
        {
            if (!TryResolveBuilder())
                return null;

            var stack = builder.transform.Find("WorldBuilderStack");
            return stack != null ? stack.GetComponent<WorldBuildingMaterializer>() : null;
        }

        internal void EnableFastIterationMode()
        {
            roadBuildQuality = RoadBuildQualityMode.FastIteration;
            fastRoadsMode = false;
            SetHubSurfacePaintQuality(SurfacePaintQualityMode.Fast);
        }

        internal void DisableFastIterationMode()
        {
            roadBuildQuality = RoadBuildQualityMode.FullFidelity;
            fastRoadsMode = false;
            // Acceptance / coastal verify: Quality soft paint (not Balanced).
            SetHubSurfacePaintQuality(SurfacePaintQualityMode.Quality);
        }

        internal void SetHubSurfacePaintMode(SurfacePaintQualityMode mode) =>
            SetHubSurfacePaintQuality(mode);

        private const string HubSurfacePaintQualityPrefKey = "Zombera.WorldBuilder.HubSurfacePaintQuality";
        private const string HubFastSurfacePaintPrefKeyLegacy = "Zombera.WorldBuilder.HubFastSurfacePaint";

        private void EnsureHubSurfacePaintQualityDefault()
        {
            if (EditorPrefs.HasKey(HubSurfacePaintQualityPrefKey))
            {
                hubSurfacePaintQuality = (SurfacePaintQualityMode)EditorPrefs.GetInt(
                    HubSurfacePaintQualityPrefKey,
                    (int)SurfacePaintQualityMode.Quality);
                return;
            }

            // One-shot migrate legacy bool: true→Fast, false→Quality; missing→Quality (acceptance default).
            if (EditorPrefs.HasKey(HubFastSurfacePaintPrefKeyLegacy))
            {
                var legacyFast = EditorPrefs.GetBool(HubFastSurfacePaintPrefKeyLegacy, true);
                hubSurfacePaintQuality = legacyFast
                    ? SurfacePaintQualityMode.Fast
                    : SurfacePaintQualityMode.Quality;
            }
            else
            {
                hubSurfacePaintQuality = SurfacePaintQualityMode.Quality;
            }

            EditorPrefs.SetInt(HubSurfacePaintQualityPrefKey, (int)hubSurfacePaintQuality);
        }

        private void SetHubSurfacePaintQuality(SurfacePaintQualityMode mode)
        {
            hubSurfacePaintQuality = mode;
            EditorPrefs.SetInt(HubSurfacePaintQualityPrefKey, (int)mode);
        }

        /// <summary>
        /// FullFidelity roads/bridges/tunnels acceptance: no Fast* shortcuts, no road-mesh cache reuse.
        /// Locked seed = <see cref="ThreeOceansOneMountainReferenceSeed"/> (mountain/pass fixture intent).
        /// </summary>
        internal void ApplyRoadsInfrastructureAcceptanceConfiguration()
        {
            DisableFastIterationMode();
            ApplyVerticalSliceTestConfiguration();
            Debug.Log(
                "[WorldBuilderHub] Roads infrastructure acceptance: FullFidelity, " +
                "FastLandforms/FastRoads/FastBiome=false, ReuseCachedRoads=false, seed=" +
                editorWorldSeed);
        }

        internal void ApplyVerticalSliceTestConfiguration()
        {
            editorMapTier = WorldMapSizeTier.Medium;
            editorWorldSeed = ThreeOceansOneMountainReferenceSeed;
            if (builder == null)
                return;

            Undo.RecordObject(builder, "Apply Vertical Slice Test Settings");
            builder.ReuseCachedRoadsOnSameSeed = false;
            EditorUtility.SetDirty(builder);
        }

        /// <summary>
        /// River / hydrology agent loop: Large map tier + locked seed.
        /// Do not use Medium vertical-slice sizing for soft-bank river iteration.
        /// </summary>
        /// <param name="worldSeed">World seed (0 maps to 1). Default remains the three-oceans fixture (16).</param>
        /// <param name="fixedRegionSeedOverride">
        /// When set, writes <see cref="CityPrefabRoadNetworkBuilder.FixedRegionSeedOverride"/> for repeatable region layout.
        /// </param>
        internal void ApplyHydrologyLoopConfiguration(
            int worldSeed = ThreeOceansOneMountainReferenceSeed,
            int? fixedRegionSeedOverride = null)
        {
            editorMapTier = WorldMapSizeTier.Large;
            editorWorldSeed = worldSeed == 0 ? 1 : worldSeed;
            if (builder == null)
                return;

            Undo.RecordObject(builder, "Apply Hydrology Loop Settings");
            builder.ReuseCachedRoadsOnSameSeed = false;
            if (fixedRegionSeedOverride.HasValue)
                builder.FixedRegionSeedOverride = fixedRegionSeedOverride.Value;
            EditorUtility.SetDirty(builder);
            Debug.Log(
                "[WorldBuilderHub] Hydrology loop config: mapTier=Large seed=" +
                editorWorldSeed +
                " fixedRegionSeed=" +
                builder.FixedRegionSeedOverride);
        }

        internal WorldMapSizeTier EditorMapTier => editorMapTier;

        internal int EditorWorldSeed => editorWorldSeed;

        internal void SetEditorWorldSeed(int seed) =>
            editorWorldSeed = seed == 0 ? 1 : seed;

        /// <summary>Default hub world seed for interior landform variation.</summary>
        internal const int ThreeOceansOneMountainReferenceSeed = 16;

        internal static bool TryParseBuildingId(string text, out WorldEntityId id)
        {
            id = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            const string prefix = "Building:";
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(prefix.Length);

            if (string.IsNullOrWhiteSpace(trimmed))
                return false;

            id = new WorldEntityId(WorldEntityKind.Building, trimmed);
            return true;
        }

        internal static bool TryFindFirstBuilding(WorldStateManager manager, out WorldEntityId id)
        {
            id = default;
            if (manager == null || !manager.HasState)
                return false;

            var state = manager.CaptureCanonicalCopy();
            if (state?.tiles == null)
                return false;

            for (var z = 0; z < state.tiles.Count; z++)
            {
                var buildings = state.tiles[z]?.buildings;
                if (buildings == null)
                    continue;

                for (var i = 0; i < buildings.Count; i++)
                {
                    var building = buildings[i];
                    if (building == null || building.id.kind != WorldEntityKind.Building)
                        continue;

                    id = building.id;
                    return true;
                }
            }

            return false;
        }

        internal static WorldStateEntityCounts CountEntities(WorldStateManager manager)
        {
            var counts = new WorldStateEntityCounts();
            if (manager == null || !manager.HasState)
                return counts;

            var state = manager.CaptureCanonicalCopy();
            if (state?.tiles == null)
                return counts;

            counts.PendingEvents = state.pendingEvents?.Count ?? 0;
            counts.EventHistory = state.eventHistory?.Count ?? 0;
            counts.CurrentHour = state.clock?.currentHour ?? 0L;
            counts.Revision = state.revision;

            for (var i = 0; i < state.tiles.Count; i++)
            {
                var tile = state.tiles[i];
                if (tile == null)
                    continue;

                counts.Regions += tile.regions?.Count ?? 0;
                counts.Settlements += tile.settlements?.Count ?? 0;
                counts.Roads += tile.roads?.Count ?? 0;
                counts.Districts += tile.districts?.Count ?? 0;
                counts.Lots += tile.lots?.Count ?? 0;
                counts.Buildings += tile.buildings?.Count ?? 0;
                counts.Pois += tile.pois?.Count ?? 0;
                counts.TerrainMods += tile.terrain?.modifications?.Count ?? 0;
            }

            return counts;
        }

        internal static bool TryPumpCoroutineSynchronously(
            IEnumerator routine,
            float maxSeconds,
            out string error)
        {
            error = null;
            if (routine == null)
                return true;

            var deadline = EditorApplication.timeSinceStartup + maxSeconds;
            try
            {
                while (routine.MoveNext())
                {
                    if (EditorApplication.timeSinceStartup > deadline)
                    {
                        error = "Operation timed out after " + maxSeconds.ToString("F0") + "s.";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal sealed class WorldStateEntityCounts
        {
            public int Regions;
            public int Settlements;
            public int Roads;
            public int Districts;
            public int Lots;
            public int Buildings;
            public int Pois;
            public int TerrainMods;
            public int PendingEvents;
            public int EventHistory;
            public long CurrentHour;
            public long Revision;
        }
    }
}
#endif
