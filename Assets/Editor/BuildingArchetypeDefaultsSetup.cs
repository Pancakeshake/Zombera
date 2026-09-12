#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Zombera.World.City;
using static Zombera.Editor.RoomType;

namespace Zombera.Editor
{
    /// <summary>
    ///     One-shot setup: creates the default BuildingArchetypeLibrary and ResidentialHouseTemplate
    ///     assets at <c>Assets/02_Shared/ScriptableObjects/World/Buildings/</c>.
    ///     Run via Tools → Build → Mod Kits → Building Generator → Setup Default Archetypes.
    /// </summary>
    internal static class BuildingArchetypeDefaultsSetup
    {
        private const string OutputFolder = "Assets/02_Shared/ScriptableObjects/World/Buildings";
        private const string ResidentialSubFolder = OutputFolder + "/Templates/Residential";

        private const string MenuSetup =
            "Tools/Build/Mod Kits/Building Generator/Setup Default Archetypes";

        [MenuItem(MenuSetup, priority = -499)]
        private static void CreateDefaults()
        {
            EnsureFolder(OutputFolder);
            EnsureFolder(ResidentialSubFolder);

            var libraryPath = OutputFolder + "/BuildingArchetypeLibrary.asset";
            var roomSettingsPath = OutputFolder + "/RoomSettings.asset";

            // ── Room Settings ────────────────────────────────────
            var roomSettings = CreateOrLoadRoomSettings(roomSettingsPath);

            // ── Residential templates ─────────────────────────────

            var studio = CreateTemplate(
                ResidentialSubFolder + "/Residential_Studio.asset",
                "Studio",
                CityDistrictType.Residential,
                preferOpenPlan: true,
                preferredFloors: 1,
                minW: 3, maxW: 4,
                required: new[]
                {
                    Room(LivingRoom, 1, 1),
                    Room(Kitchen,    1, 1),
                    Room(Bathroom,   1, 1),
                },
                optional: new[]
                {
                    Room(Hallway, 0, 1),
                },
                minDoors: 1, maxDoors: 1,
                roomSettings: roomSettings);

            var oneBR = CreateTemplate(
                ResidentialSubFolder + "/Residential_1BR.asset",
                "1 Bedroom",
                CityDistrictType.Residential,
                preferOpenPlan: false,
                preferredFloors: 1,
                minW: 4, maxW: 6,
                required: new[]
                {
                    Room(LivingRoom, 1, 1),
                    Room(Kitchen,    1, 1),
                    Room(Bedroom,    1, 1),
                    Room(Bathroom,   1, 1),
                    Room(Hallway,    1, 1),
                },
                optional: new[]
                {
                    Room(Dining, 0, 1),
                },
                minDoors: 1, maxDoors: 2,
                roomSettings: roomSettings);

            var twoBR = CreateTemplate(
                ResidentialSubFolder + "/Residential_2BR.asset",
                "2 Bedroom",
                CityDistrictType.Residential,
                preferOpenPlan: false,
                preferredFloors: 1,
                minW: 5, maxW: 8,
                required: new[]
                {
                    Room(LivingRoom, 1, 1),
                    Room(Kitchen,    1, 1),
                    Room(Bedroom,    2, 2),
                    Room(Bathroom,   1, 1),
                    Room(Hallway,    1, 1),
                },
                optional: new[]
                {
                    Room(Dining,  0, 1),
                    Room(Storage, 0, 1),
                },
                minDoors: 1, maxDoors: 2,
                roomSettings: roomSettings);

            var threeBR = CreateTemplate(
                ResidentialSubFolder + "/Residential_3BR.asset",
                "3 Bedroom",
                CityDistrictType.Residential,
                preferOpenPlan: false,
                preferredFloors: 2,
                minW: 6, maxW: 10,
                required: new[]
                {
                    Room(LivingRoom, 1, 1),
                    Room(Kitchen,    1, 1),
                    Room(Bedroom,    3, 3),
                    Room(Bathroom,   2, 2),
                    Room(Hallway,    1, 1),
                },
                optional: new[]
                {
                    Room(Dining,  0, 1),
                    Room(Storage, 0, 1),
                    Room(Garage,  0, 1),
                    Room(Utility, 0, 1),
                },
                minDoors: 2, maxDoors: 3,
                roomSettings: roomSettings);

            // ── Library asset ─────────────────────────────────────

            BuildingArchetypeLibrary library;
            if (File.Exists(ToAbsolutePath(libraryPath)))
            {
                library = AssetDatabase.LoadAssetAtPath<BuildingArchetypeLibrary>(libraryPath);
            }
            else
            {
                library = ScriptableObject.CreateInstance<BuildingArchetypeLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
            }

            library.ResidentialTemplates = new[] { studio, oneBR, twoBR, threeBR };
            EditorUtility.SetDirty(library);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);

