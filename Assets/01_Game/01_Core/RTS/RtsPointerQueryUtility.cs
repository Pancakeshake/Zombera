#region

using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.Systems
{
    public static class RtsPointerQueryUtility
    {
        public static bool TryGetGroundPoint(
            Camera worldCamera,
            Vector2 pointerScreenPosition,
            LayerMask groundMask,
            float rayDistance,
            out Vector3 groundPoint)
        {
            return MovementDestinationResolver.TryResolveFromPointer(
                new MovementDestinationResolveRequest
                {
                    Camera = worldCamera,
                    PointerScreenPosition = pointerScreenPosition,
                    GroundMask = groundMask,
                    RayDistance = rayDistance
                },
                out groundPoint);
        }

        public static bool TryGetGroundPoint(
            Camera worldCamera,
            Vector2 pointerScreenPosition,
            LayerMask groundMask,
            float rayDistance,
            bool logDiagnostics,
            out Vector3 groundPoint)
        {
            return MovementDestinationResolver.TryResolveFromPointer(
                new MovementDestinationResolveRequest
                {
                    Camera = worldCamera,
                    PointerScreenPosition = pointerScreenPosition,
                    GroundMask = groundMask,
                    RayDistance = rayDistance,
                    LogDiagnostics = logDiagnostics
                },
                out groundPoint);
        }

        public static bool TryGetNearestUnitHealthUnderPointer(
            Camera worldCamera,
            Vector2 pointerScreenPosition,
            LayerMask targetMask,
            QueryTriggerInteraction queryTriggerInteraction,
            float rayDistance,
            RaycastHit[] hitBuffer,
            out UnitHealth targetHealth)
        {
            return TryGetNearestUnitHealthUnderPointer(
                worldCamera,
                pointerScreenPosition,
                targetMask,
                queryTriggerInteraction,
                rayDistance,
                hitBuffer,
                out targetHealth,
                0f,
                null,
                null);
        }

        /// <summary>
        ///     Pointer-to-unit query with optional sphere-cast hover assist and an optional
        ///     target validity filter (e.g. hostility checks). Pass a cached delegate for
        ///     <paramref name="isValidTarget" /> when calling from per-frame code.
        /// </summary>
        public static bool TryGetNearestUnitHealthUnderPointer(
            Camera worldCamera,
            Vector2 pointerScreenPosition,
            LayerMask targetMask,
            QueryTriggerInteraction queryTriggerInteraction,
            float rayDistance,
            RaycastHit[] hitBuffer,
            out UnitHealth targetHealth,
            float sphereAssistRadius,
            RaycastHit[] sphereHitBuffer,
            System.Func<UnitHealth, bool> isValidTarget)
        {
            targetHealth = null;
            if (worldCamera == null || hitBuffer == null || hitBuffer.Length == 0) return false;

            var ray = worldCamera.ScreenPointToRay(pointerScreenPosition);
            var clampedDistance = Mathf.Max(1f, rayDistance);

            var hitCount = Physics.RaycastNonAlloc(
                ray,
                hitBuffer,
                clampedDistance,
                targetMask,
                queryTriggerInteraction);

            if (TryResolveNearestFromHits(hitBuffer, hitCount, isValidTarget, out targetHealth)) return true;

            if (sphereAssistRadius <= 0.0001f || sphereHitBuffer == null || sphereHitBuffer.Length == 0) return false;

            var sphereHitCount = Physics.SphereCastNonAlloc(
                ray,
                sphereAssistRadius,
                sphereHitBuffer,
                clampedDistance,
                targetMask,
                queryTriggerInteraction);

            return TryResolveNearestFromHits(sphereHitBuffer, sphereHitCount, isValidTarget, out targetHealth);
        }

        private static bool TryResolveNearestFromHits(
            RaycastHit[] hitBuffer,
            int hitCount,
            System.Func<UnitHealth, bool> isValidTarget,
            out UnitHealth nearestTarget)
        {
            nearestTarget = null;

            if (hitBuffer == null || hitCount <= 0) return false;

            var clampedHitCount = Mathf.Min(hitCount, hitBuffer.Length);
            var nearestDistance = float.MaxValue;

            for (var i = 0; i < clampedHitCount; i++)
            {
                var hit = hitBuffer[i];
                hitBuffer[i] = default;

                if (hit.collider == null) continue;

                var candidate = hit.collider.GetComponentInParent<UnitHealth>();
                if (candidate == null || candidate.IsDead) continue;
                if (isValidTarget != null && !isValidTarget(candidate)) continue;
                if (hit.distance >= nearestDistance) continue;

                nearestDistance = hit.distance;
                nearestTarget = candidate;
            }

            return nearestTarget != null;
        }
    }
}
