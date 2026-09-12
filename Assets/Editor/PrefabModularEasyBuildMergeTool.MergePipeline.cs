#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    public static partial class PrefabModularEasyBuildMergeTool
    {
        private static OperationResult MergeEasyBuildScripts(string donorPath, string targetPath, bool overwriteExistingScripts)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(donorPath) == null)
                return OperationResult.Fail($"Donor prefab not found: {donorPath}");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) == null)
                return OperationResult.Fail($"Target prefab not found: {targetPath}");

            using var loadedPrefabs = new PrefabContentsPairScope(donorPath, targetPath);

            try
            {
                if (!loadedPrefabs.IsValid)
                    return OperationResult.Fail("Could not load donor/target prefab contents.");

                var mergeContext = new MergeContext(
                    donorPath,
                    targetPath,
                    overwriteExistingScripts,
                    loadedPrefabs.DonorRoot,
                    loadedPrefabs.TargetRoot);

                MergeComponentsByMatchingTransformPath(mergeContext);

                var repairStats = RepairEasyBuildPartsInPrefabRoot(mergeContext.TargetRoot);
                PrefabUtility.SaveAsPrefabAsset(mergeContext.TargetRoot, targetPath);

                return OperationResult.Ok(BuildMergeSummary(mergeContext, repairStats), repairStats.RepairedPartCount);
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Exception during merge: {ex}");
            }
        }

        private static string BuildMergeSummary(MergeContext context, RepairStats repairStats)
        {
            return
                $"Merged EasyBuild scripts from donor into target.\n" +
                $"Donor: {context.DonorPath}\n" +
                $"Target: {context.TargetPath}\n" +
                $"Added scripts: {context.AddedScriptCount}\n" +
                $"Skipped existing scripts: {context.SkippedExistingScriptCount}\n" +
                $"Skipped non-EasyBuild components: {context.SkippedNonEasyBuildComponentCount}\n" +
                $"Missing matching transform paths: {context.SkippedMissingTransformPathCount}\n" +
                $"EasyBuild parts scanned: {repairStats.PartCount}\n" +
                $"EasyBuild parts repaired: {repairStats.RepairedPartCount}\n" +
                $"Overwrite existing scripts: {context.OverwriteExistingScripts}";
        }

        private static void MergeComponentsByMatchingTransformPath(MergeContext context)
        {
            var donorTransforms = context.DonorRoot.GetComponentsInChildren<Transform>(true);

            for (var i = 0; i < donorTransforms.Length; i++)
            {
                var donorTransform = donorTransforms[i];
                if (!TryResolveMatchingTargetTransform(context, donorTransform, out var targetTransform))
                {
                    context.SkippedMissingTransformPathCount++;
                    continue;
                }

                MergeComponentsOnTransform(context, donorTransform, targetTransform);
            }
        }

        private static bool TryResolveMatchingTargetTransform(MergeContext context, Transform donorTransform,
            out Transform targetTransform)
        {
            var path = TransformReferenceRemapUtility.GetTransformPath(context.DonorRoot.transform, donorTransform);
            return context.TargetByPath.TryGetValue(path, out targetTransform);
        }

        private static void MergeComponentsOnTransform(MergeContext context, Transform donorTransform,
            Transform targetTransform)
        {
            var donorComponents = donorTransform.GetComponents<Component>();
            for (var i = 0; i < donorComponents.Length; i++)
            {
                var donorComponent = donorComponents[i];

                if (!ShouldMergeComponent(donorComponent))
                {
                    context.SkippedNonEasyBuildComponentCount++;
                    continue;
                }

                MergeComponent(context, donorComponent, targetTransform);
            }
        }

        private static void MergeComponent(MergeContext context, Component donorComponent, Transform targetTransform)
        {
            var type = donorComponent.GetType();
            var existingTargetComponent =
                TransformReferenceRemapUtility.ResolveMatchingComponentByTypeAndOrder(donorComponent, targetTransform);

            if (existingTargetComponent != null && !context.OverwriteExistingScripts)
            {
                context.SkippedExistingScriptCount++;
                context.ComponentMap[donorComponent] = existingTargetComponent;
                return;
            }

            Component targetComponent;
            if (existingTargetComponent != null)
            {
                targetComponent = existingTargetComponent;
            }
            else
            {
                targetComponent = targetTransform.gameObject.AddComponent(type);
                if (targetComponent == null)
                    return;

                context.AddedScriptCount++;
            }

            context.ComponentMap[donorComponent] = targetComponent;

            TransformReferenceRemapUtility.CopySerializedAndRemapReferences(
                donorComponent,
                targetComponent,
                context.DonorRoot.transform,
                context.TargetByPath,
                context.ComponentMap);
        }

        private static bool ShouldMergeComponent(Component component)
        {
            if (component == null || component is Transform)
                return false;

            return IsMindCodeInteractiveComponent(component);
        }

        private static bool IsMindCodeInteractiveComponent(Component component)
        {
            if (component is not MonoBehaviour monoBehaviour)
                return false;

            var script = MonoScript.FromMonoBehaviour(monoBehaviour);
            if (script == null)
                return false;

            var scriptPath = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrWhiteSpace(scriptPath))
                return false;

            return scriptPath.IndexOf("/ThirdParty/Mind Code Interactive/", StringComparison.OrdinalIgnoreCase) >= 0
                   || scriptPath.IndexOf("/Mind Code Interactive/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class MergeContext
        {
            public string DonorPath { get; }
            public string TargetPath { get; }
            public bool OverwriteExistingScripts { get; }
            public GameObject DonorRoot { get; }
            public GameObject TargetRoot { get; }
            public Dictionary<string, Transform> TargetByPath { get; }
            public Dictionary<Component, Component> ComponentMap { get; } = new();

            public int AddedScriptCount;
            public int SkippedExistingScriptCount;
            public int SkippedMissingTransformPathCount;
            public int SkippedNonEasyBuildComponentCount;

            public MergeContext(string donorPath, string targetPath, bool overwriteExistingScripts, GameObject donorRoot,
                GameObject targetRoot)
            {
                DonorPath = donorPath;
                TargetPath = targetPath;
                OverwriteExistingScripts = overwriteExistingScripts;
                DonorRoot = donorRoot;
                TargetRoot = targetRoot;
                TargetByPath = TransformReferenceRemapUtility.BuildTransformPathMap(targetRoot.transform);
            }
        }

        private sealed class PrefabContentsPairScope : IDisposable
        {
            public GameObject DonorRoot { get; }
            public GameObject TargetRoot { get; }

            public bool IsValid => DonorRoot != null && TargetRoot != null;

            public PrefabContentsPairScope(string donorPath, string targetPath)
            {
                DonorRoot = PrefabUtility.LoadPrefabContents(donorPath);
                TargetRoot = PrefabUtility.LoadPrefabContents(targetPath);
            }

            public void Dispose()
            {
                if (DonorRoot != null)
                    PrefabUtility.UnloadPrefabContents(DonorRoot);

                if (TargetRoot != null)
                    PrefabUtility.UnloadPrefabContents(TargetRoot);
            }
        }

        private readonly struct OperationResult
        {
            public bool Success { get; }
            public string Summary { get; }
            public int RepairedPartCount { get; }

            private OperationResult(bool success, string summary, int repairedPartCount)
            {
                Success = success;
                Summary = summary;
                RepairedPartCount = Mathf.Max(0, repairedPartCount);
            }

            public static OperationResult Ok(string summary, int repairedPartCount)
            {
                return new OperationResult(true, summary, repairedPartCount);
            }

            public static OperationResult Fail(string summary)
            {
                return new OperationResult(false, summary, 0);
            }
        }

        private readonly struct RepairStats
        {
            public int PartCount { get; }
            public int RepairedPartCount { get; }

            public RepairStats(int partCount, int repairedPartCount)
            {
                PartCount = Mathf.Max(0, partCount);
                RepairedPartCount = Mathf.Max(0, repairedPartCount);
            }
        }
    }
}
#endif