            Debug.Log(
                $"[BuildingArchetypeDefaults] Created {library.ResidentialTemplates.Length} residential templates + library at '{libraryPath}'.");
        }

        private static RoomSettings CreateOrLoadRoomSettings(string path)
        {
            RoomSettings settings;
            if (File.Exists(ToAbsolutePath(path)))
            {
                settings = AssetDatabase.LoadAssetAtPath<RoomSettings>(path);
            }
            else
            {
                settings = ScriptableObject.CreateInstance<RoomSettings>();
                settings.RoomTypes = new[]
                {
                    Cfg(LivingRoom, allowOpenPlan: true,  blockFrontDoor: false, minPct: 20, maxPct: 40),
                    Cfg(Kitchen,    allowOpenPlan: true,  blockFrontDoor: false, minPct: 10, maxPct: 20),
                    Cfg(Bedroom,    allowOpenPlan: false, blockFrontDoor: true,  minPct: 10, maxPct: 25),
                    Cfg(Bathroom,   allowOpenPlan: false, blockFrontDoor: false, minPct:  5, maxPct: 12),
                    Cfg(Hallway,    allowOpenPlan: true,  blockFrontDoor: false, minPct:  5, maxPct: 10),
                    Cfg(Dining,     allowOpenPlan: true,  blockFrontDoor: false, minPct:  8, maxPct: 18),
                    Cfg(Storage,    allowOpenPlan: false, blockFrontDoor: false, minPct:  5, maxPct: 10),
                    Cfg(Garage,     allowOpenPlan: false, blockFrontDoor: false, minPct: 15, maxPct: 30),
                    Cfg(Utility,    allowOpenPlan: false, blockFrontDoor: false, minPct:  5, maxPct: 10),
                };
                settings.AdjacencyRules = new[]
                {
                    Adj(LivingRoom, Kitchen,  AdjacencyType.Required),
                    Adj(Kitchen,    Dining,   AdjacencyType.Preferred),
                    Adj(Bedroom,    Bathroom, AdjacencyType.Preferred),
                    Adj(Bathroom,   Hallway,  AdjacencyType.Required),
                    Adj(Bathroom,   Kitchen,  AdjacencyType.Avoid),
                    Adj(Garage,     Entry,    AdjacencyType.Required),
                };
                AssetDatabase.CreateAsset(settings, path);
            }

            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static RoomTypeConfig Cfg(RoomType type, bool allowOpenPlan, bool blockFrontDoor, int minPct, int maxPct)
        {
            return new RoomTypeConfig
            {
                Type = type,
                AllowOpenPlan = allowOpenPlan,
                BlockFrontDoor = blockFrontDoor,
                FloorPercentageMin = minPct,
                FloorPercentageMax = maxPct
            };
        }

        private static ResidentialHouseTemplate CreateTemplate(
            string path,
            string displayName,
            CityDistrictType lotType,
            bool preferOpenPlan,
            int preferredFloors,
            int minW, int maxW,
            RoomTemplateEntry[] required,
            RoomTemplateEntry[] optional,
            int minDoors,
            int maxDoors,
            RoomSettings roomSettings)
        {
            ResidentialHouseTemplate template;
            if (File.Exists(ToAbsolutePath(path)))
            {
                template = AssetDatabase.LoadAssetAtPath<ResidentialHouseTemplate>(path);
            }
            else
            {
                template = ScriptableObject.CreateInstance<ResidentialHouseTemplate>();
                AssetDatabase.CreateAsset(template, path);
            }

            template.DisplayName = displayName;
            template.LotType = lotType;
            template.RoomSettings = roomSettings;
            template.PreferOpenPlan = preferOpenPlan;
            template.PreferredFloorCount = preferredFloors;
            template.MinFootprintWidth = minW;
            template.MaxFootprintWidth = maxW;
            // Depth defaults mirror width; edit the template for rectangular footprints.
            template.MinFootprintDepth = minW;
            template.MaxFootprintDepth = maxW;
            template.RequiredRooms = required;
            template.OptionalRooms = optional;
            template.MinExteriorDoors = minDoors;
            template.MaxExteriorDoors = maxDoors;

            EditorUtility.SetDirty(template);
            return template;
        }

        private static RoomTemplateEntry Room(RoomType type, int minCount, int maxCount)
        {
            return new RoomTemplateEntry { RoomType = type, MinCount = minCount, MaxCount = maxCount };
        }

        private static RoomAdjacencyRule Adj(RoomType a, RoomType b, AdjacencyType type)
        {
            return new RoomAdjacencyRule { RoomA = a, RoomB = b, Adjacency = type };
        }

        private static void EnsureFolder(string assetPath)
        {
            var parent = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(parent) || AssetDatabase.IsValidFolder(parent))
                return;
            EnsureFolder(parent);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath["Assets/".Length..]);
        }
    }
}
#endif
