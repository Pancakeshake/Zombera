using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeCatalogHelper
    {
#if UNITY_EDITOR
        internal static int ComputeExtensionCatalogSignature(IReadOnlyList<string> extensionCatalogFolders)
        {
            unchecked
            {
                var hash = 17;

                if (extensionCatalogFolders == null)
                    return hash;

                for (var i = 0; i < extensionCatalogFolders.Count; i++)
                {
                    var folder = extensionCatalogFolders[i];
                    if (string.IsNullOrWhiteSpace(folder))
                    {
                        hash *= 31;
                        continue;
                    }

                    hash = (hash * 31) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(folder.Trim());
                }

                return hash;
            }
        }

        internal static int ResolveManagerPartCount(object manager)
        {
            if (manager == null)
                return -1;

            var partCountRaw = EasyBuildBridgeReflectionHelper.InvokeMethodWithReturn(manager, "GetPartCount");
            return partCountRaw is int i ? Mathf.Max(0, i) : 0;
        }

        internal static void PopulateFilteredCatalogFromFolderIds(
            IEnumerable<string> folderPrefabIds,
            ISet<string> filteredCatalogPartReferenceSet,
            ICollection<string> filteredCatalogPartReferences)
        {
            foreach (var id in folderPrefabIds)
            {
                if (!filteredCatalogPartReferenceSet.Add(id)) continue;
                filteredCatalogPartReferences.Add(id);
            }
        }

        internal static void PopulateFilteredCatalogFromManager(
            object manager,
            int partCount,
            ISet<string> folderPrefabIds,
            ISet<string> filteredCatalogPartReferenceSet,
            ICollection<string> filteredCatalogPartReferences)
        {
            for (var index = 0; index < partCount; index++)
            {
                var part = EasyBuildBridgeReflectionHelper.InvokeMethodWithReturn(manager, "GetPartByIndex", index);
                var partId = EasyBuildBridgePartDataHelper.ResolvePartIdentifier(part);
                if (string.IsNullOrWhiteSpace(partId) || !folderPrefabIds.Contains(partId)) continue;
                if (!filteredCatalogPartReferenceSet.Add(partId)) continue;

                filteredCatalogPartReferences.Add(partId);
            }
        }

        internal static HashSet<string> CollectExtensionFolderPrefabIds(
            IReadOnlyList<string> extensionCatalogFolders,
            Dictionary<string, string> prefabNamesByPartReference)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (extensionCatalogFolders == null || extensionCatalogFolders.Count == 0)
                return ids;

            for (var i = 0; i < extensionCatalogFolders.Count; i++)
                CollectPrefabIdsFromFolder(extensionCatalogFolders[i], ids, prefabNamesByPartReference);

            return ids;
        }

        private static void CollectPrefabIdsFromFolder(string folder, ISet<string> ids,
            Dictionary<string, string> prefabNamesByPartReference)
        {
            if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder)) return;

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (var g = 0; g < guids.Length; g++)
                CollectPrefabIdFromGuid(guids[g], ids, prefabNamesByPartReference);
        }

        private static void CollectPrefabIdFromGuid(string guid, ISet<string> ids,
            Dictionary<string, string> prefabNamesByPartReference)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path)) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            if (!TryExtractPrefabIdFromPrefab(prefab, out var prefabId)) return;
            ids.Add(prefabId);

            TryCachePrefabDisplayName(prefabNamesByPartReference, prefabId, prefab, path);
        }

        private static void TryCachePrefabDisplayName(Dictionary<string, string> prefabNamesByPartReference,
            string prefabId, GameObject prefab, string path)
        {
            if (prefabNamesByPartReference == null || prefabNamesByPartReference.ContainsKey(prefabId)) return;

            var displayName = EasyBuildBridgePartDataHelper.NormalizePrefabDisplayName(prefab.name);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = EasyBuildBridgePartDataHelper.NormalizePrefabDisplayName(
                    System.IO.Path.GetFileNameWithoutExtension(path));

            if (!string.IsNullOrWhiteSpace(displayName))
                prefabNamesByPartReference[prefabId] = displayName;
        }

        private static bool TryExtractPrefabIdFromPrefab(GameObject prefab, out string prefabId)
        {
            prefabId = null;
            if (prefab == null) return false;

            var components = prefab.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                if (TryExtractPrefabIdFromComponent(components[i], out prefabId))
                    return true;
            }

            return false;
        }

        private static bool TryExtractPrefabIdFromComponent(Component component, out string prefabId)
        {
            prefabId = null;
            if (component == null) return false;

            var type = component.GetType();

            return TryReadStringProperty(type, component, "PrefabId", out prefabId)
                   || TryReadStringField(type, component, out prefabId, "m_prefabId", "prefabId");
        }

        private static bool TryReadStringProperty(Type type, object instance, string propertyName,
            out string value)
        {
            value = null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var property = type.GetProperty(propertyName, flags);
            if (property == null || property.PropertyType != typeof(string)) return false;

            var rawValue = property.GetValue(instance) as string;
            if (string.IsNullOrWhiteSpace(rawValue)) return false;

            value = rawValue;
            return true;
        }

        private static bool TryReadStringField(Type type, object instance, out string value,
            params string[] fieldNames)
        {
            value = null;
            if (fieldNames == null || fieldNames.Length == 0) return false;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            for (var i = 0; i < fieldNames.Length; i++)
            {
                var field = type.GetField(fieldNames[i], flags);
                if (field == null || field.FieldType != typeof(string)) continue;

                var rawValue = field.GetValue(instance) as string;
                if (string.IsNullOrWhiteSpace(rawValue)) continue;

                value = rawValue;
                return true;
            }

            return false;
        }
#endif
    }
}
