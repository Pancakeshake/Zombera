#region

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zombera.BuildingSystem;

#endregion

namespace Zombera.Editor
{
    public static partial class BuildPrefabSpritePipelineTool
    {
        private static void CreateOrUpdateSpriteLibrary(List<BuildPrefabSpriteEntry> generatedEntries,
            bool mergeWithExisting)
        {
            EnsureFolderRecursive(Path.GetDirectoryName(SpriteLibraryAssetPath)?.Replace("\\", "/"));

            var library = LoadOrCreateLibraryAsset();
            var entriesByPartReference = mergeWithExisting
                ? ReadExistingEntriesForMerge(library)
                : new Dictionary<string, BuildPrefabSpriteEntry>(StringComparer.OrdinalIgnoreCase);

            MergeGeneratedEntries(entriesByPartReference, generatedEntries);
            PersistReplacementEntries(library, entriesByPartReference);
        }

        private static BuildPrefabSpriteLibrary LoadOrCreateLibraryAsset()
        {
            var library = AssetDatabase.LoadAssetAtPath<BuildPrefabSpriteLibrary>(SpriteLibraryAssetPath);
            if (library != null) return library;

            library = ScriptableObject.CreateInstance<BuildPrefabSpriteLibrary>();
            AssetDatabase.CreateAsset(library, SpriteLibraryAssetPath);
            return library;
        }

        private static Dictionary<string, BuildPrefabSpriteEntry> ReadExistingEntriesForMerge(
            BuildPrefabSpriteLibrary library)
        {
            var entriesByPartReference = new Dictionary<string, BuildPrefabSpriteEntry>(StringComparer.OrdinalIgnoreCase);
            if (library == null) return entriesByPartReference;

            var serializedObject = new SerializedObject(library);
            var entriesProperty = serializedObject.FindProperty("entries");
            if (entriesProperty == null) return entriesByPartReference;

            for (var i = 0; i < entriesProperty.arraySize; i++)
            {
                var element = entriesProperty.GetArrayElementAtIndex(i);
                var partReference = element.FindPropertyRelative("partReference")?.stringValue;
                var sprite = element.FindPropertyRelative("sprite")?.objectReferenceValue as Sprite;
                var prefabAssetPath = element.FindPropertyRelative("prefabAssetPath")?.stringValue;

                var normalizedPartReference = NormalizePartReference(partReference);
                if (normalizedPartReference == null || sprite == null)
                    continue;

                entriesByPartReference[normalizedPartReference] = new BuildPrefabSpriteEntry
                {
                    partReference = normalizedPartReference,
                    sprite = sprite,
                    prefabAssetPath = prefabAssetPath
                };
            }

            return entriesByPartReference;
        }

        private static void MergeGeneratedEntries(Dictionary<string, BuildPrefabSpriteEntry> entriesByPartReference,
            List<BuildPrefabSpriteEntry> generatedEntries)
        {
            if (generatedEntries == null || generatedEntries.Count == 0) return;

            for (var i = 0; i < generatedEntries.Count; i++)
            {
                var entry = generatedEntries[i];
                if (entry == null || entry.sprite == null) continue;

                var key = NormalizePartReference(entry.partReference);
                if (key == null) continue;

                if (entriesByPartReference.TryGetValue(key, out var existing))
                {
                    var existingPath = string.IsNullOrWhiteSpace(existing.prefabAssetPath)
                        ? "(unknown existing prefab path)"
                        : existing.prefabAssetPath;

                    var incomingPath = string.IsNullOrWhiteSpace(entry.prefabAssetPath)
                        ? "(unknown incoming prefab path)"
                        : entry.prefabAssetPath;

                    if (!string.Equals(existingPath, incomingPath, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogWarning(
                            $"[BuildPrefabSpritePipelineTool] Sprite library merge collision for part reference '{key}'. Existing '{existingPath}', incoming '{incomingPath}'.");
                    }
                }

                entriesByPartReference[key] = new BuildPrefabSpriteEntry
                {
                    partReference = key,
                    sprite = entry.sprite,
                    prefabAssetPath = entry.prefabAssetPath
                };
            }
        }

        private static void PersistReplacementEntries(BuildPrefabSpriteLibrary library,
            Dictionary<string, BuildPrefabSpriteEntry> entriesByPartReference)
        {
            var replacementEntries = BuildDeterministicEntries(entriesByPartReference);
            library.ReplaceEntries(replacementEntries);

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static List<BuildPrefabSpriteEntry> BuildDeterministicEntries(
            Dictionary<string, BuildPrefabSpriteEntry> entriesByPartReference)
        {
            var replacementEntries = new List<BuildPrefabSpriteEntry>(entriesByPartReference.Values);
            replacementEntries.Sort((left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left?.partReference, right?.partReference));
            return replacementEntries;
        }

        private static string NormalizePartReference(string partReference)
        {
            if (string.IsNullOrWhiteSpace(partReference)) return null;
            return partReference.Trim();
        }
    }
}
