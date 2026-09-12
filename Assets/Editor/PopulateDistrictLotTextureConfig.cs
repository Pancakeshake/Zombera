#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using JBooth.MicroSplat;

namespace Zombera.Editor
{
    /// <summary>
    ///     One-shot tool: populates the empty Microsplat_DIstrict_Lots TextureArrayConfig
    ///     with terrain textures from Assets/02_Shared/Textures/Generic/Terrain/,
    ///     creates a TerrainLayer asset per texture, and rewrites DistrictLotTerrainLayout
    ///     to reference the actual layer names and texture file names.
    /// </summary>
    internal static class PopulateDistrictLotTextureConfig
    {
        private const string ConfigPath =
            "Assets/02_Shared/ScriptableObjects/World/City/CItygen/CityMicrosplat/CityLotTextures.asset";

        private const string TerrainTexDir = "Assets/02_Shared/Textures/Generic/Terrain";
        private const string ConcreteTexDir = "Assets/02_Shared/Textures/Generic/Concrete";
        private const string LayerOutputDir =
            "Assets/02_Shared/ScriptableObjects/World/City/CItygen/CityMicrosplat";

        private const string LayoutPath =
            "Assets/02_Shared/ScriptableObjects/World/City/CItygen/DistrictLotTerrainLayout.asset";

        // (layer name, diffuse file, normal file, source dir override — null = TerrainTexDir)
        private static readonly (string name, string diff, string norm, string dir)[] Entries =
        {
            ("ForrestGround_01",     "forrest_ground_01_diff_2k.jpg",     "forrest_ground_01_nor_gl_2k.exr",   null),
            ("ForestGround_05",      "forest_ground_05_diff_2k.jpg",      "forest_ground_05_nor_gl_2k.exr",    null),
            ("GrassPath_2",          "grass_path_2_diff_2k.jpg",          "grass_path_2_nor_gl_2k.exr",        null),
            ("GravelRoad",           "gravel_road_diff_2k.jpg",           "gravel_road_nor_gl_2k.exr",         null),
            ("GravelFloor_04",       "gravel_floor_04_diff_2k.jpg",       "gravel_floor_04_nor_gl_2k.exr",     null),
            ("SandyGravel_02",       "sandy_gravel_02_diff_2k.jpg",       "sandy_gravel_02_nor_gl_2k.exr",     null),
            ("DarkRock",             "dark_rock_diff_2k.jpg",             "dark_rock_nor_gl_2k.exr",           null),
            ("GrayRocks",            "gray_rocks_diff_2k.jpg",            "gray_rocks_nor_gl_2k.exr",          null),
            ("Rock_01",              "rock_01_diff_2k.jpg",               "rock_01_nor_gl_2k.exr",             null),
            ("StonePathway_02",      "stone_pathway_02_diff_2k.jpg",      "stone_pathway_02_nor_gl_2k.exr",    null),
            ("MuddyTracks",          "muddy_tracks_diff_2k.jpg",          "muddy_tracks_nor_gl_2k.exr",        null),
            ("BrownMudLeaves_01",    "brown_mud_leaves_01_diff_2k.jpg",   "brown_mud_leaves_01_nor_gl_2k.exr", null),
            ("ConcreteSlab",         "concrete_floor_damaged_01_diff_2k.jpg", "concrete_floor_damaged_01_ao_2k.jpg", ConcreteTexDir),
        };

        [MenuItem("Zombera/Terrain/Populate District Lots Texture Config")]
        public static void Run()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<TextureArrayConfig>(ConfigPath);
            if (cfg == null)
            {
                Debug.LogError($"[PopulateDistrictLotTextureConfig] Config not found at {ConfigPath}");
                return;
            }

            // Clear existing entries
            cfg.sourceTextures = new List<TextureArrayConfig.TextureEntry>();

