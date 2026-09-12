#region

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

#endregion

namespace Zombera.Editor
{
    public static partial class BuildPrefabSpritePipelineTool
    {
        private static List<GameObject> GetSelectedPrefabAssets()
        {
            var selectedAssets = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets);
            var prefabs = new List<GameObject>(selectedAssets.Length);

            for (var i = 0; i < selectedAssets.Length; i++)
            {
                var gameObject = selectedAssets[i] as GameObject;
                if (gameObject == null) continue;

                if (PrefabUtility.GetPrefabAssetType(gameObject) == PrefabAssetType.NotAPrefab) continue;

                prefabs.Add(gameObject);
            }

            return prefabs;
        }

        private static bool TryGetSelectedFolderPath(out string folderPath)
        {
            folderPath = null;

            if (Selection.activeObject == null) return false;

            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrWhiteSpace(path)) return false;

            path = path.Replace("\\", "/");

            if (!AssetDatabase.IsValidFolder(path)) return false;

            folderPath = path;
            return true;
        }

        private static string NormalizeAssetFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return null;
            return folderPath.Replace("\\", "/").Trim();
        }

        private static string BuildOutputPath(string prefabName, string partReference)
        {
            var baseName = !string.IsNullOrWhiteSpace(partReference) ? partReference.Trim() : prefabName.Trim();
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "BuildIcon";

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
                baseName = baseName.Replace(invalidChar, '_');

            return $"{IconOutputFolderPath}/{baseName}.png";
        }

        private static void EnsureFolderRecursive(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;

            folderPath = folderPath.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
                throw new InvalidOperationException($"Folder path must start with 'Assets': {folderPath}");

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;

                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
