#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    public static partial class RoadGameplayTooling
    {
        private static GameplayRoadDerivedData GetOrCreateDerivedAssetForGraph(GameplayRoadGraph graph)
        {
            var folder = graph != null ? GetAssetDirectory(graph) : DefaultAssetFolder;
            if (string.IsNullOrWhiteSpace(folder)) folder = DefaultAssetFolder;
            RoadEditorAssetUtility.EnsureFolderHierarchy(folder);

            var graphName = graph != null && !string.IsNullOrWhiteSpace(graph.name) ? graph.name : "GameplayRoadGraph";
            var derivedPath = folder + "/" + graphName + "_Derived.asset";
            return GetOrCreateAsset<GameplayRoadDerivedData>(derivedPath);
        }

        private static RoadGameplayMaskSet GetOrCreateMaskSetAssetForGraph(GameplayRoadGraph graph)
        {
            var folder = graph != null ? GetAssetDirectory(graph) : DefaultAssetFolder;
            if (string.IsNullOrWhiteSpace(folder)) folder = DefaultAssetFolder;
            RoadEditorAssetUtility.EnsureFolderHierarchy(folder);

            var graphName = graph != null && !string.IsNullOrWhiteSpace(graph.name) ? graph.name : "GameplayRoadGraph";
            var maskSetPath = folder + "/" + graphName + "_Masks.asset";
            return GetOrCreateAsset<RoadGameplayMaskSet>(maskSetPath);
        }

        private static RoadGameplayMaskBakeSettings GetOrCreateMaskSettingsAsset()
        {
            RoadEditorAssetUtility.EnsureFolderHierarchy(DefaultAssetFolder);
            return GetOrCreateAsset<RoadGameplayMaskBakeSettings>(DefaultMaskSettingsPath);
        }

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var directory = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? DefaultAssetFolder;
            RoadEditorAssetUtility.EnsureFolderHierarchy(directory);

            if (!TryPrepareAssetPathForTypedAsset<T>(path)) return null;
            return RoadEditorAssetUtility.GetOrCreateAsset<T>(path);
        }

        // Road gameplay tooling intentionally replaces incompatible or unreadable assets in-place.
        private static bool TryPrepareAssetPathForTypedAsset<T>(string path) where T : ScriptableObject
        {
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            if (mainAsset != null && mainAsset is not T)
            {
                Debug.LogWarning(
                    $"[RoadGameplayTooling] Recreating incompatible asset at '{path}'. Existing type='{mainAsset.GetType().Name}', expected='{typeof(T).Name}'.",
                    mainAsset);

                if (!AssetDatabase.DeleteAsset(path))
                {
                    Debug.LogError($"[RoadGameplayTooling] Could not delete incompatible asset at '{path}'.");
                    return false;
                }

                return true;
            }

            var fullPath = Path.GetFullPath(path);
            if (mainAsset == null && File.Exists(fullPath))
            {
                if (!AssetDatabase.DeleteAsset(path))
                {
                    Debug.LogError($"[RoadGameplayTooling] Could not delete unreadable asset at '{path}'.");
                    return false;
                }
            }

            return true;
        }

        private static string GetAssetDirectory(Object asset)
        {
            if (asset == null) return DefaultAssetFolder;

            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(path)) return DefaultAssetFolder;

            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory)) return DefaultAssetFolder;

            return directory.Replace("\\", "/");
        }
    }
}
#endif
