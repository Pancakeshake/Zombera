using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Roads
{
    /// <summary>Shared inland-water footprint guards for road-stack terrain writes.</summary>
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        private const float DefaultWaterBedClearanceMeters = 0.5f;

        private InlandWaterFootprintPlan _inlandWaterFootprintPlan;
        private float _waterBedClearanceMeters = DefaultWaterBedClearanceMeters;

        public InlandWaterFootprintPlan InlandWaterFootprintPlan => _inlandWaterFootprintPlan;

        public void SetWaterFootprintPlan(
            InlandWaterFootprintPlan footprintPlan,
            float minimumBedClearanceMeters = DefaultWaterBedClearanceMeters)
        {
            _inlandWaterFootprintPlan = footprintPlan;
            _waterBedClearanceMeters = Mathf.Max(0f, minimumBedClearanceMeters);
            InstallTerrainWriteGuard();
        }

        /// <summary>
        ///     Publishes this builder's water-bed rule to the static terrain write utilities so every
        ///     road, highway and city-pad heightmap write inherits it. Both delegates are method groups
        ///     on this builder, keeping <see cref="ResolveProtectedGroundHeight"/> the single
        ///     clearance rule. A null plan clears the guard.
        /// </summary>
        private void InstallTerrainWriteGuard()
        {
            if (_inlandWaterFootprintPlan == null)
            {
                TerrainHeightFlattener.SetProtectedWaterWriteGuard(null, null);
                return;
            }

            TerrainHeightFlattener.SetProtectedWaterWriteGuard(IsWaterProtected, ResolveProtectedGroundHeight);
        }

        public bool IsWaterProtected(Vector2 worldXZ) =>
            TryResolveProtectedSurface(worldXZ, out _);

        public float ResolveProtectedGroundHeight(Vector2 worldXZ, float candidateGroundWorldY)
        {
            if (!TryResolveProtectedSurface(worldXZ, out var surfaceWorldY))
                return candidateGroundWorldY;

            return Mathf.Min(candidateGroundWorldY, surfaceWorldY - _waterBedClearanceMeters);
        }

        private bool IsWaterProtected(Rect boundsXZ)
        {
            var plan = _inlandWaterFootprintPlan;
            if (plan?.Features == null)
                return false;

            var features = plan.Features;
            for (var i = 0; i < features.Count; i++)
            {
                if (FeatureIntersectsBounds(features[i], boundsXZ))
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     Resolves the protected water surface at a world XZ. True when the position lies inside a
        ///     river channel or lake bed footprint.
        /// </summary>
        public bool TryResolveProtectedSurface(Vector2 worldXZ, out float surfaceWorldY)
        {
            surfaceWorldY = 0f;
            var plan = _inlandWaterFootprintPlan;
            if (plan?.Features == null)
                return false;

            var found = false;
            var closestDistanceSq = float.PositiveInfinity;
            var features = plan.Features;
            for (var i = 0; i < features.Count; i++)
                ResolveFeatureSurface(features[i], worldXZ, ref found, ref closestDistanceSq, ref surfaceWorldY);
            return found;
        }

        private static void ResolveFeatureSurface(
            InlandWaterFootprintPlan.Feature feature,
            Vector2 worldXZ,
            ref bool found,
            ref float closestDistanceSq,
            ref float surfaceWorldY)
        {
            if (feature?.Points == null || feature.Points.Count == 0)
                return;
            if (feature.Points.Count == 1)
            {
                ResolvePointSurface(feature.Points[0], worldXZ, ref found, ref closestDistanceSq, ref surfaceWorldY);
                return;
            }

            for (var i = 0; i < feature.Points.Count - 1; i++)
                ResolveSegmentSurface(feature.Points[i], feature.Points[i + 1], worldXZ,
                    ref found, ref closestDistanceSq, ref surfaceWorldY);

            if (feature.Closed)
                ResolveSegmentSurface(feature.Points[feature.Points.Count - 1], feature.Points[0], worldXZ,
                    ref found, ref closestDistanceSq, ref surfaceWorldY);
        }

        private static void ResolvePointSurface(
            InlandWaterFootprintPlan.Point point,
            Vector2 worldXZ,
            ref bool found,
            ref float closestDistanceSq,
            ref float surfaceWorldY)
        {
            if (point == null)
                return;
            var distanceSq = (worldXZ - point.CenterXZ).sqrMagnitude;
            var radius = Mathf.Max(0f, point.TargetWetHalfWidthMeters);
            if (distanceSq > radius * radius || distanceSq >= closestDistanceSq)
                return;
            found = true;
            closestDistanceSq = distanceSq;
            surfaceWorldY = point.SurfaceWorldY;
        }

        private static void ResolveSegmentSurface(
            InlandWaterFootprintPlan.Point from,
            InlandWaterFootprintPlan.Point to,
            Vector2 worldXZ,
            ref bool found,
            ref float closestDistanceSq,
            ref float surfaceWorldY)
        {
            if (from == null || to == null)
                return;
            var segment = to.CenterXZ - from.CenterXZ;
            var lengthSq = segment.sqrMagnitude;
            var t = lengthSq > 0.0001f
                ? Mathf.Clamp01(Vector2.Dot(worldXZ - from.CenterXZ, segment) / lengthSq)
                : 0f;
            var nearest = Vector2.Lerp(from.CenterXZ, to.CenterXZ, t);
            var distanceSq = (worldXZ - nearest).sqrMagnitude;
            var radius = Mathf.Lerp(from.TargetWetHalfWidthMeters, to.TargetWetHalfWidthMeters, t);
            if (distanceSq > radius * radius || distanceSq >= closestDistanceSq)
                return;
            found = true;
            closestDistanceSq = distanceSq;
            surfaceWorldY = Mathf.Lerp(from.SurfaceWorldY, to.SurfaceWorldY, t);
        }

        private static bool FeatureIntersectsBounds(InlandWaterFootprintPlan.Feature feature, Rect boundsXZ)
        {
            if (feature?.Points == null || feature.Points.Count == 0)
                return false;
            if (feature.Points.Count == 1)
                return PointFootprintIntersectsBounds(feature.Points[0], boundsXZ);

            for (var i = 0; i < feature.Points.Count - 1; i++)
            {
                if (SegmentFootprintIntersectsBounds(feature.Points[i], feature.Points[i + 1], boundsXZ))
                    return true;
            }

            return feature.Closed &&
                   SegmentFootprintIntersectsBounds(
                       feature.Points[feature.Points.Count - 1], feature.Points[0], boundsXZ);
        }

        private static bool PointFootprintIntersectsBounds(InlandWaterFootprintPlan.Point point, Rect boundsXZ)
        {
            if (point == null)
                return false;
            var expanded = ExpandRect(boundsXZ, Mathf.Max(0f, point.TargetWetHalfWidthMeters));
            return expanded.Contains(point.CenterXZ);
        }

        private static bool SegmentFootprintIntersectsBounds(
            InlandWaterFootprintPlan.Point from,
            InlandWaterFootprintPlan.Point to,
            Rect boundsXZ)
        {
            if (from == null || to == null)
                return false;
            var radius = Mathf.Max(from.TargetWetHalfWidthMeters, to.TargetWetHalfWidthMeters);
            return SegmentIntersectsRect(from.CenterXZ, to.CenterXZ, ExpandRect(boundsXZ, radius));
        }

        private static Rect ExpandRect(Rect rect, float amount)
        {
            var pad = Mathf.Max(0f, amount);
            return Rect.MinMaxRect(rect.xMin - pad, rect.yMin - pad, rect.xMax + pad, rect.yMax + pad);
        }

        private static bool SegmentIntersectsRect(Vector2 from, Vector2 to, Rect rect)
        {
            if (rect.Contains(from) || rect.Contains(to))
                return true;
            var tMin = 0f;
            var tMax = 1f;
            var delta = to - from;
            return ClipLine(-delta.x, from.x - rect.xMin, ref tMin, ref tMax) &&
                   ClipLine(delta.x, rect.xMax - from.x, ref tMin, ref tMax) &&
                   ClipLine(-delta.y, from.y - rect.yMin, ref tMin, ref tMax) &&
                   ClipLine(delta.y, rect.yMax - from.y, ref tMin, ref tMax);
        }

        private static bool ClipLine(float direction, float distance, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(direction) < 0.0001f)
                return distance >= 0f;
            var ratio = distance / direction;
            if (direction < 0f)
                tMin = Mathf.Max(tMin, ratio);
            else
                tMax = Mathf.Min(tMax, ratio);
            return tMin <= tMax;
        }
    }
}
