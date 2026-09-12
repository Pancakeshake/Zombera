#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     One-shot tool: creates and populates the InteriorPropConfig asset at
    ///     <c>Assets/02_Shared/ScriptableObjects/Buildings/Props/InteriorPropConfig.asset</c>
    ///     by scanning the Furniture Mega Pack (<c>Assets/03_ThirdParty/Furniture Mega Pack/Prefabs/</c>)
    ///     and the zombie-clutter props (<c>Assets/02_Shared/Prefabs/Props/</c>), assigning
    ///     prefabs to room types based on folder name and prefab name heuristics.
    ///
    ///     Run via Tools → Build → Mod Kits → Prop Tables → Populate Interior Prop Config.
    /// </summary>
    public static class InteriorPropConfigPopulateTool
    {
        private const string MenuPath =
            "Tools/Build/Mod Kits/Prop Tables/Populate Interior Prop Config";

        private const string AssetPath =
            "Assets/02_Shared/ScriptableObjects/Buildings/Props/InteriorPropConfig.asset";

        // ── Scan roots ───────────────────────────────────────────────────────────

        /// <summary>Primary source: Furniture Mega Pack with clean room-typed folders.</summary>
        private const string FurnitureRoot =
            "Assets/03_ThirdParty/Furniture Mega Pack/Prefabs";

        /// <summary>Secondary source: zombie-apocalypse clutter props.</summary>
        private const string PropsRoot =
            "Assets/02_Shared/Prefabs/Props";

        // ── Furniture folder → room mapping ──────────────────────────────────────
        //
        // Each entry maps a Furniture Mega Pack subfolder name to one or more
        // (RoomType, weight) pairs.  Weight > 1.0 makes the prop appear more
        // often; weight < 1.0 makes it rarer.  Props in the same folder are
        // assigned to ALL matching rooms.

        private static readonly Dictionary<string, (RoomType room, float weight)[]> FurnitureFolderMap = new()
        {
            ["Bathroom"] = new[] { (RoomType.Bathroom, 1.0f) },

            ["Beds"] = new[] { (RoomType.Bedroom, 1.0f) },

            ["Chairs"] = new[]
            {
                (RoomType.LivingRoom, 1.0f),
                (RoomType.Dining,     1.0f),
                (RoomType.Kitchen,    0.5f),
                (RoomType.Bedroom,    0.3f),
                (RoomType.Hallway,    0.3f),
                (RoomType.Entry,      0.3f),
            },

            ["Closets"] = new[]
            {
                (RoomType.Bedroom,    1.0f),
                (RoomType.Hallway,    0.5f),
                (RoomType.Storage,    0.5f),
                (RoomType.Entry,      0.5f),
                (RoomType.LivingRoom, 0.3f),
            },

            ["Cushioins"] = new[]
            {
                (RoomType.LivingRoom, 0.8f),
                (RoomType.Bedroom,    0.5f),
            },

            ["Drawers"] = new[]
            {
                (RoomType.Bedroom,    1.0f),
                (RoomType.LivingRoom, 0.5f),
                (RoomType.Hallway,    0.3f),
                (RoomType.Storage,    0.5f),
                (RoomType.Entry,      0.3f),
                (RoomType.Kitchen,    0.3f),
            },

            ["Kitchen"] = new[] { (RoomType.Kitchen, 1.0f) },

            ["Sofas"] = new[] { (RoomType.LivingRoom, 1.0f) },

            ["Tables"] = new[]
            {
                (RoomType.LivingRoom, 1.0f),
                (RoomType.Dining,     1.0f),
                (RoomType.Kitchen,    0.5f),
                (RoomType.Bedroom,    0.5f),
                (RoomType.Hallway,    0.3f),
            },
        };

        // ── Kitchen sub-weighting ────────────────────────────────────────────────
        //
        // Within the Kitchen folder, cabinet variants are abundant so they get a
        // boost; large appliances (stove, fridge, oven) are tuned down so every
        // kitchen doesn't end up with all of them.

        private static readonly Dictionary<string, float> KitchenSubWeights = new()
        {
            ["Cabinet"]         = 1.2f,  // many variants — keep them common
            ["GasStove"]        = 0.6f,  // one per kitchen
            ["Refrigerator"]    = 0.6f,
            ["Sink"]            = 0.7f,
            ["KitchenOven"]     = 0.5f,
            ["MicrowaveOven"]   = 0.5f,
            ["KitchenExhaust"]  = 0.4f,
        };

        // ── Legacy clutter rules (Props/ folder) ─────────────────────────────────
        //
        // Simplified version of the old rule system — maps old Props subfolder
        // names to room types.  Only folders that add value *beyond* what the
        // furniture pack already covers are listed.

        private static readonly Dictionary<string, (RoomType room, float weight)[]> ClutterFolderMap = new()
        {
            // ── Furniture Mega Pack folder names (now extracted to Props/) ──
            ["Bathroom"] = new[] { (RoomType.Bathroom, 1.0f) },
            ["Beds"] = new[] { (RoomType.Bedroom, 1.0f) },
            ["Chairs"] = new[]
            {
                (RoomType.LivingRoom, 1.0f), (RoomType.Dining, 1.0f),
                (RoomType.Kitchen, 0.5f), (RoomType.Bedroom, 0.3f),
                (RoomType.Hallway, 0.3f), (RoomType.Entry, 0.3f),
            },
            ["Closets"] = new[]
            {
                (RoomType.Bedroom, 1.0f), (RoomType.Hallway, 0.5f),
                (RoomType.Storage, 0.5f), (RoomType.Entry, 0.5f),
                (RoomType.LivingRoom, 0.3f),
            },
            ["Cushioins"] = new[] { (RoomType.LivingRoom, 0.8f), (RoomType.Bedroom, 0.5f) },
            ["Drawers"] = new[]
            {
                (RoomType.Bedroom, 1.0f), (RoomType.LivingRoom, 0.5f),
                (RoomType.Hallway, 0.3f), (RoomType.Storage, 0.5f),
                (RoomType.Entry, 0.3f), (RoomType.Kitchen, 0.3f),
            },
            ["Kitchen"] = new[] { (RoomType.Kitchen, 1.0f) },
            ["Sofas"] = new[] { (RoomType.LivingRoom, 1.0f) },

            // ── Legacy clutter folders ──
            ["Bins"] = new[]
            {
                (RoomType.Kitchen,  1.0f),
                (RoomType.Bathroom, 1.0f),
                (RoomType.Hallway,  0.5f),
                (RoomType.Storage,  0.5f),
                (RoomType.Garage,   0.5f),
            },
            ["Boxes"] = new[]
            {
                (RoomType.Storage, 1.0f),
                (RoomType.Garage,  0.5f),
                (RoomType.Bedroom, 0.3f),
                (RoomType.Hallway, 0.3f),
            },
            ["Cans"] = new[]
            {
                (RoomType.Kitchen, 1.0f),
                (RoomType.Storage, 0.5f),
            },
            ["Carts"] = new[]
            {
                (RoomType.Storage, 1.0f),
                (RoomType.Garage,  0.5f),
            },
            ["Fabric"] = new[]
            {
                (RoomType.Bedroom, 1.0f),
                (RoomType.Kitchen, 0.5f),
                (RoomType.Storage, 0.5f),
                (RoomType.Bathroom,0.5f),
            },
            ["Fire Exstinguishers"] = new[]
            {
                (RoomType.Garage,  1.0f),
                (RoomType.Utility, 0.5f),
            },
            ["Fuel"] = new[] { (RoomType.Garage, 1.0f) },
            ["FuseBoxes"] = new[] { (RoomType.Utility, 1.0f) },
            ["Gas_Bottles"] = new[] { (RoomType.Garage, 1.0f) },
            ["Living Modular"] = new[]
            {
                (RoomType.LivingRoom, 0.5f),
                (RoomType.Hallway,    0.3f),
                (RoomType.Dining,     0.5f),
            },
            ["Misc"] = new[]
            {
                (RoomType.LivingRoom, 0.5f),
                (RoomType.Hallway,    0.5f),
                (RoomType.Entry,      0.5f),
                (RoomType.Bedroom,    0.5f),
            },
            ["Pipes"] = new[] { (RoomType.Utility, 0.5f) },
            ["Racks"] = new[]
            {
                (RoomType.Storage, 1.0f),
                (RoomType.Garage,  0.5f),
            },
            ["Shelves"] = new[]
            {
                (RoomType.Kitchen,  0.8f),
                (RoomType.Storage,  1.0f),
                (RoomType.Garage,   0.5f),
                (RoomType.Bedroom,  0.3f),
                (RoomType.Utility,  0.5f),
            },
            ["Storage"] = new[]
            {
                (RoomType.Storage, 1.0f),
                (RoomType.Garage,  0.5f),
            },
            ["Tables"] = new[]
            {
                (RoomType.LivingRoom, 1.0f),
                (RoomType.Dining,     1.0f),
                (RoomType.Kitchen,    1.0f),
                (RoomType.Bedroom,    0.5f),
                (RoomType.Hallway,    0.3f),
            },
            ["Chair"] = new[]
            {
                (RoomType.Kitchen,    1.2f),
                (RoomType.LivingRoom, 1.5f),
                (RoomType.Dining,     1.0f),
                (RoomType.Bedroom,    0.5f),
            },
            ["Toilets"] = new[] { (RoomType.Bathroom, 1.0f) },
            ["Tools"] = new[]
            {
                (RoomType.Kitchen,  1.0f),
                (RoomType.Bathroom, 1.0f),
                (RoomType.Storage,  0.3f),
                (RoomType.Garage,   1.0f),
            },
            ["Tyres"] = new[] { (RoomType.Garage, 1.0f) },
            ["Ventilation"] = new[] { (RoomType.Utility, 0.5f) },
            ["Water"] = new[]
            {
                (RoomType.Kitchen,  0.5f),
                (RoomType.Bathroom, 0.5f),
                (RoomType.Utility,  1.0f),
                (RoomType.Garage,   0.5f),
            },
            ["WorkBenches"] = new[] { (RoomType.Garage, 0.5f) },
            ["Work Equipment"] = new[] { (RoomType.Garage, 0.5f) },
        };

        /// <summary>Folders inside the old Props root to skip (outdoor / non-interior).</summary>
        private static readonly HashSet<string> ClutterSkipFolders = new()
        {
            "Barricades", "Construction", "Fences", "Fire_Hydrants", "Kiosk",
            "Ladders", "Lockers", "Mailboxes", "Nature", "Phone_Booths",
            "Rails", "Railway", "Road", "Scaffolds", "Screws_bolts",
            "Signs", "Solar Panels", "Storage_Tanks", "Traffic_Cones",
        };

        // ── Spawn attempts per room type ─────────────────────────────────────────

        private static readonly Dictionary<RoomType, int> SpawnAttempts = new()
        {
            [RoomType.Kitchen]    = 10,
            [RoomType.LivingRoom] = 10,
            [RoomType.Bedroom]    = 8,
            [RoomType.Bathroom]   = 6,
            [RoomType.Hallway]    = 3,
            [RoomType.Dining]     = 8,
            [RoomType.Storage]    = 5,
            [RoomType.Garage]     = 4,
            [RoomType.Entry]      = 2,
            [RoomType.Utility]    = 2,
        };

        // ── Menu entry ───────────────────────────────────────────────────────────

        [MenuItem(MenuPath, priority = -500)]
        private static void Populate()
        {
            // Load or create the asset.
            var config = AssetDatabase.LoadAssetAtPath<InteriorPropConfig>(AssetPath);
            var isNew = config == null;
            if (isNew)
            {
                EnsureFolderExists(Path.GetDirectoryName(AssetPath));
                config = ScriptableObject.CreateInstance<InteriorPropConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
            }

            Undo.RecordObject(config, "Populate Interior Prop Config");

            // Clear existing entries.
            config.roomEntries = Array.Empty<RoomPropEntry>();
            config.fallbackProps = Array.Empty<PropPlacement>();

            // Scan both sources.
            var roomMap = new Dictionary<RoomType, List<(GameObject prefab, float weight)>>();
            ScanFurniturePack(roomMap);
            ScanClutterProps(roomMap);

            // Build room entries.
            var entries = new List<RoomPropEntry>();
            foreach (var kv in roomMap.OrderBy(kv => kv.Key))
            {
                var roomType = kv.Key;
                var propList = kv.Value;

                // Deduplicate by prefab, keeping highest weight.
                var deduped = new Dictionary<GameObject, float>();
                foreach (var (prefab, weight) in propList)
                {
                    if (prefab == null) continue;
                    if (!deduped.ContainsKey(prefab) || weight > deduped[prefab])
                        deduped[prefab] = weight;
                }

                var placements = new List<PropPlacement>();
                foreach (var (prefab, weight) in deduped)
                {
                    placements.Add(new PropPlacement
                    {
                        prefab           = prefab,
                        weight           = weight,
                        maxPerRoom       = 2,
                        guaranteedCount  = 0,
                        randomiseYaw     = true,
                        yawSnapDegrees   = 90f,
                        yOffset          = 0f,
                        wallAlignment    = WallAlignment.AgainstWall,
                        minWallDistance  = 0.3f,
                        minDoorDistance  = 0.5f,
                        footprintRadius  = 0.5f,
                        scaleOverride    = 1f,
                    });
                }

                entries.Add(new RoomPropEntry
                {
                    roomType      = roomType,
                    spawnAttempts = SpawnAttempts.TryGetValue(roomType, out var a) ? a : 4,
                    props         = placements.ToArray(),
                });
            }

            config.roomEntries = entries.ToArray();
            config.fallbackSpawnAttempts = 2;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var total = 0;
            foreach (var e in entries) total += e.props.Length;

            Debug.Log(
                $"[InteriorPropConfigPopulateTool] {(isNew ? "Created" : "Updated")} '{AssetPath}': " +
                $"{entries.Count} room types, {total} unique props.");
            EditorUtility.DisplayDialog("Populate Interior Prop Config",
                $"Done! {entries.Count} room types, {total} unique props assigned.\n\n" +
                $"Furniture Mega Pack + legacy clutter combined.\n" +
                "Assign this to RoomSettings → PropConfig to use it during generation.",
                "OK");
        }

        // ── Scanning: Furniture Mega Pack ────────────────────────────────────────

        private static void ScanFurniturePack(
            Dictionary<RoomType, List<(GameObject prefab, float weight)>> map)
        {
            if (!AssetDatabase.IsValidFolder(FurnitureRoot))
            {
                Debug.LogWarning(
                    $"[InteriorPropConfigPopulateTool] Furniture root not found: '{FurnitureRoot}' — skipping.");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { FurnitureRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var folderName = Path.GetFileName(Path.GetDirectoryName(path));

                if (!FurnitureFolderMap.TryGetValue(folderName, out var roomWeights))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var prefabName = prefab.name;

                foreach (var (room, baseWeight) in roomWeights)
                {
                    var weight = baseWeight;

                    // Apply kitchen sub-weighting based on prefab name prefix.
                    if (folderName == "Kitchen")
                    {
                        foreach (var kv in KitchenSubWeights)
                        {
                            if (prefabName.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                weight *= kv.Value;
                                break;
                            }
                        }
                    }

                    AddToMap(map, room, prefab, weight);
                }
            }
        }

        // ── Scanning: legacy clutter props ───────────────────────────────────────

        private static void ScanClutterProps(
            Dictionary<RoomType, List<(GameObject prefab, float weight)>> map)
        {
            if (!AssetDatabase.IsValidFolder(PropsRoot))
            {
                Debug.LogWarning(
                    $"[InteriorPropConfigPopulateTool] Props root not found: '{PropsRoot}' — skipping.");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PropsRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var folderName = Path.GetFileName(Path.GetDirectoryName(path));

                if (ClutterSkipFolders.Contains(folderName))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                if (ClutterFolderMap.TryGetValue(folderName, out var roomWeights))
                {
                    foreach (var (room, weight) in roomWeights)
                        AddToMap(map, room, prefab, weight);
                }
                else
                {
                    // Catch-all: unmatched folders → Storage at low weight.
                    AddToMap(map, RoomType.Storage, prefab, 0.3f);
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static void AddToMap(
            Dictionary<RoomType, List<(GameObject prefab, float weight)>> map,
            RoomType room, GameObject prefab, float weight)
        {
            if (!map.ContainsKey(room))
                map[room] = new List<(GameObject, float)>();
            map[room].Add((prefab, weight));
        }

        private static void EnsureFolderExists(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder)) return;
            var parts = assetFolder.Replace('\\', '/').Trim('/').Split('/');
            var current = "";
            for (var i = 0; i < parts.Length; i++)
            {
                current = i == 0 ? parts[i] : $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(current))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(current), Path.GetFileName(current));
            }
        }
    }
}
#endif
