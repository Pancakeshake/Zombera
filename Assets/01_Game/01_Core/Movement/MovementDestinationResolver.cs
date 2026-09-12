#region

using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.Systems
{
    public struct MovementDestinationResolveRequest
    {
        public Camera Camera;
        public Vector2 PointerScreenPosition;
        public LayerMask GroundMask;
        public float RayDistance;
        public bool LogDiagnostics;
    }

    public struct MovementDestinationResolveResult
    {
        public bool Success;
        public Vector3 RequestedPoint;
        public Vector3 RayHitPoint;
        public Vector3 GroundProjectedPoint;
        public Vector3 NavMeshSampledPoint;
        public Vector3 FinalPoint;
        public string FailureReason;
    }

    /// <summary>
    ///     Shared grounding pipeline for click/command movement destinations.
    /// </summary>
    public static class MovementDestinationResolver
    {
        public static MovementDestinationResolveResult LastResolveResult { get; private set; }

        public static bool TryResolveFromPointer(
            in MovementDestinationResolveRequest request,
            out Vector3 destination)
        {
            destination = default;
            var result = new MovementDestinationResolveResult();
            var profile = MovementGroundingSettings.Active;

            if (request.Camera == null)
            {
                result.FailureReason = "missing-camera";
                LastResolveResult = result;
                return false;
            }

            var mask = profile.ResolveGroundMask(request.GroundMask);
            var ray = request.Camera.ScreenPointToRay(request.PointerScreenPosition);

            if (!Physics.Raycast(
                    ray,
                    out var hit,
                    Mathf.Max(1f, request.RayDistance),
                    mask,
                    QueryTriggerInteraction.Ignore))
            {
                result.FailureReason = "raycast-miss";
                LastResolveResult = result;
                return false;
            }

            result.RayHitPoint = hit.point;
            result.RequestedPoint = hit.point;

            if (!TryValidateMovementPoint(hit.point, profile, out destination, ref result))
            {
                LastResolveResult = result;
                return false;
            }

            result.Success = true;
            result.FinalPoint = destination;
            LastResolveResult = result;

            if (request.LogDiagnostics || profile.LogMovementDestinationResolution)
                LogResolveResult("pointer", result);

            return true;
        }

        public static bool TryValidateSlotPosition(
            Vector3 candidate,
            Vector3 fallbackDestination,
            out Vector3 groundedSlot)
        {
            return TryValidateSlotPosition(candidate, fallbackDestination, out groundedSlot, MovementGroundingSettings.Active);
        }

        internal static bool TryValidateSlotPosition(
            Vector3 candidate,
            Vector3 fallbackDestination,
            out Vector3 groundedSlot,
            MovementGroundingProfile profile)
        {
            groundedSlot = candidate;
            var result = new MovementDestinationResolveResult { RequestedPoint = candidate };

            if (profile.TryResolveGroundedPoint(candidate, out groundedSlot, ref result))
                return true;

            if (profile.TryResolveGroundedPoint(fallbackDestination, out groundedSlot, ref result))
                return true;

            groundedSlot = fallbackDestination;
            return false;
        }

        public static bool TryProjectToGround(
            Vector3 position,
            LayerMask groundMask,
            out Vector3 groundPoint,
            float? upOffset = null,
            float? downDistance = null)
        {
            var profile = MovementGroundingSettings.Active;

            if (upOffset == null && downDistance == null && groundMask.value == 0)
                return profile.TryProjectGround(position, out groundPoint);

            groundPoint = position;
            var mask = profile.ResolveGroundMask(groundMask);
            var up = upOffset ?? profile.GroundProjectUpOffset;
            var down = downDistance ?? profile.GroundProjectDownDistance;
            var origin = position + Vector3.up * up;

            if (!Physics.Raycast(
                    origin,
                    Vector3.down,
                    out var hit,
                    up + down,
                    mask,
                    QueryTriggerInteraction.Ignore))
                return false;

            groundPoint = profile.ApplyFootOffset(hit.point);
            return true;
        }

        private static bool TryValidateMovementPoint(
            Vector3 candidate,
            MovementGroundingProfile profile,
            out Vector3 finalPoint,
            ref MovementDestinationResolveResult result)
        {
            return profile.TryResolveGroundedPoint(candidate, out finalPoint, ref result);
        }

        private static void LogResolveResult(string source, in MovementDestinationResolveResult result)
        {
            Debug.Log(
                $"[MovementDestinationResolver][{source}] success={result.Success} reason={result.FailureReason}\n" +
                $"  ray={result.RayHitPoint:F2} ground={result.GroundProjectedPoint:F2} nav={result.NavMeshSampledPoint:F2} final={result.FinalPoint:F2}");
        }
    }
}
