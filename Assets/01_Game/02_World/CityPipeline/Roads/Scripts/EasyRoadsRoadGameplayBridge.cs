using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.Roads
{
    [AddComponentMenu("Zombera/World/EasyRoads Gameplay Road Bridge")]
    [DisallowMultipleComponent]
    public sealed partial class EasyRoadsRoadGameplayBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldTileStreamSource tileStreamBridge;
        [SerializeField] private RoadGameplayService roadGameplayService;
        [SerializeField] private GameplayRoadGraph gameplayRoadGraph;
        [SerializeField] private GameplayRoadDerivedData gameplayRoadDerivedData;

        [Header("Sync")]
        [SerializeField] private bool autoResolveReferences = true;
        [SerializeField] private bool syncOnTileApplied = true;
        [SerializeField] private bool syncOnceOnEnable = true;
        [SerializeField] [Range(1, 8)] private int maxRoadSyncsPerFrame = 1;
        [SerializeField] [Range(0f, 1f)] private float roadSyncDebounceSeconds = 0.1f;
        [SerializeField] [Range(0f, 5f)] private float minSecondsBetweenRoadSyncs = 0.35f;
        [SerializeField] [Range(0f, 10f)] private float minSecondsBetweenUnchangedRoadSyncs = 1.5f;
        [SerializeField] [Min(0.5f)] private float sampleSpacingMeters = 4f;

        [Header("Runtime Bake")]
        [SerializeField] private bool bakeDerivedRoadDataOnSync;
        [SerializeField] [Min(0f)] private float roadSyncSpikeWarningMilliseconds = 30f;

        [Header("Road Conversion")]
        [SerializeField] private GameplayRoadType defaultRoadType = GameplayRoadType.Local;
        [SerializeField] [Min(0.5f)] private float defaultRoadWidthMeters = 8f;
        [SerializeField] private bool includeVehiclesByDefault = true;
        [SerializeField] private bool includeRoadsideLotsByDefault = true;
        [SerializeField] private bool excludeRoughRoadNameTokens = true;
        [SerializeField] private string[] roughRoadNameTokens =
        {
            "dirt", "gravel", "offroad", "trail", "track"
        };

        [Header("City Zone Mapping")]
        [SerializeField]
        [Tooltip("Road name tokens that should force CityCore zoning for dense city blocks.")]
        private string[] cityCoreNameTokens =
        {
            "city_area", "city area", "citycore", "city_core", "downtown", "urban_core"
        };

        [SerializeField]
        [Tooltip("When enabled, any active MapMagic graph name matching a city-core token forces CityCore zoning.")]
        private bool useMapMagicGraphNameForCityCoreZone = true;

        [SerializeField]
        [Tooltip("MapMagic graph-name tokens that force CityCore zoning for converted roads.")]
        private string[] cityCoreGraphNameTokens = { "city_area", "city area", "citycore", "city_core" };

        [SerializeField] private string[] residentialNameTokens = { "res", "residential", "suburb", "housing" };
        [SerializeField] private string[] commercialNameTokens = { "com", "commercial", "market", "shop", "retail" };
        [SerializeField] private string[] industrialNameTokens = { "ind", "industrial", "factory", "plant", "warehouse" };
        [SerializeField] private string[] slumNameTokens = { "slum", "shanty", "favela" };

        [Header("Debug")]
        [SerializeField] private bool logSync;

        private readonly List<EasyRoadLineData> _lineBuffer = new(64);
        private bool _hasPendingRoadSync;
        private float _pendingRoadSyncReadyAt;

        private float _nextAllowedRoadSyncAt;
        private bool _hasLastRuntimeRoadHash;
        private int _lastRuntimeRoadHash;

        public bool HasProcessedRoadSync { get; private set; }
        public bool HasValidRoadData { get; private set; }
        public int PendingRoadSyncRequestCount => _hasPendingRoadSync ? 1 : 0;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshTileStreamSubscription(tileStreamBridge);

            if (syncOnceOnEnable)
                EnqueueRequest();
        }

        private void OnDisable()
        {
            RefreshTileStreamSubscription(null);
            _hasPendingRoadSync = false;
        }

        private void Update()
        {
            ProcessQueue();
        }

        public void Configure(WorldTileStreamSource streamBridge)
        {
            if (streamBridge != null) tileStreamBridge = streamBridge;

            ResolveReferences();
            RefreshTileStreamSubscription(tileStreamBridge);
            EnqueueRequest();
        }

        private void ResolveReferences()
        {
            if (!autoResolveReferences) return;

            if (tileStreamBridge == null)
                tileStreamBridge = WorldTileStreamSourceUtility.FindBridge();

            if (roadGameplayService == null)
                roadGameplayService = FindFirstObjectByType<RoadGameplayService>();

            if (roadGameplayService != null)
            {
                if (gameplayRoadGraph == null)
                    gameplayRoadGraph = roadGameplayService.RoadGraph;

                if (gameplayRoadDerivedData == null)
                    gameplayRoadDerivedData = roadGameplayService.BakedData;
            }
        }

        private void RefreshTileStreamSubscription(WorldTileStreamSource bridge)
        {
            WorldTileStreamSourceUtility.UnsubscribeTileApplied(tileStreamBridge, HandleTileApplied);

            tileStreamBridge = bridge;

            if (tileStreamBridge == null || !syncOnTileApplied || !isActiveAndEnabled) return;
            WorldTileStreamSourceUtility.SubscribeTileApplied(tileStreamBridge, HandleTileApplied);
        }

        private void HandleTileApplied(WorldTileInfo tile)
        {
            _ = tile;
            EnqueueRequest();
        }

        private void EnqueueRequest()
        {
            _hasPendingRoadSync = true;
            var debounceSeconds = Mathf.Max(0f, roadSyncDebounceSeconds);
            _pendingRoadSyncReadyAt = Mathf.Max(_pendingRoadSyncReadyAt, Time.unscaledTime + debounceSeconds);
        }

        private void ProcessQueue()
        {
            if (!_hasPendingRoadSync) return;

            var maxPerFrame = Mathf.Max(1, maxRoadSyncsPerFrame);
            var processed = 0;
            while (processed < maxPerFrame && _hasPendingRoadSync)
            {
                if (Time.unscaledTime < _nextAllowedRoadSyncAt) break;
                if (Time.unscaledTime < _pendingRoadSyncReadyAt) break;

                _hasPendingRoadSync = false;
                _pendingRoadSyncReadyAt = 0f;

                processed++;
                HasProcessedRoadSync = true;

                var syncStartedAt = Time.realtimeSinceStartup;
                var outcome = TrySyncRoadGameplayFromEasyRoads(out var roadCount, out var segmentCount);
                var syncElapsedMs = (Time.realtimeSinceStartup - syncStartedAt) * 1000f;

                LogSyncOutcomeIfNeeded(outcome, roadCount, segmentCount, syncElapsedMs);
                _nextAllowedRoadSyncAt = Time.unscaledTime + ResolveSyncCooldown(outcome);
            }
        }

        private void LogSyncOutcomeIfNeeded(RoadSyncOutcome outcome, int roadCount, int segmentCount, float syncElapsedMs)
        {
            var isSpike = roadSyncSpikeWarningMilliseconds > 0f && syncElapsedMs >= roadSyncSpikeWarningMilliseconds;
            var shouldWarn = outcome == RoadSyncOutcome.Failed || (isSpike && outcome == RoadSyncOutcome.Synced);
            if (!logSync && !shouldWarn) return;

            var message = outcome switch
            {
                RoadSyncOutcome.Synced =>
                    $"[EasyRoadsRoadGameplayBridge] Synced {roadCount} EasyRoads roads into {segmentCount} gameplay segments in {syncElapsedMs:0.0} ms.",
                RoadSyncOutcome.SkippedUnchanged =>
                    $"[EasyRoadsRoadGameplayBridge] Skipped unchanged road sync ({roadCount} roads, {segmentCount} segments) in {syncElapsedMs:0.0} ms.",
                _ =>
                    $"[EasyRoadsRoadGameplayBridge] Road sync failed (missing references or EasyRoads runtime unavailable) after {syncElapsedMs:0.0} ms."
            };

            if (shouldWarn)
                Debug.LogWarning(message, this);
            else
                Debug.Log(message, this);
        }

        private float ResolveSyncCooldown(RoadSyncOutcome outcome)
        {
            return outcome == RoadSyncOutcome.SkippedUnchanged
                ? Mathf.Max(minSecondsBetweenRoadSyncs, minSecondsBetweenUnchangedRoadSyncs)
                : Mathf.Max(0f, minSecondsBetweenRoadSyncs);
        }

        private RoadSyncOutcome TrySyncRoadGameplayFromEasyRoads(out int roadCount, out int segmentCount)
        {
            roadCount = 0;
            segmentCount = 0;

            ResolveReferences();
            if (roadGameplayService == null) return RoadSyncOutcome.Failed;

            if (gameplayRoadGraph == null)
                gameplayRoadGraph = roadGameplayService.RoadGraph;

            if (gameplayRoadGraph == null) return RoadSyncOutcome.Failed;

            _lineBuffer.Clear();
            var hasRuntimeRoads = EasyRoadsRuntimeApi.TryCollectRoadCenterLines(_lineBuffer);
            var runtimeRoadHash = ComputeRoadSnapshotHash(_lineBuffer, out roadCount);
            if (_hasLastRuntimeRoadHash && runtimeRoadHash == _lastRuntimeRoadHash)
            {
                segmentCount = gameplayRoadGraph.Segments?.Count ?? 0;
                HasValidRoadData = roadGameplayService.RuntimeSamples.Count > 0;
                return RoadSyncOutcome.SkippedUnchanged;
            }

            if (!hasRuntimeRoads || roadCount == 0)
            {
                ApplyRoadGraphData(
                    new List<RoadGraphNode>(0),
                    new List<RoadGraphSegment>(0),
                    new List<RoadSpawnPoint>(0),
                    new List<RoadTownConnection>(0),
                    new List<RoadBiomeConnection>(0));

                _lastRuntimeRoadHash = runtimeRoadHash;
                _hasLastRuntimeRoadHash = true;
                HasValidRoadData = roadGameplayService.RuntimeSamples.Count > 0;
                return RoadSyncOutcome.Synced;
            }

            var nodes = new List<RoadGraphNode>(roadCount * 2);
            var segments = new List<RoadGraphSegment>(roadCount);
            var spawnPoints = new List<RoadSpawnPoint>(0);
            var townConnections = new List<RoadTownConnection>(0);
            var biomeConnections = new List<RoadBiomeConnection>(0);
            var forceCityCoreByGraphName = ShouldForceCityCoreFromMapGraphName();

            var lineIndex = -1;
            foreach (var line in _lineBuffer)
            {
                lineIndex++;
                if (line.Points == null || line.Points.Count < 2) continue;
                if (excludeRoughRoadNameTokens && ContainsAnyToken(line.Name, roughRoadNameTokens)) continue;

                var startNodeId = $"easyroads_node_{lineIndex}_start";
                var endNodeId = $"easyroads_node_{lineIndex}_end";

                nodes.Add(new RoadGraphNode { id = startNodeId, position = line.Points[0] });
                nodes.Add(new RoadGraphNode { id = endNodeId, position = line.Points[^1] });

                var roadType = ResolveRoadTypeFromName(line.Name);
                var cityZone = ResolveCityZoneFromName(line.Name, roadType, forceCityCoreByGraphName);

                segments.Add(BuildRoadGraphSegment(line, lineIndex, roadType, cityZone));
            }

            segmentCount = segments.Count;
            ApplyRoadGraphData(nodes, segments, spawnPoints, townConnections, biomeConnections);

            _lastRuntimeRoadHash = runtimeRoadHash;
            _hasLastRuntimeRoadHash = true;
            HasValidRoadData = roadGameplayService.RuntimeSamples.Count > 0;
            return RoadSyncOutcome.Synced;
        }

        private RoadGraphSegment BuildRoadGraphSegment(EasyRoadLineData line, int lineIndex, GameplayRoadType roadType, RoadCityZone cityZone)
        {
            var isCityCore = cityZone == RoadCityZone.CityCore;
            return new RoadGraphSegment
            {
                id = $"easyroads_segment_{lineIndex}",
                startNodeId = $"easyroads_node_{lineIndex}_start",
                endNodeId = $"easyroads_node_{lineIndex}_end",
                roadType = roadType,
                speedModifier = ResolveSpeedModifier(roadType),
                noiseLevel = ResolveNoiseLevel(roadType),
                contributesPatrolRoutes = true,
                supportsVehicles = includeVehiclesByDefault && roadType != GameplayRoadType.Footpath,
                contributesLootZone = roadType != GameplayRoadType.Highway,
                lootZoneWeight = roadType == GameplayRoadType.Highway ? 0.05f : 0.25f,
                laneCount = roadType == GameplayRoadType.Highway ? 4 : 2,
                laneWidthMeters = 3f,
                navArea = ResolveNavArea(roadType),
                contributesPathArea = true,
                supportsSidewalks = roadType == GameplayRoadType.Local || roadType == GameplayRoadType.Arterial,
                supportsDecals = true,
                contributesRoadsideLots = includeRoadsideLotsByDefault && roadType != GameplayRoadType.Highway && roadType != GameplayRoadType.Footpath,
                roadsideLotWeight = ResolveRoadsideLotWeight(isCityCore, roadType),
                roadsideLotSpacingMeters = ResolveRoadsideLotSpacing(isCityCore, roadType),
                roadsideLotDepthMeters = ResolveRoadsideLotDepth(isCityCore),
                cityZone = cityZone,
                widthMeters = Mathf.Max(0.5f, line.WidthMeters > 0f ? line.WidthMeters : defaultRoadWidthMeters),
                controlPoints = line.Points
            };
        }

        private static RoadNavAreaType ResolveNavArea(GameplayRoadType roadType)
        {
            return roadType switch
            {
                GameplayRoadType.Footpath => RoadNavAreaType.Sidewalk,
                GameplayRoadType.DirtTrack => RoadNavAreaType.Offroad,
                _ => RoadNavAreaType.Road
            };
        }

        private static float ResolveRoadsideLotWeight(bool isCityCore, GameplayRoadType roadType)
        {
            if (isCityCore) return 1f;
            return roadType == GameplayRoadType.Local ? 1f : 0.6f;
        }

        private static float ResolveRoadsideLotSpacing(bool isCityCore, GameplayRoadType roadType)
        {
            if (isCityCore) return 10f;
            return roadType == GameplayRoadType.Local ? 16f : 22f;
        }

        private static float ResolveRoadsideLotDepth(bool isCityCore)
        {
            return isCityCore ? 12f : 14f;
        }
        private void ApplyRoadGraphData(
            List<RoadGraphNode> nodes,
            List<RoadGraphSegment> segments,
            List<RoadSpawnPoint> spawnPoints,
            List<RoadTownConnection> townConnections,
            List<RoadBiomeConnection> biomeConnections)
        {
            gameplayRoadGraph.ReplaceData(nodes, segments, spawnPoints, townConnections, biomeConnections);

            if (gameplayRoadDerivedData == null || !bakeDerivedRoadDataOnSync)
            {
                roadGameplayService.Configure(gameplayRoadGraph, null);
                return;
            }

            GameplayRoadBaker.BakeIntoAsset(gameplayRoadGraph, gameplayRoadDerivedData, sampleSpacingMeters);
            roadGameplayService.Configure(gameplayRoadGraph, gameplayRoadDerivedData);
        }

        private int ComputeRoadSnapshotHash(List<EasyRoadLineData> lines, out int includedRoadCount)
        {
            includedRoadCount = 0;
            if (lines == null || lines.Count == 0) return 0;

            unchecked
            {
                var hash = 17;

                foreach (var line in lines)
                {
                    if (line == null || line.Points == null || line.Points.Count < 2) continue;
                    if (excludeRoughRoadNameTokens && ContainsAnyToken(line.Name, roughRoadNameTokens)) continue;

                    includedRoadCount++;
                    hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(line.Name);
                    hash = hash * 31 + line.Points.Count;

                    foreach (var pt in line.Points)
                        hash = hash * 31 + HashQuantizedPoint(pt);
                }

                return includedRoadCount > 0 ? hash : 0;
            }
        }

        private static int HashQuantizedPoint(Vector3 point)
        {
            unchecked
            {
                var x = Mathf.RoundToInt(point.x * 20f);
                var y = Mathf.RoundToInt(point.y * 20f);
                var z = Mathf.RoundToInt(point.z * 20f);
                var hash = x;
                hash = (hash * 397) ^ y;
                hash = (hash * 397) ^ z;
                return hash;
            }
        }

        private enum RoadSyncOutcome
        {
            Failed,
            Synced,
            SkippedUnchanged
        }

        private sealed class EasyRoadLineData
        {
            public readonly string Name;
            public readonly List<Vector3> Points;
            public readonly float WidthMeters;

            public EasyRoadLineData(string name, List<Vector3> points, float widthMeters)
            {
                Name = name ?? string.Empty;
                Points = points ?? new List<Vector3>();
                WidthMeters = widthMeters;
            }
        }
    }
}
