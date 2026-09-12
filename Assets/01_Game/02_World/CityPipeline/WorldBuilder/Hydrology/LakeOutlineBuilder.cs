using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    internal static class LakeOutlineBuilder
    {
        public static Vector2[] Build(LandformField field, IReadOnlyList<int> cells)
        {
            if (field == null || cells == null || cells.Count < 3)
                return System.Array.Empty<Vector2>();
            var basin = new HashSet<int>(cells);
            var links = new Dictionary<Vector2Int, List<Vector2Int>>();
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var x = cell % field.Width;
                var z = cell / field.Width;
                AddEdge(field, basin, links, x, z, Vector2Int.left);
                AddEdge(field, basin, links, x, z, Vector2Int.right);
                AddEdge(field, basin, links, x, z, Vector2Int.down);
                AddEdge(field, basin, links, x, z, Vector2Int.up);
            }

            var loop = TraceLargest(field, links);
            if (loop.Count < 3)
                return System.Array.Empty<Vector2>();
            var result = new List<Vector2>(loop.Count * 2);
            for (var i = 0; i < loop.Count; i++)
            {
                var a = loop[i];
                var b = loop[(i + 1) % loop.Count];
                result.Add(Vector2.Lerp(a, b, 0.25f));
                result.Add(Vector2.Lerp(a, b, 0.75f));
            }
            result.Add(result[0]);
            return result.ToArray();
        }

        private static void AddEdge(
            LandformField field,
            HashSet<int> basin,
            Dictionary<Vector2Int, List<Vector2Int>> links,
            int x,
            int z,
            Vector2Int direction)
        {
            var nx = x + direction.x;
            var nz = z + direction.y;
            if (nx >= 0 && nz >= 0 && nx < field.Width && nz < field.Height &&
                basin.Contains(field.Index(nx, nz)))
                return;
            var a = direction == Vector2Int.left ? new Vector2Int(x, z + 1) :
                direction == Vector2Int.right ? new Vector2Int(x + 1, z) :
                direction == Vector2Int.down ? new Vector2Int(x, z) : new Vector2Int(x + 1, z + 1);
            var b = direction == Vector2Int.left ? new Vector2Int(x, z) :
                direction == Vector2Int.right ? new Vector2Int(x + 1, z + 1) :
                direction == Vector2Int.down ? new Vector2Int(x + 1, z) : new Vector2Int(x, z + 1);
            AddLink(links, a, b);
            AddLink(links, b, a);
        }

        private static void AddLink(Dictionary<Vector2Int, List<Vector2Int>> links, Vector2Int a, Vector2Int b)
        {
            if (!links.TryGetValue(a, out var neighbours))
            {
                neighbours = new List<Vector2Int>(2);
                links.Add(a, neighbours);
            }
            if (!neighbours.Contains(b)) neighbours.Add(b);
        }

        private static List<Vector2> TraceLargest(
            LandformField field,
            Dictionary<Vector2Int, List<Vector2Int>> links)
        {
            var visited = new HashSet<BoundaryEdge>();
            var largest = new List<Vector2>();
            foreach (var pair in links)
            {
                for (var i = 0; i < pair.Value.Count; i++)
                {
                    var edge = new BoundaryEdge(pair.Key, pair.Value[i]);
                    if (!visited.Add(edge)) continue;
                    var loop = new List<Vector2>();
                    var start = pair.Key;
                    var previous = pair.Key;
                    var current = pair.Value[i];
                    var guard = 0;
                    loop.Add(GridPoint(field, start));
                    while (guard++ < links.Count * 4)
                    {
                        loop.Add(GridPoint(field, current));
                        if (current == start) break;
                        if (!links.TryGetValue(current, out var nexts) || nexts.Count == 0) break;
                        var next = nexts[0];
                        for (var n = 0; n < nexts.Count; n++)
                            if (nexts[n] != previous) { next = nexts[n]; break; }
                        visited.Add(new BoundaryEdge(current, next));
                        previous = current;
                        current = next;
                    }
                    if (Mathf.Abs(SignedArea(loop)) > Mathf.Abs(SignedArea(largest)))
                        largest = loop;
                }
            }
            return largest;
        }

        private static Vector2 GridPoint(LandformField field, Vector2Int grid) =>
            new(field.OriginXZ.x + grid.x * field.CellSize, field.OriginXZ.y + grid.y * field.CellSize);

        private static float SignedArea(IReadOnlyList<Vector2> points)
        {
            var area = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        private readonly struct BoundaryEdge
        {
            private readonly Vector2Int _a;
            private readonly Vector2Int _b;

            public BoundaryEdge(Vector2Int a, Vector2Int b)
            {
                if (a.x < b.x || a.x == b.x && a.y <= b.y) { _a = a; _b = b; }
                else { _a = b; _b = a; }
            }

            public override bool Equals(object obj) => obj is BoundaryEdge other && _a == other._a && _b == other._b;
            public override int GetHashCode() => (_a.GetHashCode() * 397) ^ _b.GetHashCode();
        }
    }
}
