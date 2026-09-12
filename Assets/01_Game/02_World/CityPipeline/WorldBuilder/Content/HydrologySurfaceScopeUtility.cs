using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Bounds checks for hydrology surface placement against a world-build scope.</summary>
    public static class HydrologySurfaceScopeUtility
    {
        public static bool ContainsPoint(WorldBuildScope scope, Vector2 pointXZ, float radiusMeters = 0f)
        {
            if (scope.Kind == WorldBuildScopeKind.FullMap)
                return true;

            var bounds = scope.BoundsXZ;
            if (bounds.width <= 0f || bounds.height <= 0f)
                return true;

            if (radiusMeters <= 0f)
                return bounds.Contains(pointXZ);

            var expanded = Rect.MinMaxRect(
                bounds.xMin - radiusMeters,
                bounds.yMin - radiusMeters,
                bounds.xMax + radiusMeters,
                bounds.yMax + radiusMeters);
            return expanded.Contains(pointXZ);
        }

        public static bool IntersectsPolyline(WorldBuildScope scope, Vector2[] points, float radiusMeters)
        {
            if (points == null || points.Length == 0)
                return false;

            if (scope.Kind == WorldBuildScopeKind.FullMap)
                return true;

            var bounds = scope.BoundsXZ;
            if (bounds.width <= 0f || bounds.height <= 0f)
                return true;

            var expanded = Rect.MinMaxRect(
                bounds.xMin - radiusMeters,
                bounds.yMin - radiusMeters,
                bounds.xMax + radiusMeters,
                bounds.yMax + radiusMeters);

            for (var i = 0; i < points.Length; i++)
            {
                if (expanded.Contains(points[i]))
                    return true;
            }

            if (points.Length < 2)
                return false;

            for (var i = 0; i < points.Length - 1; i++)
            {
                if (SegmentIntersectsRect(points[i], points[i + 1], expanded))
                    return true;
            }

            return false;
        }

        private static bool SegmentIntersectsRect(Vector2 a, Vector2 b, Rect rect)
        {
            if (rect.Contains(a) || rect.Contains(b))
                return true;

            var min = new Vector2(rect.xMin, rect.yMin);
            var max = new Vector2(rect.xMax, rect.yMax);
            return LineIntersectsLine(a, b, min, new Vector2(max.x, min.y))
                   || LineIntersectsLine(a, b, new Vector2(max.x, min.y), max)
                   || LineIntersectsLine(a, b, max, new Vector2(min.x, max.y))
                   || LineIntersectsLine(a, b, new Vector2(min.x, max.y), min);
        }

        private static bool LineIntersectsLine(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            var d = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);
            if (Mathf.Abs(d) < 0.00001f)
                return false;

            var ua = ((p4.x - p3.x) * (p1.y - p3.y) - (p4.y - p3.y) * (p1.x - p3.x)) / d;
            var ub = ((p2.x - p1.x) * (p1.y - p3.y) - (p2.y - p1.y) * (p1.x - p3.x)) / d;
            return ua is >= 0f and <= 1f && ub is >= 0f and <= 1f;
        }
    }
}
