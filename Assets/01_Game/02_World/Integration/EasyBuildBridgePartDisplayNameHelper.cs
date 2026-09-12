using System;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgePartDisplayNameHelper
    {
        internal static string ResolvePartDisplayName(
            object part,
            Func<object, string> resolvePartIdentifier,
            Func<string, string> tryResolveCatalogPrefabName)
        {
            if (part == null) return null;

            var prefabName = EasyBuildBridgePartDataHelper.ResolvePrefabNameFromPart(part);
            if (!string.IsNullOrWhiteSpace(prefabName))
                return prefabName;

            var partId = resolvePartIdentifier?.Invoke(part);
            if (tryResolveCatalogPrefabName != null)
            {
                var catalogPrefabName = tryResolveCatalogPrefabName(partId);
                if (!string.IsNullOrWhiteSpace(catalogPrefabName))
                    return catalogPrefabName;
            }

            var explicitName = EasyBuildBridgePartDataHelper.ResolveExplicitPartDisplayName(part);
            if (!string.IsNullOrWhiteSpace(explicitName)
                && !EasyBuildBridgePartDataHelper.IsGenericPartDisplayName(explicitName))
                return explicitName;

            return string.IsNullOrWhiteSpace(partId)
                ? null
                : EasyBuildBridgePartDataHelper.NicifyPartIdentifier(partId);
        }
    }
}
