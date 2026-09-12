#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    internal static class ModularSingleLevelHouseGeneratorToolQuickValidation
    {
        private const string ValidationMenuPath =
            "Tools/Build/Mod Kits/Building Generator/Validate Modular Building Generator (Quick)";
        private const string ValidationRootFolder = "Assets/__Temp_ModularBuildingValidation";

        [MenuItem(ValidationMenuPath, priority = -500)]
        private static void RunQuickValidation()
        {
            var failures = new List<string>();
            try
            {
                RecreateFolder(ValidationRootFolder);

                if (!ValidateDeterministicNameAndLayout(out var deterministicError))
                    failures.Add(deterministicError);
                if (!ValidateMultiFloorStairRequirement(out var stairError))
                    failures.Add(stairError);
                if (!ValidateDoorwayBounds(out var doorwayError))
                    failures.Add(doorwayError);
                if (!ValidateGeneratedHierarchySections(out var hierarchyError))
                    failures.Add(hierarchyError);
                if (!ValidateCategoryNaming(out var categoryError))
                    failures.Add(categoryError);
                if (!ValidateSkyscraperShrink(out var skyscraperError))
                    failures.Add(skyscraperError);
            }
            finally
            {
                AssetDatabase.DeleteAsset(ValidationRootFolder);
                AssetDatabase.Refresh();
            }

            if (failures.Count == 0)
            {
                Debug.Log("[ModularSingleLevelHouseGeneratorTool][Validation] Quick validation passed.");
                EditorUtility.DisplayDialog("Modular House Generator Validation",
                    "Quick validation passed.", "OK");
                return;
            }

            var report = string.Join("\n- ", failures);
            Debug.LogError($"[ModularSingleLevelHouseGeneratorTool][Validation] Failed:\n- {report}");
            EditorUtility.DisplayDialog("Modular House Generator Validation",
                "Quick validation failed:\n- " + report, "OK");
        }

        private static bool ValidateDeterministicNameAndLayout(out string error)
        {
            var folderA = $"{ValidationRootFolder}/RunA";
            var folderB = $"{ValidationRootFolder}/RunB";
            RecreateFolder(folderA);
            RecreateFolder(folderB);

            var settings = CreateBaseSettings();
            settings.OutputFolder = folderA;
            var pathA = ModularSingleLevelHouseGeneratorTool.Generate(settings);
            if (string.IsNullOrWhiteSpace(pathA))
            {
                error = "Deterministic check failed: first generation returned null.";
                return false;
            }

            settings.OutputFolder = folderB;
            var pathB = ModularSingleLevelHouseGeneratorTool.Generate(settings);
            if (string.IsNullOrWhiteSpace(pathB))
            {
                error = "Deterministic check failed: second generation returned null.";
                return false;
            }

            var nameA = System.IO.Path.GetFileNameWithoutExtension(pathA);
            var nameB = System.IO.Path.GetFileNameWithoutExtension(pathB);
            if (!string.Equals(nameA, nameB, StringComparison.Ordinal))
            {
                error = $"Deterministic check failed: output names differ ('{nameA}' vs '{nameB}').";
                return false;
            }

            var prefabA = AssetDatabase.LoadAssetAtPath<GameObject>(pathA);
            var prefabB = AssetDatabase.LoadAssetAtPath<GameObject>(pathB);
            if (prefabA == null || prefabB == null)
            {
                error = "Deterministic check failed: could not load generated prefab assets.";
                return false;
            }

            var sigA = BuildPrefabSignature(prefabA);
            var sigB = BuildPrefabSignature(prefabB);
            if (!string.Equals(sigA, sigB, StringComparison.Ordinal))
            {
                error = "Deterministic check failed: layout signature mismatch for same fixed seed.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateMultiFloorStairRequirement(out string error)
        {
            var settings = CreateBaseSettings();
            settings.OutputFolder = $"{ValidationRootFolder}/MissingStair";
            settings.FloorCount = 2;
            settings.StairPrefabFileName = "DefinitelyMissing_Stair.prefab";

            var path = ModularSingleLevelHouseGeneratorTool.Generate(settings);
            if (!string.IsNullOrWhiteSpace(path))
            {
                error = "Stair requirement check failed: generation succeeded without a valid stair prefab on multi-floor config.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateDoorwayBounds(out string error)
        {
            var settings = CreateBaseSettings();
            settings.OutputFolder = $"{ValidationRootFolder}/DoorwayBounds";
            settings.FixedWidthCells = 2;
            settings.FixedDepthCells = 2;
            settings.FloorCount = 1;
            settings.GroundDoorCount = 999;
            settings.MinRoomsPerFloor = 0;
            settings.MaxRoomsPerFloor = 0;
            settings.RoomCount = 0;

            var path = ModularSingleLevelHouseGeneratorTool.Generate(settings);
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Doorway bounds check failed: generation returned null.";
                return false;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                error = "Doorway bounds check failed: could not load generated prefab.";
                return false;
            }

            var walls = prefab.transform.Find("Walls");
            if (walls == null)
            {
                error = "Doorway bounds check failed: missing Walls section.";
                return false;
            }

            var doorwayCount = 0;
            for (var i = 0; i < walls.childCount; i++)
            {
                var childName = walls.GetChild(i).name;
                if (childName.StartsWith("Wall_L0_", StringComparison.Ordinal)
                    && childName.EndsWith("_Door", StringComparison.Ordinal))
                    doorwayCount++;
            }

            var maxDoorways = 2 * settings.FixedWidthCells + 2 * settings.FixedDepthCells;
            if (doorwayCount > maxDoorways)
            {
                error =
                    $"Doorway bounds check failed: doorway count {doorwayCount} exceeds max perimeter segments {maxDoorways}.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateGeneratedHierarchySections(out string error)
        {
            var settings = CreateBaseSettings();
            settings.OutputFolder = $"{ValidationRootFolder}/Hierarchy";

            var path = ModularSingleLevelHouseGeneratorTool.Generate(settings);
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Hierarchy check failed: generation returned null.";
                return false;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                error = "Hierarchy check failed: could not load generated prefab.";
                return false;
            }

            var requiredSections = new[] { "Floors", "Walls", "Stairs", "Roof" };
            foreach (var section in requiredSections)
            {
                if (prefab.transform.Find(section) != null)
                    continue;

                error = $"Hierarchy check failed: missing required section '{section}'.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateCategoryNaming(out string error)
        {
            RecreateFolder(ValidationRootFolder);

            var settings = CreateBaseSettings();
            settings.OutputFolder = ValidationRootFolder;
            var path = ModularSingleLevelHouseGeneratorTool.Generate(settings);
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Category naming check failed: generation returned null.";
                return false;
            }

            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!fileName.StartsWith("Industrial_", StringComparison.Ordinal))
            {
                error = $"Category naming check failed: expected Industrial_* for 4x4 / 2 floors, got '{fileName}'.";
                return false;
            }

            var singleStorySettings = CreateBaseSettings();
            singleStorySettings.OutputFolder = ValidationRootFolder;
            singleStorySettings.FloorCount = 1;
            singleStorySettings.FixedWidthCells = 5;
            singleStorySettings.FixedDepthCells = 5;
            var singleStoryPath = ModularSingleLevelHouseGeneratorTool.Generate(singleStorySettings);
            if (string.IsNullOrWhiteSpace(singleStoryPath))
            {
                error = "Category naming check failed: 5x5 / 1 floor generation returned null.";
                return false;
            }

            var singleStoryName = System.IO.Path.GetFileNameWithoutExtension(singleStoryPath);
            if (!singleStoryName.StartsWith("Residential_", StringComparison.Ordinal))
            {
                error =
                    $"Category naming check failed: expected Residential_* for 5x5 / 1 floor, got '{singleStoryName}'.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateSkyscraperShrink(out string error)
        {
            RecreateFolder(ValidationRootFolder);

            var settings = CreateBaseSettings();
            settings.OutputFolder = ValidationRootFolder;
            settings.FloorCount = 12;
            settings.SkyscraperMode = true;
            settings.SkyscraperShrinkStartFloor = 8;
            settings.SkyscraperShrinkStep = 1;
            settings.FixedWidthCells = 6;
            settings.FixedDepthCells = 6;
            settings.UseFixedRandomSeed = true;
            settings.RandomSeed = 4242;

            var path = ModularSingleLevelHouseGeneratorTool.Generate(settings);
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Skyscraper shrink check failed: generation returned null.";
                return false;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                error = "Skyscraper shrink check failed: could not load generated prefab.";
                return false;
            }

            // Verify the top floor has shrunk: top floor should have fewer floor tiles than ground
            var floorsParent = prefab.transform.Find("Floors");
            if (floorsParent == null)
            {
                error = "Skyscraper shrink check failed: missing Floors parent.";
                return false;
            }

            var groundFloorCount = 0;
            var topFloorCount = 0;
            foreach (Transform child in floorsParent)
            {
                if (child.name.StartsWith("Floor_L0_", StringComparison.Ordinal))
                    groundFloorCount++;
                else if (child.name.StartsWith("Floor_L11_", StringComparison.Ordinal))
                    topFloorCount++;
            }

            if (topFloorCount >= groundFloorCount)
            {
                error =
                    $"Skyscraper shrink check failed: top floor tiles ({topFloorCount}) should be fewer than ground floor tiles ({groundFloorCount}) when shrinking is active.";
                return false;
            }

            // Verify the prefab name matches CityCore category
            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!fileName.StartsWith("CityCore_", StringComparison.Ordinal))
            {
                error = $"Skyscraper shrink check failed: expected CityCore_* for 12-floor skyscraper, got '{fileName}'.";
                return false;
            }

            error = null;
            return true;
        }

        private static GeneratorSettings CreateBaseSettings()
        {
            return new GeneratorSettings
            {
                KitFolder = ModularSingleLevelHouseGeneratorTool.DefaultKitFolder,
                OutputFolder = ValidationRootFolder,
                FloorCount = 2,
                GroundDoorCount = 2,
                MinRoomsPerFloor = 1,
                MaxRoomsPerFloor = 3,
                MinCells = 3,
                MaxCells = 3,
                FixedWidthCells = 4,
                FixedDepthCells = 4,
                UseFixedRandomSeed = true,
                RandomSeed = 1337,
                WindowChance = 0.25f,
                StairPrefabFileName = "Building_Stair.prefab",
                UpperFloorPrefabFileName = string.Empty,
                RoofPrefabPath = string.Empty
            };
        }

        private static void RecreateFolder(string folder)
        {
            AssetDatabase.DeleteAsset(folder);
            EnsureFolder(folder);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private static string BuildPrefabSignature(GameObject prefab)
        {
            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            var rows = new List<string>(transforms.Length);
            foreach (var tr in transforms)
            {
                rows.Add(BuildTransformSignature(prefab.transform, tr));
            }

            rows.Sort(StringComparer.Ordinal);
            return string.Join("\n", rows);
        }

        private static string BuildTransformSignature(Transform root, Transform tr)
        {
            var path = BuildPath(root, tr);
            var p = tr.localPosition;
            var r = tr.localEulerAngles;
            var s = tr.localScale;

            var builder = new StringBuilder();
            builder.Append(path);
            builder.Append("|");
            builder.AppendFormat("P({0:0.###},{1:0.###},{2:0.###})", p.x, p.y, p.z);
            builder.Append("|");
            builder.AppendFormat("R({0:0.###},{1:0.###},{2:0.###})", r.x, r.y, r.z);
            builder.Append("|");
            builder.AppendFormat("S({0:0.###},{1:0.###},{2:0.###})", s.x, s.y, s.z);
            return builder.ToString();
        }

        private static string BuildPath(Transform root, Transform tr)
        {
            if (tr == root)
                return root.name;

            var stack = new Stack<string>();
            var cursor = tr;
            while (cursor != null && cursor != root)
            {
                stack.Push(cursor.name);
                cursor = cursor.parent;
            }

            var path = root.name;
            while (stack.Count > 0)
                path += "/" + stack.Pop();

            return path;
        }
    }
}
#endif
