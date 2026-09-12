#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using JBooth.MicroSplat;
using UnityEditor;
using UnityEngine;
using Zombera.Editor.StyleMatch;

/// <summary>
/// Reorders <c>Microsplat_World</c> texture slots to match <c>WorldSurfacePalette</c> semantics (0–16)
/// and parks extended biome textures at 17–31.
/// </summary>
internal static class MicroSplatWorldSurfaceRemapTool
{
    private const string ConfigPath =
        "Assets/01_Game/02_World/Terrain/Materials/Microsplat/Microsplat_World.asset";

    private const string LayerFolder =
        "Assets/01_Game/02_World/Terrain/Materials/Microsplat/";

    private const int TargetCount = 32;

    // Semantic palette order 0–16, then extended naturals 17–31.
    // Sand01 intentionally binds lake pebbles (inland shores). Ocean beaches use Sand / WetSand / BlackSand.
    private static readonly string[] TargetLayerAssetNames =
    {
        "microsplat_layer_grass_ground_68_22_basecolor_diffuse_0",          // 0  GrassGreen
        "microsplat_layer_grass_ground_68_26_basecolor_diffuse_1",          // 1  GrassYellow
        "microsplat_layer_lake_pebbles_66_01_basecolor_diffuse_10",       // 2  Sand01 (lake pebbles)
        "microsplat_layer_grass_albedo_3",                                  // 3  Grass
        "microsplat_layer_sparse_grass_diff_2k_4",                          // 4  SparseGrass
        "microsplat_layer_grey_rocky_cliff_66_74_basecolor_diffuse_6",    // 5  CliffBright
        "microsplat_layer_beach_sand_59_52_basecolor_diffuse_15",         // 6  Sand (beach)
        "microsplat_layer_dark_rock_68_28_basecolor_diffuse_7",           // 7  CliffDark
        "microsplat_layer_red_canyon_cliff_66_57_basecolor_diffuse_9",    // 8  CliffRed
        "microsplat_layer_yellow_rocky_cliff_66_77_basecolor_diffuse_8",  // 9  CliffPink
        "microsplat_layer_Snow_10",                                        // 10 Snow
        "microsplat_layer_sandy_cracked_rock_69_17_basecolor_diffuse_31", // 11 SandCracks
        "microsplat_layer_dirt_albedo_12",                                  // 12 Dirt
        "microsplat_layer_Asphalt_13",                                      // 13 Asphalt
        "microsplat_layer_fresh_concrete_67_46_basecolor_diffuse_14",     // 14 RoughConcrete
        "microsplat_layer_ConcreteTiles_15",                                // 15 ConcreteTiles
        "microsplat_layer_clean_asphalt_diff_2k_16",                        // 16 CleanAsphalt
        "microsplat_layer_desert_sand_58_32_basecolor_diffuse_16",        // 17 DesertSand
        "microsplat_layer_lava_rock_60_47_basecolor_diffuse_17",          // 18 LavaRock
        "microsplat_layer_lava_ground_58_28_basecolor_diffuse_18",      // 19 LavaGround
        "microsplat_layer_black_ground_dirt_60_87_basecolor_diffuse_19",  // 20 BlackDirt
        "microsplat_layer_black_beach_sand_60_86_basecolor_diffuse_20",   // 21 BlackSand
        "microsplat_layer_frozen_lake_60_50_basecolor_diffuse_20",      // 22 FrozenLake
        "microsplat_layer_jungle_root_ground_58_38_basecolor_diffuse_21", // 23 JungleFloor
        "microsplat_layer_dark_rock_with_snow_68_29_basecolor_diffuse_22", // 24 SnowRock
        "microsplat_layer_swamp_62_40_basecolor_diffuse_23",              // 25 SwampMud
        "microsplat_layer_dry_forest_ground_60_59_basecolor_diffuse_24",  // 26 DryForestFloor
        "microsplat_layer_grass_ground_68_23_basecolor_diffuse_25",       // 27 MeadowGrass
        "microsplat_layer_wet_wavy_rock_68_86_basecolor_diffuse_26",    // 28 WetRock
        "microsplat_layer_wet_river_sand_66_98_basecolor_diffuse_27",   // 29 RiverSand
        "microsplat_layer_ice_66_50_basecolor_diffuse_28",                // 30 Ice
        "microsplat_layer_wet_sand_66_29_basecolor_diffuse_30"            // 31 WetSand
    };

    [MenuItem("Zombera/World/Remap MicroSplat World Surface Slots")]
    public static void RemapFromMenu() => Remap(log: true);

