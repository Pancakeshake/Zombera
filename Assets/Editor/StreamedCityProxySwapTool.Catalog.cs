#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    public static partial class StreamedCityProxySwapTool
    {
        private static readonly StreamedCityProxyCatalogUpdater ProxyCatalogUpdater =
            new StreamedCityProxyCatalogUpdater(DefaultProxyPrefabFolder, DefaultSwapDistanceMeters);

        private static int WireCatalogToGeneratedProxies(
            StreamedCityCatalog catalog,
            IReadOnlyDictionary<string, GameObject> proxyBySourcePath)
        {
            return ProxyCatalogUpdater.WireCatalogToGeneratedProxies(catalog, proxyBySourcePath);
        }
    }
}
#endif
