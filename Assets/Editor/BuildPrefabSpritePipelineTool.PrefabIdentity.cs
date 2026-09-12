#region

using System;
using UnityEngine;

#endregion

namespace Zombera.Editor
{
    public static partial class BuildPrefabSpritePipelineTool
    {
        private static string ResolvePartReference(GameObject prefab)
        {
            if (prefab == null) return "BuildPrefab";

            if (TryExtractPrefabIdFromPrefab(prefab, out var prefabId) && !string.IsNullOrWhiteSpace(prefabId))
                return prefabId.Trim();

            // Fallback keeps icon generation usable even for prefabs without explicit part IDs.
            return prefab.name?.Trim() ?? "BuildPrefab";
        }

        private static bool TryExtractPrefabIdFromPrefab(GameObject prefab, out string prefabId)
        {
            prefabId = null;
            if (prefab == null) return false;

            var components = prefab.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null) continue;

                var type = component.GetType();
                var property = type.GetProperty("PrefabId",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (property != null && property.PropertyType == typeof(string))
                {
                    var value = property.GetValue(component) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        prefabId = value;
                        return true;
                    }
                }

                var field = type.GetField("m_prefabId",
                                System.Reflection.BindingFlags.Public |
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Instance)
                            ?? type.GetField("prefabId",
                                System.Reflection.BindingFlags.Public |
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Instance);

                if (field == null || field.FieldType != typeof(string)) continue;

                var fieldValue = field.GetValue(component) as string;
                if (string.IsNullOrWhiteSpace(fieldValue)) continue;

                prefabId = fieldValue;
                return true;
            }

            return false;
        }
    }
}
