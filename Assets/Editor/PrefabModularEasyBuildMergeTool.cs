#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    public static partial class PrefabModularEasyBuildMergeTool
    {
        private const string PilotMenuPath =
            "Tools/World/Buildings/Building Parts Modular/Convert Pilot (Merge EasyBuild Scripts)";

        private const string BatchMenuPath =
            "Tools/World/Buildings/Building Parts Modular/Convert All (Merge EasyBuild Scripts)";

        private const string RepairMenuPath =
            "Tools/World/Buildings/Building Parts Modular/Repair EasyBuild Parts";

        private const string DonorPrefabPath =
            "Assets/03_ThirdParty/Mind Code Interactive/Easy Build System/Packages/Extensions/Survival Upgradable/Shared/Prefabs/Building Parts/Building_Foundation.prefab";

        private const string TargetPrefabPath =
            "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Building_Foundation.prefab";

        private const string TargetRootFolderPath = "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts";

        [MenuItem(PilotMenuPath, priority = -500)]
        private static void ConvertPilotBarrel()
        {
            var result = MergeEasyBuildScripts(DonorPrefabPath, TargetPrefabPath, overwriteExistingScripts: false);

            var title = result.Success ? "Pilot Conversion Complete" : "Pilot Conversion Failed";
            Debug.Log($"[PrefabModularEasyBuildMergeTool] {result.Summary}");
            EditorUtility.DisplayDialog(title, result.Summary, "OK");
        }

        [MenuItem(PilotMenuPath, true, priority = -500)]
        private static bool ValidateConvertPilotBarrel()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(DonorPrefabPath) != null
                   && AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath) != null;
        }

        [MenuItem(BatchMenuPath, priority = -500)]
        private static void ConvertAllPrefabModularPrefabs()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DonorPrefabPath) == null)
            {
                const string missingDonor = "Donor prefab is missing. Batch conversion aborted.";
                Debug.LogError($"[PrefabModularEasyBuildMergeTool] {missingDonor} Donor: {DonorPrefabPath}");
                EditorUtility.DisplayDialog("Batch Conversion Failed", missingDonor, "OK");
                return;
            }

            var targetPaths = CollectTargetPrefabPaths(includeDonor: false);
            if (targetPaths.Count == 0)
            {
                const string noTargets = "No target prefabs were found under " + TargetRootFolderPath + ".";
                Debug.LogWarning($"[PrefabModularEasyBuildMergeTool] {noTargets}");
                EditorUtility.DisplayDialog("Batch Conversion", noTargets, "OK");
                return;
            }

            var execution = ExecuteBatchOperation(new BatchExecutionRequest
            {
                OperationName = "Batch Conversion",
                TargetPaths = targetPaths,
                IncludeDonor = false,
                ShowProgressBar = false,
                ProgressTitle = "Merge EasyBuild Scripts",
                ProgressItemLabel = "Converting",
                ExecuteTarget = path => MergeEasyBuildScripts(DonorPrefabPath, path, overwriteExistingScripts: false),
                LogSuccess = (index, total, targetPath, result) =>
                {
                    Debug.Log(
                        $"[PrefabModularEasyBuildMergeTool] [{index}/{total}] {targetPath}\\n{result.Summary}");
                },
                LogFailure = (index, total, targetPath, failureLine, result) =>
                {
                    _ = targetPath;
                    _ = result;
                    Debug.LogError($"[PrefabModularEasyBuildMergeTool] [{index}/{total}] {failureLine}");
                }
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = BuildBatchMergeSummary(execution);
            var title = execution.FailedCount == 0
                ? "Batch Conversion Complete"
                : "Batch Conversion Complete (With Failures)";

            Debug.Log($"[PrefabModularEasyBuildMergeTool] {summary}");
            EditorUtility.DisplayDialog(title, summary, "OK");
        }

        [MenuItem(BatchMenuPath, true, priority = -500)]
        private static bool ValidateConvertAllPrefabModularPrefabs()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DonorPrefabPath) == null)
                return false;

            return AssetDatabase.IsValidFolder(TargetRootFolderPath);
        }

        [MenuItem(RepairMenuPath, priority = -500)]
        private static void RepairAllPrefabModularPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(TargetRootFolderPath))
            {
                const string missingFolder = "Target root folder is missing. Repair aborted.";
                Debug.LogError($"[PrefabModularEasyBuildMergeTool] {missingFolder} Folder: {TargetRootFolderPath}");
                EditorUtility.DisplayDialog("Repair Failed", missingFolder, "OK");
                return;
            }

            var targetPaths = CollectTargetPrefabPaths(includeDonor: true);
            if (targetPaths.Count == 0)
            {
                const string noTargets = "No target prefabs were found under " + TargetRootFolderPath + ".";
                Debug.LogWarning($"[PrefabModularEasyBuildMergeTool] {noTargets}");
                EditorUtility.DisplayDialog("Repair Complete", noTargets, "OK");
                return;
            }

            var execution = ExecuteBatchOperation(new BatchExecutionRequest
            {
                OperationName = "Repair EasyBuild Parts",
                TargetPaths = targetPaths,
                IncludeDonor = true,
                ShowProgressBar = true,
                ProgressTitle = "Repair EasyBuild Parts",
                ProgressItemLabel = "Repairing",
                ExecuteTarget = RepairEasyBuildBindingsInPrefab,
                LogSuccess = null,
                LogFailure = (_, _, _, failureLine, _) =>
                {
                    Debug.LogError($"[PrefabModularEasyBuildMergeTool] Repair failed: {failureLine}");
                }
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = BuildBatchRepairSummary(execution);
            Debug.Log($"[PrefabModularEasyBuildMergeTool] {summary}");
            EditorUtility.DisplayDialog("Repair EasyBuild Parts", summary, "OK");
        }

        [MenuItem(RepairMenuPath, true, priority = -500)]
        private static bool ValidateRepairAllPrefabModularPrefabs()
        {
            return AssetDatabase.IsValidFolder(TargetRootFolderPath);
        }

        private const string RenameMenuPath =
            "Tools/World/Buildings/Prefab Modular/Rename PA_SM_ → C_";

        private const string WipeNonCMenuPath =
            "Tools/World/Buildings/Prefab Modular/Wipe Non-C_ Prefabs";

        [MenuItem(RenameMenuPath, priority = -500)]
        private static void RenamePaSmToC()
        {
            if (!AssetDatabase.IsValidFolder(TargetRootFolderPath))
            {
                Debug.LogError($"[PrefabModularEasyBuildMergeTool] Target folder missing: {TargetRootFolderPath}");
                return;
            }

            var guids = AssetDatabase.FindAssets("PA_SM_ t:Prefab", new[] { TargetRootFolderPath });
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Rename PA_SM_ → C_", "No PA_SM_ prefabs found.", "OK");
                return;
            }

            var renamed = 0;
            var failed = 0;
            var failureDetails = new List<string>();

            try
            {
                for (var i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var dir = System.IO.Path.GetDirectoryName(path);
                    var oldName = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (!oldName.StartsWith("PA_SM_", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var newName = "C_" + oldName.Substring("PA_SM_".Length);
                    var newPath = System.IO.Path.Combine(dir, newName + ".prefab").Replace("\\", "/");

                    if (System.IO.File.Exists(newPath))
                    {
                        failed++;
                        failureDetails.Add($"{path} → target already exists: {newPath}");
                        continue;
                    }

                    // Load prefab to update internal m_Name
                    var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                    if (prefabRoot == null)
                    {
                        failed++;
                        failureDetails.Add($"{path}: could not load contents");
                        continue;
                    }

                    try
                    {
                        prefabRoot.name = newName;
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                    }

                    var error = AssetDatabase.RenameAsset(path, newName);
                    if (!string.IsNullOrEmpty(error))
                    {
                        failed++;
                        failureDetails.Add($"{path}: rename failed: {error}");
                    }
                    else
                    {
                        renamed++;
                        if (renamed % 20 == 0)
                            AssetDatabase.SaveAssets();
                    }
                }
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            var summary = $"Renamed {renamed} prefabs from PA_SM_ → C_.";
            if (failed > 0)
                summary += $"\nFailed: {failed}.";

            Debug.Log($"[PrefabModularEasyBuildMergeTool] {summary}");
            if (failureDetails.Count > 0)
            {
                foreach (var detail in failureDetails)
                    Debug.LogWarning($"[PrefabModularEasyBuildMergeTool] {detail}");
            }

            EditorUtility.DisplayDialog("Rename PA_SM_ → C_", summary, "OK");
        }

        [MenuItem(RenameMenuPath, true, priority = -500)]
        private static bool ValidateRenamePaSmToC()
        {
            return AssetDatabase.IsValidFolder(TargetRootFolderPath);
        }

        [MenuItem(WipeNonCMenuPath, priority = -500)]
        private static void WipeNonCPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(TargetRootFolderPath))
            {
                Debug.LogError($"[PrefabModularEasyBuildMergeTool] Target folder missing: {TargetRootFolderPath}");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { TargetRootFolderPath });
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Wipe Non-C_ Prefabs", "No prefabs found.", "OK");
                return;
            }

            var toDelete = new List<string>();
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!fileName.StartsWith("C_", StringComparison.OrdinalIgnoreCase))
                    toDelete.Add(path);
            }

            if (toDelete.Count == 0)
            {
                EditorUtility.DisplayDialog("Wipe Non-C_ Prefabs",
                    "All prefabs already use C_ prefix. Nothing to wipe.", "OK");
                return;
            }

            var confirmMessage = $"Found {toDelete.Count} prefab(s) NOT starting with C_.\n\n" +
                                 "Delete them all?\n\n" +
                                 "(This cannot be undone. Use git to recover if needed.)";

            if (!EditorUtility.DisplayDialog("Wipe Non-C_ Prefabs", confirmMessage, "Delete All", "Cancel"))
                return;

            var deleted = 0;
            var failed = 0;
            var failureDetails = new List<string>();

            try
            {
                for (var i = 0; i < toDelete.Count; i++)
                {
                    var path = toDelete[i];
                    if (AssetDatabase.DeleteAsset(path))
                    {
                        deleted++;
                    }
                    else
                    {
                        failed++;
                        failureDetails.Add($"Delete failed: {path}");
                    }
                }
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            var summary = $"Deleted {deleted} non-C_ prefabs.";
            if (failed > 0)
                summary += $"\nFailed: {failed}.";

            Debug.Log($"[PrefabModularEasyBuildMergeTool] {summary}");
            foreach (var detail in failureDetails)
                Debug.LogError($"[PrefabModularEasyBuildMergeTool] {detail}");

            EditorUtility.DisplayDialog("Wipe Non-C_ Prefabs", summary, "OK");
        }

        [MenuItem(WipeNonCMenuPath, true, priority = -500)]
        private static bool ValidateWipeNonCPrefabs()
        {
            return AssetDatabase.IsValidFolder(TargetRootFolderPath);
        }

        private static List<string> CollectTargetPrefabPaths(bool includeDonor)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { TargetRootFolderPath });
            var paths = new List<string>(guids.Length);

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!includeDonor && string.Equals(path, DonorPrefabPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                paths.Add(path);
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static BatchExecutionSummary ExecuteBatchOperation(BatchExecutionRequest request)
        {
            var succeeded = 0;
            var failed = 0;
            var repairedPrefabs = 0;
            var repairedParts = 0;
            var failureLines = new List<string>();

            try
            {
                for (var i = 0; i < request.TargetPaths.Count; i++)
                {
                    var targetPath = request.TargetPaths[i];

                    if (request.ShowProgressBar)
                    {
                        var progress = request.TargetPaths.Count > 0
                            ? (i + 1f) / request.TargetPaths.Count
                            : 1f;
                        EditorUtility.DisplayProgressBar(
                            request.ProgressTitle,
                            $"{request.ProgressItemLabel} {targetPath} ({i + 1}/{request.TargetPaths.Count})",
                            progress);
                    }

                    var result = request.ExecuteTarget(targetPath);
                    if (result.Success)
                    {
                        succeeded++;
                        if (result.RepairedPartCount > 0)
                        {
                            repairedPrefabs++;
                            repairedParts += result.RepairedPartCount;
                        }

                        request.LogSuccess?.Invoke(i + 1, request.TargetPaths.Count, targetPath, result);
                        continue;
                    }

                    failed++;
                    var failureLine = $"{targetPath} -> {result.Summary}";
                    failureLines.Add(failureLine);
                    request.LogFailure?.Invoke(i + 1, request.TargetPaths.Count, targetPath, failureLine, result);
                }
            }
            finally
            {
                if (request.ShowProgressBar)
                    EditorUtility.ClearProgressBar();
            }

            return new BatchExecutionSummary
            {
                OperationName = request.OperationName,
                IncludeDonor = request.IncludeDonor,
                TargetCount = Mathf.Max(0, request.TargetPaths.Count),
                SuccessCount = Mathf.Max(0, succeeded),
                FailedCount = Mathf.Max(0, failed),
                RepairedPrefabCount = Mathf.Max(0, repairedPrefabs),
                RepairedPartCount = Mathf.Max(0, repairedParts),
                FailureLines = failureLines
            };
        }

        private static string BuildBatchMergeSummary(BatchExecutionSummary summary)
        {
            var summaryText =
                $"Batch merge complete.\\n" +
                $"Donor: {DonorPrefabPath}\\n" +
                $"Targets processed: {summary.TargetCount}\\n" +
                $"Succeeded: {summary.SuccessCount}\\n" +
                $"Failed: {summary.FailedCount}\\n" +
                $"EasyBuild parts repaired: {summary.RepairedPartCount}";

            return AppendFailureLines(summaryText, summary.FailureLines);
        }

        private static string BuildBatchRepairSummary(BatchExecutionSummary summary)
        {
            var summaryText =
                $"Repair complete.\\n" +
                $"Targets processed: {summary.TargetCount}\\n" +
                $"Prefabs repaired: {summary.RepairedPrefabCount}\\n" +
                $"EasyBuild parts repaired: {summary.RepairedPartCount}\\n" +
                $"Failed: {summary.FailedCount}";

            return AppendFailureLines(summaryText, summary.FailureLines);
        }

        private static string AppendFailureLines(string summaryText, IReadOnlyList<string> failureLines)
        {
            if (failureLines == null || failureLines.Count == 0)
                return summaryText;

            var maxFailuresToDisplay = Mathf.Min(10, failureLines.Count);
            summaryText += "\\n\\nFirst failures:";

            for (var i = 0; i < maxFailuresToDisplay; i++)
                summaryText += $"\\n- {failureLines[i]}";

            if (failureLines.Count > maxFailuresToDisplay)
                summaryText += $"\\n- ... and {failureLines.Count - maxFailuresToDisplay} more";

            return summaryText;
        }

        private sealed class BatchExecutionRequest
        {
            public string OperationName { get; set; } = string.Empty;
            public IReadOnlyList<string> TargetPaths { get; set; } = Array.Empty<string>();
            public bool IncludeDonor { get; set; }
            public bool ShowProgressBar { get; set; }
            public string ProgressTitle { get; set; } = string.Empty;
            public string ProgressItemLabel { get; set; } = string.Empty;
            public Func<string, OperationResult> ExecuteTarget { get; set; }
            public Action<int, int, string, OperationResult> LogSuccess { get; set; }
            public Action<int, int, string, string, OperationResult> LogFailure { get; set; }
        }

        private sealed class BatchExecutionSummary
        {
            public string OperationName { get; set; } = string.Empty;
            public bool IncludeDonor { get; set; }
            public int TargetCount { get; set; }
            public int SuccessCount { get; set; }
            public int FailedCount { get; set; }
            public int RepairedPrefabCount { get; set; }
            public int RepairedPartCount { get; set; }
            public IReadOnlyList<string> FailureLines { get; set; } = Array.Empty<string>();
        }
    }
}
#endif
