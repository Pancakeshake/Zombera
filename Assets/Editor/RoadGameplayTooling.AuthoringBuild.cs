#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    public static partial class RoadGameplayTooling
    {
        private readonly struct AuthoringBuildArtifacts
        {
            public readonly List<RoadGraphNode> NodeData;
            public readonly List<RoadGraphSegment> SegmentData;
            public readonly List<RoadSpawnPoint> SpawnData;
            public readonly List<RoadTownConnection> TownConnections;
            public readonly List<RoadBiomeConnection> BiomeConnections;

            public AuthoringBuildArtifacts(
                List<RoadGraphNode> nodeData,
                List<RoadGraphSegment> segmentData,
                List<RoadSpawnPoint> spawnData,
                List<RoadTownConnection> townConnections,
                List<RoadBiomeConnection> biomeConnections)
            {
                NodeData = nodeData;
                SegmentData = segmentData;
                SpawnData = spawnData;
                TownConnections = townConnections;
                BiomeConnections = biomeConnections;
            }
        }

        private readonly struct SegmentBuildArtifacts
        {
            public readonly List<RoadGraphSegment> SegmentData;
            public readonly Dictionary<RoadGameplayAuthoringSegment, string> SegmentIdByAuthoringSegment;

            public SegmentBuildArtifacts(
                List<RoadGraphSegment> segmentData,
                Dictionary<RoadGameplayAuthoringSegment, string> segmentIdByAuthoringSegment)
            {
                SegmentData = segmentData;
                SegmentIdByAuthoringSegment = segmentIdByAuthoringSegment;
            }
        }

        private static bool TryBuildGraphDataFromAuthoring(
            RoadGameplayAuthoringRoot authoringRoot,
            out AuthoringBuildArtifacts buildArtifacts,
            out string summary)
        {
            buildArtifacts = new AuthoringBuildArtifacts(
                new List<RoadGraphNode>(0),
                new List<RoadGraphSegment>(0),
                new List<RoadSpawnPoint>(0),
                new List<RoadTownConnection>(0),
                new List<RoadBiomeConnection>(0));
            summary = string.Empty;

            if (authoringRoot == null)
            {
                summary = "Authoring root is missing.";
                return false;
            }

            BuildNodes(authoringRoot, out var nodeData, out var nodeIdByAuthoringNode);
            BuildSegments(authoringRoot, nodeIdByAuthoringNode, out var segmentBuild);
            BuildSpawnPoints(authoringRoot, segmentBuild.SegmentIdByAuthoringSegment, out var spawnData);
            BuildConnectionLists(
                authoringRoot,
                nodeIdByAuthoringNode,
                segmentBuild.SegmentIdByAuthoringSegment,
                out var townConnections,
                out var biomeConnections);

            buildArtifacts = new AuthoringBuildArtifacts(
                nodeData,
                segmentBuild.SegmentData,
                spawnData,
                townConnections,
                biomeConnections);

            summary =
                "Nodes: " + buildArtifacts.NodeData.Count + "\n" +
                "Segments: " + buildArtifacts.SegmentData.Count + "\n" +
                "Spawn points: " + buildArtifacts.SpawnData.Count + "\n" +
                "Town connections: " + buildArtifacts.TownConnections.Count + "\n" +
                "Biome connections: " + buildArtifacts.BiomeConnections.Count;

            return buildArtifacts.SegmentData.Count > 0;
        }

        private static void BuildNodes(
            RoadGameplayAuthoringRoot authoringRoot,
            out List<RoadGraphNode> nodeData,
            out Dictionary<RoadGameplayAuthoringNode, string> nodeIdByAuthoringNode)
        {
            nodeData = new List<RoadGraphNode>(64);
            nodeIdByAuthoringNode = new Dictionary<RoadGameplayAuthoringNode, string>(64);

            var nodes = authoringRoot.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var sourceNode = nodes[i];
                if (sourceNode == null) continue;

                var id = EnsureStableId(sourceNode.nodeId, "node", i);
                sourceNode.nodeId = id;

                nodeData.Add(new RoadGraphNode
                {
                    id = id,
                    position = sourceNode.transform.position,
                    isTownConnection = sourceNode.isTownConnection,
                    townId = sourceNode.townId,
                    tag = sourceNode.tag
                });

                nodeIdByAuthoringNode[sourceNode] = id;
            }
        }

        private static void BuildSegments(
            RoadGameplayAuthoringRoot authoringRoot,
            IReadOnlyDictionary<RoadGameplayAuthoringNode, string> nodeIdByAuthoringNode,
            out SegmentBuildArtifacts segmentBuild)
        {
            var segmentData = new List<RoadGraphSegment>(64);
            var segmentIdByAuthoringSegment = new Dictionary<RoadGameplayAuthoringSegment, string>(64);

            var segments = authoringRoot.Segments;
            for (var i = 0; i < segments.Count; i++)
            {
                var sourceSegment = segments[i];
                if (sourceSegment == null || sourceSegment.startNode == null || sourceSegment.endNode == null) continue;

                if (!nodeIdByAuthoringNode.TryGetValue(sourceSegment.startNode, out var startId) ||
                    !nodeIdByAuthoringNode.TryGetValue(sourceSegment.endNode, out var endId))
                    continue;

                var segmentId = EnsureStableId(sourceSegment.segmentId, "segment", i);
                sourceSegment.segmentId = segmentId;

                var controlPoints = new List<Vector3>(8)
                {
                    sourceSegment.startNode.transform.position
                };

                for (var wp = 0; wp < sourceSegment.waypoints.Count; wp++)
                {
                    var waypoint = sourceSegment.waypoints[wp];
                    if (waypoint == null) continue;
                    controlPoints.Add(waypoint.position);
                }

                controlPoints.Add(sourceSegment.endNode.transform.position);

                segmentData.Add(new RoadGraphSegment
                {
                    id = segmentId,
                    startNodeId = startId,
                    endNodeId = endId,
                    roadType = sourceSegment.roadType,
                    speedModifier = Mathf.Max(0.1f, sourceSegment.speedModifier),
                    noiseLevel = Mathf.Clamp01(sourceSegment.noiseLevel),
                    contributesPatrolRoutes = sourceSegment.contributesPatrolRoutes,
                    supportsVehicles = sourceSegment.supportsVehicles,
                    contributesLootZone = sourceSegment.contributesLootZone,
                    lootZoneWeight = Mathf.Clamp01(sourceSegment.lootZoneWeight),
                    laneCount = Mathf.Max(1, sourceSegment.laneCount),
                    laneWidthMeters = Mathf.Max(0.5f, sourceSegment.laneWidthMeters),
                    fromBiomeId = sourceSegment.fromBiomeId,
                    toBiomeId = sourceSegment.toBiomeId,
                    biomeConnectionWeight = Mathf.Clamp01(sourceSegment.biomeConnectionWeight),
                    navArea = sourceSegment.navArea,
                    contributesPathArea = sourceSegment.contributesPathArea,
                    supportsSidewalks = sourceSegment.supportsSidewalks,
                    sidewalkWidthMeters = Mathf.Max(0f, sourceSegment.sidewalkWidthMeters),
                    supportsDecals = sourceSegment.supportsDecals,
                    decalDensity = Mathf.Clamp01(sourceSegment.decalDensity),
                    contributesRoadsideLots = sourceSegment.contributesRoadsideLots,
                    roadsideLotWeight = Mathf.Clamp01(sourceSegment.roadsideLotWeight),
                    roadsideLotSpacingMeters = Mathf.Max(1f, sourceSegment.roadsideLotSpacingMeters),
                    roadsideLotDepthMeters = Mathf.Max(1f, sourceSegment.roadsideLotDepthMeters),
                    cityZone = sourceSegment.cityZone,
                    widthMeters = Mathf.Max(0.5f, sourceSegment.widthMeters),
                    controlPoints = controlPoints
                });

                segmentIdByAuthoringSegment[sourceSegment] = segmentId;
            }

            segmentBuild = new SegmentBuildArtifacts(segmentData, segmentIdByAuthoringSegment);
        }

        private static void BuildSpawnPoints(
            RoadGameplayAuthoringRoot authoringRoot,
            IReadOnlyDictionary<RoadGameplayAuthoringSegment, string> segmentIdByAuthoringSegment,
            out List<RoadSpawnPoint> spawnData)
        {
            spawnData = new List<RoadSpawnPoint>(64);

            var spawnPoints = authoringRoot.SpawnPoints;
            for (var i = 0; i < spawnPoints.Count; i++)
            {
                var sourceSpawn = spawnPoints[i];
                if (sourceSpawn == null || sourceSpawn.segment == null) continue;
                if (!segmentIdByAuthoringSegment.TryGetValue(sourceSpawn.segment, out var segmentId)) continue;

                var spawnId = EnsureStableId(sourceSpawn.spawnId, "spawn", i);
                sourceSpawn.spawnId = spawnId;

                spawnData.Add(new RoadSpawnPoint
                {
                    id = spawnId,
                    segmentId = segmentId,
                    normalizedDistance = Mathf.Clamp01(sourceSpawn.normalizedDistance),
                    role = sourceSpawn.role,
                    radiusMeters = Mathf.Max(0f, sourceSpawn.radiusMeters),
                    weight = Mathf.Clamp01(sourceSpawn.weight),
                    requiresNavMesh = sourceSpawn.requiresNavMesh,
                    tag = sourceSpawn.tag
                });
            }
        }

        private static void BuildConnectionLists(
            RoadGameplayAuthoringRoot authoringRoot,
            IReadOnlyDictionary<RoadGameplayAuthoringNode, string> nodeIdByAuthoringNode,
            IReadOnlyDictionary<RoadGameplayAuthoringSegment, string> segmentIdByAuthoringSegment,
            out List<RoadTownConnection> townConnections,
            out List<RoadBiomeConnection> biomeConnections)
        {
            townConnections = new List<RoadTownConnection>(16);
            biomeConnections = new List<RoadBiomeConnection>(16);

            var nodes = authoringRoot.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var sourceNode = nodes[i];
                if (sourceNode == null || !sourceNode.isTownConnection || string.IsNullOrWhiteSpace(sourceNode.townId)) continue;
                if (!nodeIdByAuthoringNode.TryGetValue(sourceNode, out var nodeId)) continue;

                townConnections.Add(new RoadTownConnection
                {
                    townId = sourceNode.townId,
                    nodeId = nodeId,
                    connectionTag = string.IsNullOrWhiteSpace(sourceNode.tag) ? "default" : sourceNode.tag
                });
            }

            var segments = authoringRoot.Segments;
            for (var i = 0; i < segments.Count; i++)
            {
                var sourceSegment = segments[i];
                if (sourceSegment == null) continue;
                if (!segmentIdByAuthoringSegment.TryGetValue(sourceSegment, out var segmentId)) continue;
                if (string.IsNullOrWhiteSpace(sourceSegment.fromBiomeId) || string.IsNullOrWhiteSpace(sourceSegment.toBiomeId))
                    continue;
                if (string.Equals(sourceSegment.fromBiomeId, sourceSegment.toBiomeId, StringComparison.OrdinalIgnoreCase))
                    continue;

                biomeConnections.Add(new RoadBiomeConnection
                {
                    segmentId = segmentId,
                    fromBiomeId = sourceSegment.fromBiomeId.Trim(),
                    toBiomeId = sourceSegment.toBiomeId.Trim(),
                    weight = Mathf.Clamp01(sourceSegment.biomeConnectionWeight),
                    bidirectional = false
                });
            }
        }

        private static string EnsureStableId(string existingId, string prefix, int index)
        {
            if (!string.IsNullOrWhiteSpace(existingId)) return existingId.Trim();

            return prefix + "_" + index + "_" + Guid.NewGuid().ToString("N")[..8];
        }

        private static void EnsureDefaultAuthoringChildren(
            Transform authoringRoot,
            out int ensuredSegmentCount,
            out int ensuredSpawnPointCount)
        {
            ensuredSegmentCount = 0;
            ensuredSpawnPointCount = 0;
            if (authoringRoot == null) return;

            var scene = authoringRoot.gameObject.scene;
            var nodesRoot = EnsureChildObject(authoringRoot, SceneNodesRootName, scene);
            var segmentsRoot = EnsureChildObject(authoringRoot, SceneSegmentsRootName, scene);
            var spawnPointsRoot = EnsureChildObject(authoringRoot, SceneSpawnPointsRootName, scene);

            var nodeAObject = EnsureChildObject(nodesRoot.transform, "Node_A", scene);
            var nodeBObject = EnsureChildObject(nodesRoot.transform, "Node_B", scene);
            var nodeA = EnsureComponent<RoadGameplayAuthoringNode>(nodeAObject);
            var nodeB = EnsureComponent<RoadGameplayAuthoringNode>(nodeBObject);

            InitializeDefaultNode(
                nodeA,
                "node_a",
                ResolveDefaultRoadPoint(authoringRoot.position, new Vector3(-30f, 0f, 0f)));
            InitializeDefaultNode(
                nodeB,
                "node_b",
                ResolveDefaultRoadPoint(authoringRoot.position, new Vector3(30f, 0f, 0f)));

            var segmentObject = EnsureChildObject(segmentsRoot.transform, "Segment_Main", scene);
            var segment = EnsureComponent<RoadGameplayAuthoringSegment>(segmentObject);
            if (segment != null)
            {
                Undo.RecordObject(segment, "Configure Default Road Segment");
                if (string.IsNullOrWhiteSpace(segment.segmentId)) segment.segmentId = "segment_main";
                if (segment.startNode == null) segment.startNode = nodeA;
                if (segment.endNode == null) segment.endNode = nodeB;
                if (segment.widthMeters < 0.5f) segment.widthMeters = 6f;
                if (segment.speedModifier < 0.1f) segment.speedModifier = 1f;
                if (segment.laneCount < 1) segment.laneCount = 2;
                if (segment.laneWidthMeters < 0.5f) segment.laneWidthMeters = 3f;
                if (segment.sidewalkWidthMeters < 0f) segment.sidewalkWidthMeters = 1.5f;
                if (segment.roadsideLotSpacingMeters < 1f) segment.roadsideLotSpacingMeters = 16f;
                if (segment.roadsideLotDepthMeters < 1f) segment.roadsideLotDepthMeters = 14f;
                if (segment.cityZone == RoadCityZone.Unknown) segment.cityZone = RoadCityZone.Mixed;
                EditorUtility.SetDirty(segment);
                ensuredSegmentCount = 1;
            }

            var spawnObject = EnsureChildObject(spawnPointsRoot.transform, "Spawn_Ambient_01", scene);
            var spawnPoint = EnsureComponent<RoadGameplayAuthoringSpawnPoint>(spawnObject);
            if (spawnPoint != null)
            {
                Undo.RecordObject(spawnPoint, "Configure Default Road Spawn Point");
                if (string.IsNullOrWhiteSpace(spawnPoint.spawnId)) spawnPoint.spawnId = "spawn_ambient_01";
                if (spawnPoint.segment == null) spawnPoint.segment = segment;
                if (spawnPoint.radiusMeters < 0f) spawnPoint.radiusMeters = 8f;
                if (spawnPoint.weight <= 0f) spawnPoint.weight = 1f;
                spawnPoint.normalizedDistance = Mathf.Clamp01(
                    spawnPoint.normalizedDistance <= 0f ? 0.5f : spawnPoint.normalizedDistance);
                if (string.IsNullOrWhiteSpace(spawnPoint.tag)) spawnPoint.tag = "starter";

                var midpoint = ResolveMidpoint(nodeA, nodeB);
                if (spawnObject.transform.position.sqrMagnitude <= 0.001f)
                    spawnObject.transform.position = midpoint;

                EditorUtility.SetDirty(spawnPoint);
                ensuredSpawnPointCount = 1;
            }
        }

        private static void InitializeDefaultNode(
            RoadGameplayAuthoringNode node,
            string defaultId,
            Vector3 defaultWorldPosition)
        {
            if (node == null) return;

            Undo.RecordObject(node, "Configure Default Road Node");
            if (string.IsNullOrWhiteSpace(node.nodeId)) node.nodeId = defaultId;
            if (string.IsNullOrWhiteSpace(node.townId)) node.townId = string.Empty;
            if (string.IsNullOrWhiteSpace(node.tag)) node.tag = string.Empty;

            if (node.transform.position.sqrMagnitude <= 0.001f)
                node.transform.position = defaultWorldPosition;

            EditorUtility.SetDirty(node);
        }

        private static Vector3 ResolveDefaultRoadPoint(Vector3 center, Vector3 offset)
        {
            var candidate = center + offset;
            if (Terrain.activeTerrain == null || Terrain.activeTerrain.terrainData == null) return candidate;

            var terrain = Terrain.activeTerrain;
            var terrainPosition = terrain.transform.position;
            var size = terrain.terrainData.size;

            if (candidate.x < terrainPosition.x || candidate.x > terrainPosition.x + size.x ||
                candidate.z < terrainPosition.z || candidate.z > terrainPosition.z + size.z)
                return candidate;

            candidate.y = terrain.SampleHeight(candidate) + terrainPosition.y;
            return candidate;
        }

        private static Vector3 ResolveMidpoint(RoadGameplayAuthoringNode a, RoadGameplayAuthoringNode b)
        {
            if (a == null && b == null) return Vector3.zero;
            if (a == null) return b.transform.position;
            if (b == null) return a.transform.position;

            return (a.transform.position + b.transform.position) * 0.5f;
        }
    }
}
#endif
