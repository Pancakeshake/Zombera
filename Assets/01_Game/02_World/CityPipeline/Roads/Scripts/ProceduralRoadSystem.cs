using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.Roads
{
    [AddComponentMenu("Zombera/World/Procedural Road System")]
    public sealed partial class ProceduralRoadSystem : MonoBehaviour
    {
        internal const string DedicatedCityRoadStackName = "CityRoadStack";

        public event Action<WorldTileInfo> RoadsApplied;

        internal static bool IsDedicatedCityRoadStackTransform(Transform transform)
        {
            var current = transform;
            while (current != null)
            {
                if (current.name == DedicatedCityRoadStackName)
                    return true;

                current = current.parent;
            }

            return false;
        }

        private bool IsDedicatedCityRoadStack() => IsDedicatedCityRoadStackTransform(transform);

        [Header("References")]
        [SerializeField] private WorldTileStreamSource tileStreamBridge;
        [SerializeField] private RoadNetworkSettings settings;
        [SerializeField] private RoadGameplayService roadGameplayService;

        [Header("Runtime Wiring")]
        [SerializeField] private bool autoResolveReferences = true;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("When enabled, roads are generated as soon as each terrain tile is applied. Disable for staged generation.")]
        private bool autoGenerateOnTileApply = true;

        [Header("Procedural Tile Meshes")]
        [SerializeField]
        [Tooltip("When enabled, tile roads use procedural strip meshes.")]
        private bool useProceduralTileMeshes = true;

        [Header("Editor Pinned-Tile Authoring")]
        [SerializeField]
        [Tooltip("Set by MapMagic Pinned Tile Road Authoring during editor preview. Does not affect play-mode tile streaming.")]
        private bool enableEditorPinnedTileAuthoringMode;

        [SerializeField] private string lastAuthoringPinnedTileLabel = string.Empty;
        [SerializeField] private int lastAuthoringPreviewRoadCount;
        [SerializeField] private float lastAuthoringRegenDurationSeconds;

        private RoadNetworkRuntime _globalNetwork;
        private readonly Dictionary<(int x, int z), GameObject> _proceduralTileRoots = new();
        private WorldTileStreamSource _activeBridge;
        private IWorldTileGameplayEvents _activeBridgeEvents;

        /// <summary>Legacy EasyRoads sidewalk flag. Always false after the EasyRoads purge.</summary>
        internal static bool NativeSidewalksConfigured => false;

        private int _processedTileCount;
        private bool _savedAutoGenerateOnTileApply = true;

        public const string AuthoringPreviewRootPrefix = "Authoring Preview";
        public const int CityPrefabMaxSplitSegmentsWithJunctions = 100;

        /// <summary>
        ///     Hard segment budget: builds exceeding this are blocked with an actionable error
        ///     instead of running for 5+ minutes. Set to 0 to disable the hard block.
        /// </summary>
        public const int CityPrefabMaxSplitSegmentsHardBlock = 5000;

        public RoadNetworkRuntime GlobalNetwork => _globalNetwork;
        public int ProcessedTileCount => _processedTileCount;
        public bool IsEditorPinnedTileAuthoringActive => enableEditorPinnedTileAuthoringMode;
        public string LastAuthoringPinnedTileLabel => lastAuthoringPinnedTileLabel;
        public int LastAuthoringPreviewRoadCount => lastAuthoringPreviewRoadCount;
        public float LastAuthoringRegenDurationSeconds => lastAuthoringRegenDurationSeconds;

        public void SetGlobalNetwork(RoadNetworkRuntime network)
        {
            _globalNetwork = network;
            _cachedGlobalNetworkSeed = network != null ? network.Seed : int.MinValue;
        }

        public void Configure(WorldTileStreamSource streamBridge, RoadGameplayService gameplayService = null)
        {
            if (streamBridge != null) tileStreamBridge = streamBridge;
            if (gameplayService != null) roadGameplayService = gameplayService;

            ResolveReferences();
            RefreshTileStreamSubscription(tileStreamBridge);
            TryGenerateRoadsForDeployedTilesIfNeeded();
        }

        private void TryGenerateRoadsForDeployedTilesIfNeeded()
        {
            if (IsDedicatedCityRoadStack()) return;
            if (!Application.isPlaying || !isActiveAndEnabled || !autoGenerateOnTileApply) return;
            if (!IsWorldSessionActiveForRoadGeneration()) return;
            if (_proceduralTileRoots.Count > 0) return;

            var bridge = tileStreamBridge != null ? tileStreamBridge : WorldTileStreamSourceUtility.FindBridge();
            if (bridge == null)
            {
                if (StreamedWorldMetrics.MapMagicTileAppliedEvents > 0)
                    GenerateRoadsForFinishedTerrainInternal();
                return;
            }

            var tiles = new List<WorldTileInfo>(8);
            bridge.CopyTilesAtOrAbove(WorldTileState.TerrainReady, tiles);
            if (tiles.Count == 0 && StreamedWorldMetrics.MapMagicTileAppliedEvents <= 0)
                return;

            GenerateRoadsForFinishedTerrainInternal();
        }

        private void ResolveReferences()
        {
            if (!autoResolveReferences) return;

            if (tileStreamBridge == null)
                tileStreamBridge = WorldTileStreamSourceUtility.FindBridge();

            if (roadGameplayService == null)
                roadGameplayService = FindFirstObjectByType<RoadGameplayService>();
        }

        public void GenerateRoadsForFinishedTerrain()
        {
            GenerateRoadsForFinishedTerrainInternal();
        }
    }
}
