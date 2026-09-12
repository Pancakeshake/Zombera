#if UNITY_EDITOR
using System;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    internal static class GeneratorSettingsValidator
    {
        internal static void NormalizeForEditor(ref GeneratorSettings settings)
        {
            settings ??= new GeneratorSettings();

            settings.KitFolder = string.IsNullOrWhiteSpace(settings.KitFolder)
                ? ModularSingleLevelHouseGeneratorTool.DefaultKitFolder
                : settings.KitFolder.Trim();
            settings.OutputFolder = string.IsNullOrWhiteSpace(settings.OutputFolder)
                ? ModularSingleLevelHouseGeneratorTool.DefaultOutputFolder
                : settings.OutputFolder.Trim();

            settings.FloorCount = Mathf.Clamp(settings.FloorCount, 1, ModularSingleLevelHouseGeneratorTool.MaxFloors);
            settings.GroundDoorCount = Mathf.Max(0, settings.GroundDoorCount);

            settings.MinRoomsPerFloor = Mathf.Max(0, settings.MinRoomsPerFloor);
            settings.MaxRoomsPerFloor = Mathf.Max(0, settings.MaxRoomsPerFloor);
            if (settings.MaxRoomsPerFloor < settings.MinRoomsPerFloor)
                settings.MaxRoomsPerFloor = settings.MinRoomsPerFloor;

            settings.MinCells = Mathf.Clamp(settings.MinCells, ModularSingleLevelHouseGeneratorTool.MinFootprintCells,
                ModularSingleLevelHouseGeneratorTool.MaxFootprintCells);
            settings.MaxCells = Mathf.Clamp(settings.MaxCells, ModularSingleLevelHouseGeneratorTool.MinFootprintCells,
                ModularSingleLevelHouseGeneratorTool.MaxFootprintCells);
            if (settings.MaxCells < settings.MinCells)
                settings.MaxCells = settings.MinCells;

            settings.FixedWidthCells = settings.FixedWidthCells > 0
                ? Mathf.Clamp(settings.FixedWidthCells, ModularSingleLevelHouseGeneratorTool.MinFootprintCells,
                    ModularSingleLevelHouseGeneratorTool.MaxFootprintCells)
                : 0;

            settings.FixedDepthCells = settings.FixedDepthCells > 0
                ? Mathf.Clamp(settings.FixedDepthCells, ModularSingleLevelHouseGeneratorTool.MinFootprintCells,
                    ModularSingleLevelHouseGeneratorTool.MaxFootprintCells)
                : 0;

            settings.WindowChance = Mathf.Clamp01(settings.WindowChance);
            settings.StairPrefabFileName = string.IsNullOrWhiteSpace(settings.StairPrefabFileName)
                ? "Building_Stair.prefab"
                : settings.StairPrefabFileName.Trim();
            settings.UpperFloorPrefabFileName = settings.UpperFloorPrefabFileName?.Trim() ?? string.Empty;
            settings.RoofPrefabPath = settings.RoofPrefabPath?.Trim() ?? string.Empty;
            settings.ExteriorStairsPrefabPath = string.IsNullOrWhiteSpace(settings.ExteriorStairsPrefabPath)
                ? ModularSingleLevelHouseGeneratorTool.DefaultExteriorStairsPrefabPath
                : settings.ExteriorStairsPrefabPath.Trim();

            settings.SkyscraperShrinkStartFloor = Mathf.Clamp(settings.SkyscraperShrinkStartFloor, 3,
                ModularSingleLevelHouseGeneratorTool.MaxFloors);
            settings.SkyscraperShrinkStep = Mathf.Clamp(settings.SkyscraperShrinkStep, 1, 4);
            if (settings.SkyscraperShrinkStartFloor >= settings.FloorCount)
                settings.SkyscraperMode = false;

            // Clamp to a known zone type
            if (System.Array.IndexOf(
                    ModularBuildingCategoryResolver.AllCategoryOptions,
                    settings.BuildingCategoryOverride) < 0)
                settings.BuildingCategoryOverride = CityDistrictType.Residential;

            ApplyLegacyRoomCountFallback(settings);
        }

        internal static bool TryNormalizeForGeneration(GeneratorSettings settings, out GeneratorSettings normalized,
            out string errorMessage)
        {
            normalized = Clone(settings);
            NormalizeForEditor(ref normalized);

            if (!normalized.OutputFolder.StartsWith("Assets", StringComparison.Ordinal))
            {
                errorMessage =
                    $"Output folder must be inside Assets. Received: '{normalized.OutputFolder}'";
                return false;
            }

            if (!normalized.KitFolder.StartsWith("Assets", StringComparison.Ordinal))
            {
                errorMessage =
                    $"Kit folder must be inside Assets. Received: '{normalized.KitFolder}'";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static void ApplyLegacyRoomCountFallback(GeneratorSettings settings)
        {
            if (settings.MinRoomsPerFloor == 0 && settings.MaxRoomsPerFloor == 0 && settings.RoomCount > 0)
            {
                settings.MinRoomsPerFloor = settings.RoomCount;
                settings.MaxRoomsPerFloor = settings.RoomCount;
            }
        }

        private static GeneratorSettings Clone(GeneratorSettings source)
        {
            if (source == null)
                return new GeneratorSettings();

            return new GeneratorSettings
            {
                KitFolder = source.KitFolder,
                RoofPrefabPath = source.RoofPrefabPath,
                OutputFolder = source.OutputFolder,
                UseManualGeneration = source.UseManualGeneration,
                FloorCount = source.FloorCount,
                GroundDoorCount = source.GroundDoorCount,
                RoomCount = source.RoomCount,
                MinRoomsPerFloor = source.MinRoomsPerFloor,
                MaxRoomsPerFloor = source.MaxRoomsPerFloor,
                MinCells = source.MinCells,
                MaxCells = source.MaxCells,
                FixedWidthCells = source.FixedWidthCells,
                FixedDepthCells = source.FixedDepthCells,
                UseFixedRandomSeed = source.UseFixedRandomSeed,
                RandomSeed = source.RandomSeed,
                WindowChance = source.WindowChance,
                StairPrefabFileName = source.StairPrefabFileName,
                UpperFloorPrefabFileName = source.UpperFloorPrefabFileName,
                ExteriorStairsPrefabPath = source.ExteriorStairsPrefabPath,
                SkyscraperMode = source.SkyscraperMode,
                SkyscraperShrinkStartFloor = source.SkyscraperShrinkStartFloor,
                SkyscraperShrinkStep = source.SkyscraperShrinkStep,
                SkipRoofAndCeiling = source.SkipRoofAndCeiling,
                BuildingCategoryOverride = source.BuildingCategoryOverride,
                RoomSettings = source.RoomSettings,
                ArchetypeLibrary = source.ArchetypeLibrary,
                SelectedTemplateIndex = source.SelectedTemplateIndex,
                SkinTable = source.SkinTable,
                KitConfig = source.KitConfig,
                ExteriorDoorSourcePath = source.ExteriorDoorSourcePath,
                InteriorDoorSourcePath = source.InteriorDoorSourcePath,
                DoorScale = source.DoorScale,
                StairPrefab = source.StairPrefab,
                UpperFloorPrefab = source.UpperFloorPrefab,
                ExteriorStairsPrefab = source.ExteriorStairsPrefab,
                RoofPrefab = source.RoofPrefab,
                ExteriorDoorPrefab = source.ExteriorDoorPrefab,
                InteriorDoorPrefab = source.InteriorDoorPrefab
            };
        }
    }
}
#endif
