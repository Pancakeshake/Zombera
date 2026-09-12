#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    public static partial class PrefabModularEasyBuildMergeTool
    {
        private static class SerializedPropertyNames
        {
            public const string PrefabId = "m_prefabId";
            public const string PlacementSystem = "m_placementSystem";
            public const string RendererSystem = "m_rendererSystem";
            public const string RendererSystemVariants = "m_variants";
            public const string RendererSystemActiveIndex = "m_activeIndex";

            public const string VariantRoot = "m_root";
            public const string VariantRenderers = "m_renderers";
            public const string VariantColliders = "m_colliders";
            public const string VariantMeshFilters = "m_meshFilters";
            public const string VariantLodGroups = "m_lodGroups";
            public const string VariantCachedBounds = "m_cachedBounds";
            public const string VariantMaterialVariants = "m_materialVariants";

            public const string CachedBoundsCenter = "m_Center";
            public const string CachedBoundsExtent = "m_Extent";

            public const string MaterialVariantRendererMaterials = "m_rendererMaterials";
            public const string RendererMaterials = "m_materials";

            public const string DisplayName = "name";
            public const string Category = "m_category";
            public const string CategoryFoundation = "Foundation";
            public const string CategoryDefault = "Default";

            public const string PartReference = "m_part";
            public const string ConditionDisabledFlag = "m_isDisabled";
            public const string BehaviorSystem = "m_behaviorSystem";
            public const string BehaviorList = "m_behaviors";
            public const string ConditionSystem = "m_conditionSystem";
            public const string ConditionList = "m_conditions";
            public const string CacheSystem = "m_cacheSystem";
            public const string CachedSockets = "m_cachedSockets";
        }

        private static Transform FindBestVisualRoot(Transform root)
        {
            if (root == null)
                return null;

            Transform best = null;
            var bestRendererCount = -1;

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null)
                    continue;

                var childRendererCount = child.GetComponentsInChildren<Renderer>(true).Length;
                if (childRendererCount <= bestRendererCount)
                    continue;

                bestRendererCount = childRendererCount;
                best = child;
            }

            if (best != null && bestRendererCount > 0)
                return best;

            return root;
        }

        private static class SerializedPropertyMutationUtility
        {
            public static bool SetObjectReference(SerializedProperty property, UnityEngine.Object value)
            {
                if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                    return false;

                if (property.objectReferenceValue == value)
                    return false;

                property.objectReferenceValue = value;
                return true;
            }

            public static bool SetObjectArray<T>(SerializedProperty arrayProperty, IReadOnlyList<T> values)
                where T : UnityEngine.Object
            {
                if (arrayProperty == null || !arrayProperty.isArray)
                    return false;

                var changed = false;
                var targetSize = values?.Count ?? 0;

                if (arrayProperty.arraySize != targetSize)
                {
                    arrayProperty.arraySize = targetSize;
                    changed = true;
                }

                for (var i = 0; i < targetSize; i++)
                {
                    var element = arrayProperty.GetArrayElementAtIndex(i);
                    if (element == null || element.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    var value = values[i];
                    if (element.objectReferenceValue == value)
                        continue;

                    element.objectReferenceValue = value;
                    changed = true;
                }

                return changed;
            }

            public static bool RemoveNullObjectArrayEntries(SerializedProperty arrayProperty)
            {
                if (arrayProperty == null || !arrayProperty.isArray)
                    return false;

                var changed = false;

                for (var i = arrayProperty.arraySize - 1; i >= 0; i--)
                {
                    var element = arrayProperty.GetArrayElementAtIndex(i);
                    if (element == null || element.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    if (element.objectReferenceValue != null)
                        continue;

                    arrayProperty.DeleteArrayElementAtIndex(i);
                    changed = true;
                }

                return changed;
            }

            public static bool RebuildCachedBounds(SerializedProperty cachedBoundsProperty, Renderer[] renderers,
                Transform localSpace)
            {
                if (cachedBoundsProperty == null || !cachedBoundsProperty.isArray)
                    return false;

                var boundsList = new List<Bounds>();

                if (renderers != null)
                {
                    for (var i = 0; i < renderers.Length; i++)
                    {
                        var renderer = renderers[i];
                        if (!TryGetLocalRendererBounds(renderer, localSpace, out var bounds))
                            continue;

                        boundsList.Add(bounds);
                    }
                }

                var changed = false;
                if (cachedBoundsProperty.arraySize != boundsList.Count)
                {
                    cachedBoundsProperty.arraySize = boundsList.Count;
                    changed = true;
                }

                for (var i = 0; i < boundsList.Count; i++)
                {
                    var element = cachedBoundsProperty.GetArrayElementAtIndex(i);
                    if (element == null)
                        continue;

                    changed |= SetVector3(
                        element.FindPropertyRelative(SerializedPropertyNames.CachedBoundsCenter),
                        boundsList[i].center);
                    changed |= SetVector3(
                        element.FindPropertyRelative(SerializedPropertyNames.CachedBoundsExtent),
                        boundsList[i].extents);
                }

                return changed;
            }

            public static bool SyncMaterialVariants(SerializedProperty materialVariantsProperty, Renderer[] renderers)
            {
                if (materialVariantsProperty == null || !materialVariantsProperty.isArray)
                    return false;

                var changed = false;
                if (materialVariantsProperty.arraySize <= 0)
                {
                    materialVariantsProperty.arraySize = 1;
                    changed = true;
                }

                var materialVariant = materialVariantsProperty.GetArrayElementAtIndex(0);
                var rendererMaterials = materialVariant?.FindPropertyRelative(
                    SerializedPropertyNames.MaterialVariantRendererMaterials);
                if (rendererMaterials == null || !rendererMaterials.isArray)
                    return changed;

                var rendererCount = renderers?.Length ?? 0;
                if (rendererMaterials.arraySize != rendererCount)
                {
                    rendererMaterials.arraySize = rendererCount;
                    changed = true;
                }

                for (var i = 0; i < rendererCount; i++)
                {
                    var rendererMaterial = rendererMaterials.GetArrayElementAtIndex(i);
                    var materials = rendererMaterial?.FindPropertyRelative(SerializedPropertyNames.RendererMaterials);
                    if (materials == null || !materials.isArray)
                        continue;

                    var sharedMaterials = renderers[i] != null ? renderers[i].sharedMaterials : Array.Empty<Material>();

                    if (materials.arraySize != sharedMaterials.Length)
                    {
                        materials.arraySize = sharedMaterials.Length;
                        changed = true;
                    }

                    for (var m = 0; m < sharedMaterials.Length; m++)
                    {
                        var materialElement = materials.GetArrayElementAtIndex(m);
                        if (materialElement == null ||
                            materialElement.propertyType != SerializedPropertyType.ObjectReference)
                            continue;

                        if (materialElement.objectReferenceValue == sharedMaterials[m])
                            continue;

                        materialElement.objectReferenceValue = sharedMaterials[m];
                        changed = true;
                    }
                }

                return changed;
            }

            public static bool TrySetEnumByName(SerializedProperty enumProperty, string enumName)
            {
                if (enumProperty == null || enumProperty.propertyType != SerializedPropertyType.Enum
                                         || string.IsNullOrWhiteSpace(enumName))
                    return false;

                var names = enumProperty.enumNames;
                for (var i = 0; i < names.Length; i++)
                {
                    if (!string.Equals(names[i], enumName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (enumProperty.enumValueIndex == i)
                        return false;

                    enumProperty.enumValueIndex = i;
                    return true;
                }

                return false;
            }

            public static string GetEnumName(SerializedProperty enumProperty)
            {
                if (enumProperty == null || enumProperty.propertyType != SerializedPropertyType.Enum)
                    return string.Empty;

                var names = enumProperty.enumNames;
                var index = enumProperty.enumValueIndex;

                if (index < 0 || index >= names.Length)
                    return string.Empty;

                return names[index];
            }

            private static bool SetVector3(SerializedProperty vectorProperty, Vector3 value)
            {
                if (vectorProperty == null || vectorProperty.propertyType != SerializedPropertyType.Vector3)
                    return false;

                if (vectorProperty.vector3Value == value)
                    return false;

                vectorProperty.vector3Value = value;
                return true;
            }

            private static bool TryGetLocalRendererBounds(Renderer renderer, Transform localSpace, out Bounds bounds)
            {
                bounds = default;

                if (renderer == null || localSpace == null)
                    return false;

                var worldBounds = renderer.bounds;
                var corners = GetBoundsCorners(worldBounds);

                var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

                for (var i = 0; i < corners.Length; i++)
                {
                    var local = localSpace.InverseTransformPoint(corners[i]);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }

                bounds = new Bounds((min + max) * 0.5f, max - min);
                return true;
            }

            private static Vector3[] GetBoundsCorners(Bounds bounds)
            {
                var min = bounds.min;
                var max = bounds.max;

                return new[]
                {
                    new Vector3(min.x, min.y, min.z),
                    new Vector3(min.x, min.y, max.z),
                    new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, max.y, max.z),
                    new Vector3(max.x, min.y, min.z),
                    new Vector3(max.x, min.y, max.z),
                    new Vector3(max.x, max.y, min.z),
                    new Vector3(max.x, max.y, max.z)
                };
            }
        }

        private static class TransformReferenceRemapUtility
        {
            public static Dictionary<string, Transform> BuildTransformPathMap(Transform root)
            {
                var map = new Dictionary<string, Transform>(StringComparer.Ordinal);
                var transforms = root.GetComponentsInChildren<Transform>(true);

                for (var i = 0; i < transforms.Length; i++)
                {
                    var tr = transforms[i];
                    map[GetTransformPath(root, tr)] = tr;
                }

                return map;
            }

            public static string GetTransformPath(Transform root, Transform transform)
            {
                if (transform == root)
                    return string.Empty;

                var stack = new Stack<string>();
                var cursor = transform;

                while (cursor != null && cursor != root)
                {
                    stack.Push(cursor.name);
                    cursor = cursor.parent;
                }

                return string.Join("/", stack);
            }

            public static Component ResolveMatchingComponentByTypeAndOrder(Component donorComponent,
                Transform targetTransform)
            {
                if (donorComponent == null || targetTransform == null)
                    return null;

                var type = donorComponent.GetType();
                var donorComponents = donorComponent.GetComponents(type);
                var targetComponents = targetTransform.GetComponents(type);
                if (targetComponents == null || targetComponents.Length == 0)
                    return null;

                var donorIndex = 0;
                for (var i = 0; i < donorComponents.Length; i++)
                {
                    if (!ReferenceEquals(donorComponents[i], donorComponent))
                        continue;

                    donorIndex = i;
                    break;
                }

                if (donorIndex < targetComponents.Length)
                    return targetComponents[donorIndex];

                return targetComponents[0];
            }

            public static void CopySerializedAndRemapReferences(
                Component donorComponent,
                Component targetComponent,
                Transform donorRoot,
                IReadOnlyDictionary<string, Transform> targetByPath,
                IReadOnlyDictionary<Component, Component> componentMap)
            {
                if (donorComponent == null || targetComponent == null)
                    return;

                EditorUtility.CopySerialized(donorComponent, targetComponent);

                var serializedObject = new SerializedObject(targetComponent);
                var iterator = serializedObject.GetIterator();
                var enterChildren = true;

                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    var reference = iterator.objectReferenceValue;
                    if (reference == null)
                        continue;

                    if (AssetDatabase.Contains(reference))
                        continue;

                    if (!TryRemapObjectReference(reference, donorRoot, targetByPath, componentMap,
                            out var remappedReference))
                        continue;

                    iterator.objectReferenceValue = remappedReference;
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(targetComponent);
            }

            private static bool TryRemapObjectReference(
                UnityEngine.Object reference,
                Transform donorRoot,
                IReadOnlyDictionary<string, Transform> targetByPath,
                IReadOnlyDictionary<Component, Component> componentMap,
                out UnityEngine.Object remappedReference)
            {
                remappedReference = null;

                switch (reference)
                {
                    case Component donorComponent:
                    {
                        if (componentMap.TryGetValue(donorComponent, out var mappedComponent))
                        {
                            remappedReference = mappedComponent;
                            return true;
                        }

                        if (!donorComponent.transform.IsChildOf(donorRoot))
                            return false;

                        var path = GetTransformPath(donorRoot, donorComponent.transform);
                        if (!targetByPath.TryGetValue(path, out var targetTransform))
                            return false;

                        if (donorComponent is Transform)
                        {
                            remappedReference = targetTransform;
                            return true;
                        }

                        remappedReference = ResolveMatchingComponentByTypeAndOrder(donorComponent, targetTransform);
                        return remappedReference != null;
                    }
                    case GameObject donorGameObject:
                    {
                        if (!donorGameObject.transform.IsChildOf(donorRoot))
                            return false;

                        var path = GetTransformPath(donorRoot, donorGameObject.transform);
                        if (!targetByPath.TryGetValue(path, out var targetTransform))
                            return false;

                        remappedReference = targetTransform.gameObject;
                        return true;
                    }
                    default:
                        return false;
                }
            }
        }
    }
}
#endif
