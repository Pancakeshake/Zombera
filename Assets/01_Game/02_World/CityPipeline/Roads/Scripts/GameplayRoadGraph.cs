using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public enum GameplayRoadType
    {
        Highway,
        Arterial,
        Local,
        DirtTrack,
        ServiceRoad,
        Footpath
    }

    public enum RoadCityZone
    {
        Unknown,
        Mixed,
        Residential,
        Commercial,
        Industrial,
        Service,
        Slum,
        CityCore
    }

    public enum RoadNavAreaType
    {
        Default,
        Road,
        Sidewalk,
        Restricted,
        Offroad
    }

    public enum RoadSpawnRole
    {
        AmbientZombie,
        Patrol,
        Vehicle,
        Loot,
        Survivor,
        Encounter
    }

    [Serializable]
    public sealed class RoadGraphNode
    {
        public string id = string.Empty;
        public Vector3 position;
        public bool isTownConnection;
        public string townId = string.Empty;
        public string tag = string.Empty;
    }

    [Serializable]
    public sealed class RoadGraphSegment
    {
        public string id = string.Empty;
        public string startNodeId = string.Empty;
        public string endNodeId = string.Empty;

        [Header("Gameplay")]
        public GameplayRoadType roadType = GameplayRoadType.Local;

        [Min(0.1f)] public float speedModifier = 1f;
        [Range(0f, 1f)] public float noiseLevel = 0.15f;
        public bool contributesPatrolRoutes = true;
        public bool supportsVehicles;
        public bool contributesLootZone;

        [Range(0f, 1f)] public float lootZoneWeight = 0.25f;

        [Header("Topology Metadata")]
        [Min(1)] public int laneCount = 2;
        [Min(0.5f)] public float laneWidthMeters = 3f;

        [Tooltip("Optional biome id the segment starts in.")]
        public string fromBiomeId = string.Empty;

        [Tooltip("Optional biome id the segment ends in.")]
        public string toBiomeId = string.Empty;

        [Range(0f, 1f)] public float biomeConnectionWeight = 1f;

        [Header("Navigation")]
        public RoadNavAreaType navArea = RoadNavAreaType.Road;
        public bool contributesPathArea = true;

        [Header("Visual Extras")]
        public bool supportsSidewalks;
        [Min(0f)] public float sidewalkWidthMeters = 1.5f;
        public bool supportsDecals;
        [Range(0f, 1f)] public float decalDensity = 0.5f;

        [Header("Roadside Lots")]
        public bool contributesRoadsideLots = true;
        [Range(0f, 1f)] public float roadsideLotWeight = 1f;
        [Min(1f)] public float roadsideLotSpacingMeters = 16f;
        [Min(1f)] public float roadsideLotDepthMeters = 14f;

        [Header("City Zone")]
        [Tooltip("Zone tag used by zone-aware city-lot spawning (for example, City_Area roads -> CityCore).")]
        public RoadCityZone cityZone = RoadCityZone.Mixed;

        [Header("Shape")]
        [Min(0.5f)]
        public float widthMeters = 6f;

        [Tooltip("Optional polyline points in world space (XZ). If empty, start/end node positions are used.")]
        public List<Vector3> controlPoints = new();

        public Rect BoundsXZ
        {
            get
            {
                if (controlPoints == null || controlPoints.Count == 0) return default;

                var min = new Vector2(controlPoints[0].x, controlPoints[0].z);
                var max = min;

                for (var i = 1; i < controlPoints.Count; i++)
                {
                    var p = controlPoints[i];
                    var xz = new Vector2(p.x, p.z);
                    min = Vector2.Min(min, xz);
                    max = Vector2.Max(max, xz);
                }

                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }
    }

    [Serializable]
    public sealed class RoadTownConnection
    {
        public string townId = string.Empty;
        public string nodeId = string.Empty;
        public string connectionTag = "default";
    }

    [Serializable]
    public sealed class RoadBiomeConnection
    {
        public string segmentId = string.Empty;
        public string fromBiomeId = string.Empty;
        public string toBiomeId = string.Empty;
        [Range(0f, 1f)] public float weight = 1f;
        public bool bidirectional = true;
    }

    [Serializable]
    public sealed class RoadSpawnPoint
    {
        public string id = string.Empty;
        public string segmentId = string.Empty;

        [Range(0f, 1f)] public float normalizedDistance = 0.5f;
        public RoadSpawnRole role = RoadSpawnRole.AmbientZombie;

        [Min(0f)] public float radiusMeters = 10f;
        [Range(0f, 1f)] public float weight = 1f;

        public bool requiresNavMesh = true;
        public string tag = string.Empty;
    }

    [CreateAssetMenu(menuName = "Zombera/World/Gameplay Road Graph", fileName = "GameplayRoadGraph")]
    public sealed class GameplayRoadGraph : ScriptableObject
    {
        [SerializeField] private List<RoadGraphNode> nodes = new();
        [SerializeField] private List<RoadGraphSegment> segments = new();
        [SerializeField] private List<RoadSpawnPoint> spawnPoints = new();
        [SerializeField] private List<RoadTownConnection> townConnections = new();
        [SerializeField] private List<RoadBiomeConnection> biomeConnections = new();

        public IReadOnlyList<RoadGraphNode> Nodes => nodes;
        public IReadOnlyList<RoadGraphSegment> Segments => segments;
        public IReadOnlyList<RoadSpawnPoint> SpawnPoints => spawnPoints;
        public IReadOnlyList<RoadTownConnection> TownConnections => townConnections;
        public IReadOnlyList<RoadBiomeConnection> BiomeConnections => biomeConnections;

        /// <summary>Lightweight authoring summary (keeps connection lists referenced for tooling).</summary>
        // ReSharper disable once UnusedMember.Global
        public int GetAuthoringConnectionTotals() => TownConnections.Count + BiomeConnections.Count;

        public void ReplaceData(
            List<RoadGraphNode> newNodes,
            List<RoadGraphSegment> newSegments,
            List<RoadSpawnPoint> newSpawnPoints,
            List<RoadTownConnection> newTownConnections)
        {
            ReplaceData(newNodes, newSegments, newSpawnPoints, newTownConnections, null);
        }

        public void ReplaceData(
            List<RoadGraphNode> newNodes,
            List<RoadGraphSegment> newSegments,
            List<RoadSpawnPoint> newSpawnPoints,
            List<RoadTownConnection> newTownConnections,
            List<RoadBiomeConnection> newBiomeConnections)
        {
            nodes = newNodes ?? new List<RoadGraphNode>();
            segments = newSegments ?? new List<RoadGraphSegment>();
            spawnPoints = newSpawnPoints ?? new List<RoadSpawnPoint>();
            townConnections = newTownConnections ?? new List<RoadTownConnection>();
            biomeConnections = newBiomeConnections ?? new List<RoadBiomeConnection>();
        }

        public bool TryGetNode(string nodeId, out RoadGraphNode node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(nodeId) || nodes == null) return false;

            foreach (var candidate in nodes)
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.id)) continue;
                if (!string.Equals(candidate.id, nodeId, StringComparison.OrdinalIgnoreCase)) continue;

                node = candidate;
                return true;
            }

            return false;
        }

        public bool TryGetSegment(string segmentId, out RoadGraphSegment segment)
        {
            segment = null;
            if (string.IsNullOrWhiteSpace(segmentId) || segments == null) return false;

            foreach (var candidate in segments)
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.id)) continue;
                if (!string.Equals(candidate.id, segmentId, StringComparison.OrdinalIgnoreCase)) continue;

                segment = candidate;
                return true;
            }

            return false;
        }

        public bool TryGetSegmentPolyline(RoadGraphSegment segment, List<Vector3> pointsBuffer)
        {
            if (segment == null || pointsBuffer == null) return false;

            pointsBuffer.Clear();

            if (segment.controlPoints is { Count: >= 2 })
            {
                foreach (var p in segment.controlPoints)
                    pointsBuffer.Add(p);

                return pointsBuffer.Count >= 2;
            }

            if (!TryGetNode(segment.startNodeId, out var startNode) || !TryGetNode(segment.endNodeId, out var endNode))
                return false;

            pointsBuffer.Add(startNode.position);
            pointsBuffer.Add(endNode.position);
            return true;
        }

        public Bounds ComputeWorldBounds(float paddingMeters = 0f)
        {
            var hasPoint = false;
            var min = Vector3.zero;
            var max = Vector3.zero;

            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    if (node == null) continue;
                    ExpandBounds(node.position, ref hasPoint, ref min, ref max);
                }
            }

            if (segments != null)
            {
                foreach (var segment in segments)
                {
                    if (segment?.controlPoints == null) continue;

                    if (segment.controlPoints.Count >= 2)
                    {
                        var xz = segment.BoundsXZ;
                        ExpandBounds(new Vector3(xz.xMin, 0f, xz.yMin), ref hasPoint, ref min, ref max);
                        ExpandBounds(new Vector3(xz.xMax, 0f, xz.yMax), ref hasPoint, ref min, ref max);
                    }

                    foreach (var p in segment.controlPoints)
                        ExpandBounds(p, ref hasPoint, ref min, ref max);
                }
            }

            if (!hasPoint)
                return new Bounds(Vector3.zero, Vector3.one * 2f);

            var pad = Mathf.Max(0f, paddingMeters);
            var center = (min + max) * 0.5f;
            var size = max - min;
            size.x += pad * 2f;
            size.y += pad * 2f;
            size.z += pad * 2f;
            if (size.x < 1f) size.x = 1f;
            if (size.y < 1f) size.y = 1f;
            if (size.z < 1f) size.z = 1f;

            return new Bounds(center, size);
        }

        private static void ExpandBounds(Vector3 point, ref bool hasPoint, ref Vector3 min, ref Vector3 max)
        {
            if (!hasPoint)
            {
                min = point;
                max = point;
                hasPoint = true;
                return;
            }

            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }
    }
}
