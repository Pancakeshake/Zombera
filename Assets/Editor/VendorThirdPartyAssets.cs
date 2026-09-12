#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombera.Editor
{
    /// <summary>
    ///     Copies URP textures, materials, prefabs, and meshes from each third-party
    ///     pack into the correct category folders under Assets/02_Shared/ and re-links
    ///     all GUID references so the copies are self-contained. Skips duplicates.
    ///
    ///     Run via: Zombera > Vendor Third-Party URP Assets
    /// </summary>
    public static class VendorThirdPartyAssets
    {
        private const string ThirdPartyRoot = "Assets/03_ThirdParty";
        private const string SharedRoot = "Assets/02_Shared";

        private static readonly Regex GuidRefRegex = new(
            @"guid:\s*([a-fA-F0-9]{32})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        ///     Maps each third-party pack to the category folder used under
        ///     Textures/Specific/, Materials/Specific/, Prefabs/, and Meshes/.
        ///     Packs not listed here are skipped.
        /// </summary>
        private static readonly Dictionary<string, string> PackCategoryMap = new()
        {
            // ── Building / Architecture ──
            ["BuildingPropsPack"] = "Building",
            ["Free Wood Door Pack"] = "Building",
            ["ToiletSet"] = "Building",

            // ── Construction ──
            ["ConstructionPropsBundle"] = "Construction",
            ["HandToolsPack"] = "Construction",
            ["LaddersPack"] = "Construction",
            ["ScaffoldsPack"] = "Construction",
            ["WorkbenchPack"] = "Construction",

            // ── Industrial ──
            ["FiresetPack"] = "Industrial",
            ["FuseBoxPack"] = "Industrial",
            ["Generators"] = "Industrial",
            ["IndustrialEquipmentPack"] = "Industrial",
            ["IndustrialPropsMegaBundle"] = "Industrial",
            ["ShipContainerPack"] = "Industrial",

            // ── Storage ──
            ["LockersPack"] = "Storage",
            ["RacksPack"] = "Storage",

            // ── Roads / Street ──
            ["RoadPropsPack"] = "Roads",

            // ── Street Furniture ──
            ["12PropsPack"] = "StreetFurniture",

            // ── Vehicles ──
            ["ConstructionTruck"] = "Vehicles",
            ["SideWallTrailer"] = "Vehicles",

            // ── Exterior / Abandoned ──
            ["AbandonedPropsPack"] = "Architecture",
            ["FabricPack"] = "Fabric",
        };

        private struct VendorCopyStats
        {
            public int CopiedTex;
            public int CopiedMat;
            public int CopiedPrefab;
            public int CopiedMesh;
            public int RelinkedMat;
            public int RelinkedPrefab;
            public int Skipped;
        }

        private struct PackPaths
        {
            public string PackName;
            public string Category;
            public string TexSrc;
            public string MeshSrc;
            public string MatSrc;
            public string PrefabSrc;
            public string TexDest;
            public string MatDest;
            public string PrefabDest;
            public string MeshDest;
        }

        [MenuItem(MenuPaths.Assets + "Third Party Extractor", false, 500)]
        public static void Run()
        {
            var stats = new VendorCopyStats();
            foreach (var packPath in Directory.GetDirectories(ThirdPartyRoot))
            {
                if (!TryResolvePackPaths(packPath, out var paths))
                    continue;

                try
                {
                    ProcessPack(paths, ref stats);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[VendorThirdParty] Failed '{paths.PackName}': {ex.Message}");
                }
            }

            var validated = ValidateAndRepairAll();
            Debug.Log(
                $"[VendorThirdParty] Done — copied {stats.CopiedTex} textures, {stats.CopiedMat} materials, " +
                $"{stats.CopiedPrefab} prefabs, {stats.CopiedMesh} meshes. " +
                $"Re-linked {stats.RelinkedMat} material(s), {stats.RelinkedPrefab} prefab(s). " +
                $"Skipped {stats.Skipped} duplicate(s). " +
                $"Post-validated {validated} broken reference(s).");
        }

        private static bool TryResolvePackPaths(string packPath, out PackPaths paths)
        {
            paths = default;
            var packName = Path.GetFileName(packPath);
            if (!PackCategoryMap.TryGetValue(packName, out var category))
                return false;

            var urpPath = Path.Combine(packPath, "URP");
            var texPath = Path.Combine(packPath, "Textures");
            if (!Directory.Exists(urpPath) || !Directory.Exists(texPath))
                return false;

            paths = new PackPaths
            {
                PackName = packName,
                Category = category,
                TexSrc = texPath,
                MeshSrc = Path.Combine(packPath, "Mesh"),
                MatSrc = Path.Combine(urpPath, "Materials"),
                PrefabSrc = Path.Combine(urpPath, "Prefabs"),
                TexDest = Path.Combine(SharedRoot, "Textures", "Specific", category).Replace("\\", "/"),
                MatDest = Path.Combine(SharedRoot, "Materials", "Specific", category).Replace("\\", "/"),
                PrefabDest = Path.Combine(SharedRoot, "Prefabs", category).Replace("\\", "/"),
                MeshDest = Path.Combine(SharedRoot, "Meshes", category).Replace("\\", "/")
            };
            return true;
        }

        private static void ProcessPack(in PackPaths paths, ref VendorCopyStats stats)
        {
            stats.CopiedTex += CopyDirectoryContents(paths.TexSrc, paths.TexDest, ref stats.Skipped);

            if (Directory.Exists(paths.MeshSrc))
                stats.CopiedMesh += CopyDirectoryContents(paths.MeshSrc, paths.MeshDest, ref stats.Skipped);

            if (Directory.Exists(paths.MatSrc))
                stats.CopiedMat += CopyDirectoryContents(paths.MatSrc, paths.MatDest, ref stats.Skipped);

            if (Directory.Exists(paths.PrefabSrc))
                stats.CopiedPrefab += CopyDirectoryContents(paths.PrefabSrc, paths.PrefabDest, ref stats.Skipped);

            AssetDatabase.Refresh();

            var actualMatDest = Path.Combine(SharedRoot, "Materials", "Specific", paths.Category);
            var actualTexDest = Path.Combine(SharedRoot, "Textures", "Specific", paths.Category);
            var actualPrefabDest = Path.Combine(SharedRoot, "Prefabs", paths.Category);

            if (Directory.Exists(actualMatDest))
                stats.RelinkedMat += RelinkMaterialTextures(actualMatDest, actualTexDest);

            if (Directory.Exists(actualPrefabDest))
            {
                stats.RelinkedPrefab += RelinkPrefabMaterials(actualPrefabDest, actualMatDest);
                stats.RelinkedPrefab += RemapPrefabTextGUIDs(
                    actualPrefabDest, actualMatDest, paths.TexDest, paths.MeshDest);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SafeCreateFolder(string parentPath)
        {
            var p = parentPath.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(p))
                return;

            var par = Path.GetDirectoryName(p).Replace("\\", "/");
            var name = Path.GetFileName(p);
            if (!AssetDatabase.IsValidFolder(par))
                SafeCreateFolder(par);
            AssetDatabase.CreateFolder(par, name);
        }

        private static int CopyDirectoryContents(string srcDir, string destDir, ref int skipped)
        {
            SafeCreateFolder(destDir);
            var count = 0;

            foreach (var sub in Directory.GetDirectories(srcDir))
            {
                var subDest = Path.Combine(destDir, Path.GetFileName(sub));
                count += CopyDirectoryContents(sub, subDest, ref skipped);
            }

            foreach (var file in Directory.GetFiles(srcDir))
            {
                if (file.EndsWith(".meta"))
                    continue;

                var destFile = Path.Combine(destDir, Path.GetFileName(file)).Replace("\\", "/");
                var srcFile = file.Replace("\\", "/");
                if (File.Exists(destFile))
                {
                    AssetDatabase.DeleteAsset(destFile);
                    skipped++;
                }

                AssetDatabase.CopyAsset(srcFile, destFile);
                count++;
            }

            return count;
        }

        private static int RelinkMaterialTextures(string matDir, string texDir)
        {
            if (!Directory.Exists(matDir) || !Directory.Exists(texDir))
                return 0;

            var texGuidByName = BuildGuidLookup(texDir, "*");
            var relinked = 0;
            foreach (var matFile in Directory.GetFiles(matDir, "*.mat", SearchOption.AllDirectories))
            {
                if (TryRelinkMaterialFile(matFile, texGuidByName))
                    relinked++;
            }

            return relinked;
        }

        private static bool TryRelinkMaterialFile(string matFile, Dictionary<string, string> texGuidByName)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matFile.Replace("\\", "/"));
            if (mat?.shader == null)
                return false;

            var changed = false;
            var propCount = mat.shader.GetPropertyCount();
            for (var p = 0; p < propCount; p++)
            {
                if (TryReplaceMaterialTexture(mat, mat.shader, p, texGuidByName))
                    changed = true;
            }

            if (!changed)
                return false;

            EditorUtility.SetDirty(mat);
            return true;
        }

        private static bool TryReplaceMaterialTexture(
            Material mat,
            Shader shader,
            int propertyIndex,
            Dictionary<string, string> texGuidByName)
        {
            if (shader.GetPropertyType(propertyIndex) != ShaderPropertyType.Texture)
                return false;

            var propName = shader.GetPropertyName(propertyIndex);
            var oldTex = mat.GetTexture(propName);
            if (oldTex == null)
                return false;

            var oldPath = AssetDatabase.GetAssetPath(oldTex);
            if (string.IsNullOrEmpty(oldPath) || !oldPath.StartsWith(ThirdPartyRoot))
                return false;

            var oldName = Path.GetFileNameWithoutExtension(oldPath);
            if (!texGuidByName.TryGetValue(oldName, out var newGuid))
                return false;

            var newPath = AssetDatabase.GUIDToAssetPath(newGuid);
            if (string.IsNullOrEmpty(newPath))
                return false;

            var newTex = AssetDatabase.LoadAssetAtPath<Texture>(newPath);
            if (newTex == null || newTex == oldTex)
                return false;

            mat.SetTexture(propName, newTex);
            return true;
        }

        private static int RelinkPrefabMaterials(string prefabDir, string matDir)
        {
            if (!Directory.Exists(prefabDir) || !Directory.Exists(matDir))
                return 0;

            var matGuidByName = BuildGuidLookup(matDir, "*.mat");
            var relinked = 0;
            foreach (var prefabFile in Directory.GetFiles(prefabDir, "*.prefab", SearchOption.AllDirectories))
            {
                if (TryRelinkPrefabFile(prefabFile.Replace("\\", "/"), matGuidByName))
                    relinked++;
            }

            return relinked;
        }

        private static bool TryRelinkPrefabFile(string prefabPath, Dictionary<string, string> matGuidByName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return false;

            var changed = false;
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (TryReplaceRendererMaterials(renderers[i], matGuidByName))
                    changed = true;
            }

            if (!changed)
                return false;

            PrefabUtility.SavePrefabAsset(prefab);
            return true;
        }

        private static bool TryReplaceRendererMaterials(
            Renderer renderer,
            Dictionary<string, string> matGuidByName)
        {
            var mats = renderer.sharedMaterials;
            var changed = false;
            for (var m = 0; m < mats.Length; m++)
            {
                if (!TryResolveSharedMaterial(mats[m], matGuidByName, out var newMat))
                    continue;

                mats[m] = newMat;
                changed = true;
            }

            if (!changed)
                return false;

            renderer.sharedMaterials = mats;
            return true;
        }

        private static bool TryResolveSharedMaterial(
            Material oldMat,
            Dictionary<string, string> matGuidByName,
            out Material newMat)
        {
            newMat = null;
            if (oldMat == null)
                return false;

            var oldPath = AssetDatabase.GetAssetPath(oldMat);
            if (string.IsNullOrEmpty(oldPath) || !oldPath.StartsWith(ThirdPartyRoot))
                return false;

            var oldName = Path.GetFileNameWithoutExtension(oldPath);
            if (!matGuidByName.TryGetValue(oldName, out var newGuid))
                return false;

            var newPath = AssetDatabase.GUIDToAssetPath(newGuid);
            if (string.IsNullOrEmpty(newPath))
                return false;

            newMat = AssetDatabase.LoadAssetAtPath<Material>(newPath);
            return newMat != null;
        }

        /// <summary>
        ///     Text-level GUID remap for copied prefabs. Walks every .prefab in
        ///     the destination and replaces ThirdParty GUID references with the
        ///     matching Shared GUID. Returns the number of files modified.
        /// </summary>
        private static int RemapPrefabTextGUIDs(
            string prefabDir,
            string matDir,
            string texDir,
            string meshDir)
        {
            if (!Directory.Exists(prefabDir))
                return 0;

            var guidMap = BuildGuidLookup(matDir, "*");
            MergeGuidLookup(guidMap, texDir, "*");
            MergeGuidLookup(guidMap, meshDir, "*");
            return RemapPrefabFiles(prefabDir, guidMap, RemapThirdPartyGuid);
        }

        /// <summary>
        ///     Post-extraction validation pass. Scans shared prefabs for dead GUID
        ///     references and repairs them by filename against 02_Shared assets.
        /// </summary>
        private static int ValidateAndRepairAll()
        {
            var prefabRoot = SharedRoot + "/Prefabs";
            if (!Directory.Exists(prefabRoot))
                return 0;

            var sharedByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            MergeGuidLookup(sharedByName, SharedRoot + "/Materials", "*");
            MergeGuidLookup(sharedByName, SharedRoot + "/Meshes", "*");
            MergeGuidLookup(sharedByName, SharedRoot + "/Textures", "*");
            return RemapPrefabFiles(prefabRoot, sharedByName, RemapBrokenGuid);
        }

        private static int RemapPrefabFiles(
            string prefabDir,
            Dictionary<string, string> guidMap,
            Func<string, Dictionary<string, string>, string> remapMatch)
        {
            var remapped = 0;
            foreach (var prefabFile in Directory.GetFiles(prefabDir, "*.prefab", SearchOption.AllDirectories))
            {
                if (TryRewritePrefabGuids(prefabFile.Replace("\\", "/"), guidMap, remapMatch))
                    remapped++;
            }

            if (remapped > 0)
                AssetDatabase.Refresh();

            return remapped;
        }

        private static bool TryRewritePrefabGuids(
            string prefabPath,
            Dictionary<string, string> guidMap,
            Func<string, Dictionary<string, string>, string> remapMatch)
        {
            string text;
            try
            {
                text = File.ReadAllText(prefabPath);
            }
            catch
            {
                return false;
            }

            var rewritten = GuidRefRegex.Replace(text, match =>
            {
                var replacement = remapMatch(match.Groups[1].Value, guidMap);
                return replacement == null ? match.Value : "guid: " + replacement;
            });

            if (string.Equals(rewritten, text, StringComparison.Ordinal))
                return false;

            try
            {
                File.WriteAllText(prefabPath, rewritten);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VendorThirdParty] GUID rewrite failed for {prefabPath}: {ex.Message}");
                return false;
            }
        }

        private static string RemapThirdPartyGuid(string oldGuid, Dictionary<string, string> guidMap)
        {
            var oldPath = AssetDatabase.GUIDToAssetPath(oldGuid);
            if (string.IsNullOrEmpty(oldPath) ||
                !oldPath.StartsWith(ThirdPartyRoot, StringComparison.OrdinalIgnoreCase))
                return null;

            var oldName = Path.GetFileNameWithoutExtension(oldPath);
            return guidMap.TryGetValue(oldName, out var newGuid) ? newGuid : null;
        }

        private static string RemapBrokenGuid(string oldGuid, Dictionary<string, string> sharedByName)
        {
            var oldPath = AssetDatabase.GUIDToAssetPath(oldGuid);
            if (!string.IsNullOrEmpty(oldPath) && File.Exists(oldPath))
                return null;

            if (string.IsNullOrEmpty(oldPath))
                return null;

            var oldName = Path.GetFileNameWithoutExtension(oldPath);
            return sharedByName.TryGetValue(oldName, out var newGuid) ? newGuid : null;
        }

        private static Dictionary<string, string> BuildGuidLookup(string dir, string searchPattern)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            MergeGuidLookup(map, dir, searchPattern);
            return map;
        }

        private static void MergeGuidLookup(
            Dictionary<string, string> map,
            string dir,
            string searchPattern)
        {
            if (!Directory.Exists(dir))
                return;

            foreach (var file in Directory.GetFiles(dir, searchPattern, SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta"))
                    continue;

                var name = Path.GetFileNameWithoutExtension(file);
                var guid = AssetDatabase.AssetPathToGUID(file.Replace("\\", "/"));
                if (!string.IsNullOrEmpty(guid) && !map.ContainsKey(name))
                    map[name] = guid;
            }
        }
    }
}
#endif
