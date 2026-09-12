#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    /// Unified one-shot tool that copies third-party assets into 02_Shared/
    /// with full GUID remapping so 02_Shared/ is self-contained and the
    /// ThirdParty folder can be deleted independently.
    ///
    /// Menu: Tools → Organizing → Import Third-Party Assets
    /// </summary>
    public static class BuildingPrefabsReorganizationTool
    {
        private const string MenuPath = "Tools/Assets/Import Third-Party Assets";

        private const string SharedRoot = "Assets/02_Shared";
        private const string PrefabsRoot = SharedRoot + "/Prefabs";
        private const string MaterialsRoot = SharedRoot + "/Materials";
        private const string TexturesRoot = SharedRoot + "/Textures";
        private const string MeshesRoot = SharedRoot + "/Meshes";
        private const string PropsRoot = PrefabsRoot + "/Props";
        private const string BuildingRoot = PrefabsRoot + "/Building";
        private const string BuildingExtrasRoot = BuildingRoot + "/Building Extras";
        private const string ThirdPartyRoot = "Assets/03_ThirdParty";

        // ═══════════════════════════════════════════════════════════════════════
        // ── Pack Manifest ──────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        private struct PackEntry
        {
            /// <summary>Relative path under Assets/03_ThirdParty/.</summary>
            public string SourcePath;
            /// <summary>Destination category under Props/, or null for Building packs.</summary>
            public string Category;
            /// <summary>True if this pack's prefabs go to Building Extras instead of Props.</summary>
            public bool IsBuildingPack;
            /// <summary>True to skip BuiltIn/HDRP subfolders (standard packs).</summary>
            public bool StandardLayout;
        }

        /// <summary>
        /// All packs to import. Post_Apocalyptic_Asset_Pack is excluded —
        /// already correctly placed.
        /// </summary>
        private static readonly PackEntry[] Packs =
        {
            // ── Standard-layout packs (URP/Prefabs, URP/Materials, Mesh, Textures) ──
            new()
            {
                SourcePath = "12PropsPack",
                Category = "Exterior/AC_Units",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "BuildingPropsPack",
                Category = "Walls/Panels",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "ConstructionPropsBundle",
                Category = "Construction",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "ConstructionTruck",
                Category = "Vehicles/Wrecks",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "FabricPack",
                Category = "Furniture",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "FiresetPack",
                Category = "Industrial",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "FuseBoxPack",
                Category = "Industrial",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "LaddersPack",
                Category = "Industrial",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "LockersPack",
                Category = "Furniture",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "RacksPack",
                Category = "Industrial",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "RoadPropsPack",
                Category = "Road",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "ScaffoldsPack",
                Category = "Construction",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "ShipContainerPack",
                Category = "Industrial",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "SideWallTrailer",
                Category = "Vehicles/Wrecks",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "ToiletSet",
                Category = "Furniture",
                StandardLayout = true,
            },
            new()
            {
                SourcePath = "WorkbenchPack",
                Category = "Furniture",
                StandardLayout = true,
            },

            // ── Custom-layout packs ──────────────────────────────────────────
            new()
            {
                SourcePath = "Free Wood Door Pack",
                Category = "Doors",
                StandardLayout = false,
            },
            new()
            {
                SourcePath = "EasyRoads3D Assets",
                Category = null, // handled specially — preserves sub-categories
                StandardLayout = false,
            },

            // ── Building packs ────────────────────────────────────────────────
            new()
            {
                SourcePath = "GameReady3D/NYC_Buildings_Volume2/NYC_Building",
                Category = null,
                IsBuildingPack = true,
                StandardLayout = false,
            },
        };

        /// <summary>
        /// Props subfolders under 02_Shared for all four asset types.
        /// Created on-demand if missing.
        /// </summary>
        private static readonly string[] PropsSubfolders =
        {
            "Barricades",
            "Construction",
            "Decals/Concrete",
            "Decals/Garbage",
            "Decals/Graffiti",
            "Decals/Leaves",
            "Decals/Manhole",
            "Decals/Posters",
            "Doors",
            "Exterior/AC_Units",
            "Exterior/AirVents",
            "Exterior/Awnings",
            "Exterior/Balconies",
            "Exterior/Rooftop",
            "Furniture",
            "Industrial",
            "Nature",
            "PowerLines",
            "Road",
            "Signage/Billboards",
            "Signage/Misc",
            "Signage/Road",
            "Signage/ShopBoards",
            "Signage/Street",
            "Signage/Traffic",
            "StreetFurniture",
            "Vehicles/Parked",
            "Vehicles/Wrecks",
            "Walls/Joints",
            "Walls/Panels",
            "Walls/Supports",
            "Walls/WindowWalls",
            "Waste",
        };

        /// <summary>
        /// Additional subfolders for EasyRoads3D that map directly.
        /// </summary>
        private static readonly Dictionary<string, string> EasyRoadsCategoryMap = new()
        {
            ["Barriers"] = "Barricades",
            ["Fences"] = "Fences",
            ["Roads"] = "Road",
            ["Props"] = "StreetFurniture",
            ["Walls"] = "Walls/Panels",
            ["Water"] = "Nature",
            ["Tunnels"] = "Road",
            ["Bridges"] = "Road",
            ["RailRoad"] = "Road",
            ["Misc"] = "StreetFurniture",
        };

        // ═══════════════════════════════════════════════════════════════════════
        // ── Runner ─────────────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        [MenuItem(MenuPath, priority = -500)]
        private static void RunImport()
        {
            var log = new List<string>();
            var errors = new List<string>();
            var guidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            int totalPacks = Packs.Length;
            int currentPack = 0;
            int totalCopied = 0;
            int totalRemapped = 0;

            // ── 1. Create all destination folders ─────────────────────────────
            try
            {
                EditorUtility.DisplayProgressBar("Import Third-Party Assets",
                    "Creating destination folders…", 0f);
                CreateAllDestinationFolders(log, errors);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // ── 2. Copy pass: copy all assets + record GUIDs ──────────────────
            foreach (var pack in Packs)
            {
                currentPack++;
                var sourceFull = ThirdPartyRoot + "/" + pack.SourcePath;

                if (!AssetDatabase.IsValidFolder(sourceFull))
                {
                    errors.Add($"Pack not found: {sourceFull}");
                    continue;
                }

                var progressLabel = $"Copying {pack.SourcePath} ({currentPack}/{totalPacks})";
                var progress = (float)(currentPack - 1) / totalPacks;

                try
                {
                    EditorUtility.DisplayProgressBar("Import Third-Party Assets",
                        progressLabel, progress);
                }
                catch
                {
                    // Progress bar may throw in batch mode
                }

                int copied = CopyPack(pack, sourceFull, guidMap, log, errors);
                totalCopied += copied;
            }

            // ── 3. Refresh so AssetDatabase knows about new assets ─────────────
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            // ── 4. Remap pass: rewrite GUID references in copied assets ───────
            int remapped = RemapAllReferences(guidMap, log, errors);
            totalRemapped += remapped;

            // ── 5. Final refresh ───────────────────────────────────────────────
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            EditorUtility.ClearProgressBar();

            // ── Report ─────────────────────────────────────────────────────────
            Debug.Log("═══════════════════════════════════════════");
            Debug.Log(" Third-Party Asset Import — Done");
            Debug.Log("═══════════════════════════════════════════");
            Debug.Log($"  Packs processed: {currentPack}/{totalPacks}");
            Debug.Log($"  Assets copied:   {totalCopied}");
            Debug.Log($"  References remapped: {totalRemapped}");
            Debug.Log($"  GUID map entries: {guidMap.Count}");

            foreach (var entry in log)
                Debug.Log($"  ✓ {entry}");

            if (errors.Count > 0)
            {
                Debug.LogWarning("── Errors ──");
                foreach (var err in errors)
                    Debug.LogWarning($"  ⚠ {err}");
            }

            EditorUtility.DisplayDialog(
                "Third-Party Asset Import",
                $"Done. {totalCopied} assets copied, {totalRemapped} references remapped.\n" +
                $"{errors.Count} errors. See Console for details.",
                "OK");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Copy Pass ──────────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        private static int CopyPack(
            PackEntry pack,
            string sourceFull,
            Dictionary<string, string> guidMap,
            List<string> log,
            List<string> errors)
        {
            int copied = 0;

            if (pack.IsBuildingPack)
            {
                // ── Building pack: all content → Building Extras ──────────────
                copied += CopyBuildingPackFolder(sourceFull, BuildingExtrasRoot,
                    guidMap, log, errors);
            }
            else if (pack.SourcePath == "EasyRoads3D Assets")
            {
                // ── EasyRoads3D: categorized subfolders ───────────────────────
                copied += CopyEasyRoads3D(sourceFull, guidMap, log, errors);
            }
            else if (pack.StandardLayout)
            {
                // ── Standard pack: URP content → Props category ───────────────
                var urpRoot = sourceFull + "/URP";
                var meshRoot = sourceFull + "/Mesh";
                var texRoot = sourceFull + "/Textures";

                if (AssetDatabase.IsValidFolder(urpRoot))
                {
                    copied += CopyAllInFolder(urpRoot, PropsRoot, pack.Category,
                        guidMap, log, errors);
                }

                if (AssetDatabase.IsValidFolder(meshRoot))
                {
                    copied += CopyFolderFlat(meshRoot,
                        MeshesRoot + "/Props/" + pack.Category,
                        guidMap, log, errors);
                }

                if (AssetDatabase.IsValidFolder(texRoot))
                {
                    copied += CopyFolderFlat(texRoot,
                        TexturesRoot + "/Props/" + pack.Category,
                        guidMap, log, errors);
                }
            }
            else
            {
                // ── Custom pack (Free Wood Door Pack): walk everything ────────
                copied += CopyAllInFolder(sourceFull, PropsRoot, pack.Category,
                    guidMap, log, errors);
            }

            if (copied > 0)
                log.Add($"  {pack.SourcePath}: {copied} assets copied");

            return copied;
        }

        /// <summary>
        /// Recursively copies all assets from <paramref name="sourceFolder"/>
        /// into a destination structure, classifying prefabs by name.
        /// </summary>
        private static int CopyAllInFolder(
            string sourceFolder,
            string destPrefabRoot,
            string defaultCategory,
            Dictionary<string, string> guidMap,
            List<string> log,
            List<string> errors)
        {
            int copied = 0;
            var guids = AssetDatabase.FindAssets("", new[] { sourceFolder });

            foreach (var guid in guids)
            {
                var srcPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!File.Exists(srcPath))
                    continue; // skip folders

                var ext = Path.GetExtension(srcPath).ToLowerInvariant();
                var fileName = Path.GetFileName(srcPath);
                var sanitized = SanitizeFileName(fileName);

                // Determine destination
                string destDir;
                if (ext is ".prefab" or ".mat")
                {
                    var category = defaultCategory;
                    if (ext == ".prefab")
                    {
                        var classified = ClassifyPrefab(Path.GetFileNameWithoutExtension(fileName));
                        if (classified != null)
                            category = classified;
                    }

                    destDir = ext == ".prefab"
                        ? PrefabsRoot + "/Props/" + category
                        : MaterialsRoot + "/Props/" + category;
                }
                else if (ext is ".fbx" or ".obj" or ".blend")
                {
                    destDir = MeshesRoot + "/Props/" + defaultCategory;
                }
                else if (ext is ".png" or ".tga" or ".jpg" or ".jpeg" or ".psd" or ".tiff" or ".bmp" or ".exr" or ".hdr")
                {
                    destDir = TexturesRoot + "/Props/" + defaultCategory;
                }
                else
                {
                    // .meta, .cs, .shader, .mat, scene files — skip or handle
                    if (ext is ".shader" or ".shadergraph" or ".shadersubgraph" or ".hlsl" or ".cginc")
                    {
                        destDir = SharedRoot + "/Shaders";
                    }
                    else if (ext is ".cs")
                    {
                        destDir = SharedRoot + "/Scripts";
                    }
                    else
                    {
                        continue; // skip scenes, .meta, etc.
                    }
                }

                var destPath = destDir + "/" + sanitized;
                destPath = AssetDatabase.GenerateUniqueAssetPath(destPath);

                CreateFolderRecursive(destDir, log, errors);

                // Record old GUID before copy (copy changes it)
                var oldGuid = AssetDatabase.AssetPathToGUID(srcPath);

                if (!AssetDatabase.CopyAsset(srcPath, destPath))
                {
                    errors.Add($"Copy failed: {srcPath} → {destPath}");
                    continue;
                }

                var newGuid = AssetDatabase.AssetPathToGUID(destPath);
                if (!string.IsNullOrEmpty(oldGuid) && !string.IsNullOrEmpty(newGuid)
                    && !oldGuid.Equals(newGuid, StringComparison.OrdinalIgnoreCase))
                {
                    guidMap[oldGuid] = newGuid;
                }

                copied++;
            }

            return copied;
        }

        /// <summary>
        /// Copies all files from a flat source folder to a single destination.
        /// Used for Mesh/ and Textures/ subfolders of standard packs.
        /// </summary>
        private static int CopyFolderFlat(
            string sourceFolder,
            string destFolder,
            Dictionary<string, string> guidMap,
            List<string> log,
            List<string> errors)
        {
            int copied = 0;
            var guids = AssetDatabase.FindAssets("", new[] { sourceFolder });

            foreach (var guid in guids)
            {
                var srcPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!File.Exists(srcPath))
                    continue;

                var fileName = Path.GetFileName(srcPath);
                var sanitized = SanitizeFileName(fileName);
                var destPath = destFolder + "/" + sanitized;
                destPath = AssetDatabase.GenerateUniqueAssetPath(destPath);

                CreateFolderRecursive(destFolder, log, errors);

                var oldGuid = AssetDatabase.AssetPathToGUID(srcPath);

                if (!AssetDatabase.CopyAsset(srcPath, destPath))
                {
                    errors.Add($"Copy failed: {srcPath} → {destPath}");
                    continue;
                }

                var newGuid = AssetDatabase.AssetPathToGUID(destPath);
                if (!string.IsNullOrEmpty(oldGuid) && !string.IsNullOrEmpty(newGuid)
                    && !oldGuid.Equals(newGuid, StringComparison.OrdinalIgnoreCase))
                {
                    guidMap[oldGuid] = newGuid;
                }

                copied++;
            }

            return copied;
        }

        /// <summary>
        /// Copies all files from a source folder into a Building destination,
        /// used for NYC_Building pack.
        /// </summary>
        private static int CopyBuildingPackFolder(
            string sourceFolder,
            string prefabDest,
            Dictionary<string, string> guidMap,
            List<string> log,
            List<string> errors)
        {
            int copied = 0;
            var guids = AssetDatabase.FindAssets("", new[] { sourceFolder });

            foreach (var guid in guids)
            {
                var srcPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!File.Exists(srcPath))
                    continue;

                var ext = Path.GetExtension(srcPath).ToLowerInvariant();
                var fileName = SanitizeFileName(Path.GetFileName(srcPath));

                string destDir;
                if (ext == ".prefab")
                {
                    destDir = prefabDest;
                }
                else if (ext == ".mat")
                {
                    destDir = MaterialsRoot + "/Building";
                }
                else if (ext is ".fbx" or ".obj" or ".blend")
                {
                    destDir = MeshesRoot + "/Building";
                }
                else if (ext is ".png" or ".tga" or ".jpg" or ".jpeg" or ".psd" or ".tiff" or ".bmp" or ".exr" or ".hdr")
                {
                    destDir = TexturesRoot + "/Building";
                }
                else
                {
                    continue;
                }

                var destPath = destDir + "/" + fileName;
                destPath = AssetDatabase.GenerateUniqueAssetPath(destPath);

                CreateFolderRecursive(destDir, log, errors);

                var oldGuid = AssetDatabase.AssetPathToGUID(srcPath);

                if (!AssetDatabase.CopyAsset(srcPath, destPath))
                {
                    errors.Add($"Copy failed: {srcPath} → {destPath}");
                    continue;
                }

                var newGuid = AssetDatabase.AssetPathToGUID(destPath);
                if (!string.IsNullOrEmpty(oldGuid) && !string.IsNullOrEmpty(newGuid)
                    && !oldGuid.Equals(newGuid, StringComparison.OrdinalIgnoreCase))
                {
                    guidMap[oldGuid] = newGuid;
                }

                copied++;
            }

            return copied;
        }

        /// <summary>
        /// Handles EasyRoads3D Assets which are already categorized into
        /// subfolders (Barriers, Fences, Roads, Props, etc.).
        /// </summary>
        private static int CopyEasyRoads3D(
            string sourceFull,
            Dictionary<string, string> guidMap,
            List<string> log,
            List<string> errors)
        {
            int copied = 0;

            foreach (var kvp in EasyRoadsCategoryMap)
            {
                var subFolder = sourceFull + "/" + kvp.Key;
                if (!AssetDatabase.IsValidFolder(subFolder))
                    continue;

                var category = kvp.Value;
                copied += CopyAllInFolder(subFolder, PropsRoot, category, guidMap, log, errors);
            }

            // Copy Materials and Textures if they exist
            var matFolder = sourceFull + "/Materials";
            if (AssetDatabase.IsValidFolder(matFolder))
            {
                copied += CopyFolderFlat(matFolder,
                    MaterialsRoot + "/Props/StreetFurniture",
                    guidMap, log, errors);
            }

            var texFolder = sourceFull + "/Textures";
            if (AssetDatabase.IsValidFolder(texFolder))
            {
                copied += CopyFolderFlat(texFolder,
                    TexturesRoot + "/Props/StreetFurniture",
                    guidMap, log, errors);
            }

            // Shaders
            var shaderFolder = sourceFull + "/Shaders";
            if (AssetDatabase.IsValidFolder(shaderFolder))
            {
                copied += CopyFolderFlat(shaderFolder,
                    SharedRoot + "/Shaders",
                    guidMap, log, errors);
            }

            return copied;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── GUID Remap Pass ────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Regex that matches a Unity YAML GUID reference: "guid: <32-char-hex>".
        /// Captures the full hex string.
        /// </summary>
        private static readonly Regex GuidRefRegex = new(
            @"guid:\s*([a-fA-F0-9]{32})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Walks every copied prefab, material, controller, and anim file
        /// and replaces old ThirdParty GUIDs with their new 02_Shared GUIDs.
        /// Returns the number of files modified.
        /// </summary>
        private static int RemapAllReferences(
            Dictionary<string, string> guidMap,
            List<string> log,
            List<string> errors)
        {
            if (guidMap.Count == 0)
                return 0;

            int remapped = 0;

            // Collect all text-based assets under 02_Shared that we just copied.
            // Dynamically scan every subdirectory of PrefabsRoot so newly added
            // categories (Industrial, Construction, Vehicles, etc.) are always
            // included in GUID remapping without requiring manual updates.
            var scanFolders = new List<string>();

            // All Prefabs subdirectories
            if (AssetDatabase.IsValidFolder(PrefabsRoot))
            {
                foreach (var sub in AssetDatabase.GetSubFolders(PrefabsRoot))
                    scanFolders.Add(sub);
            }

            // Materials root (all subdirectories)
            if (AssetDatabase.IsValidFolder(MaterialsRoot))
            {
                scanFolders.Add(MaterialsRoot);
            }

            // Shaders
            var shadersPath = SharedRoot + "/Shaders";
            if (AssetDatabase.IsValidFolder(shadersPath))
                scanFolders.Add(shadersPath);

            var allGuids = AssetDatabase.FindAssets("", scanFolders.ToArray());

            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var assetGuid in allGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (processed.Contains(path))
                    continue;
                processed.Add(path);

                var ext = Path.GetExtension(path).ToLowerInvariant();

                // Only text-serialized assets can have embedded GUID references
                if (ext is not (".prefab" or ".mat" or ".asset" or ".controller"
                    or ".anim" or ".overrideController" or ".physicMaterial"
                    or ".physicsMaterial2D" or ".preset"))
                    continue;

                if (!File.Exists(path))
                    continue;

                try
                {
                    var text = File.ReadAllText(path);
                    var modified = false;

                    text = GuidRefRegex.Replace(text, match =>
                    {
                        var oldGuid = match.Groups[1].Value;
                        if (guidMap.TryGetValue(oldGuid, out var newGuid))
                        {
                            modified = true;
                            return "guid: " + newGuid;
                        }

                        return match.Value;
                    });

                    if (!modified)
                        continue;

                    File.WriteAllText(path, text);
                    remapped++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Remap failed for {path}: {ex.Message}");
                }
            }

            if (remapped > 0)
                log.Add($"  GUID references remapped in {remapped} files");

            return remapped;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Folder Setup ───────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        private static void CreateAllDestinationFolders(List<string> log, List<string> errors)
        {
            // Props subfolders under all four roots
            foreach (var root in new[] { PrefabsRoot + "/Props", MaterialsRoot + "/Props",
                         TexturesRoot + "/Props", MeshesRoot + "/Props" })
            {
                foreach (var sub in PropsSubfolders)
                {
                    CreateFolderRecursive(root + "/" + sub, log, errors);
                }
            }

            // Building folders
            CreateFolderRecursive(BuildingExtrasRoot, log, errors);
            CreateFolderRecursive(MaterialsRoot + "/Building", log, errors);
            CreateFolderRecursive(TexturesRoot + "/Building", log, errors);
            CreateFolderRecursive(MeshesRoot + "/Building", log, errors);

            // Shaders and Scripts
            CreateFolderRecursive(SharedRoot + "/Shaders", log, errors);
            CreateFolderRecursive(SharedRoot + "/Scripts", log, errors);

            // EasyRoads3D extra categories
            CreateFolderRecursive(PrefabsRoot + "/Fences", log, errors);
            CreateFolderRecursive(MaterialsRoot + "/Fences", log, errors);
            CreateFolderRecursive(TexturesRoot + "/Fences", log, errors);
            CreateFolderRecursive(MeshesRoot + "/Fences", log, errors);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Classification ─────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the destination sub-path under Props for a prefab with the
        /// given file name (no extension), or null if the prefab should stay
        /// where it is.
        /// </summary>
        private static string ClassifyPrefab(string prefabNameNoExt)
        {
            // ── Decals ─────────────────────────────────────────────────────────
            if (prefabNameNoExt.Contains("_Decals") || prefabNameNoExt.Contains("Decals_"))
            {
                if (prefabNameNoExt.StartsWith("Concrete"))
                    return "Decals/Concrete";
                if (prefabNameNoExt.Contains("Graffiti"))
                    return "Decals/Graffiti";
                if (prefabNameNoExt.Contains("Leaves"))
                    return "Decals/Leaves";
                if (prefabNameNoExt.Contains("Manhole"))
                    return "Decals/Manhole";
                if (prefabNameNoExt.Contains("Torn_Poster") || prefabNameNoExt.Contains("Poster"))
                    return "Decals/Posters";
                return "Decals/Garbage";
            }

            // ── Exterior attachments ───────────────────────────────────────────
            if (prefabNameNoExt.StartsWith("SM_AC_")
                || prefabNameNoExt.StartsWith("SM_Hvac")
                || prefabNameNoExt.Contains("AirCond")
                || prefabNameNoExt.Contains("AirCooler")
                || prefabNameNoExt.StartsWith("SM_WallConditioner"))
                return "Exterior/AC_Units";

            if (prefabNameNoExt.StartsWith("SM_Air_Vent")
                || prefabNameNoExt.Contains("Ventilation")
                || prefabNameNoExt.Equals("SM_Ventilation"))
                return "Exterior/AirVents";

            if (prefabNameNoExt.StartsWith("SM_Balcony"))
                return "Exterior/Balconies";

            if (prefabNameNoExt.Contains("RoofFan")
                || prefabNameNoExt.Contains("SolarPanel"))
                return "Exterior/Rooftop";

            // ── Signage ────────────────────────────────────────────────────────
            if (prefabNameNoExt.Contains("BillBoard") || prefabNameNoExt.Contains("Billboard"))
                return "Signage/Billboards";

            if (prefabNameNoExt.Contains("RoadSign") || prefabNameNoExt.Contains("Road_Sign"))
                return "Signage/Road";

            if (prefabNameNoExt.Contains("Street_Sign") || prefabNameNoExt.Contains("StreetSign"))
                return "Signage/Street";

            if (prefabNameNoExt.Contains("Traffic_Sign")
                || prefabNameNoExt.Contains("TrafficSign")
                || prefabNameNoExt.Contains("Traffic_Signal"))
                return "Signage/Traffic";

            if (prefabNameNoExt.Contains("WetFloorSign")
                || prefabNameNoExt.Contains("exitSign")
                || prefabNameNoExt.Contains("sharpTurnSign")
                || prefabNameNoExt.Contains("crosswalk")
                || prefabNameNoExt.Contains("Crosswalk"))
                return "Signage/Misc";

            // ── Shop boards / awnings / shades ─────────────────────────────────
            if (prefabNameNoExt.StartsWith("SM_Shop_Board"))
                return "Signage/ShopBoards";

            if (prefabNameNoExt.StartsWith("SM_Shop_Awning")
                || prefabNameNoExt.StartsWith("SM_Shop_Shade"))
                return "Exterior/Awnings";

            // ── Street furniture ───────────────────────────────────────────────
            if (prefabNameNoExt.Contains("Bench")
                && !prefabNameNoExt.Contains("Workbench")
                && !prefabNameNoExt.Contains("BenchGrinder"))
                return "StreetFurniture";

            if (prefabNameNoExt.Contains("Fire_Hydrant")
                || prefabNameNoExt.Contains("FireHydrant"))
                return "StreetFurniture";

            if (prefabNameNoExt.Contains("Mailbox"))
                return "StreetFurniture";

            if (prefabNameNoExt.StartsWith("SM_Umbrella"))
                return "StreetFurniture";

            if (prefabNameNoExt.Contains("PhoneKiosk"))
                return "StreetFurniture";

            // ── Vehicles ───────────────────────────────────────────────────────
            if (prefabNameNoExt.Contains("ParkedCar") || prefabNameNoExt.Contains("Sedan"))
                return "Vehicles/Parked";

            if (prefabNameNoExt.Contains("WreckedCar") || prefabNameNoExt.Contains("Wreck"))
                return "Vehicles/Wrecks";

            if (prefabNameNoExt.Contains("CarCrane")
                || prefabNameNoExt.Contains("GardenTractor")
                || prefabNameNoExt.Contains("ConstructionTruck"))
                return "Vehicles/Wrecks";

            if (prefabNameNoExt.StartsWith("SM_Tram"))
                return "Vehicles/Wrecks";

            // ── Waste ──────────────────────────────────────────────────────────
            if (prefabNameNoExt.Contains("Dumpster")
                || prefabNameNoExt.Contains("Dustbin")
                || prefabNameNoExt.Contains("wastebin")
                || prefabNameNoExt.Contains("Wastebin")
                || prefabNameNoExt.StartsWith("SM_TrashBin")
                || prefabNameNoExt.StartsWith("SM_TrashCan")
                || prefabNameNoExt.Contains("TrashBin")
                || prefabNameNoExt.Contains("TrashCan")
                || prefabNameNoExt.Contains("Skip_"))
                return "Waste";

            if (prefabNameNoExt.Contains("Metal_wastebin"))
                return "Waste";

            // ── Furniture ──────────────────────────────────────────────────────
            if (prefabNameNoExt.StartsWith("SM_Chair")
                || prefabNameNoExt.StartsWith("Chair_")
                || prefabNameNoExt.StartsWith("SM_table")
                || prefabNameNoExt.StartsWith("Table01")
                || prefabNameNoExt.StartsWith("Table02")
                || prefabNameNoExt.Contains("WoodenTable")
                || prefabNameNoExt.StartsWith("SM_Desk")
                || prefabNameNoExt.StartsWith("SM_WoodenBox"))
                return "Furniture";

            if (prefabNameNoExt.StartsWith("SM_Locker")
                || prefabNameNoExt.StartsWith("Locker"))
                return "Furniture";

            if (prefabNameNoExt.Contains("Cardboard"))
                return "Furniture";

            if (prefabNameNoExt.StartsWith("Shelf")
                || prefabNameNoExt.StartsWith("SM_Shelf"))
                return "Furniture";

            // ── PowerLines ─────────────────────────────────────────────────────
            if (prefabNameNoExt.Contains("UtilityPole")
                || prefabNameNoExt.StartsWith("SM_Electricpost"))
                return "PowerLines";

            // ── Nature ─────────────────────────────────────────────────────────
            if (prefabNameNoExt.StartsWith("SM_Tree")
                || prefabNameNoExt.StartsWith("SM_Bush")
                || prefabNameNoExt.StartsWith("SM_Grass")
                || prefabNameNoExt.StartsWith("SM_Pine_Tree")
                || prefabNameNoExt.StartsWith("SM_Trees_"))
                return "Nature";

            // ── Road items ─────────────────────────────────────────────────────
            if (prefabNameNoExt.Contains("Traffic_Barrel")
                || prefabNameNoExt.Contains("TrafficBarrel")
                || prefabNameNoExt.Contains("Road_Barricade")
                || prefabNameNoExt.Contains("RoadBarricade"))
                return "Road";

            if (prefabNameNoExt == "SM_Road"
                || prefabNameNoExt == "SM_Road_Curve"
                || prefabNameNoExt == "SM_Road_junction")
                return "Road";

            // ── Barricades ─────────────────────────────────────────────────────
            if (prefabNameNoExt.Contains("Barricade")
                && !prefabNameNoExt.Contains("Road_Barricade"))
                return "Barricades";

            if (prefabNameNoExt.Contains("SandBag"))
                return "Barricades";

            // ── Doors ──────────────────────────────────────────────────────────
            if (prefabNameNoExt.Contains("Door")
                && !prefabNameNoExt.Contains("Outdoor")
                && !prefabNameNoExt.StartsWith("SM_Shop_Door")
                && !prefabNameNoExt.StartsWith("SM_StorefrontDoor")
                && !prefabNameNoExt.StartsWith("SM_DoorWall"))
                return "Doors";

            // ── Walls (wall joints, supports, panels) ──────────────────────────
            if (prefabNameNoExt.StartsWith("SM_Wall_Joint"))
                return "Walls/Joints";

            if (prefabNameNoExt.StartsWith("SM_Wall_Support")
                || prefabNameNoExt.StartsWith("SM_Wall_Pillar"))
                return "Walls/Supports";

            if (prefabNameNoExt.StartsWith("SM_Wall_Dec"))
                return "Walls/Panels";

            if (prefabNameNoExt.StartsWith("SM_Bigwall_")
                || prefabNameNoExt.StartsWith("SM_Wall_")
                && !prefabNameNoExt.Contains("Window")
                && !prefabNameNoExt.Contains("Joint")
                && !prefabNameNoExt.Contains("Support")
                && !prefabNameNoExt.Contains("Pillar")
                && !prefabNameNoExt.Contains("Dec")
                && !prefabNameNoExt.Contains("AC_"))
                return "Walls/Panels";

            if (prefabNameNoExt.StartsWith("SM_WindowWall")
                || prefabNameNoExt.StartsWith("SM_Wall_Window")
                || prefabNameNoExt.StartsWith("SM_Wall_AWindow"))
                return "Walls/WindowWalls";

            // ── Misc props ─────────────────────────────────────────────────────
            if (prefabNameNoExt.StartsWith("SM_Kiosk"))
                return "StreetFurniture";

            if (prefabNameNoExt.StartsWith("SM_Med_Table"))
                return "Furniture";

            return null; // not classified — leave with pack-default category
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ── Helpers ────────────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════════

        private static void CreateFolderRecursive(string path, List<string> log, List<string> errors)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (parent != null && !AssetDatabase.IsValidFolder(parent))
                CreateFolderRecursive(parent, log, errors);

            if (AssetDatabase.IsValidFolder(path))
                return;

            var name = Path.GetFileName(path);
            var guid = AssetDatabase.CreateFolder(parent!, name);
            if (!string.IsNullOrEmpty(guid))
            {
                // Quiet — too noisy for hundreds of subfolders
            }
            else
            {
                errors.Add($"Failed to create folder: {path}");
            }
        }

        /// <summary>
        /// Cleans up filename artifacts: trailing spaces, double dots, etc.
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return fileName;

            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);

            return nameWithoutExt.Trim() + ext.Trim();
        }
    }
}
#endif
