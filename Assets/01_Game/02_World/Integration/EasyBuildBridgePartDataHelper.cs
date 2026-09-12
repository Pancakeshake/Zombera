using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgePartDataHelper
    {
        internal static string ResolvePartIdentifier(object part)
        {
            if (part == null) return null;

            return EasyBuildBridgeReflectionHelper.GetMemberValue(part, "PrefabId") as string
                   ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "PartReference") as string
                   ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "ID") as string
                   ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "Name") as string;
        }

        internal static bool IsGenericPartDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;

            var normalized = value.Trim().ToLowerInvariant();
            return normalized is "part" or "building part" or "buildingpart" or "new part" or "building" or "prefab";
        }

        internal static string ResolveExplicitPartDisplayName(object part)
        {
            if (part == null) return null;

            var explicitName = EasyBuildBridgeReflectionHelper.GetMemberValue(part, "DisplayName") as string
                               ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "Title") as string
                               ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "Name") as string;

            return string.IsNullOrWhiteSpace(explicitName) ? null : explicitName.Trim();
        }

        internal static string ResolvePrefabNameFromPart(object part)
        {
            if (part == null) return null;

            var prefabObject = EasyBuildBridgeReflectionHelper.GetMemberValue(part, "Prefab")
                               ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "PartPrefab")
                               ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "BuildingPrefab")
                               ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "GameObject")
                               ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "PreviewPrefab");

            if (prefabObject is GameObject prefabGo)
                return NormalizePrefabDisplayName(prefabGo.name);

            if (prefabObject is Component prefabComponent && prefabComponent.gameObject != null)
                return NormalizePrefabDisplayName(prefabComponent.gameObject.name);

#if UNITY_EDITOR
            if (prefabObject is UnityEngine.Object prefabAsset)
            {
                var assetPath = AssetDatabase.GetAssetPath(prefabAsset);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                    if (!string.IsNullOrWhiteSpace(fileName))
                        return NormalizePrefabDisplayName(fileName);
                }
            }
#endif

            return null;
        }

        internal static string NormalizePrefabDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var normalized = value.Trim();
            const string cloneSuffix = "(Clone)";

            if (normalized.EndsWith(cloneSuffix, StringComparison.Ordinal))
                normalized = normalized.Substring(0, normalized.Length - cloneSuffix.Length).TrimEnd();

            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        internal static string NicifyPartIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Item";

            var text = value.Trim().Replace('_', ' ').Replace('-', ' ');
            text = text.Replace("  ", " ");

            if (text.StartsWith("PA ", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(3);

            if (text.StartsWith("SM ", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(3);

            return text;
        }

        internal static Texture2D ResolveMainTextureFromPart(object part)
        {
            if (part == null) return null;

            var prefabObject = EasyBuildBridgeReflectionHelper.GetMemberValue(part, "Prefab")
                               ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "PartPrefab")
                               ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "BuildingPrefab")
                               ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "GameObject")
                               ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "PreviewPrefab");

            if (prefabObject is GameObject prefabGo)
            {
                var tex = ResolveMainTextureFromPrefab(prefabGo);
                if (tex != null) return tex;
            }
            else if (prefabObject is Component prefabComp)
            {
                var tex = ResolveMainTextureFromPrefab(prefabComp.gameObject);
                if (tex != null) return tex;
            }

            var previewMat = EasyBuildBridgeReflectionHelper.GetMemberValue(part, "PreviewMaterial") as Material
                             ?? EasyBuildBridgeReflectionHelper.GetMemberValue(part, "Material") as Material;
            return ResolveMainTextureFromMaterial(previewMat);
        }

        internal static Texture2D ResolveMainTextureFromPrefab(GameObject prefab)
        {
            if (prefab == null) return null;

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;

                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0) continue;

                for (var m = 0; m < materials.Length; m++)
                {
                    var tex = ResolveMainTextureFromMaterial(materials[m]);
                    if (tex != null) return tex;
                }
            }

            return null;
        }

        internal static Texture2D ResolveMainTextureFromMaterial(Material material)
        {
            if (material == null) return null;

            if (material.mainTexture is Texture2D mainTexture) return mainTexture;

            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") is Texture2D baseMap)
                return baseMap;

            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") is Texture2D legacyMain)
                return legacyMain;

            return null;
        }
    }
}
