using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Bakes city math polylines into a <see cref="GameplayRoadGraph"/> without EasyRoads.
    /// </summary>
    public static class PolylineRoadGraphBuilder
    {
        private const float NodeMergeDistanceMeters = 0.5f;

        public static void PopulateGraph(
            GameplayRoadGraph graph,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings)
        {
            if (graph == null || roads == null || roads.Count == 0)
                return;

            var nodes = new List<RoadGraphNode>(roads.Count * 2);
            var segments = new List<RoadGraphSegment>(roads.Count);
            var nodeIndex = new Dictionary<Vector2Int, string>(roads.Count * 2);

            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                var width = road.widthMeters > 0f
                    ? road.widthMeters
                    : settings != null
                        ? settings.ResolveWidthMeters(road.roadClass)
                        : 6f;
                var laneCount = Mathf.Max(1, Mathf.RoundToInt(width / 3f));
                var startId = GetOrCreateNode(nodes, nodeIndex, road.pointsXZ[0]);
                var endId = GetOrCreateNode(nodes, nodeIndex, road.pointsXZ[^1]);

                segments.Add(new RoadGraphSegment
                {
                    id = $"road_{road.id}",
                    startNodeId = startId,
                    endNodeId = endId,
                    roadType = MapRoadType(road.roadClass),
                    widthMeters = width,
                    laneCount = laneCount,
                    laneWidthMeters = width / laneCount,
                    supportsSidewalks = settings == null || settings.spawnProceduralSidewalkMeshes,
                    sidewalkWidthMeters = settings != null
                        ? settings.proceduralSidewalkWidthMeters
                        : 1.5f,
                    controlPoints = BuildControlPoints(road.pointsXZ, settings)
                });
            }

            graph.ReplaceData(nodes, segments, new List<RoadSpawnPoint>(), new List<RoadTownConnection>());
        }

        private static List<Vector3> BuildControlPoints(
            IReadOnlyList<Vector2> pointsXZ,
            RoadNetworkSettings settings)
        {
            var lift = settings != null ? settings.surfaceLiftMeters : 0f;
            var points = new List<Vector3>(pointsXZ.Count);
            for (var i = 0; i < pointsXZ.Count; i++)
                points.Add(new Vector3(pointsXZ[i].x, lift, pointsXZ[i].y));
            return points;
        }

        private static string GetOrCreateNode(
            List<RoadGraphNode> nodes,
            Dictionary<Vector2Int, string> nodeIndex,
            Vector2 positionXZ)
        {
            var key = Quantize(positionXZ);
            if (nodeIndex.TryGetValue(key, out var existingId))
                return existingId;

            var id = $"node_{nodes.Count}";
            nodes.Add(new RoadGraphNode
            {
                id = id,
                position = new Vector3(positionXZ.x, 0f, positionXZ.y)
            });
            nodeIndex[key] = id;
            return id;
        }

        private static Vector2Int Quantize(Vector2 positionXZ)
        {
            var scale = 1f / NodeMergeDistanceMeters;
            return new Vector2Int(
                Mathf.RoundToInt(positionXZ.x * scale),
                Mathf.RoundToInt(positionXZ.y * scale));
        }

        private static GameplayRoadType MapRoadType(RoadClass roadClass) => roadClass switch
        {
            RoadClass.Highway => GameplayRoadType.Highway,
            RoadClass.Arterial => GameplayRoadType.Arterial,
            RoadClass.Local => GameplayRoadType.Local,
            _ => GameplayRoadType.Local
        };
    }
}
