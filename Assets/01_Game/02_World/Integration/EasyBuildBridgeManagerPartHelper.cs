using System;
using UnityEngine;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeManagerPartHelper
    {
        internal static int GetManagerPartCount(object manager)
        {
            var partCountRaw = EasyBuildBridgeReflectionHelper.InvokeMethodWithReturn(manager, "GetPartCount");
            return partCountRaw is int i ? i : 0;
        }

        internal static object GetManagerPartByIndex(object manager, int index)
        {
            return EasyBuildBridgeReflectionHelper.InvokeMethodWithReturn(manager, "GetPartByIndex", index);
        }

        internal static object ResolvePartByManagerIndex(object manager, int index)
        {
            if (manager == null) return null;

            var partCount = GetManagerPartCount(manager);
            if (partCount <= 0) return null;

            var clamped = Mathf.Clamp(index, 0, partCount - 1);
            return GetManagerPartByIndex(manager, clamped);
        }

        internal static object ResolvePartByReference(
            object manager,
            string partReference,
            Func<object, string> resolvePartIdentifier)
        {
            if (manager == null) return null;
            if (string.IsNullOrWhiteSpace(partReference)) return null;
            if (resolvePartIdentifier == null) return null;

            var partCount = GetManagerPartCount(manager);
            if (partCount <= 0) return null;

            for (var index = 0; index < partCount; index++)
            {
                var part = GetManagerPartByIndex(manager, index);
                if (part == null) continue;

                var partId = resolvePartIdentifier(part);
                if (string.Equals(partId, partReference, StringComparison.OrdinalIgnoreCase))
                    return part;
            }

            return null;
        }

        internal static bool TrySelectPartByManagerIndex(
            object controller,
            object manager,
            int index,
            Func<object, string> resolvePartIdentifier,
            Action<string, string, int, int> logFilteredPlacementDiagnostic)
        {
            if (controller == null || manager == null) return false;

            var partCount = GetManagerPartCount(manager);
            if (partCount <= 0) return false;

            var clamped = Mathf.Clamp(index, 0, partCount - 1);
            var part = GetManagerPartByIndex(manager, clamped);
            if (part == null) return false;

            EasyBuildBridgeReflectionHelper.InvokeMethod(controller, "SelectPart", part);
            var partReference = resolvePartIdentifier(part);
            logFilteredPlacementDiagnostic("SelectByManagerIndex", partReference, index, clamped);
            return true;
        }

        internal static bool TrySelectPartByPartReference(
            object controller,
            object manager,
            string partReference,
            Func<object, string> resolvePartIdentifier,
            Action<string, string, int, int> logFilteredPlacementDiagnostic)
        {
            if (controller == null || manager == null) return false;
            if (string.IsNullOrWhiteSpace(partReference)) return false;
            if (resolvePartIdentifier == null) return false;

            var partCount = GetManagerPartCount(manager);
            if (partCount <= 0) return false;

            for (var index = 0; index < partCount; index++)
            {
                var part = GetManagerPartByIndex(manager, index);
                if (part == null) continue;

                var partId = resolvePartIdentifier(part);
                if (!string.Equals(partId, partReference, StringComparison.OrdinalIgnoreCase)) continue;

                EasyBuildBridgeReflectionHelper.InvokeMethod(controller, "SelectPart", part);
                logFilteredPlacementDiagnostic("SelectPartByReference.match", partId, -1, index);
                return true;
            }

            return false;
        }
    }
}
