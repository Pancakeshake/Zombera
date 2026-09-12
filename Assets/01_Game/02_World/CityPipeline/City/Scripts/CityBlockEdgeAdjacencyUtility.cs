using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Shared rect-edge adjacency checks for district blocks (sidewalks, fill expansion, etc.).
    /// </summary>
    public static class CityBlockEdgeAdjacencyUtility
    {
        private const float DefaultSharedEdgeEpsilonMeters = 0.5f;
        private const float MinEdgeSpanMeters = 1f;

        public static bool IsSharedStreetBlockEdge(
            Rect blockA,
            Rect blockB,
            Vector2 edgeStart,
            Vector2 edgeEnd,
            float eps)
        {
            if (Mathf.Abs(blockA.xMax - blockB.xMin) < eps || Mathf.Abs(blockB.xMax - blockA.xMin) < eps)
            {
                var sharedX = Mathf.Abs(blockA.xMax - blockB.xMin) < eps ? blockA.xMax : blockA.xMin;
                if (Mathf.Abs(edgeStart.x - sharedX) < eps && Mathf.Abs(edgeEnd.x - sharedX) < eps)
                {
                    var z0 = Mathf.Max(Mathf.Min(edgeStart.y, edgeEnd.y), Mathf.Max(blockA.yMin, blockB.yMin));
                    var z1 = Mathf.Min(Mathf.Max(edgeStart.y, edgeEnd.y), Mathf.Min(blockA.yMax, blockB.yMax));
                    return z1 - z0 >= MinEdgeSpanMeters;
                }
            }

            if (Mathf.Abs(blockA.yMax - blockB.yMin) < eps || Mathf.Abs(blockB.yMax - blockA.yMin) < eps)
            {
                var sharedZ = Mathf.Abs(blockA.yMax - blockB.yMin) < eps ? blockA.yMax : blockA.yMin;
                if (Mathf.Abs(edgeStart.y - sharedZ) < eps && Mathf.Abs(edgeEnd.y - sharedZ) < eps)
                {
                    var x0 = Mathf.Max(Mathf.Min(edgeStart.x, edgeEnd.x), Mathf.Max(blockA.xMin, blockB.xMin));
                    var x1 = Mathf.Min(Mathf.Max(edgeStart.x, edgeEnd.x), Mathf.Min(blockA.xMax, blockB.xMax));
                    return x1 - x0 >= MinEdgeSpanMeters;
                }
            }

            return false;
        }

        public static bool IsSharedStreetBlockEdge(
            Rect blockA,
            Rect blockB,
            Vector2 edgeStart,
            Vector2 edgeEnd)
        {
            return IsSharedStreetBlockEdge(blockA, blockB, edgeStart, edgeEnd, DefaultSharedEdgeEpsilonMeters);
        }
    }
}
