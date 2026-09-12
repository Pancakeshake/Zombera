using System;
using UnityEngine;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeSampleArtifactHelper
    {
        internal static bool TryResolveSampleArtifactRoot(Transform candidate, out GameObject rootObject)
        {
            rootObject = null;
            if (candidate == null) return false;

            var gameObject = candidate.gameObject;
            if (gameObject == null || !gameObject.name.StartsWith("Building Group", StringComparison.Ordinal))
                return false;

            if (candidate.childCount != 1) return false;

            var child = candidate.GetChild(0);
            if (child == null || child.gameObject == null) return false;
            if (!IsSampleArtifactChildName(child.gameObject.name)) return false;

            rootObject = gameObject;
            return true;
        }

        internal static bool IsSampleArtifactChildName(string childName)
        {
            if (string.IsNullOrWhiteSpace(childName)) return false;

            return childName.IndexOf("cube", StringComparison.OrdinalIgnoreCase) >= 0
                   || childName.IndexOf("sm_", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
