#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    internal static partial class MeshyImportPipelineRunner
    {
        private interface IMaterialTexturePolicy
        {
            void WireTexturesToMaterialsAndFixNormals(string folderPath, MeshyImportSummary summary);
        }

        private sealed class MeshyMaterialTexturePolicy : IMaterialTexturePolicy
        {
            private enum TextureRole
            {
                Base,
                Normal,
                Metallic,
                Emission
            }

            public void WireTexturesToMaterialsAndFixNormals(string folderPath, MeshyImportSummary summary)
            {
                var texturePaths = MeshyAssetPathUtility.FindDirectAssetPaths(folderPath, "t:Texture2D", TextureFileExtensions);
                if (texturePaths.Count == 0)
                    return;

                for (var i = 0; i < texturePaths.Count; i++)
                {
                    var texturePath = texturePaths[i];
                    if (!LooksLikeNormalTexture(texturePath))
                        continue;

                    if (EnsureTextureImporterNormal(texturePath))
                        summary.NormalImportersFixed++;
                }

                texturePaths = MeshyAssetPathUtility.FindDirectAssetPaths(folderPath, "t:Texture2D", TextureFileExtensions);
                var textures = new List<Texture2D>(texturePaths.Count);
                for (var i = 0; i < texturePaths.Count; i++)
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePaths[i]);
                    if (texture != null)
                        textures.Add(texture);
                }

                if (textures.Count == 0)
                    return;

                var baseTexture = SelectBestTextureForRole(textures, TextureRole.Base);
                var normalTexture = SelectBestTextureForRole(textures, TextureRole.Normal);
                var emissionTexture = SelectBestTextureForRole(textures, TextureRole.Emission);
                var materialPaths = MeshyAssetPathUtility.FindDirectAssetPaths(folderPath, "t:Material", MaterialFileExtensions);
                for (var i = 0; i < materialPaths.Count; i++)
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPaths[i]);
                    if (material == null) continue;

                    var changed = false;

                    if (baseTexture != null)
                    {
                        changed |= SetTextureIfPropertyExists(material, "_BaseMap", baseTexture);
                        changed |= SetTextureIfPropertyExists(material, "_MainTex", baseTexture);
                    }

                    if (normalTexture != null)
                    {
                        changed |= SetTextureIfPropertyExists(material, "_BumpMap", normalTexture);
                        if (material.HasProperty("_BumpMap"))
                            material.EnableKeyword("_NORMALMAP");
                    }

                    if (emissionTexture != null)
                    {
                        changed |= SetTextureIfPropertyExists(material, "_EmissionMap", emissionTexture);
                        if (material.HasProperty("_EmissionColor"))
                        {
                            material.SetColor("_EmissionColor", Color.white);
                            changed = true;
                        }

                        material.EnableKeyword("_EMISSION");
                    }

                    if (material.HasProperty("_MetallicGlossMap") && material.GetTexture("_MetallicGlossMap") != null)
                    {
                        material.SetTexture("_MetallicGlossMap", null);
                        changed = true;
                    }

                    if (material.HasProperty("_Metallic") && !Mathf.Approximately(material.GetFloat("_Metallic"), 0f))
                    {
                        material.SetFloat("_Metallic", 0f);
                        changed = true;
                    }

                    if (material.HasProperty("_SpecularHighlights") &&
                        !Mathf.Approximately(material.GetFloat("_SpecularHighlights"), 0f))
                    {
                        material.SetFloat("_SpecularHighlights", 0f);
                        changed = true;
                    }

                    if (material.HasProperty("_Smoothness") && !Mathf.Approximately(material.GetFloat("_Smoothness"), 0.1f))
                    {
                        material.SetFloat("_Smoothness", 0.1f);
                        changed = true;
                    }

                    if (material.HasProperty("_Glossiness") && !Mathf.Approximately(material.GetFloat("_Glossiness"), 0.1f))
                    {
                        material.SetFloat("_Glossiness", 0.1f);
                        changed = true;
                    }

                    material.DisableKeyword("_METALLICSPECGLOSSMAP");
                    material.DisableKeyword("_SPECGLOSSMAP");

                    if (!changed)
                        continue;

                    EditorUtility.SetDirty(material);
                    summary.MaterialsRewired++;
                }
            }

            private static bool SetTextureIfPropertyExists(Material material, string propertyName, Texture texture)
            {
                if (material == null || texture == null || string.IsNullOrWhiteSpace(propertyName)) return false;
                if (!material.HasProperty(propertyName)) return false;

                if (material.GetTexture(propertyName) == texture)
                    return false;

                material.SetTexture(propertyName, texture);
                return true;
            }

            private static Texture2D SelectBestTextureForRole(IReadOnlyList<Texture2D> textures, TextureRole role)
            {
                Texture2D best = null;
                var bestScore = int.MinValue;

                for (var i = 0; i < textures.Count; i++)
                {
                    var texture = textures[i];
                    if (texture == null) continue;

                    var path = AssetDatabase.GetAssetPath(texture);
                    var score = ScoreTextureForRole(path, role);
                    if (score <= bestScore) continue;

                    bestScore = score;
                    best = texture;
                }

                return bestScore > 0 ? best : null;
            }

            private static int ScoreTextureForRole(string texturePath, TextureRole role)
            {
                var name = Path.GetFileNameWithoutExtension(texturePath) ?? string.Empty;
                var key = MeshyAssetPathUtility.NormalizeNameKey(name);

                if (string.IsNullOrWhiteSpace(key))
                    return 0;

                switch (role)
                {
                    case TextureRole.Base:
                        if (key.Contains("basemap")) return 100;
                        if (key.Contains("albedo")) return 95;
                        if (key.Contains("diffuse")) return 90;
                        if (key.Contains("image0")) return 85;
                        if (key.Contains("base")) return 80;
                        if (key.EndsWith("texture", StringComparison.Ordinal)) return 75;
                        if (key.Contains("texture") && !key.Contains("normal") && !key.Contains("metallic") &&
                            !key.Contains("roughness") && !key.Contains("emission"))
                            return 70;

                        return 0;

                    case TextureRole.Normal:
                        if (key.Contains("normalmap")) return 100;
                        if (key.Contains("texturenormal")) return 95;
                        if (key.Contains("normal")) return 90;
                        return 0;

                    case TextureRole.Metallic:
                        if (key.Contains("roughness")) return 110;
                        if (key.Contains("metallicmap")) return 100;
                        if (key.Contains("metallic")) return 95;
                        return 0;

                    case TextureRole.Emission:
                        if (key.Contains("emissionmap")) return 100;
                        if (key.Contains("emission")) return 95;
                        if (key.Contains("image3")) return 90;
                        return 0;

                    default:
                        return 0;
                }
            }

            private static bool LooksLikeNormalTexture(string texturePath)
            {
                var name = Path.GetFileNameWithoutExtension(texturePath) ?? string.Empty;
                var key = MeshyAssetPathUtility.NormalizeNameKey(name);
                return key.Contains("normal");
            }

            private static bool EnsureTextureImporterNormal(string texturePath)
            {
                var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer == null)
                    return false;

                if (importer.textureType == TextureImporterType.NormalMap)
                    return false;

                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
                return true;
            }
        }
    }
}
#endif
