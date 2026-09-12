#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    public static partial class PrefabModularEasyBuildMergeTool
    {
        private static OperationResult RepairEasyBuildBindingsInPrefab(string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                return OperationResult.Fail($"Prefab not found: {prefabPath}");

            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                if (prefabRoot == null)
                    return OperationResult.Fail($"Could not load prefab contents: {prefabPath}");

                var repairStats = RepairEasyBuildPartsInPrefabRoot(prefabRoot);
                if (repairStats.RepairedPartCount > 0)
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                return OperationResult.Ok(
                    $"EasyBuild repair scanned parts: {repairStats.PartCount}, repaired: {repairStats.RepairedPartCount}.",
                    repairStats.RepairedPartCount);
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Exception during repair: {ex.Message}");
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static RepairStats RepairEasyBuildPartsInPrefabRoot(GameObject prefabRoot)
        {
            if (prefabRoot == null)
                return new RepairStats(0, 0);

            var partCount = 0;
            var repairedPartCount = 0;

            var monoBehaviours = prefabRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < monoBehaviours.Length; i++)
            {
                var monoBehaviour = monoBehaviours[i];
                if (!IsLikelyEasyBuildPart(monoBehaviour))
                    continue;

                partCount++;
                if (RepairEasyBuildPart(monoBehaviour))
                    repairedPartCount++;
            }

            return new RepairStats(partCount, repairedPartCount);
        }

        private static bool IsLikelyEasyBuildPart(MonoBehaviour monoBehaviour)
        {
            if (monoBehaviour == null || !IsMindCodeInteractiveComponent(monoBehaviour))
                return false;

            var serializedObject = new SerializedObject(monoBehaviour);
            return serializedObject.FindProperty(SerializedPropertyNames.PrefabId) != null
                   && serializedObject.FindProperty(SerializedPropertyNames.RendererSystem) != null
                   && serializedObject.FindProperty(SerializedPropertyNames.PlacementSystem) != null;
        }

        private static bool RepairEasyBuildPart(MonoBehaviour partComponent)
        {
            if (partComponent == null)
                return false;

            var context = CreateRepairContext(partComponent);

            var changed = false;
            changed |= RepairRendererAndVariantSync(context);
            changed |= RepairPartDisplayNameAndCategory(context.PartSerialized, context.PartComponent.gameObject.name);
            changed |= RepairBehaviorAndConditionRelinking(context);
            changed |= CleanupPartCaches(context.PartSerialized);

            if (!changed)
                return false;

            context.PartSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(context.PartComponent);
            return true;
        }

        private static RepairContext CreateRepairContext(MonoBehaviour partComponent)
        {
            var partTransform = partComponent.transform;
            var visualRoot = FindBestVisualRoot(partTransform);

            var renderers = visualRoot != null
                ? visualRoot.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();

            var colliders = partTransform.GetComponentsInChildren<Collider>(true);
            var meshFilters = visualRoot != null
                ? visualRoot.GetComponentsInChildren<MeshFilter>(true)
                : Array.Empty<MeshFilter>();

            return new RepairContext(
                partComponent,
                new SerializedObject(partComponent),
                partTransform,
                visualRoot,
                renderers,
                colliders,
                meshFilters);
        }

        private static bool RepairRendererAndVariantSync(RepairContext context)
        {
            var changed = false;

            var rendererSystem = context.PartSerialized.FindProperty(SerializedPropertyNames.RendererSystem);
            var variants = rendererSystem?.FindPropertyRelative(SerializedPropertyNames.RendererSystemVariants);
            if (variants == null || !variants.isArray)
                return false;

            if (variants.arraySize <= 0)
            {
                variants.InsertArrayElementAtIndex(0);
                changed = true;
            }

            for (var i = 0; i < variants.arraySize; i++)
            {
                var variant = variants.GetArrayElementAtIndex(i);
                changed |= SerializedPropertyMutationUtility.SetObjectReference(
                    variant.FindPropertyRelative(SerializedPropertyNames.VariantRoot),
                    context.VisualRoot);
                changed |= SerializedPropertyMutationUtility.SetObjectArray(
                    variant.FindPropertyRelative(SerializedPropertyNames.VariantRenderers),
                    context.Renderers);
                changed |= SerializedPropertyMutationUtility.SetObjectArray(
                    variant.FindPropertyRelative(SerializedPropertyNames.VariantColliders),
                    context.Colliders);
                changed |= SerializedPropertyMutationUtility.SetObjectArray(
                    variant.FindPropertyRelative(SerializedPropertyNames.VariantMeshFilters),
                    context.MeshFilters);
                changed |= SerializedPropertyMutationUtility.RemoveNullObjectArrayEntries(
                    variant.FindPropertyRelative(SerializedPropertyNames.VariantLodGroups));
                changed |= SerializedPropertyMutationUtility.RebuildCachedBounds(
                    variant.FindPropertyRelative(SerializedPropertyNames.VariantCachedBounds),
                    context.Renderers,
                    context.PartTransform);
                changed |= SerializedPropertyMutationUtility.SyncMaterialVariants(
                    variant.FindPropertyRelative(SerializedPropertyNames.VariantMaterialVariants),
                    context.Renderers);
            }

            var activeIndex = rendererSystem.FindPropertyRelative(SerializedPropertyNames.RendererSystemActiveIndex);
            if (activeIndex == null || variants.arraySize <= 0)
                return changed;

            var clampedActiveIndex = Mathf.Clamp(activeIndex.intValue, 0, variants.arraySize - 1);
            if (activeIndex.intValue != clampedActiveIndex)
            {
                activeIndex.intValue = clampedActiveIndex;
                changed = true;
            }

            return changed;
        }

        private static bool RepairBehaviorAndConditionRelinking(RepairContext context)
        {
            var changed = false;
            var siblingComponents = context.PartComponent.GetComponents<MonoBehaviour>();
            var behaviorComponents = new List<MonoBehaviour>();
            var conditionComponents = new List<MonoBehaviour>();

            for (var i = 0; i < siblingComponents.Length; i++)
            {
                var sibling = siblingComponents[i];
                if (sibling == null || sibling == context.PartComponent || !IsMindCodeInteractiveComponent(sibling))
                    continue;

                var siblingSerialized = new SerializedObject(sibling);
                var partReference = siblingSerialized.FindProperty(SerializedPropertyNames.PartReference);
                if (partReference != null && partReference.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (partReference.objectReferenceValue != context.PartComponent)
                    {
                        partReference.objectReferenceValue = context.PartComponent;
                        siblingSerialized.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(sibling);
                        changed = true;
                    }

                    behaviorComponents.Add(sibling);
                    continue;
                }

                if (siblingSerialized.FindProperty(SerializedPropertyNames.ConditionDisabledFlag) != null)
                    conditionComponents.Add(sibling);
            }

            changed |= SerializedPropertyMutationUtility.SetObjectArray(
                context.PartSerialized.FindProperty(SerializedPropertyNames.BehaviorSystem)
                    ?.FindPropertyRelative(SerializedPropertyNames.BehaviorList),
                behaviorComponents);
            changed |= SerializedPropertyMutationUtility.SetObjectArray(
                context.PartSerialized.FindProperty(SerializedPropertyNames.ConditionSystem)
                    ?.FindPropertyRelative(SerializedPropertyNames.ConditionList),
                conditionComponents);

            return changed;
        }

        private static bool CleanupPartCaches(SerializedObject partSerialized)
        {
            return SerializedPropertyMutationUtility.RemoveNullObjectArrayEntries(
                partSerialized.FindProperty(SerializedPropertyNames.CacheSystem)
                    ?.FindPropertyRelative(SerializedPropertyNames.CachedSockets));
        }

        private static bool RepairPartDisplayNameAndCategory(SerializedObject partSerialized, string fallbackName)
        {
            if (partSerialized == null)
                return false;

            var changed = false;
            var cleanName = string.IsNullOrWhiteSpace(fallbackName) ? "Build Part" : fallbackName.Trim();
            var isFoundationPrefabName =
                cleanName.IndexOf("foundation", StringComparison.OrdinalIgnoreCase) >= 0;

            var displayName = partSerialized.FindProperty(SerializedPropertyNames.DisplayName);
            if (displayName != null)
            {
                var currentDisplayName = displayName.stringValue?.Trim();
                var needsDisplayNameRepair = string.IsNullOrWhiteSpace(currentDisplayName)
                                             || (!isFoundationPrefabName
                                                 && currentDisplayName.IndexOf("foundation",
                                                     StringComparison.OrdinalIgnoreCase) >= 0);

                if (needsDisplayNameRepair)
                {
                    displayName.stringValue = cleanName;
                    changed = true;
                }
            }

            var category = partSerialized.FindProperty(SerializedPropertyNames.Category);
            if (category != null && category.propertyType == SerializedPropertyType.Enum && !isFoundationPrefabName)
            {
                var currentEnumName = SerializedPropertyMutationUtility.GetEnumName(category);
                if (string.Equals(currentEnumName, SerializedPropertyNames.CategoryFoundation,
                        StringComparison.OrdinalIgnoreCase))
                {
                    changed |= SerializedPropertyMutationUtility.TrySetEnumByName(
                        category,
                        SerializedPropertyNames.CategoryDefault);
                }
            }

            return changed;
        }

        private sealed class RepairContext
        {
            public MonoBehaviour PartComponent { get; }
            public SerializedObject PartSerialized { get; }
            public Transform PartTransform { get; }
            public Transform VisualRoot { get; }
            public Renderer[] Renderers { get; }
            public Collider[] Colliders { get; }
            public MeshFilter[] MeshFilters { get; }

            public RepairContext(
                MonoBehaviour partComponent,
                SerializedObject partSerialized,
                Transform partTransform,
                Transform visualRoot,
                Renderer[] renderers,
                Collider[] colliders,
                MeshFilter[] meshFilters)
            {
                PartComponent = partComponent;
                PartSerialized = partSerialized;
                PartTransform = partTransform;
                VisualRoot = visualRoot;
                Renderers = renderers;
                Colliders = colliders;
                MeshFilters = meshFilters;
            }
        }
    }
}
#endif
