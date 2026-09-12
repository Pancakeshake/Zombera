using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    [AddComponentMenu("Zombera/World/Road Gameplay Authoring Root")]
    [DisallowMultipleComponent]
    public sealed class RoadGameplayAuthoringRoot : MonoBehaviour
    {
        [SerializeField] private GameplayRoadGraph targetGraph;
        [SerializeField] private bool autoCollectChildComponents = true;
        [SerializeField] private List<RoadGameplayAuthoringNode> nodes = new();
        [SerializeField] private List<RoadGameplayAuthoringSegment> segments = new();
        [SerializeField] private List<RoadGameplayAuthoringSpawnPoint> spawnPoints = new();

        public GameplayRoadGraph TargetGraph
        {
            get => targetGraph;
            set => targetGraph = value;
        }

        public bool AutoCollectChildComponents => autoCollectChildComponents;
        public IReadOnlyList<RoadGameplayAuthoringNode> Nodes => nodes;
        public IReadOnlyList<RoadGameplayAuthoringSegment> Segments => segments;
        public IReadOnlyList<RoadGameplayAuthoringSpawnPoint> SpawnPoints => spawnPoints;

        [ContextMenu("Refresh Authoring References")]
        public void RefreshAuthoringReferences()
        {
            nodes.Clear();
            segments.Clear();
            spawnPoints.Clear();

            var foundNodes = GetComponentsInChildren<RoadGameplayAuthoringNode>(true);
            foreach (var node in foundNodes)
            {
                if (node == null) continue;
                nodes.Add(node);
            }

            var foundSegments = GetComponentsInChildren<RoadGameplayAuthoringSegment>(true);
            foreach (var segment in foundSegments)
            {
                if (segment == null) continue;
                segments.Add(segment);
            }

            var foundSpawnPoints = GetComponentsInChildren<RoadGameplayAuthoringSpawnPoint>(true);
            foreach (var spawnPoint in foundSpawnPoints)
            {
                if (spawnPoint == null) continue;
                spawnPoints.Add(spawnPoint);
            }
        }

        public void EnsureAuthoringReferencesUpToDate()
        {
            if (!AutoCollectChildComponents) return;
            RefreshAuthoringReferences();
        }
    }

    [AddComponentMenu("Zombera/World/Road Gameplay Authoring Node")]
    [DisallowMultipleComponent]
    public sealed class RoadGameplayAuthoringNode : MonoBehaviour
    {
        public string nodeId = string.Empty;
        public bool isTownConnection;
        public string townId = string.Empty;
        public new string tag = string.Empty;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isTownConnection
                ? new Color(1f, 0.75f, 0.20f, 0.9f)
                : new Color(0.20f, 0.75f, 1f, 0.9f);

            Gizmos.DrawSphere(transform.position, 0.6f);
        }
    }

    [AddComponentMenu("Zombera/World/Road Gameplay Authoring Segment")]
    [DisallowMultipleComponent]
    public sealed class RoadGameplayAuthoringSegment : MonoBehaviour
    {
        public string segmentId = string.Empty;

        [Header("Topology")]
        public RoadGameplayAuthoringNode startNode;
        public RoadGameplayAuthoringNode endNode;

        [Tooltip("Optional intermediate waypoints in world space between start and end.")]
        public List<Transform> waypoints = new();

        [Header("Gameplay")]
        public GameplayRoadType roadType = GameplayRoadType.Local;

        [Min(0.1f)] public float speedModifier = 1f;
        [Range(0f, 1f)] public float noiseLevel = 0.2f;
        public bool contributesPatrolRoutes = true;
        public bool supportsVehicles;
        public bool contributesLootZone;

        [Range(0f, 1f)] public float lootZoneWeight = 0.25f;

        [Header("Topology Metadata")]
        [Min(1)] public int laneCount = 2;
        [Min(0.5f)] public float laneWidthMeters = 3f;

        [Tooltip("Optional biome id this segment starts in.")]
        public string fromBiomeId = string.Empty;

        [Tooltip("Optional biome id this segment ends in.")]
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
        public RoadCityZone cityZone = RoadCityZone.Mixed;

        [Header("Shape")]
        [Min(0.5f)]
        public float widthMeters = 6f;

        private void OnDrawGizmosSelected()
        {
            if (startNode == null || endNode == null) return;

            Gizmos.color = supportsVehicles
                ? new Color(0.95f, 0.85f, 0.20f, 0.9f)
                : new Color(0.20f, 0.95f, 0.45f, 0.9f);

            var points = new List<Vector3>(8) { startNode.transform.position };
            foreach (var waypoint in waypoints)
            {
                if (waypoint == null) continue;
                points.Add(waypoint.position);
            }

            points.Add(endNode.transform.position);

            for (var i = 1; i < points.Count; i++)
                Gizmos.DrawLine(points[i - 1], points[i]);
        }
    }

    [AddComponentMenu("Zombera/World/Road Gameplay Authoring Spawn Point")]
    [DisallowMultipleComponent]
    public sealed class RoadGameplayAuthoringSpawnPoint : MonoBehaviour
    {
        public string spawnId = string.Empty;
        public RoadGameplayAuthoringSegment segment;

        [Range(0f, 1f)] public float normalizedDistance = 0.5f;
        public RoadSpawnRole role = RoadSpawnRole.AmbientZombie;

        [Min(0f)] public float radiusMeters = 10f;
        [Range(0f, 1f)] public float weight = 1f;
        public bool requiresNavMesh = true;
        public new string tag = string.Empty;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.5f, radiusMeters));
        }
    }
}
