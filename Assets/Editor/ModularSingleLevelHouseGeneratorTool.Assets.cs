#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    public static partial class ModularSingleLevelHouseGeneratorTool
    {
        private static string AssetPathToAbsolute(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/');
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return assetPath;

            var tail = assetPath.Length > "Assets/".Length ? assetPath["Assets/".Length..] : string.Empty;
            return Path.Combine(Application.dataPath, tail);
        }

        internal static string CombinePrefabPath(string folder, string fileName)
        {
            return $"{folder.TrimEnd('/', '\\')}/{fileName}";
        }

        private static string ResolveFileName(string value, string defaultFileName)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultFileName : value.Trim();
        }

        /// <summary>
        ///     Resolves a door source path. If it points to a specific .prefab, returns that path.
        ///     If it's a folder, recursively collects all .prefab files and picks one randomly.
        ///     Returns null if the path is empty or no prefabs are found.
        /// </summary>
        private static string ResolveDoorSource(string sourcePath, GenerationRandom random)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return null;

            var trimmed = sourcePath.Trim();

            // Specific prefab path
            if (trimmed.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return trimmed;

            // Folder: collect all .prefab files recursively
            var prefabs = new List<string>();
            CollectPrefabsInFolder(trimmed, prefabs);
            if (prefabs.Count == 0)
                return null;

            return prefabs[random.RangeExclusive(0, prefabs.Count)];
        }

        private static void CollectPrefabsInFolder(string folderPath, List<string> results)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
                return;

            var assetGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            foreach (var guid in assetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    results.Add(path);
            }
        }

        private static GameObject LoadPrefab(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            assetFolderPath = assetFolderPath.Replace('\\', '/');
            var parts = assetFolderPath.Split('/');
            if (parts.Length < 2 || parts[0] != "Assets")
                return;

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private static int NextRandomBuildingNumber(
            string outputFolder, string categoryPrefix, string footprintSubFolder, GenerationRandom random)
        {
            var folder = string.IsNullOrWhiteSpace(outputFolder) ? DefaultOutputFolder : outputFolder;
            folder = folder.TrimEnd('/', '\\').Replace('\\', '/');
            categoryPrefix = string.IsNullOrWhiteSpace(categoryPrefix) ? "Building" : categoryPrefix.Trim();

            var subFolder = string.IsNullOrWhiteSpace(footprintSubFolder)
                ? categoryPrefix
                : $"{categoryPrefix}/{footprintSubFolder.Trim('/')}";

            for (var attempt = 0; attempt < 64; attempt++)
            {
                var number = random.RangeExclusive(100000, 1000000);
                var relative = $"{folder}/{subFolder}/{categoryPrefix}_{number}.prefab";
                if (!File.Exists(AssetPathToAbsolute(relative)))
                    return number;
            }

            return random.RangeExclusive(100000, 1000000);
        }

        private static bool TryResolvePrefabDependencies(GenerationContext context, out string errorMessage)
        {
            var useFloorAsRoof = PopulatePrefabPaths(context);
            LoadPrefabDependencies(context.Prefabs, useFloorAsRoof);
            return TryValidatePrefabDependencies(context, out errorMessage);
        }

        private static bool PopulatePrefabPaths(GenerationContext context)
        {
            var settings = context.Settings;
            var prefabs = context.Prefabs;
            var kit = settings.KitConfig;
            var kf = context.KitFolder;

            prefabs.FoundationPath  = ResolveKitPartPath(kit, kf, kit?.foundationPrefab,   "Foundation.prefab");
            prefabs.FloorPath       = ResolveKitPartPath(kit, kf, kit?.floorPrefab,        "Floor.prefab");
            prefabs.WallPath        = ResolveKitPartPath(kit, kf, kit?.wallPrefab,         "Wall.prefab");
            prefabs.DoorwayPath     = ResolveKitPartPath(kit, kf, kit?.doorwayPrefab,      "Doorway.prefab");
            prefabs.WindowPath      = ResolveKitPartPath(kit, kf, kit?.windowPrefab,       "Window.prefab");
            prefabs.ShopGlassFullPath    = ResolveKitPartPath(kit, kf, kit?.shopGlassFullPrefab,    "ShopGlass_Full.prefab");
            prefabs.ShopGlassCapLeftPath = ResolveKitPartPath(kit, kf, kit?.shopGlassCapLeftPrefab, "ShopGlass_CapLeft.prefab");
            prefabs.ShopGlassCapRightPath = ResolveKitPartPath(kit, kf, kit?.shopGlassCapRightPrefab, "ShopGlass_CapRight.prefab");
            prefabs.ShopGlassCapBothPath = ResolveKitPartPath(kit, kf, kit?.shopGlassCapBothPrefab, "ShopGlass_CapBoth.prefab");

            PopulateDoorPaths(settings, kit, prefabs, context.Random);
            PopulateKitSlotPaths(settings, kit, prefabs, kf);

            prefabs.RoofRidgePath       = ResolveKitPartPath(kit, kf, kit?.roofRidgePrefab,       "Roof_Ridge.prefab");
            prefabs.RoofPanelPath       = ResolveKitPartPath(kit, kf, kit?.roofPanelPrefab,       "Roof_Panel.prefab");
            prefabs.RoofGablePath       = ResolveKitPartPath(kit, kf, kit?.roofGablePrefab,       "Roof_Gable.prefab");
            prefabs.WindowClosedPath    = ResolveKitPartPath(kit, kf, kit?.windowClosedPrefab,    "Window_Modular_Closed.prefab");
            prefabs.WindowMouldingPath  = ResolveKitPartPath(kit, kf, kit?.windowMouldingPrefab,  "Window_Moulding.prefab");
            prefabs.Gutter3mPath        = ResolveKitPartPath(kit, kf, kit?.gutter3mPrefab,        "Gutter_3m.prefab");
            prefabs.GutterBracketsPath  = ResolveKitPartPath(kit, kf, kit?.gutterBracketsPrefab,  "Gutter_Brackets_Fused.prefab");
            prefabs.DownpipePath        = ResolveKitPartPath(kit, kf, kit?.downpipePrefab,        "Downpipe_3m.prefab");

            var roofPrefab = kit?.roofPrefab ?? settings.RoofPrefab;
            return roofPrefab == null && string.IsNullOrWhiteSpace(settings.RoofPrefabPath);
        }

        private static string ResolveKitPartPath(BuildingKitConfig kit, string kitFolder, GameObject prefabRef, string hardcodedFileName)
        {
            if (kit != null)
                return kit.ResolveKitPartPath(kitFolder, prefabRef, hardcodedFileName);

            return CombinePrefabPath(kitFolder, hardcodedFileName);
        }

        private static void PopulateDoorPaths(GeneratorSettings settings, BuildingKitConfig kit, PrefabDependencies prefabs, GenerationRandom random)
        {
            prefabs.ExteriorDoorPath = ResolveOneDoorPath(settings.ExteriorDoorPrefab, kit?.exteriorDoorSourceFolder, settings.ExteriorDoorSourcePath, random);
            if (prefabs.ExteriorDoorPath == null)
                Debug.Log("[ModularSingleLevelHouseGeneratorTool] No exterior door source set — using kit Doorway.prefab");

            var intSource = kit != null && !string.IsNullOrWhiteSpace(kit.interiorDoorSourceFolder)
                ? kit.interiorDoorSourceFolder
                : settings.InteriorDoorSourcePath;
            if (string.IsNullOrWhiteSpace(intSource))
                intSource = settings.ExteriorDoorSourcePath;

            prefabs.InteriorDoorPath = ResolveOneDoorPath(settings.InteriorDoorPrefab, intSource, null, random);
            if (prefabs.InteriorDoorPath == null)
                Debug.Log("[ModularSingleLevelHouseGeneratorTool] No interior door source set — using kit Doorway.prefab");
        }

        private static string ResolveOneDoorPath(GameObject prefabSlot, string folderSource, string legacyPath, GenerationRandom random)
        {
            if (prefabSlot != null)
                return AssetDatabase.GetAssetPath(prefabSlot);

            var source = !string.IsNullOrWhiteSpace(folderSource) ? folderSource : legacyPath;
            return ResolveDoorSource(source, random);
        }

        private static void PopulateKitSlotPaths(GeneratorSettings settings, BuildingKitConfig kit, PrefabDependencies prefabs, string kitFolder)
        {
            var stairPrefab = kit?.stairPrefab ?? settings.StairPrefab;
            var roofPrefab = kit?.roofPrefab ?? settings.RoofPrefab;

            // Stair: slot first, fall back to kit filename
            if (stairPrefab != null)
            {
                prefabs.StairPath = AssetDatabase.GetAssetPath(stairPrefab);
            }
            else
            {
                var fileName = ResolveFileName(settings.StairPrefabFileName, "Stairs.prefab");
                prefabs.StairPath = ResolveKitPartPath(kit, kitFolder, kit?.stairPrefab, fileName);
            }

            // Upper floor: slot first, fall back to kit filename
            if (settings.UpperFloorPrefab != null)
            {
                prefabs.UpperFloorPath = AssetDatabase.GetAssetPath(settings.UpperFloorPrefab);
            }
            else if (string.IsNullOrWhiteSpace(settings.UpperFloorPrefabFileName))
            {
                prefabs.UpperFloorPath = null;
            }
            else
            {
                prefabs.UpperFloorPath = CombinePrefabPath(kitFolder, settings.UpperFloorPrefabFileName.Trim());
            }

            // Roof: slot first, fall back to kit filename
            if (roofPrefab != null)
            {
                prefabs.RoofPath = AssetDatabase.GetAssetPath(roofPrefab);
            }
            else if (string.IsNullOrWhiteSpace(settings.RoofPrefabPath))
            {
                prefabs.RoofPath = ResolveKitPartPath(kit, kitFolder, kit?.roofPrefab, "Roof.prefab");
            }
            else
            {
                prefabs.RoofPath = settings.RoofPrefabPath.Trim();
            }

            // Exterior stairs: slot first, fall back to legacy path
            if (settings.ExteriorStairsPrefab != null)
            {
                prefabs.ExteriorStairsPath = AssetDatabase.GetAssetPath(settings.ExteriorStairsPrefab);
            }
            else if (string.IsNullOrWhiteSpace(settings.ExteriorStairsPrefabPath))
            {
                prefabs.ExteriorStairsPath = null;
            }
            else
            {
                prefabs.ExteriorStairsPath = settings.ExteriorStairsPrefabPath.Trim();
            }
        }

        private static void LoadPrefabDependencies(PrefabDependencies prefabs, bool useFloorAsRoof)
        {
            prefabs.Foundation = LoadPrefab(prefabs.FoundationPath);
            prefabs.Floor = LoadPrefab(prefabs.FloorPath);
            prefabs.Wall = LoadPrefab(prefabs.WallPath);
            prefabs.Doorway = LoadPrefab(prefabs.DoorwayPath);
            prefabs.ExteriorDoor = prefabs.ExteriorDoorPath != null ? LoadPrefab(prefabs.ExteriorDoorPath) : null;
            prefabs.InteriorDoor = prefabs.InteriorDoorPath != null ? LoadPrefab(prefabs.InteriorDoorPath) : null;
            prefabs.Window = LoadPrefab(prefabs.WindowPath);
            prefabs.ShopGlassFull = LoadPrefab(prefabs.ShopGlassFullPath);
            prefabs.ShopGlassCapLeft = LoadPrefab(prefabs.ShopGlassCapLeftPath);
            prefabs.ShopGlassCapRight = LoadPrefab(prefabs.ShopGlassCapRightPath);
            prefabs.ShopGlassCapBoth = LoadPrefab(prefabs.ShopGlassCapBothPath);
            prefabs.Stair = LoadPrefab(prefabs.StairPath);
            prefabs.UpperFloor = prefabs.UpperFloorPath != null ? LoadPrefab(prefabs.UpperFloorPath) : null;
            prefabs.Roof = useFloorAsRoof ? prefabs.Floor : LoadPrefab(prefabs.RoofPath);
            prefabs.RoofRidge = LoadPrefab(prefabs.RoofRidgePath);
            prefabs.RoofPanel = LoadPrefab(prefabs.RoofPanelPath);
            prefabs.RoofGable = LoadPrefab(prefabs.RoofGablePath);
            prefabs.ExteriorStairs = prefabs.ExteriorStairsPath != null ? LoadPrefab(prefabs.ExteriorStairsPath) : null;
            prefabs.WindowClosed = LoadPrefab(prefabs.WindowClosedPath);
            prefabs.WindowMoulding = LoadPrefab(prefabs.WindowMouldingPath);
            prefabs.Gutter3m = LoadPrefab(prefabs.Gutter3mPath);
            prefabs.GutterBrackets = LoadPrefab(prefabs.GutterBracketsPath);
            prefabs.Downpipe = LoadPrefab(prefabs.DownpipePath);
        }

        private static bool TryValidatePrefabDependencies(GenerationContext context, out string errorMessage)
        {
            var settings = context.Settings;
            var prefabs = context.Prefabs;

            var missing = new System.Collections.Generic.List<string>();
            if (prefabs.Foundation == null) missing.Add(prefabs.FoundationPath);
            if (prefabs.Floor == null) missing.Add(prefabs.FloorPath);
            if (prefabs.Wall == null) missing.Add(prefabs.WallPath);
            if (prefabs.Doorway == null) missing.Add(prefabs.DoorwayPath);
            // Roof: require either kit-of-parts (Ridge+Panel) or a single Roof tile, not both
            var hasKitRoof = prefabs.RoofRidge != null || prefabs.RoofPanel != null;
            if (!hasKitRoof && prefabs.Roof == null)
                missing.Add(prefabs.RoofPath);

            if (missing.Count > 0)
            {
                errorMessage = "Missing prefabs:\n- " + string.Join("\n- ", missing);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(settings.UpperFloorPrefabFileName) && prefabs.UpperFloor == null)
            {
                errorMessage = "Upper floor prefab not found:\n- " + prefabs.UpperFloorPath;
                return false;
            }

            if (context.Plan.NeedsStairs && prefabs.Stair == null)
            {
                errorMessage = "Multi-story building requires stair prefab:\n- " + prefabs.StairPath;
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
#endif
