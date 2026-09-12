using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Zombera.BuildingSystem;
using Zombera.Data;

namespace Zombera.Editor
{
    /// <summary>
    ///     Automatically populates the BuildingSaveRegistry by scanning the project for BuildingData and BuildPiece prefabs.
    /// </summary>
    public static class BuildingCatalogBuildTool
    {
        private const string MenuPath = "Tools/World/Buildings/Rebuild Building Catalog";
        private const string RegistryPath = "Assets/ScriptableObjects/Buildings/BuildingSaveRegistry.asset";

        private static readonly string[] BuildPieceRoots = 
        {
            "Assets/02_Shared/Prefabs/Building",
            "Assets/01_Game/07_Building"
        };

        [MenuItem(MenuPath, priority = -500)]
        public static void RebuildCatalog()
        {
            EnsureFolderHierarchy("Assets/ScriptableObjects/Buildings");

            var registry = AssetDatabase.LoadAssetAtPath<BuildingSaveRegistry>(RegistryPath);
            var newlyCreated = false;
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<BuildingSaveRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
                newlyCreated = true;
            }

            registry.Clear();

            // 1. Find all BuildingData assets in the project
            var dataGuids = AssetDatabase.FindAssets("t:BuildingData");
            foreach (var guid in dataGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
                if (data != null && !registry.buildingData.Contains(data))
                {
                    registry.buildingData.Add(data);
                }
            }

            // 2. Find all prefabs with BuildPiece component in specified roots
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", BuildPieceRoots);
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<BuildPiece>() != null)
                {
                    if (!registry.buildPiecePrefabs.Contains(prefab))
                    {
                        registry.buildPiecePrefabs.Add(prefab);
                    }
                }
            }

            registry.Initialize();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            var status = newlyCreated ? "Created" : "Updated";
            Debug.Log($"[BuildingCatalogBuildTool] {status} {RegistryPath}. Data: {registry.buildingData.Count}, Pieces: {registry.buildPiecePrefabs.Count}");
        }

        private static void EnsureFolderHierarchy(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            folderPath = folderPath.Replace('\\', '/');

            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            if (parts.Length == 0 || !parts[0].Equals("Assets", System.StringComparison.Ordinal)) return;

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
