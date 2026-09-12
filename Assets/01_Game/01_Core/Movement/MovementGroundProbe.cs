#region

using UnityEngine;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Shared ground height probes for movement and NavMesh placement.
    /// </summary>
    public static class MovementGroundProbe
    {
        private const float LongRangeRayStartHeight = 1200f;
        private const float LongRangeRayLength = 2600f;

        public static bool TrySampleLongRangePhysics(Vector3 worldPosition, out float groundY)
        {
            var rayOrigin = worldPosition + Vector3.up * LongRangeRayStartHeight;
            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out var hit,
                    LongRangeRayLength,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
                return true;
            }

            groundY = worldPosition.y;
            return false;
        }
    }
}
