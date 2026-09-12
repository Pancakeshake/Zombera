using UnityEngine;
using UnityEngine.AI;
using Zombera.Systems;

namespace Zombera.Characters
{
    /// <summary>
    /// Shared utility for NavMesh-safe placement and sampling.
    /// Ensures units are grounded and agents are correctly synchronized.
    /// </summary>
    public static class UnitNavUtils
    {
        public const int WalkableAreaMask = 1;

        public static readonly float[] DefaultPlacementTiers = { 2f, 6f, 15f };

        public const float DefaultMaxVerticalDeltaFromReference = 2.5f;

        /// <summary>
        ///     Resolves a terrain-ground reference Y at XZ, even when the unit is floating far above walkable ground.
        /// </summary>
        public static bool TryResolveGroundReferenceY(Vector3 worldPosition, out float referenceY)
        {
            var profile = MovementGroundingSettings.Active;
            if (profile.TryResolveGroundReferenceY(worldPosition, out referenceY))
                return true;

            referenceY = worldPosition.y;
            return false;
        }

        /// <summary>
        ///     Samples NavMesh near an origin but rejects hits that diverge too far vertically from referenceY.
        ///     Prevents snapping to floating or stacked NavMesh above/below the intended ground plane.
        /// </summary>
        public static bool TrySampleTieredNearReferenceY(
            Vector3 origin,
            float referenceY,
            out Vector3 result,
            float maxVerticalDelta = DefaultMaxVerticalDeltaFromReference,
            float[] tiers = null,
            int areaMask = WalkableAreaMask)
        {
            tiers ??= DefaultPlacementTiers;

            if (NavMesh.SamplePosition(origin, out var hit, 2f, WalkableAreaMask)
                && IsVerticalDeltaAcceptable(hit.position.y, referenceY, maxVerticalDelta))
            {
                result = hit.position;
                return true;
            }

            foreach (var radius in tiers)
            {
                if (!NavMesh.SamplePosition(origin, out hit, radius, areaMask)) continue;
                if (!IsVerticalDeltaAcceptable(hit.position.y, referenceY, maxVerticalDelta)) continue;

                result = hit.position;
                return true;
            }

            result = origin;
            return false;
        }

        public static bool IsVerticalDeltaAcceptable(float sampleY, float referenceY, float maxVerticalDelta)
        {
            return Mathf.Abs(sampleY - referenceY) <= Mathf.Max(0.05f, maxVerticalDelta);
        }

        /// <summary>
        /// Strict local NavMesh readiness: sample must succeed within a tight radius and lie near ground height.
        /// </summary>
        public static bool IsNavMeshReadyAt(
            Vector3 worldPosition,
            float sampleRadius = 4f,
            float maxSampleDistance = 1.5f,
            float maxVerticalDelta = 2f,
            int areaMask = WalkableAreaMask)
        {
            if (!IsFiniteVector3(worldPosition)) return false;

            var profile = MovementGroundingSettings.Active;
            var sampleOrigin = new Vector3(worldPosition.x, worldPosition.y + profile.NavSampleUpOffset, worldPosition.z);

            if (!profile.PreferTerrainHeightOverNavMeshAt(worldPosition))
            {
                if (!NavMesh.SamplePosition(sampleOrigin, out var directHit, sampleRadius, areaMask)
                    && !NavMesh.SamplePosition(sampleOrigin, out directHit, sampleRadius, NavMesh.AllAreas))
                    return false;

                return directHit.distance <= maxSampleDistance;
            }

            TryResolveGroundReferenceY(worldPosition, out var referenceY);

            sampleOrigin = new Vector3(worldPosition.x, referenceY + profile.NavSampleUpOffset, worldPosition.z);
            if (!NavMesh.SamplePosition(sampleOrigin, out var hit, sampleRadius, areaMask)
                && !NavMesh.SamplePosition(sampleOrigin, out hit, sampleRadius, NavMesh.AllAreas))
                return false;

            if (hit.distance > maxSampleDistance) return false;

            return IsVerticalDeltaAcceptable(hit.position.y, referenceY, maxVerticalDelta);
        }

        /// <summary>
        /// Legacy broad check used when streaming service is unavailable.
        /// </summary>
        public static bool TryHasNearbyNavMesh(Vector3 origin)
        {
            return IsNavMeshReadyAt(origin, 6f, 2.5f, 4f, WalkableAreaMask)
                   || IsNavMeshReadyAt(origin, 8f, 3f, 6f, NavMesh.AllAreas);
        }

