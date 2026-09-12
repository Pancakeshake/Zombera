#region

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zombera.BuildingSystem;

#endregion

namespace Zombera.Editor
{
    public static partial class BuildPrefabSpritePipelineTool
    {
        private static void BatchCaptureFromFolders(IReadOnlyList<string> folders, string progressTitle)
        {
            if (!TryNormalizeAndValidateFolderInputs(folders, out var validFolders))
            {
                EditorUtility.DisplayDialog(
                    "Batch Capture Build Prefab Icons",
                    "No valid prefab folders were found for icon capture.",
                    "OK");
                return;
            }

            if (!TryDiscoverPrefabs(validFolders, out var prefabGuids))
            {
                EditorUtility.DisplayDialog(
                    "Batch Capture Build Prefab Icons",
                    "No prefab assets found in the selected/configured folders.",
                    "OK");
                return;
            }

            var rig = EnsureRenderRigInActiveScene();
            EnsureFolderRecursive(IconOutputFolderPath);

            var state = new BatchCaptureState();

            try
            {
                for (var i = 0; i < prefabGuids.Length; i++)
                {
                    var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                    if (DisplayBatchProgress(progressTitle, prefab, i, prefabGuids.Length))
                    {
                        state.WasCancelled = true;
                        break;
                    }

                    ProcessPrefabResult(prefabPath, prefab, rig, state);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var generatedEntries = BuildGeneratedEntries(state);
            CreateOrUpdateSpriteLibrary(generatedEntries, mergeWithExisting: false);

            var summary = BuildFinalSummaryText(state, prefabGuids.Length);

            Debug.Log($"[BuildPrefabSpritePipelineTool] Batch capture complete.\n{summary}");
            EditorUtility.DisplayDialog("Batch Capture Build Prefab Icons", summary, "OK");
        }

        private static bool TryNormalizeAndValidateFolderInputs(IReadOnlyList<string> folders, out List<string> validFolders)
        {
            validFolders = new List<string>();
            if (folders == null || folders.Count == 0) return false;

            for (var i = 0; i < folders.Count; i++)
            {
                var normalized = NormalizeAssetFolderPath(folders[i]);
                if (string.IsNullOrWhiteSpace(normalized)) continue;
                if (!AssetDatabase.IsValidFolder(normalized)) continue;

                validFolders.Add(normalized);
            }

            return validFolders.Count > 0;
        }

        private static bool TryDiscoverPrefabs(List<string> validFolders, out string[] prefabGuids)
        {
            prefabGuids = AssetDatabase.FindAssets("t:Prefab", validFolders.ToArray());
            return prefabGuids is { Length: > 0 };
        }

        private static bool DisplayBatchProgress(string progressTitle, GameObject prefab, int index, int total)
        {
            var prefabName = prefab != null ? prefab.name : "(missing prefab asset)";
            var info = $"Rendering {prefabName} ({index + 1}/{total})";
            var progress = total <= 0 ? 1f : (index + 1f) / total;

            EditorUtility.DisplayProgressBar(progressTitle, info, progress);
            return false;
        }

        private static void ProcessPrefabResult(string prefabPath, GameObject prefab, RenderRig rig, BatchCaptureState state)
        {
            state.ProcessedCount++;

            if (prefab == null)
            {
                RecordFailure(state, $"(null) {prefabPath}");
                return;
            }

            var partReference = ResolvePartReference(prefab);
            var outputPath = BuildOutputPath(prefab.name, partReference);

            var success = CapturePrefabIcon(prefab, outputPath, rig, out var error);
            if (!success)
            {
                RecordFailure(state, $"{prefab.name}: {error}");
                return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            if (sprite == null)
            {
                RecordFailure(state, $"{prefab.name}: generated sprite failed to import ({outputPath})");
                return;
            }

            state.SuccessCount++;
            RecordSuccessOrDuplicate(state, partReference, sprite, prefabPath);
        }

        private static void RecordFailure(BatchCaptureState state, string failure)
        {
            state.FailureCount++;
            state.Failures.Add(failure);
        }

        private static void RecordSuccessOrDuplicate(BatchCaptureState state, string partReference, Sprite sprite,
            string prefabPath)
        {
            if (state.EntriesByPartReference.TryGetValue(partReference, out var existing))
            {
                state.DuplicateCount++;
                RecordDuplicateCollision(state, partReference, existing.prefabAssetPath, prefabPath);
                return;
            }

            state.EntriesByPartReference[partReference] = new BuildPrefabSpriteEntry
            {
                partReference = partReference,
                sprite = sprite,
                prefabAssetPath = prefabPath
            };
        }

        private static void RecordDuplicateCollision(BatchCaptureState state, string partReference, string existingPrefabPath,
            string incomingPrefabPath)
        {
            var existingPath = string.IsNullOrWhiteSpace(existingPrefabPath)
                ? "(unknown existing prefab path)"
                : existingPrefabPath;

            var incomingPath = string.IsNullOrWhiteSpace(incomingPrefabPath)
                ? "(unknown incoming prefab path)"
                : incomingPrefabPath;

            var collisionMessage =
                $"{partReference}: existing '{existingPath}' vs incoming '{incomingPath}' (kept existing)";

            state.DuplicateCollisions.Add(collisionMessage);
            Debug.LogWarning($"[BuildPrefabSpritePipelineTool] Duplicate part reference collision. {collisionMessage}");
        }

        private static List<BuildPrefabSpriteEntry> BuildGeneratedEntries(BatchCaptureState state)
        {
            return new List<BuildPrefabSpriteEntry>(state.EntriesByPartReference.Values);
        }

        private static string BuildFinalSummaryText(BatchCaptureState state, int totalPrefabCount)
        {
            var processed = state.WasCancelled ? state.ProcessedCount : totalPrefabCount;
            var summary =
                $"Prefabs Processed: {processed}\nCaptured: {state.SuccessCount}\nFailed: {state.FailureCount}\nDuplicate Part References Skipped: {state.DuplicateCount}\nGenerated Icons: {IconOutputFolderPath}\nSprite Library: {SpriteLibraryAssetPath}";

            if (state.WasCancelled)
                summary += "\n\nBatch capture was cancelled by user.";

            if (state.DuplicateCollisions.Count > 0)
                summary += "\n\nDuplicate Collisions:\n- " + string.Join("\n- ", state.DuplicateCollisions);

            if (state.Failures.Count > 0)
                summary += "\n\nFailures:\n- " + string.Join("\n- ", state.Failures);

            return summary;
        }

        private sealed class BatchCaptureState
        {
            public int ProcessedCount { get; set; }
            public int SuccessCount { get; set; }
            public int FailureCount { get; set; }
            public int DuplicateCount { get; set; }
            public bool WasCancelled { get; set; }

            public List<string> Failures { get; } = new();
            public List<string> DuplicateCollisions { get; } = new();

            public Dictionary<string, BuildPrefabSpriteEntry> EntriesByPartReference { get; } =
                new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