            for (var i = 0; i < Entries.Length; i++)
            {
                var (layerName, diffFile, normFile, texDir) = Entries[i];
                var baseDir = texDir ?? TerrainTexDir;

                var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    Path.Combine(baseDir, diffFile));
                var normal  = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    Path.Combine(baseDir, normFile));

                if (diffuse == null)
                {
                    Debug.LogWarning($"[PopulateDistrictLotTextureConfig] Missing diffuse: {diffFile}");
                    continue;
                }

                // Create or reuse TerrainLayer asset
                var layerPath = Path.Combine(LayerOutputDir,
                    $"microsplat_layer_{layerName}_{i}.terrainlayer");
                var terrainLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                if (terrainLayer == null)
                {
                    terrainLayer = new TerrainLayer();
                    terrainLayer.name = $"{layerName}_{i}";
                    terrainLayer.diffuseTexture = diffuse;
                    terrainLayer.normalMapTexture = normal;
                    AssetDatabase.CreateAsset(terrainLayer, layerPath);
                }

                // Add entry to TextureArrayConfig
                var entry = new TextureArrayConfig.TextureEntry();
                entry.diffuse = diffuse;
                entry.normal  = normal;
                entry.terrainLayer = terrainLayer;
                cfg.sourceTextures.Add(entry);

                Debug.Log($"[PopulateDistrictLotTextureConfig] Layer {i}: {layerName} → diffuse={diffFile} normal={normFile}");
            }

            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PopulateDistrictLotTextureConfig] Done — {cfg.sourceTextures.Count} layers populated.");

            // Also rewrite the DistrictLotTerrainLayout to remove generic enum names
            RewriteLayout();
        }

        private static void ApplyIndices(SerializedObject so, string districtName)
        {
            var prop = so.FindProperty(districtName);
            if (prop == null) return;

            // Default indices:
            //   0=ForrestGround_01  6=DarkRock  8=Rock_01  9=StonePathway_02  12=ConcreteSlab
            var defaults = districtName switch
            {
                "residential" => (front: 0, back: 0, side: 0, drive: 6, pad: 12, path: 9),
                "commercial"  => (front: 9, back: 3, side: 0, drive: 6, pad: 12, path: 9),
                "industrial"  => (front: 8, back: 3, side: 8, drive: 8, pad: 12, path: 9),
                "cityCore"    => (front: 9, back: 9, side: 9, drive: 9, pad: 12, path: 9),
                _             => (front: 0, back: 0, side: 0, drive: 6, pad: 12, path: 9),
            };

            SetInt(prop, "frontYardTextureIndex",   defaults.front);
            SetInt(prop, "backyardTextureIndex",    defaults.back);
            SetInt(prop, "sideYardTextureIndex",    defaults.side);
            SetInt(prop, "drivewayTextureIndex",    defaults.drive);
            SetInt(prop, "buildingPadTextureIndex", defaults.pad);
            SetInt(prop, "footpathTextureIndex",    defaults.path);
        }

        private static void SetInt(SerializedProperty parent, string name, int value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p != null) p.intValue = value;
        }

        private static void RewriteLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<Zombera.World.Roads.DistrictLotTerrainLayout>(LayoutPath);
            if (layout == null)
            {
                Debug.LogWarning($"[PopulateDistrictLotTextureConfig] Layout not found at {LayoutPath}");
                return;
            }

            var so = new SerializedObject(layout);

            // Wire the config reference so the property drawer can find it.
            var cfg = AssetDatabase.LoadAssetAtPath<TextureArrayConfig>(ConfigPath);
            var cfgProp = so.FindProperty("textureArrayConfig");
            if (cfgProp != null && cfg != null)
            {
                cfgProp.objectReferenceValue = cfg;
            }

            // Apply per-district texture indices.
            ApplyIndices(so, "residential");
            ApplyIndices(so, "commercial");
            ApplyIndices(so, "industrial");
            ApplyIndices(so, "cityCore");
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PopulateDistrictLotTextureConfig] Layout at {LayoutPath} — config wired + indices applied.");
        }
    }
}
#endif
