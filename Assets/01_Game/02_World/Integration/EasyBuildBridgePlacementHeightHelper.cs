using System;
using UnityEngine;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgePlacementHeightHelper
    {
        internal static bool TryAdjustPlacementHeight(
            object settings,
            float delta,
            Vector2 placementHeightRange,
            Action<string> bridgeLog,
            ref float nextControlsHelpRefreshAt,
            ref float nextPlacementHeightLogAt)
        {
            if (settings == null) return false;
            if (Mathf.Abs(delta) <= Mathf.Epsilon) return false;

            if (EasyBuildBridgeReflectionHelper.GetMemberValue(settings, "PreviewOffsetPosition") is not Vector3 offset)
                return false;

            var minY = Mathf.Min(placementHeightRange.x, placementHeightRange.y);
            var maxY = Mathf.Max(placementHeightRange.x, placementHeightRange.y);
            var nextY = Mathf.Clamp(offset.y + delta, minY, maxY);
            if (Mathf.Abs(nextY - offset.y) <= 0.0001f) return false;

            offset.y = nextY;
            EasyBuildBridgeReflectionHelper.SetMemberValue(settings, "PreviewOffsetPosition", offset);
            nextControlsHelpRefreshAt = 0f;

            if (Time.unscaledTime >= nextPlacementHeightLogAt)
            {
                nextPlacementHeightLogAt = Time.unscaledTime + 0.25f;
                bridgeLog?.Invoke($"Placement height adjusted by {delta:+0.00;-0.00;0.00} => {nextY:+0.00;-0.00;0.00}m");
            }

            return true;
        }

        internal static float GetPlacementHeightOffset(object settings)
        {
            if (settings == null) return 0f;

            return EasyBuildBridgeReflectionHelper.GetMemberValue(settings, "PreviewOffsetPosition") is Vector3 offset
                ? offset.y
                : 0f;
        }
    }
}
