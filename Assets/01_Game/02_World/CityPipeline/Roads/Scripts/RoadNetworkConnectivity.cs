using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public static class RoadNetworkConnectivity
    {
        public sealed class JunctionIndex
        {
            private readonly Dictionary<Vector2Int, List<VertexRef>> _cells = new();
            private readonly float _cellSize;

            public JunctionIndex(float cellSize)
            {
                _cellSize = Mathf.Max(0.5f, cellSize);
            }

            public void Add(Vector2 pointXZ, int roadId, bool isTerminus)
            {
                var key = ToCell(pointXZ, _cellSize);
                if (!_cells.TryGetValue(key, out var list))
                {
                    list = new List<VertexRef>(4);
                    _cells[key] = list;
                }

                list.Add(new VertexRef(roadId, isTerminus));
            }

            public bool IsNetworkJunction(Vector2 pointXZ, int sourceRoadId, float snapMeters)
            {
                var key = ToCell(pointXZ, _cellSize);
                if (!_cells.TryGetValue(key, out var list))
                    return false;

                var distinctRoads = new HashSet<int>();
                for (var i = 0; i < list.Count; i++)
                    distinctRoads.Add(list[i].roadId);

                if (distinctRoads.Count >= 2)
                    return true;

                if (distinctRoads.Count == 1 && !distinctRoads.Contains(sourceRoadId))
                    return true;

                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        var neighborKey = new Vector2Int(key.x + dx, key.y + dz);
                        if (!_cells.TryGetValue(neighborKey, out var neighborList)) continue;

                        for (var i = 0; i < neighborList.Count; i++)
                        {
                            if (neighborList[i].roadId == sourceRoadId) continue;
                            distinctRoads.Add(neighborList[i].roadId);
                            if (distinctRoads.Count >= 2)
                                return true;
                        }
                    }
                }

                return false;
            }

            private readonly struct VertexRef
            {
                public readonly int roadId;
                public readonly bool isTerminus;

                public VertexRef(int roadId, bool isTerminus)
                {
                    this.roadId = roadId;
                    this.isTerminus = isTerminus;
                }
            }
        }

        public static JunctionIndex BuildJunctionIndex(IReadOnlyList<RoadPolyline> roads, float cellSize)
        {
            var index = new JunctionIndex(cellSize);
            if (roads == null) return index;

            for (var r = 0; r < roads.Count; r++)
            {
                var road = roads[r];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2) continue;

                for (var i = 0; i < road.pointsXZ.Count; i++)
                {
                    var isTerminus = i == 0 || i == road.pointsXZ.Count - 1;
                    index.Add(road.pointsXZ[i], road.id, isTerminus);
                }
            }

            return index;
        }

        public static bool IsTileContinuityExit(Vector2 pointXZ, Rect tileRect, float marginMeters)
        {
            var margin = Mathf.Max(0.5f, marginMeters);
            if (pointXZ.x <= tileRect.xMin + margin) return true;
            if (pointXZ.x >= tileRect.xMax - margin) return true;
            if (pointXZ.y <= tileRect.yMin + margin) return true;
            if (pointXZ.y >= tileRect.yMax - margin) return true;
            return false;
        }

        public static bool IsTileRoadEndpointJunction(
            Vector2 pointXZ,
            IReadOnlyList<IReadOnlyList<Vector3>> tilePaths,
            float snapMeters)
        {
            if (tilePaths == null || tilePaths.Count == 0) return false;

            var snapSq = snapMeters * snapMeters;
            var matches = 0;
            for (var i = 0; i < tilePaths.Count; i++)
            {
                var path = tilePaths[i];
                if (path == null || path.Count < 2) continue;

                var start = new Vector2(path[0].x, path[0].z);
                var end = new Vector2(path[path.Count - 1].x, path[path.Count - 1].z);

                if ((start - pointXZ).sqrMagnitude <= snapSq) matches++;
                if ((end - pointXZ).sqrMagnitude <= snapSq) matches++;
                if (matches >= 2) return true;
            }

            return false;
        }

        public static bool IsAuthoredTerminus(Vector2 pointXZ, IReadOnlyList<RoadPolyline> roads, float snapMeters)
        {
            if (roads == null) return false;

            var snapSq = snapMeters * snapMeters;
            for (var r = 0; r < roads.Count; r++)
            {
                var road = roads[r];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2) continue;

                if ((road.pointsXZ[0] - pointXZ).sqrMagnitude <= snapSq)
                    return true;
                if ((road.pointsXZ[^1] - pointXZ).sqrMagnitude <= snapSq)
                    return true;
            }

            return false;
        }

        private static Vector2Int ToCell(Vector2 pointXZ, float cellSize)
        {
            return new Vector2Int(
                Mathf.RoundToInt(pointXZ.x / cellSize),
                Mathf.RoundToInt(pointXZ.y / cellSize));
        }
    }
}
