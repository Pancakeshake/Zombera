using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Shared map-edge and corner distance helpers for landforms and biomes.</summary>
    public static partial class WorldMapBoundaryUtility
    {
        public static float DistanceToRectEdge(float x, float z, Rect bounds)
        {
            var left = x - bounds.xMin;
            var right = bounds.xMax - x;
            var bottom = z - bounds.yMin;
            var top = bounds.yMax - z;
            return Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
        }

        public static bool TryGetNearestEdgeSide(
            float x,
            float z,
            Rect bounds,
            out WorldMapEdgeSide side,
            out float edgeDistanceMeters)
        {
            var left = x - bounds.xMin;
            var right = bounds.xMax - x;
            var bottom = z - bounds.yMin;
            var top = bounds.yMax - z;
            edgeDistanceMeters = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));

            if (Mathf.Approximately(edgeDistanceMeters, left))
                side = WorldMapEdgeSide.West;
            else if (Mathf.Approximately(edgeDistanceMeters, right))
                side = WorldMapEdgeSide.East;
            else if (Mathf.Approximately(edgeDistanceMeters, bottom))
                side = WorldMapEdgeSide.South;
            else
                side = WorldMapEdgeSide.North;
            return true;
        }

        public static bool TryGetEdgeBarrier(
            float x,
            float z,
            Rect bounds,
            float edgeBarrierDepthMeters,
            WorldMapBoundaryLayout layout,
            out WorldMapBoundaryKind boundaryKind)
        {
            boundaryKind = WorldMapBoundaryKind.Ocean;
            if (edgeBarrierDepthMeters <= 1f)
                return false;

            TryGetNearestEdgeSide(x, z, bounds, out var side, out var edgeDistance);
            if (edgeDistance >= edgeBarrierDepthMeters)
                return false;

            boundaryKind = layout.Get(side);
            return true;
        }

        public static float EvaluateEdgeBarrierBlend(
            float edgeDistanceMeters,
            float edgeBarrierDepthMeters,
            float falloffMeters)
        {
            if (edgeBarrierDepthMeters <= 1f)
                return 0f;
            if (edgeDistanceMeters >= edgeBarrierDepthMeters)
                return 0f;

            var inner = Mathf.Max(1f, edgeBarrierDepthMeters - Mathf.Max(1f, falloffMeters));
            if (edgeDistanceMeters <= inner)
                return 1f;

            return 1f - Mathf.Clamp01(
                (edgeDistanceMeters - inner) / Mathf.Max(1f, edgeBarrierDepthMeters - inner));
        }

        public static float GetAlongEdgeCoordinate(
            float x,
            float z,
            Rect bounds,
            WorldMapEdgeSide side) =>
            side switch
            {
                WorldMapEdgeSide.West or WorldMapEdgeSide.East => z - bounds.yMin,
                _ => x - bounds.xMin
            };
    }
}
