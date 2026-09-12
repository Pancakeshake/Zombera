using System;

namespace Zombera.BuildingSystem
{
    internal delegate bool TryResolveFilteredHudPartDelegate(int hudIndex, out object part);
    internal delegate bool TryResolveHudPartReferenceDelegate(int hudIndex, out string partReference);

    internal static class EasyBuildBridgeHudPartResolverHelper
    {
        internal static object ResolveHudPart(
            int hudIndex,
            TryResolveFilteredHudPartDelegate tryResolveFilteredHudPart,
            TryResolveHudPartReferenceDelegate tryResolveHudPartReference,
            Func<string, object> resolvePartByReference,
            Func<int, object> resolvePartByManagerIndex)
        {
            if (hudIndex < 0) return null;

            if (tryResolveFilteredHudPart != null
                && tryResolveFilteredHudPart(hudIndex, out var filteredPart)
                && filteredPart != null)
                return filteredPart;

            if (tryResolveHudPartReference != null
                && tryResolveHudPartReference(hudIndex, out var partReference)
                && !string.IsNullOrWhiteSpace(partReference)
                && resolvePartByReference != null)
            {
                var resolved = resolvePartByReference(partReference);
                if (resolved != null) return resolved;
            }

            return resolvePartByManagerIndex?.Invoke(hudIndex);
        }
    }
}
