using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>Footprint / plateau edge anchors for inter-city highway endpoints.</summary>
    public static class InterCitySiteFootprintUtility
    {
        public static Vector2 ResolveEdgePoint(
            Vector2 centerXZ,
            float halfWidthMeters,
            float halfDepthMeters,
            Vector2 toward,
            out Vector2 outward)
        {
            var halfX = Mathf.Max(1f, halfWidthMeters);
            var halfZ = Mathf.Max(1f, halfDepthMeters);
            var dir = toward - centerXZ;
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            {
                outward = new Vector2(dir.x > 0f ? 1f : -1f, 0f);
                return new Vector2(centerXZ.x + (dir.x > 0f ? halfX : -halfX), centerXZ.y);
            }

            outward = new Vector2(0f, dir.y > 0f ? 1f : -1f);
            return new Vector2(centerXZ.x, centerXZ.y + (dir.y > 0f ? halfZ : -halfZ));
        }

        public static Vector2 ResolveEdgePointOnFace(
            Vector2 centerXZ,
            float halfWidthMeters,
            float halfDepthMeters,
            Vector2 outward)
        {
            var halfX = Mathf.Max(1f, halfWidthMeters);
            var halfZ = Mathf.Max(1f, halfDepthMeters);
            if (Mathf.Abs(outward.x) >= Mathf.Abs(outward.y))
                return new Vector2(centerXZ.x + (outward.x >= 0f ? halfX : -halfX), centerXZ.y);
            return new Vector2(centerXZ.x, centerXZ.y + (outward.y >= 0f ? halfZ : -halfZ));
        }

        public static void CollectCardinalFaceOutwards(Vector2 centerXZ, Vector2 toward, Vector2[] facesOut)
        {
            if (facesOut == null || facesOut.Length < 4)
                return;

            var dir = toward - centerXZ;
            var primaryX = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y);
            if (primaryX)
            {
                facesOut[0] = new Vector2(dir.x >= 0f ? 1f : -1f, 0f);
                facesOut[1] = new Vector2(0f, dir.y >= 0f ? 1f : -1f);
                facesOut[2] = new Vector2(0f, dir.y >= 0f ? -1f : 1f);
                facesOut[3] = new Vector2(dir.x >= 0f ? -1f : 1f, 0f);
            }
            else
            {
                facesOut[0] = new Vector2(0f, dir.y >= 0f ? 1f : -1f);
                facesOut[1] = new Vector2(dir.x >= 0f ? 1f : -1f, 0f);
                facesOut[2] = new Vector2(dir.x >= 0f ? -1f : 1f, 0f);
                facesOut[3] = new Vector2(0f, dir.y >= 0f ? -1f : 1f);
            }
        }

        public static float DistanceToRectEdge(Vector2 point, Rect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return float.PositiveInfinity;

            if (point.x >= rect.xMin && point.x <= rect.xMax &&
                point.y >= rect.yMin && point.y <= rect.yMax)
            {
                var dx = Mathf.Min(point.x - rect.xMin, rect.xMax - point.x);
                var dy = Mathf.Min(point.y - rect.yMin, rect.yMax - point.y);
                return Mathf.Min(dx, dy);
            }

            var cx = Mathf.Clamp(point.x, rect.xMin, rect.xMax);
            var cy = Mathf.Clamp(point.y, rect.yMin, rect.yMax);
            return Vector2.Distance(point, new Vector2(cx, cy));
        }
    }
}
