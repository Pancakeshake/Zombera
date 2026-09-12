#region

using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.Systems
{
    public sealed partial class MovementGroundingProfile
    {
        private static readonly float[] DefaultNavSampleRadii = { 2f, 6f, 15f };

        private LayerMask _resolvedGroundMask;
        private float _groundRayLength;
        private float[] _navSampleRadii = DefaultNavSampleRadii;
        private bool _hasFootOffset;
        private bool _cacheReady;

        public float[] NavSampleRadii
        {
            get
            {
                EnsureCache();
                return _navSampleRadii;
            }
        }

        public void RebuildCache()
        {
            NormalizeConfigDefaults();
            _resolvedGroundMask = MovementGroundLayers.Resolve(default, ground.layers);
            _groundRayLength = ground.probeUpMeters + ground.probeDownMeters;
            _navSampleRadii = BuildNavSampleRadii(height.navSampleRadiiMeters);
            _hasFootOffset = Mathf.Abs(ground.footPivotOffsetY) > 0.0001f;
            _cacheReady = true;
        }

        public bool TryProjectGround(Vector3 position, out Vector3 footPoint)
        {
            footPoint = position;
            EnsureCache();

            var origin = position + Vector3.up * ground.probeUpMeters;
            if (!Physics.Raycast(
                    origin,
                    Vector3.down,
                    out var hit,
                    _groundRayLength,
                    _resolvedGroundMask,
                    QueryTriggerInteraction.Ignore))
                return false;

            footPoint = hit.point;
            if (_hasFootOffset)
                footPoint.y += ground.footPivotOffsetY;

            return true;
        }

        public bool TryResolveGroundedPosition(Vector3 candidate, out Vector3 grounded)
        {
            return MovementDestinationResolver.TryValidateSlotPosition(candidate, candidate, out grounded, this);
        }

        public bool TryGetFootSnapTarget(Vector3 position, out float targetY)
        {
            targetY = position.y;
            if (TryProjectGround(position, out var foot))
            {
                targetY = foot.y;
                return true;
            }

            if (MovementGroundProbe.TrySampleLongRangePhysics(position, out var physicsY))
            {
                targetY = physicsY + ground.footPivotOffsetY;
                return true;
            }

            return false;
        }

        public bool TryResolveGroundReferenceY(Vector3 position, out float referenceY)
        {
            if (TryGetFootSnapTarget(position, out referenceY))
                return true;

            referenceY = position.y;
            return false;
        }

        public bool ShouldSnapDown(float currentY, float targetY)
        {
            return currentY - targetY > footSnap.threshold;
        }

        public float ComputeSnapDownY(float currentY, float targetY)
        {
            var step = footSnap.speed * Mathf.Max(0.02f, footSnap.interval);
            return Mathf.MoveTowards(currentY, targetY, step);
        }

        public bool ShouldFallbackSnapDown(float currentY, float targetY)
        {
            return currentY - targetY > fallback.threshold;
        }

        public float ComputeFallbackSnapDownY(float currentY, float targetY)
        {
            var step = fallback.speed * Mathf.Max(0.02f, fallback.interval);
            return Mathf.MoveTowards(currentY, targetY, step);
        }

        public Vector3 ApplyFootOffset(Vector3 groundPoint)
        {
            if (!_hasFootOffset) return groundPoint;

            groundPoint.y += ground.footPivotOffsetY;
            return groundPoint;
        }

        public Vector3 BlendNavMeshWithTerrain(Vector3 navPoint, Vector3 terrainPoint)
        {
            if (!PreferTerrainHeightOverNavMeshAt(navPoint)) return navPoint;

            return new Vector3(navPoint.x, terrainPoint.y, navPoint.z);
        }

        internal bool TryResolveGroundedPoint(
            Vector3 candidate,
            out Vector3 finalPoint,
            ref MovementDestinationResolveResult result)
        {
            finalPoint = candidate;

            if (!PreferTerrainHeightOverNavMeshAt(candidate))
                return TryResolveNavMeshGroundedPoint(candidate, out finalPoint, ref result);

            if (!TryProjectGround(candidate, out var groundPoint))
            {
                result.FailureReason = "no-ground-projection";
                return false;
            }

            result.GroundProjectedPoint = groundPoint;
            var sampleOrigin = groundPoint + Vector3.up * height.navSampleUpMeters;

            if (UnitNavUtils.TrySampleTieredNearReferenceY(
                    sampleOrigin,
                    groundPoint.y,
                    out var navPoint,
                    height.maxVerticalDelta,
                    _navSampleRadii,
                    UnitNavUtils.WalkableAreaMask))
            {
                result.NavMeshSampledPoint = navPoint;
                finalPoint = navPoint;
                return true;
            }

            finalPoint = groundPoint;
            return true;
        }

        private bool TryResolveNavMeshGroundedPoint(
            Vector3 candidate,
            out Vector3 finalPoint,
            ref MovementDestinationResolveResult result)
        {
            finalPoint = candidate;
            var sampleOrigin = candidate + Vector3.up * height.navSampleUpMeters;

            if (UnitNavUtils.TrySampleTiered(
                    sampleOrigin,
                    out var navPoint,
                    _navSampleRadii,
                    UnitNavUtils.WalkableAreaMask))
            {
                result.NavMeshSampledPoint = navPoint;
                finalPoint = navPoint;
                return true;
            }

            result.FailureReason = "no-navmesh-sample";
            return false;
        }

        private void EnsureCache()
        {
            if (_cacheReady) return;
            RebuildCache();
        }

        private void NormalizeConfigDefaults()
        {
            if (height.navSampleRadiiMeters == null || height.navSampleRadiiMeters.Length == 0)
                height.navSampleRadiiMeters = DefaultNavSampleRadii;
        }

        private static float[] BuildNavSampleRadii(float[] configured)
        {
            if (configured == null || configured.Length == 0)
                return DefaultNavSampleRadii;

            var copy = new float[configured.Length];
            for (var i = 0; i < configured.Length; i++)
                copy[i] = Mathf.Max(0.5f, configured[i]);

            return copy;
        }
    }
}
