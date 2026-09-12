#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    /// <summary>
    ///     Proxy baking for the modular building generator: after a building prefab is
    ///     saved and skinned, a game-ready proxy is baked into
    ///     <see cref="ModularSingleLevelHouseGeneratorTool.DefaultProxyOutputFolder" />
    ///     mirroring the source subfolders (e.g. <c>Residential/1x1/</c>).
    ///     Also hosts the batch backfill menu that proxies every existing building and
    ///     removes proxy assets this run did not produce (stale / orphan proxies).
    /// </summary>
    public static partial class ModularSingleLevelHouseGeneratorTool
    {
        private const string MenuRebuildAllProxies =
            "Tools/Build/Mod Kits/Building Generator/Rebuild All Building Proxies";

        private const string MenuClearAllBuildingsAndProxies =
            "Tools/Build/Mod Kits/Building Generator/Clear All Buildings And Proxies";

        private const bool EnableGpuInstancingOnBuildingMaterials = true;

        [MenuItem(MenuRebuildAllProxies, priority = -490)]
        private static void RebuildAllBuildingProxiesFromMenu()
        {
            RebuildAllBuildingProxies();
        }

        [MenuItem(MenuRebuildAllProxies, true, priority = -490)]
        private static bool ValidateRebuildAllBuildingProxiesFromMenu()
        {
            return AssetDatabase.IsValidFolder(DefaultOutputFolder);
        }

        [MenuItem(MenuClearAllBuildingsAndProxies, priority = -485)]
        private static void ClearAllBuildingsAndProxiesFromMenu()
        {
            ClearAllBuildingsAndProxies();
        }

        [MenuItem(MenuClearAllBuildingsAndProxies, true, priority = -485)]
        private static bool ValidateClearAllBuildingsAndProxiesFromMenu()
        {
            return AssetDatabase.IsValidFolder(DefaultOutputFolder) ||
                   AssetDatabase.IsValidFolder(DefaultProxyOutputFolder);
        }

        /// <summary>
        ///     Deletes every building prefab under <see cref="DefaultOutputFolder" /> and
        ///     every proxy prefab / proxy-mesh bundle under <see cref="DefaultProxyOutputFolder" />,
        ///     keeping the folder structure so buildings can be regenerated quickly.
        /// </summary>
        public static int ClearAllBuildingsAndProxies(bool confirm = true)
        {
            var buildingPaths = BuildingProxyBaker.CollectSourcePrefabPaths(DefaultOutputFolder);
            var proxyAssetPaths = CollectProxyAssetPaths(DefaultProxyOutputFolder);
            var total = buildingPaths.Count + proxyAssetPaths.Count;

            if (total == 0)
            {
                Debug.Log("[ModularSingleLevelHouseGeneratorTool] Nothing to clear — no building or proxy assets found.");
                return 0;
            }

            if (confirm && !EditorUtility.DisplayDialog(
                    "Clear All Buildings And Proxies",
                    "Delete " + buildingPaths.Count + " building prefab(s) and " + proxyAssetPaths.Count +
                    " proxy asset(s)?\n\nFolders are kept. Regenerate buildings afterwards to restore prefabs and proxies.",
                    "Delete", "Cancel"))
                return 0;

            for (var i = 0; i < buildingPaths.Count; i++)
                AssetDatabase.DeleteAsset(buildingPaths[i]);

            for (var i = 0; i < proxyAssetPaths.Count; i++)
                AssetDatabase.DeleteAsset(proxyAssetPaths[i]);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ModularSingleLevelHouseGeneratorTool] Deleted " + buildingPaths.Count +
                      " building prefab(s) and " + proxyAssetPaths.Count +
                      " proxy asset(s). Folders kept — regenerate buildings now.");
            return total;
        }

        private static List<string> CollectProxyAssetPaths(string proxyRoot)
        {
            var paths = new List<string>();
            if (string.IsNullOrWhiteSpace(proxyRoot) || !AssetDatabase.IsValidFolder(proxyRoot))
                return paths;

            var guids = AssetDatabase.FindAssets(string.Empty, new[] { proxyRoot });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(BuildingProxyBaker.ProxyMeshAssetSuffix, System.StringComparison.OrdinalIgnoreCase))
                    paths.Add(path);
            }

            paths.Sort(System.StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        /// <summary>
        ///     Batch-bakes proxies for every assembled building under <see cref="DefaultOutputFolder" />
        ///     and deletes proxy assets this run did not produce (stale / orphan proxies).
        ///     Public so editor tooling (MCP / tests) can invoke it directly.
        /// </summary>
        public static int RebuildAllBuildingProxies()
        {
            var sourcePaths = BuildingProxyBaker.CollectSourcePrefabPaths(DefaultOutputFolder);
            if (sourcePaths.Count == 0)
            {
                Debug.LogWarning("[ModularSingleLevelHouseGeneratorTool] No building prefabs found under " +
                                 DefaultOutputFolder + ".");
                return 0;
            }

            var builtProxyPaths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var built = 0;

            for (var i = 0; i < sourcePaths.Count; i++)
            {
                var path = sourcePaths[i];
                var relativeFolder = ResolveProxyRelativeFolder(path);
                if (TryBuildProxyForPrefab(path, DefaultProxyOutputFolder, relativeFolder,
                        out var proxyPath, out _))
                {
                    built++;
                    builtProxyPaths.Add(proxyPath);
                }
            }

            var orphanCount = DeleteOrphanProxies(DefaultProxyOutputFolder, builtProxyPaths);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ModularSingleLevelHouseGeneratorTool] Built " + built + " proxies for " +
                      sourcePaths.Count + " buildings; removed " + orphanCount + " orphan proxy(ies).");
            return built;
        }

        /// <summary>
        ///     Bakes a proxy for a freshly generated building. Failures are logged as
        ///     warnings — the assembled prefab remains valid without a proxy.
        /// </summary>
        private static bool TryBuildProxyForBuilding(GenerationContext context, string prefabPath)
        {
            if (context == null || string.IsNullOrWhiteSpace(prefabPath))
                return false;

            var outputFolder = ResolveProxyOutputFolder(context.Settings);
            var relativeSubFolder = $"{context.BuildingCategory}/{context.Plan.Width}x{context.Plan.Depth}";
            if (TryBuildProxyForPrefab(prefabPath, outputFolder, relativeSubFolder, out _, out var failure))
                return true;

            Debug.LogWarning("[ModularSingleLevelHouseGeneratorTool] Proxy bake failed for '" +
                             prefabPath + "': " + failure);
            return false;
        }

        private static bool TryBuildProxyForPrefab(
            string prefabPath, string outputFolder, string relativeSubFolder,
            out string proxyPath, out string failureReason)
        {
            var ok = BuildingProxyBaker.TryBuildProxy(
                prefabPath,
                outputFolder,
                relativeSubFolder,
                EnableGpuInstancingOnBuildingMaterials,
                out proxyPath,
                out failureReason);

            if (ok)
                Debug.Log("[ModularSingleLevelHouseGeneratorTool] Built proxy '" + proxyPath + "'.");

            return ok;
        }

        private static string ResolveProxyOutputFolder(GeneratorSettings settings)
        {
            var folder = settings != null && !string.IsNullOrWhiteSpace(settings.ProxyOutputFolder)
                ? settings.ProxyOutputFolder
                : DefaultProxyOutputFolder;
            return folder;
        }

        /// <summary>
        ///     Mirrors the source's subfolders under the assembled root for the proxy tree,
        ///     e.g. <c>Residential/1x1</c> for <c>…/Residential/1x1/Residential_123.prefab</c>
        ///     or just <c>Residential</c> for legacy flat-layout prefabs.
        /// </summary>
        private static string ResolveProxyRelativeFolder(string sourcePath)
        {
            var root = DefaultOutputFolder.TrimEnd('/') + "/";
            var normalized = sourcePath.Replace('\\', '/');
            if (!normalized.StartsWith(root, System.StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var relative = normalized[root.Length..];
            var separator = relative.LastIndexOf('/');
            return separator > 0 ? relative[..separator] : string.Empty;
        }

        private static int DeleteOrphanProxies(string proxyRoot, HashSet<string> builtProxyPaths)
        {
            var deleted = 0;
            var proxyPaths = BuildingProxyBaker.CollectSourcePrefabPaths(proxyRoot);
            for (var i = 0; i < proxyPaths.Count; i++)
            {
                var proxyPath = proxyPaths[i];
                if (builtProxyPaths.Contains(proxyPath))
                    continue;

                var meshAssetPath = proxyPath[..^".prefab".Length] + BuildingProxyBaker.ProxyMeshAssetSuffix;
                if (AssetDatabase.LoadAssetAtPath<Object>(meshAssetPath) != null)
                    AssetDatabase.DeleteAsset(meshAssetPath);

                AssetDatabase.DeleteAsset(proxyPath);
                deleted++;
            }

            return deleted;
        }
    }
}
#endif
