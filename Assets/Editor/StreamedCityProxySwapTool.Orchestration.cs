#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    public static partial class StreamedCityProxySwapTool
    {
        private static void ExecuteProxyBuildRun()
        {
            var context = new StreamedCityProxySwapRunContext();
            if (!TryPrepareRunContext(context))
                return;

            EnsureFolderHierarchy(DefaultProxyPrefabFolder);

            RunProxyBuildStage(context);
            RunOrphanCleanupStage(context);
            context.Summary.CatalogWiredEntries =
                WireCatalogToGeneratedProxies(context.Catalog, context.ProxyBySourcePath);
            context.Summary.SceneBuildersUpdated = EnableProxySwapOnLoadedSceneBuilders();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = BuildRunSummaryText(context);
            Debug.Log("[StreamedCityProxySwapTool] " + summary, context.Catalog);
            EditorUtility.DisplayDialog("Streamed City Proxy Swap", summary, "OK");

            if (context.Catalog != null)
                Selection.activeObject = context.Catalog;
        }

        private static bool TryPrepareRunContext(StreamedCityProxySwapRunContext context)
        {
            if (context == null)
                return false;

            if (!AssetDatabase.IsValidFolder(DefaultSourcePrefabFolder))
            {
                EditorUtility.DisplayDialog(
                    "Streamed City Proxy Swap",
                    "Source prefab folder was not found: " + DefaultSourcePrefabFolder,
                    "OK");
                return false;
            }

            context.SourcePrefabs.AddRange(CollectSourcePrefabs());
            if (context.SourcePrefabs.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Streamed City Proxy Swap",
                    "No source prefabs were found under " + DefaultSourcePrefabFolder + ".",
                    "OK");
                return false;
            }

            context.Catalog = ResolveTargetCatalog();

            var preflight = RunPreflightValidation(context);
            if (!preflight.IsValid)
            {
                EditorUtility.DisplayDialog(
                    "Streamed City Proxy Swap",
                    BuildPreflightFailureMessage(preflight),
                    "OK");
                return false;
            }

            if (preflight.Warnings.Count > 0)
                Debug.LogWarning("[StreamedCityProxySwapTool] " + BuildPreflightWarningMessage(preflight));

            return true;
        }

        private static StreamedCityProxySwapPreflightResult RunPreflightValidation(StreamedCityProxySwapRunContext context)
        {
            var result = new StreamedCityProxySwapPreflightResult();
            if (context == null)
            {
                result.Errors.Add("Run context was null.");
                return result;
            }

            if (!AssetDatabase.IsValidFolder(DefaultSourcePrefabFolder))
                result.Errors.Add("Source prefab folder does not exist: " + DefaultSourcePrefabFolder);

            ValidateProxyFolderWritable(result);
            ValidateCatalogReadable(context.Catalog, result);
            ValidateOutputAssetPaths(context.SourcePrefabs, result);

            return result;
        }

        private static void ValidateProxyFolderWritable(StreamedCityProxySwapPreflightResult result)
        {
            if (result == null)
                return;

            if (!TryConvertAssetPathToFileSystemPath(DefaultProxyPrefabFolder, out var proxyDirectoryPath))
            {
                result.Errors.Add("Proxy folder path is invalid: " + DefaultProxyPrefabFolder);
                return;
            }

            if (!TryGetNearestExistingDirectory(proxyDirectoryPath, out var existingDirectoryPath))
            {
                result.Errors.Add(
                    "Proxy folder parent could not be resolved on disk: " + DefaultProxyPrefabFolder);
                return;
            }

            if (TryGetDirectoryWriteFailure(existingDirectoryPath, out var writeFailure))
                result.Errors.Add(writeFailure);
        }

        private static void ValidateCatalogReadable(StreamedCityCatalog catalog, StreamedCityProxySwapPreflightResult result)
        {
            if (result == null)
                return;

            if (catalog == null)
            {
                result.Warnings.Add("Catalog not found; proxies will still be generated.");
                return;
            }

            var catalogPath = AssetDatabase.GetAssetPath(catalog);
            if (string.IsNullOrWhiteSpace(catalogPath))
            {
                result.Errors.Add("Catalog reference exists but asset path could not be resolved.");
                return;
            }

            if (!catalogPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                result.Warnings.Add("Catalog asset path is outside Assets/: " + catalogPath);
        }

        private static void ValidateOutputAssetPaths(
            IReadOnlyList<GameObject> sourcePrefabs,
            StreamedCityProxySwapPreflightResult result)
        {
            if (result == null || sourcePrefabs == null || sourcePrefabs.Count == 0)
                return;

            var outputPrefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var outputMeshPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < sourcePrefabs.Count; i++)
            {
                var sourcePrefab = sourcePrefabs[i];
                var sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);
                var outputPath = BuildProxyAssetPath(sourcePrefab, sourcePath);
                var outputMeshAssetPath = BuildProxyMeshAssetPath(outputPath);

                if (!IsValidOutputPrefabPath(outputPath))
                    result.Errors.Add("Invalid proxy prefab output path for source '" + sourcePath + "': " + outputPath);

                if (!IsValidOutputMeshAssetPath(outputMeshAssetPath))
                    result.Errors.Add(
                        "Invalid proxy mesh output path for source '" + sourcePath + "': " + outputMeshAssetPath);

                if (!outputPrefabPaths.Add(outputPath))
                    result.Errors.Add("Duplicate proxy prefab output path detected: " + outputPath);

                if (!outputMeshPaths.Add(outputMeshAssetPath))
                    result.Errors.Add("Duplicate proxy mesh output path detected: " + outputMeshAssetPath);

                if (result.Errors.Count >= 25)
                {
                    result.Errors.Add("Preflight stopped after 25 errors.");
                    return;
                }
            }
        }

        private static bool IsValidOutputPrefabPath(string outputPath)
        {
            return !string.IsNullOrWhiteSpace(outputPath)
                   && outputPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                   && IsAssetPathInsideFolder(outputPath, DefaultProxyPrefabFolder);
        }

        private static bool IsValidOutputMeshAssetPath(string outputMeshAssetPath)
        {
            return !string.IsNullOrWhiteSpace(outputMeshAssetPath)
                   && outputMeshAssetPath.EndsWith(ProxyMeshAssetSuffix, StringComparison.OrdinalIgnoreCase)
                   && outputMeshAssetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                   && IsAssetPathInsideFolder(outputMeshAssetPath, DefaultProxyPrefabFolder);
        }

        private static bool TryGetDirectoryWriteFailure(string directoryPath, out string failure)
        {
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                failure = "Could not validate proxy folder writability because directory path was empty.";
                return true;
            }

            try
            {
                var attributes = File.GetAttributes(directoryPath);
                if ((attributes & FileAttributes.ReadOnly) == 0)
                    return false;

                failure = "Proxy output parent folder is read-only: " + directoryPath;
                return true;
            }
            catch (Exception exception)
            {
                failure = "Failed to validate proxy folder writability for '" + directoryPath + "': " + exception.Message;
                return true;
            }
        }

        private static void RunProxyBuildStage(StreamedCityProxySwapRunContext context)
        {
            if (context?.SourcePrefabs == null)
                return;

            for (var i = 0; i < context.SourcePrefabs.Count; i++)
            {
                var sourcePrefab = context.SourcePrefabs[i];
                context.Summary.ProcessedSourcePrefabs++;

                var sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);
                var outputPath = BuildProxyAssetPath(sourcePrefab, sourcePath);
                var outputMeshAssetPath = BuildProxyMeshAssetPath(outputPath);
                context.ExpectedProxyPaths.Add(outputPath);
                context.ExpectedProxyMeshAssetPaths.Add(outputMeshAssetPath);

                var matchedEntry = FindFirstCatalogEntryForPrefab(context.Catalog, sourcePrefab);
                if (!TryBuildProxyPrefab(
                        sourcePrefab,
                        matchedEntry,
                        outputPath,
                        outputMeshAssetPath,
                        context.Summary.GpuInstancingEnabledMaterialPaths,
                        out var proxyPrefab,
                        out var failureReason))
                {
                    context.Summary.FailedProxyBuilds++;
                    context.Summary.FailureLines.Add(sourcePath + " -> " + failureReason);
                    continue;
                }

                context.Summary.GeneratedOrUpdatedProxies++;
                context.ProxyBySourcePath[sourcePath] = proxyPrefab;
            }
        }

        private static void RunOrphanCleanupStage(StreamedCityProxySwapRunContext context)
        {
            if (context == null)
                return;

            var orphanProxyPaths = CollectOrphanProxyPrefabPaths(context.ExpectedProxyPaths);
            var orphanMeshAssetPaths = CollectOrphanProxyMeshAssetPaths(context.ExpectedProxyMeshAssetPaths);

            context.Summary.DetectedOrphanProxyPrefabs = orphanProxyPaths.Count;
            context.Summary.DetectedOrphanProxyMeshAssets = orphanMeshAssetPaths.Count;

            if (!ShouldDeleteOrphans(orphanProxyPaths.Count, orphanMeshAssetPaths.Count))
            {
                context.Summary.SkippedOrphanDeletionByConfirmation =
                    orphanProxyPaths.Count + orphanMeshAssetPaths.Count > 0;
                return;
            }

            context.Summary.RemovedOrphanProxyPrefabs = DeleteAssets(orphanProxyPaths);
            context.Summary.RemovedOrphanProxyMeshAssets = DeleteAssets(orphanMeshAssetPaths);
        }

        private static bool ShouldDeleteOrphans(int orphanProxyCount, int orphanMeshAssetCount)
        {
            var total = orphanProxyCount + orphanMeshAssetCount;
            if (total <= 0)
                return false;

            if (total < OrphanDeleteConfirmationThreshold)
                return true;

            var message =
                "Large orphan cleanup detected.\n\n" +
                "Orphan proxy prefabs detected: " + orphanProxyCount + "\n" +
                "Orphan proxy mesh assets detected: " + orphanMeshAssetCount + "\n" +
                "Total orphan assets: " + total + "\n\n" +
                "Delete these orphan assets now?";

            return EditorUtility.DisplayDialog(
                "Streamed City Proxy Swap",
                message,
                "Delete Orphans",
                "Skip Deletion");
        }

        private static string BuildRunSummaryText(StreamedCityProxySwapRunContext context)
        {
            var summary =
                "Proxy build complete.\n\n" +
                "Source folder: " + DefaultSourcePrefabFolder + "\n" +
                "Proxy folder: " + DefaultProxyPrefabFolder + "\n" +
                "Source prefabs processed: " + context.Summary.ProcessedSourcePrefabs + "\n" +
                "Proxies generated/updated: " + context.Summary.GeneratedOrUpdatedProxies + "\n" +
                "Orphan proxy prefabs detected: " + context.Summary.DetectedOrphanProxyPrefabs + "\n" +
                "Orphan proxy mesh assets detected: " + context.Summary.DetectedOrphanProxyMeshAssets + "\n" +
                "Proxies removed (orphaned): " + context.Summary.RemovedOrphanProxyPrefabs + "\n" +
                "Proxy mesh assets removed (orphaned): " + context.Summary.RemovedOrphanProxyMeshAssets + "\n" +
                "Materials set to GPU instancing: " + context.Summary.GpuInstancingEnabledMaterialPaths.Count + "\n" +
                "Proxy build failures: " + context.Summary.FailedProxyBuilds + "\n" +
                "Catalog wired entries: " + context.Summary.CatalogWiredEntries + "\n" +
                "Scene builders updated: " + context.Summary.SceneBuildersUpdated;

            if (context.Summary.SkippedOrphanDeletionByConfirmation)
                summary += "\nOrphan deletion skipped by confirmation: true";

            if (context.Catalog != null)
                summary += "\nCatalog: " + AssetDatabase.GetAssetPath(context.Catalog);
            else
                summary += "\nCatalog: <not found - proxy assets still generated>";

            if (context.Summary.FailureLines.Count > 0)
            {
                var limit = Mathf.Min(10, context.Summary.FailureLines.Count);
                summary += "\n\nFirst failures:";
                for (var i = 0; i < limit; i++)
                    summary += "\n- " + context.Summary.FailureLines[i];

                if (context.Summary.FailureLines.Count > limit)
                    summary += "\n- ... and " + (context.Summary.FailureLines.Count - limit) + " more";
            }

            return summary;
        }

        private static string BuildPreflightFailureMessage(StreamedCityProxySwapPreflightResult preflight)
        {
            if (preflight == null)
                return "Preflight failed for unknown reasons.";

            var message = "Preflight checks failed:\n\n";
            for (var i = 0; i < preflight.Errors.Count; i++)
                message += "- " + preflight.Errors[i] + "\n";

            if (preflight.Warnings.Count > 0)
            {
                message += "\nWarnings:\n";
                for (var i = 0; i < preflight.Warnings.Count; i++)
                    message += "- " + preflight.Warnings[i] + "\n";
            }

            return message.TrimEnd();
        }

        private static string BuildPreflightWarningMessage(StreamedCityProxySwapPreflightResult preflight)
        {
            if (preflight == null || preflight.Warnings.Count == 0)
                return string.Empty;

            var warning = "Preflight warnings:";
            for (var i = 0; i < preflight.Warnings.Count; i++)
                warning += "\n- " + preflight.Warnings[i];

            return warning;
        }
    }
}
#endif
