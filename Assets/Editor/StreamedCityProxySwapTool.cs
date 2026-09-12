#if UNITY_EDITOR
using UnityEditor;

namespace Zombera.Editor
{
    /// <summary>
    ///     Generates lightweight city proxy prefabs and wires them into StreamedCityCatalog entries.
    /// </summary>
    public static partial class StreamedCityProxySwapTool
    {
        private const string BuildMenuPath =
            "Tools/Build/Mod Kits/Building Generator/Proxy Swap/Build Proxies And Wire Catalog";

        private const string DefaultCatalogAssetPath = "Assets/02_Shared/ScriptableObjects/StreamedCityCatalog.asset";
        private const string BuildingPrefabsRoot = "Assets/02_Shared/Prefabs/Building";
        private const string DefaultSourcePrefabFolder = BuildingPrefabsRoot + "/Buildings_Modular_Complete";
        private const string DefaultProxyPrefabFolder = "Assets/02_Shared/Proxies/Buildings_Complete";
        private const float DefaultSwapDistanceMeters = 10f;
        private const string ProxyMeshAssetSuffix = "_ProxyMeshes.asset";
        private const bool EnableGpuInstancingOnSourceMaterials = true;
        private const bool DisableShadowsOnProxyRenderers = true;
        private const int OrphanDeleteConfirmationThreshold = 100;

        [MenuItem(BuildMenuPath, priority = -500)]
        private static void BuildProxyPrefabsAndWireCatalog()
        {
            TouchSplitMembersForAnalysis();
            ExecuteProxyBuildRun();
        }

        [MenuItem(BuildMenuPath, true, priority = -500)]
        private static bool ValidateBuildProxyPrefabsAndWireCatalog()
        {
            return AssetDatabase.IsValidFolder(DefaultSourcePrefabFolder);
        }

        private static void TouchSplitMembersForAnalysis()
        {
            _ = DefaultCatalogAssetPath;
            _ = DefaultProxyPrefabFolder;
            _ = DefaultSwapDistanceMeters;
            _ = ProxyMeshAssetSuffix;
            _ = EnableGpuInstancingOnSourceMaterials;
            _ = DisableShadowsOnProxyRenderers;
            _ = OrphanDeleteConfirmationThreshold;
        }
    }
}
#endif
