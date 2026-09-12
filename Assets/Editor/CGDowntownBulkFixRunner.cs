#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombera.Editor
{
    /// <summary>
    /// Bulk-fix CG Downtown / CGAxis materials and FBX remaps.
    /// Known-good recipe: URP Lit + basecolor + normal_opengl, metallic=0,
    /// no MetallicGlossMap (CGAxis roughness is not Unity smoothness), low scalar Smoothness.
    /// </summary>
    public static class CGDowntownBulkFixRunner
    {
        public const string RootFolder = "Assets/02_Shared/CG_Downtown";
        public const string MapsFolder = RootFolder + "/maps";
        public const string MaterialsFolder = RootFolder + "/Materials";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        private static readonly string[] MapSuffixesLongestFirst =
        {
            "basecolor_diffuse",
            "normal_opengl",
            "base_color",
            "basecolor",
            "diffuse",
            "glossiness",
            "roughness",
            "metallic",
            "normal",
            "reflection",
            "opacity",
            "emissive",
            "ior",
            "height"
        };

        public struct Options
        {
            public float DefaultSmoothness;
            public float BumpScale;
            public bool FixTextureImporters;
            public bool CreateOrUpdateMaterials;
            public bool RemapFbxMaterials;
            public bool ForceSmoothness;
            public bool SetFbxNormalsToCalculate;
        }

        public struct Summary
        {
            public int TextureImportersFixed;
            public int MaterialsCreated;
            public int MaterialsUpdated;
            public int FbxRemapped;
            public int FbxNormalsFixed;
            public int MapGroups;
            public string Message;
        }

        public static Options DefaultOptions()
        {
            return new Options
            {
                DefaultSmoothness = 0.15f,
                BumpScale = 1.4f,
                FixTextureImporters = true,
                CreateOrUpdateMaterials = true,
                RemapFbxMaterials = true,
                ForceSmoothness = false,
                SetFbxNormalsToCalculate = false
            };
        }

        public static Summary Run(Options options)
        {
            var summary = new Summary();
            if (!AssetDatabase.IsValidFolder(MapsFolder))
            {
                summary.Message = "Maps folder not found: " + MapsFolder;
                return summary;
            }

            EnsureFolder(MaterialsFolder);

            var groups = BuildMapGroups();
            summary.MapGroups = groups.Count;

            if (options.FixTextureImporters)
                summary.TextureImportersFixed = FixTextureImporters(groups);

            Dictionary<string, Material> materialsByName = null;
            if (options.CreateOrUpdateMaterials)
            {
                materialsByName = CreateOrUpdateMaterials(groups, options, ref summary);
            }
            else
            {
                materialsByName = LoadExistingMaterialsByName();
            }

            if (options.RemapFbxMaterials)
                summary.FbxRemapped = RemapFbxMaterials(materialsByName);

            if (options.SetFbxNormalsToCalculate)
                summary.FbxNormalsFixed = FixFbxNormals();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            summary.Message =
                $"Groups={summary.MapGroups}, created={summary.MaterialsCreated}, updated={summary.MaterialsUpdated}, " +
                $"texFix={summary.TextureImportersFixed}, fbxRemap={summary.FbxRemapped}, fbxNormals={summary.FbxNormalsFixed}";
            return summary;
        }

        private static Dictionary<string, Dictionary<string, string>> BuildMapGroups()
        {
            var groups = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { MapsFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (!TrySplitMapName(fileName, out var materialName, out var suffix))
                    continue;

                if (!groups.TryGetValue(materialName, out var maps))
                {
                    maps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    groups[materialName] = maps;
                }

                maps[suffix] = path;
            }

            return groups;
        }

        private static bool TrySplitMapName(string fileName, out string materialName, out string suffix)
        {
            materialName = null;
            suffix = null;
            for (var i = 0; i < MapSuffixesLongestFirst.Length; i++)
            {
                var candidate = MapSuffixesLongestFirst[i];
                var token = "_" + candidate;
                if (!fileName.EndsWith(token, StringComparison.OrdinalIgnoreCase))
                    continue;

                materialName = fileName.Substring(0, fileName.Length - token.Length);
                suffix = candidate;
                return materialName.Length > 0;
            }

            return false;
        }

        private static int FixTextureImporters(Dictionary<string, Dictionary<string, string>> groups)
        {
            var fixedCount = 0;
            foreach (var pair in groups)
            {
                var maps = pair.Value;
                if (maps.TryGetValue("normal_opengl", out var openglPath) && EnsureNormalImporter(openglPath))
                    fixedCount++;
                if (maps.TryGetValue("normal", out var normalPath) && EnsureNormalImporter(normalPath))
                    fixedCount++;

                if (maps.TryGetValue("roughness", out var roughnessPath) && EnsureLinearImporter(roughnessPath))
                    fixedCount++;
                if (maps.TryGetValue("metallic", out var metallicPath) && EnsureLinearImporter(metallicPath))
                    fixedCount++;
                if (maps.TryGetValue("glossiness", out var glossPath) && EnsureLinearImporter(glossPath))
                    fixedCount++;
                if (maps.TryGetValue("opacity", out var opacityPath) && EnsureLinearImporter(opacityPath))
                    fixedCount++;
            }

            return fixedCount;
        }

        private static bool EnsureNormalImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return false;

            var changed = false;
            if (importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }

            if (importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                changed = true;
            }

            if (!changed)
                return false;

            importer.SaveAndReimport();
            return true;
        }

        private static bool EnsureLinearImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return false;

            if (!importer.sRGBTexture)
                return false;

            importer.sRGBTexture = false;
            importer.SaveAndReimport();
            return true;
        }

        private static Dictionary<string, Material> CreateOrUpdateMaterials(
            Dictionary<string, Dictionary<string, string>> groups,
            Options options,
            ref Summary summary)
        {
            var shader = Shader.Find(UrpLitShaderName);
            if (shader == null)
                throw new InvalidOperationException("Shader not found: " + UrpLitShaderName);

            var existing = LoadExistingMaterialsByName();
            var result = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in groups)
            {
                var materialName = pair.Key;
                var maps = pair.Value;
                var created = false;

                if (!existing.TryGetValue(materialName, out var material) || material == null)
                {
                    var assetPath = $"{MaterialsFolder}/{materialName}.mat";
                    material = new Material(shader) { name = materialName };
                    AssetDatabase.CreateAsset(material, assetPath);
                    created = true;
                    summary.MaterialsCreated++;
                }
                else
                {
                    if (material.shader != shader)
                        material.shader = shader;
                    summary.MaterialsUpdated++;
                }

                ApplyKnownGoodRecipe(material, maps, options, created);
                EditorUtility.SetDirty(material);
                result[materialName] = material;
            }

            return result;
        }

        private static void ApplyKnownGoodRecipe(
            Material material,
            Dictionary<string, string> maps,
            Options options,
            bool newlyCreated)
        {
            var albedo = PickTexture(maps, "basecolor", "base_color", "basecolor_diffuse", "diffuse");
            var normal = PickTexture(maps, "normal_opengl", "normal");
            var emissive = PickTexture(maps, "emissive");
            var opacity = PickTexture(maps, "opacity");

            if (albedo != null)
            {
                SetTexture(material, "_BaseMap", albedo);
                SetTexture(material, "_MainTex", albedo);
            }

            if (normal != null)
            {
                SetTexture(material, "_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
                if (material.HasProperty("_BumpScale"))
                    material.SetFloat("_BumpScale", options.BumpScale);
            }

            // CGAxis roughness/glossiness must NOT be treated as Unity smoothness maps.
            if (material.HasProperty("_MetallicGlossMap"))
                material.SetTexture("_MetallicGlossMap", null);
            if (material.HasProperty("_SpecGlossMap"))
                material.SetTexture("_SpecGlossMap", null);
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SPECGLOSSMAP");

            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);

            ApplySmoothness(material, options, newlyCreated);

            if (emissive != null)
            {
                SetTexture(material, "_EmissionMap", emissive);
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", Color.white);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }

            if (opacity != null && albedo != null)
            {
                // Opacity maps exist for glass-like assets; keep opaque by default.
                // Users can flip surface type manually for windows if needed.
            }
        }

        private static void ApplySmoothness(Material material, Options options, bool newlyCreated)
        {
            if (!material.HasProperty("_Smoothness"))
                return;

            var current = material.GetFloat("_Smoothness");
            var shouldSet = newlyCreated
                || options.ForceSmoothness
                || current > 0.5f
                || current > 1f;

            if (!shouldSet)
                return;

            material.SetFloat("_Smoothness", options.DefaultSmoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", options.DefaultSmoothness);
        }

        private static Texture2D PickTexture(Dictionary<string, string> maps, params string[] suffixes)
        {
            for (var i = 0; i < suffixes.Length; i++)
            {
                if (!maps.TryGetValue(suffixes[i], out var path))
                    continue;

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                    return texture;
            }

            return null;
        }

        private static void SetTexture(Material material, string property, Texture texture)
        {
            if (!material.HasProperty(property))
                return;
            material.SetTexture(property, texture);
        }

        private static Dictionary<string, Material> LoadExistingMaterialsByName()
        {
            var result = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                return result;

            var guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    continue;
                result[material.name] = material;
            }

            return result;
        }

        private static int RemapFbxMaterials(Dictionary<string, Material> materialsByName)
        {
            if (materialsByName == null || materialsByName.Count == 0)
                return 0;

            var remapped = 0;
            var guids = AssetDatabase.FindAssets("t:Model", new[] { RootFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                    continue;

                var changed = RemapModelMaterials(importer, materialsByName);
                if (!changed)
                    continue;

                importer.SaveAndReimport();
                remapped++;
            }

            return remapped;
        }

        private static bool RemapModelMaterials(ModelImporter importer, Dictionary<string, Material> materialsByName)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(importer.assetPath);
            if (go == null)
                return false;

            var needed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].sharedMaterials;
                for (var m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != null)
                        needed.Add(mats[m].name);
                }
            }

            var existingMap = importer.GetExternalObjectMap();
            foreach (var kv in existingMap)
            {
                if (kv.Key.type == typeof(Material))
                    needed.Add(kv.Key.name);
            }

            var changed = false;
            foreach (var materialName in needed)
            {
                if (!materialsByName.TryGetValue(materialName, out var material) || material == null)
                    continue;

                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName);
                if (existingMap.TryGetValue(id, out var current) && current == material)
                    continue;

                importer.AddRemap(id, material);
                changed = true;
            }

            return changed;
        }

        private static int FixFbxNormals()
        {
            var fixedCount = 0;
            var guids = AssetDatabase.FindAssets("t:Model", new[] { RootFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                    continue;

                var changed = false;
                if (importer.importNormals != ModelImporterNormals.Calculate)
                {
                    importer.importNormals = ModelImporterNormals.Calculate;
                    changed = true;
                }

                if (importer.importTangents != ModelImporterTangents.CalculateMikk)
                {
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    changed = true;
                }

                if (!changed)
                    continue;

                importer.SaveAndReimport();
                fixedCount++;
            }

            return fixedCount;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;

            var parts = assetFolder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