        /// <summary>
        /// Attempts to find a valid NavMesh point near the origin using tiered search radii.
        /// </summary>
        public static bool TrySampleTiered(Vector3 origin, out Vector3 result, float[] tiers = null, int areaMask = NavMesh.AllAreas)
        {
            tiers ??= DefaultPlacementTiers;

            if (NavMesh.SamplePosition(origin, out var walkHit, 2f, WalkableAreaMask))
            {
                result = walkHit.position;
                return true;
            }

            foreach (var radius in tiers)
            {
                if (NavMesh.SamplePosition(origin, out var hit, radius, areaMask))
                {
                    result = hit.position;
                    return true;
                }
            }

            result = origin;
            return false;
        }

        /// <summary>
        /// Forcefully places a unit on the NavMesh. Handles the agent enable/warp cycle.
        /// Returns true only when the agent ends on the NavMesh.
        /// </summary>
        public static bool PlaceUnitOnNavMesh(
            GameObject unit,
            Vector3 targetPosition,
            float sampleRadius = 5f,
            bool allowWideFallback = true)
        {
            if (unit == null) return false;

            var agent = unit.GetComponent<NavMeshAgent>();
            var controller = unit.GetComponent<UnitController>();

            if (controller != null)
            {
                controller.Stop();
                controller.LogGroundingState("PlaceUnitOnNavMesh.Start");
            }

            var localTiers = allowWideFallback
                ? new[] { sampleRadius, sampleRadius * 3f, 50f }
                : new[] { sampleRadius, Mathf.Max(sampleRadius * 2f, sampleRadius + 1f) };

            var profile = MovementGroundingSettings.Active;
            var preferTerrain = profile.PreferTerrainHeightOverNavMeshAt(targetPosition);

            Vector3 navPos;
            bool hasNavSample;
            if (preferTerrain)
            {
                TryResolveGroundReferenceY(targetPosition, out var groundReferenceY);

                var sampleOrigin = new Vector3(
                    targetPosition.x,
                    groundReferenceY + profile.NavSampleUpOffset,
                    targetPosition.z);

                hasNavSample = TrySampleTieredNearReferenceY(
                    sampleOrigin,
                    groundReferenceY,
                    out navPos,
                    profile.MaxNavMeshVerticalDeltaFromGround,
                    profile.NavSampleRadii);
            }
            else
            {
                var sampleOrigin = new Vector3(
                    targetPosition.x,
                    targetPosition.y + profile.NavSampleUpOffset,
                    targetPosition.z);

                hasNavSample = TrySampleTiered(
                    sampleOrigin,
                    out navPos,
                    profile.NavSampleRadii,
                    WalkableAreaMask);
            }

            if (!hasNavSample)
            {
                if (preferTerrain
                    && profile.TryResolveGroundReferenceY(targetPosition, out var groundedY)
                    && groundedY <= targetPosition.y + 0.05f)
                {
                    unit.transform.position = new Vector3(targetPosition.x, groundedY, targetPosition.z);
                }

                if (agent != null && agent.enabled) agent.enabled = false;

                controller?.LogGroundingState("PlaceUnitOnNavMesh.NoNavMeshSample");
                return false;
            }

            if (agent != null) agent.enabled = false;

            if (preferTerrain
                && profile.TryProjectGround(navPos, out var footPoint))
                navPos = profile.BlendNavMeshWithTerrain(navPos, footPoint);

            unit.transform.position = navPos;

            if (agent == null)
            {
                controller?.LogGroundingState("PlaceUnitOnNavMesh.Complete.NoAgent");
                return true;
            }

            if (!IsNavMeshReadyAt(navPos, Mathf.Max(sampleRadius, 4f), 2f, 4f))
            {
                agent.enabled = false;
                controller?.LogGroundingState("PlaceUnitOnNavMesh.NavNotReadyAtSample");
                return false;
            }

            agent.enabled = true;
            if (!agent.enabled)
            {
                controller?.LogGroundingState("PlaceUnitOnNavMesh.AgentEnableFailed");
                return false;
            }

            agent.Warp(navPos);
            agent.nextPosition = navPos;
            agent.velocity = Vector3.zero;

            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            else
            {
                agent.enabled = false;
                controller?.LogGroundingState("PlaceUnitOnNavMesh.WarpFailed");
                return false;
            }

            controller?.LogGroundingState("PlaceUnitOnNavMesh.Complete");
            return true;
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