    public static void Remap(bool log = true, bool force = false) =>
        StyleMatchMicroSplatCompileGuard.TryRun(
            StyleMatchMicroSplatCompileGuard.Operation.Remap,
            () => RemapInternal(log),
            force);

    private static bool RemapInternal(bool log)
    {
        var cfg = AssetDatabase.LoadAssetAtPath<TextureArrayConfig>(ConfigPath);
        if (cfg == null)
        {
            Debug.LogError($"[MicroSplatRemap] Missing TextureArrayConfig at {ConfigPath}");
            return false;
        }

        var byLayerGuid = BuildEntryIndex(cfg);
        var ordered = new List<TextureArrayConfig.TextureEntry>(TargetCount);

        for (var i = 0; i < TargetCount; i++)
        {
            var layerName = TargetLayerAssetNames[i];
            var layerPath = LayerFolder + layerName + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                Debug.LogError($"[MicroSplatRemap] Missing TerrainLayer: {layerPath}");
                return false;
            }

            var guid = AssetDatabase.AssetPathToGUID(layerPath);
            if (!byLayerGuid.TryGetValue(guid, out var entry))
            {
                entry = CreateEntryFromLayer(layer);
                if (log)
                    Debug.Log($"[MicroSplatRemap] Slot {i}: created entry from {layerName}");
            }
            else if (entry.terrainLayer != layer)
            {
                entry.terrainLayer = layer;
            }

            ordered.Add(CloneEntry(entry));
        }

        cfg.sourceTextures = ordered;
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();

        TextureArrayConfigEditor.CompileConfig(cfg);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (log)
            Debug.Log($"[MicroSplatRemap] Reordered {TargetCount} slots and recompiled {ConfigPath}.");

        return true;
    }

    private static Dictionary<string, TextureArrayConfig.TextureEntry> BuildEntryIndex(
        TextureArrayConfig cfg)
    {
        var index = new Dictionary<string, TextureArrayConfig.TextureEntry>(StringComparer.OrdinalIgnoreCase);
        if (cfg.sourceTextures == null)
            return index;

        for (var i = 0; i < cfg.sourceTextures.Count; i++)
        {
            var entry = cfg.sourceTextures[i];
            if (entry?.terrainLayer == null)
                continue;

            var path = AssetDatabase.GetAssetPath(entry.terrainLayer);
            if (string.IsNullOrEmpty(path))
                continue;

            var guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid) && !index.ContainsKey(guid))
                index[guid] = entry;
        }

        return index;
    }

    private static TextureArrayConfig.TextureEntry CreateEntryFromLayer(TerrainLayer layer)
    {
        var entry = new TextureArrayConfig.TextureEntry
        {
            terrainLayer = layer,
            diffuse = layer.diffuseTexture,
            normal = layer.normalMapTexture,
            ao = layer.maskMapTexture,
            smoothness = layer.maskMapTexture,
            height = layer.maskMapTexture,
            aoChannel = TextureArrayConfig.TextureChannel.G,
            smoothnessChannel = TextureArrayConfig.TextureChannel.A,
            heightChannel = TextureArrayConfig.TextureChannel.B
        };
        return entry;
    }

    private static TextureArrayConfig.TextureEntry CloneEntry(TextureArrayConfig.TextureEntry src)
    {
        return new TextureArrayConfig.TextureEntry
        {
            terrainLayer = src.terrainLayer,
            diffuse = src.diffuse,
            height = src.height,
            heightChannel = src.heightChannel,
            normal = src.normal,
            smoothness = src.smoothness,
            smoothnessChannel = src.smoothnessChannel,
            isRoughness = src.isRoughness,
            ao = src.ao,
            aoChannel = src.aoChannel,
            emis = src.emis,
            metal = src.metal,
            metalChannel = src.metalChannel,
            specular = src.specular,
            noiseNormal = src.noiseNormal,
            detailNoise = src.detailNoise,
            detailChannel = src.detailChannel,
            distanceNoise = src.distanceNoise,
            distanceChannel = src.distanceChannel,
            traxDiffuse = src.traxDiffuse,
            traxHeight = src.traxHeight,
            traxHeightChannel = src.traxHeightChannel,
            traxNormal = src.traxNormal,
            traxSmoothness = src.traxSmoothness,
            traxSmoothnessChannel = src.traxSmoothnessChannel,
            traxIsRoughness = src.traxIsRoughness,
            traxAO = src.traxAO,
            traxAOChannel = src.traxAOChannel,
            splat = src.splat
        };
    }
}
#endif
