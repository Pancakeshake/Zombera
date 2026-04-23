#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombera.Editor
{
    /// <summary>
    /// Converts the Free medieval weapons pack materials to URP/Lit and validates prefab references.
    /// </summary>
    public static class FreeMedievalWeaponsUrpFixTool
    {
        private const string RootFolder = "Assets/ThirdParty/Free medieval weapons";
        private const string MaterialsFolder = "Assets/ThirdParty/Free medieval weapons/Materials";
        private const string PrefabsFolder = "Assets/ThirdParty/Free medieval weapons/Prefabs";

        [MenuItem("Tools/Zombera/Art/Fix Free Medieval Weapons (URP Lit)")]
        public static void FixFreeMedievalWeaponsPack()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                EditorUtility.DisplayDialog(
                    "URP Lit Not Found",
                    "Could not find 'Universal Render Pipeline/Lit'. Ensure URP is installed and active in Graphics settings.",
                    "OK");
                return;
            }

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder });
            int materialCount = 0;
            int changedCount = 0;

            for (int i = 0; i < materialGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                materialCount++;

                if (ConvertMaterialToUrpLit(material, urpLit))
                {
                    changedCount++;
                    EditorUtility.SetDirty(material);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ValidatePrefabs(out int prefabCount, out int prefabsWithIssues, out List<string> issueLines);

            string summary =
                "[Zombera] Free medieval weapons URP fix complete.\n"
                + $"  Pack root: {RootFolder}\n"
                + $"  Materials scanned: {materialCount}\n"
                + $"  Materials changed: {changedCount}\n"
                + $"  Prefabs scanned: {prefabCount}\n"
                + $"  Prefabs with issues: {prefabsWithIssues}";

            if (issueLines.Count > 0)
            {
                summary += "\n  See Console for issue details.";
                Debug.LogWarning(summary + "\n" + string.Join("\n", issueLines));
            }
            else
            {
                Debug.Log(summary);
            }

            EditorUtility.DisplayDialog("Free Medieval Weapons URP Fix", summary, "OK");
        }

        private static bool ConvertMaterialToUrpLit(Material material, Shader urpLit)
        {
            bool changed = false;

            Texture baseMap = GetTexture(material, "_BaseMap", "_MainTex");
            Texture normalMap = GetTexture(material, "_BumpMap");
            Texture metallicGlossMap = GetTexture(material, "_MetallicGlossMap");
            Texture occlusionMap = GetTexture(material, "_OcclusionMap");
            Texture emissionMap = GetTexture(material, "_EmissionMap");

            Color baseColor = GetColor(material, "_BaseColor", "_Color", Color.white);
            Color emissionColor = GetColor(material, "_EmissionColor", null, Color.black);

            float metallic = GetFloat(material, "_Metallic", null, 0f);
            float smoothness = GetFloat(material, "_Smoothness", "_Glossiness", 0.5f);
            float bumpScale = GetFloat(material, "_BumpScale", null, 1f);
            float occlusionStrength = GetFloat(material, "_OcclusionStrength", null, 1f);
            float cutoff = GetFloat(material, "_Cutoff", null, 0.5f);

            float legacyMode = GetFloat(material, "_Mode", null, 0f);
            bool wantsTransparent = legacyMode >= 2f || HasNameHint(material.name, "transparent") || HasNameHint(material.name, "transparency");
            bool wantsCutout = Mathf.Approximately(legacyMode, 1f) || HasNameHint(material.name, "cutout") || HasNameHint(material.name, "alpha");
            bool wantsDoubleSided = HasNameHint(material.name, "double") || HasNameHint(material.name, "two sided") || HasNameHint(material.name, "twosided");

            if (material.shader != urpLit)
            {
                material.shader = urpLit;
                changed = true;
            }

            changed |= TrySetTexture(material, "_BaseMap", baseMap);
            changed |= TrySetTexture(material, "_MainTex", baseMap);
            changed |= TrySetColor(material, "_BaseColor", baseColor);
            changed |= TrySetColor(material, "_Color", baseColor);

            changed |= TrySetTexture(material, "_BumpMap", normalMap);
            changed |= TrySetTexture(material, "_MetallicGlossMap", metallicGlossMap);
            changed |= TrySetTexture(material, "_OcclusionMap", occlusionMap);
            changed |= TrySetTexture(material, "_EmissionMap", emissionMap);
            changed |= TrySetColor(material, "_EmissionColor", emissionColor);

            changed |= TrySetFloat(material, "_Metallic", metallic);
            changed |= TrySetFloat(material, "_Smoothness", smoothness);
            changed |= TrySetFloat(material, "_BumpScale", bumpScale);
            changed |= TrySetFloat(material, "_OcclusionStrength", occlusionStrength);
            changed |= TrySetFloat(material, "_Cutoff", cutoff);

            changed |= ApplySurfaceMode(material, wantsTransparent, wantsCutout);

            if (wantsDoubleSided)
            {
                changed |= TrySetFloat(material, "_Cull", 0f);
            }

            if (emissionMap != null || emissionColor.maxColorComponent > 0.0001f)
            {
                material.EnableKeyword("_EMISSION");
                changed = true;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            return changed;
        }

        private static bool ApplySurfaceMode(Material material, bool wantsTransparent, bool wantsCutout)
        {
            bool changed = false;

            if (wantsTransparent)
            {
                changed |= TrySetFloat(material, "_Surface", 1f);
                changed |= TrySetFloat(material, "_Blend", 0f);
                changed |= TrySetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
                changed |= TrySetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                changed |= TrySetFloat(material, "_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                changed |= TrySetFloat(material, "_Surface", 0f);
                changed |= TrySetFloat(material, "_Blend", 0f);
                changed |= TrySetFloat(material, "_SrcBlend", (float)BlendMode.One);
                changed |= TrySetFloat(material, "_DstBlend", (float)BlendMode.Zero);
                changed |= TrySetFloat(material, "_ZWrite", 1f);
                material.SetOverrideTag("RenderType", wantsCutout ? "TransparentCutout" : "Opaque");
                material.renderQueue = wantsCutout ? (int)RenderQueue.AlphaTest : -1;
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            changed |= TrySetFloat(material, "_AlphaClip", wantsCutout ? 1f : 0f);
            if (wantsCutout)
            {
                material.EnableKeyword("_ALPHATEST_ON");
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
            }

            return changed;
        }

        private static void ValidatePrefabs(out int prefabCount, out int prefabsWithIssues, out List<string> issueLines)
        {
            prefabCount = 0;
            prefabsWithIssues = 0;
            issueLines = new List<string>();

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                prefabCount++;

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                bool hasIssue = false;

                try
                {
                    Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
                    for (int r = 0; r < renderers.Length; r++)
                    {
                        Renderer renderer = renderers[r];
                        Material[] materials = renderer.sharedMaterials;

                        for (int m = 0; m < materials.Length; m++)
                        {
                            Material material = materials[m];
                            if (material == null)
                            {
                                hasIssue = true;
                                issueLines.Add($"[Free medieval weapons] {prefabPath} -> Renderer '{renderer.name}' has null material slot {m}.");
                                continue;
                            }

                            Shader shader = material.shader;
                            if (shader == null || shader.name.IndexOf("internalerrorshader", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                hasIssue = true;
                                issueLines.Add($"[Free medieval weapons] {prefabPath} -> Material '{material.name}' has invalid shader.");
                            }
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }

                if (hasIssue)
                {
                    prefabsWithIssues++;
                }
            }
        }

        private static bool HasNameHint(string value, string hint)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(hint))
            {
                return false;
            }

            return value.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Texture GetTexture(Material material, string primaryProperty, string fallbackProperty = null)
        {
            if (material == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(primaryProperty) && material.HasProperty(primaryProperty))
            {
                Texture primary = material.GetTexture(primaryProperty);
                if (primary != null)
                {
                    return primary;
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackProperty) && material.HasProperty(fallbackProperty))
            {
                return material.GetTexture(fallbackProperty);
            }

            return null;
        }

        private static Color GetColor(Material material, string primaryProperty, string fallbackProperty, Color defaultValue)
        {
            if (material == null)
            {
                return defaultValue;
            }

            if (!string.IsNullOrWhiteSpace(primaryProperty) && material.HasProperty(primaryProperty))
            {
                return material.GetColor(primaryProperty);
            }

            if (!string.IsNullOrWhiteSpace(fallbackProperty) && material.HasProperty(fallbackProperty))
            {
                return material.GetColor(fallbackProperty);
            }

            return defaultValue;
        }

        private static float GetFloat(Material material, string primaryProperty, string fallbackProperty, float defaultValue)
        {
            if (material == null)
            {
                return defaultValue;
            }

            if (!string.IsNullOrWhiteSpace(primaryProperty) && material.HasProperty(primaryProperty))
            {
                return material.GetFloat(primaryProperty);
            }

            if (!string.IsNullOrWhiteSpace(fallbackProperty) && material.HasProperty(fallbackProperty))
            {
                return material.GetFloat(fallbackProperty);
            }

            return defaultValue;
        }

        private static bool TrySetTexture(Material material, string propertyName, Texture value)
        {
            if (material == null || string.IsNullOrWhiteSpace(propertyName) || !material.HasProperty(propertyName))
            {
                return false;
            }

            Texture current = material.GetTexture(propertyName);
            if (current == value)
            {
                return false;
            }

            material.SetTexture(propertyName, value);
            return true;
        }

        private static bool TrySetColor(Material material, string propertyName, Color value)
        {
            if (material == null || string.IsNullOrWhiteSpace(propertyName) || !material.HasProperty(propertyName))
            {
                return false;
            }

            Color current = material.GetColor(propertyName);
            if (current.Equals(value))
            {
                return false;
            }

            material.SetColor(propertyName, value);
            return true;
        }

        private static bool TrySetFloat(Material material, string propertyName, float value)
        {
            if (material == null || string.IsNullOrWhiteSpace(propertyName) || !material.HasProperty(propertyName))
            {
                return false;
            }

            float current = material.GetFloat(propertyName);
            if (Mathf.Approximately(current, value))
            {
                return false;
            }

            material.SetFloat(propertyName, value);
            return true;
        }
    }
}
#endif
