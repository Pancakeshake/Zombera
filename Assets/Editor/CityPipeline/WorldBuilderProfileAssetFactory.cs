#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>
    /// Creates default World Builder profile ScriptableObjects under
    /// Assets/02_Shared/ScriptableObjects/World/ with plan defaults.
    /// </summary>
    public static class WorldBuilderProfileAssetFactory
    {
        private const string RootFolder = "Assets/02_Shared/ScriptableObjects/World";
        private const string ProfilesFolder = RootFolder + "/Profiles";

        [MenuItem("Tools/World/Create Default World Generation Profiles")]
        public static void CreateDefaultProfiles()
        {
            EnsureFolders();

            var mapSize = CreateOrLoad<WorldMapSizeSettings>(ProfilesFolder + "/WorldMapSizeSettings.asset");
            var terrainGrid = CreateOrLoad<TerrainGridProfile>(ProfilesFolder + "/TerrainGridProfile.asset");
            var landforms = CreateOrLoad<LandformProfile>(ProfilesFolder + "/LandformProfile.asset");
            var hydrology = CreateOrLoad<HydrologyProfile>(ProfilesFolder + "/HydrologyProfile.asset");
            var water = CreateOrLoad<WorldWaterProfile>(ProfilesFolder + "/WorldWaterProfile.asset");
            var biomes = CreateOrLoad<WorldBiomePalette>(ProfilesFolder + "/WorldBiomePalette.asset");
            var surfaces = CreateOrLoad<WorldSurfacePalette>(ProfilesFolder + "/WorldSurfacePalette.asset");
            var nature = CreateOrLoad<WorldNatureProfile>(ProfilesFolder + "/WorldNatureProfile.asset");
            var pois = CreateOrLoad<WorldPoiProfile>(ProfilesFolder + "/WorldPoiProfile.asset");
            var environment = CreateOrLoad<WorldEnvironmentProfile>(ProfilesFolder + "/WorldEnvironmentProfile.asset");

            ApplyMapSizeDefaults(mapSize);
            ApplyTerrainGridDefaults(terrainGrid);
            ApplyLandformDefaults(landforms);
            ApplyWaterProfileDefaults(water);
            ApplyBiomePaletteDefaults(biomes);
            ApplySurfacePaletteDefaults(surfaces);
            ApplyEnvironmentProfileDefaults(environment);

            var root = CreateOrLoad<WorldGenerationProfile>(RootFolder + "/WorldGenerationProfile.asset");
            WireRoot(root, new WorldGenerationProfileWireArgs
            {
                MapSize = mapSize,
                TerrainGrid = terrainGrid,
                Landforms = landforms,
                Hydrology = hydrology,
                Water = water,
                Biomes = biomes,
                Surfaces = surfaces,
                Nature = nature,
                Pois = pois,
                Environment = environment
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[WorldBuilderProfileAssetFactory] Default world generation profiles ready at " + RootFolder);
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets/02_Shared/ScriptableObjects", "World");
            if (!AssetDatabase.IsValidFolder(ProfilesFolder))
                AssetDatabase.CreateFolder(RootFolder, "Profiles");
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                // Parent folders are ensured by EnsureFolders for our known paths.
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ApplyMapSizeDefaults(WorldMapSizeSettings settings)
        {
            var so = new SerializedObject(settings);
            so.FindProperty("smallTilesPerSide").intValue = 4;
            so.FindProperty("mediumTilesPerSide").intValue = 8;
            so.FindProperty("largeTilesPerSide").intValue = 10;
            so.FindProperty("smallOceanRingTiles").intValue = 0;
            so.FindProperty("mediumOceanRingTiles").intValue = 0;
            so.FindProperty("largeOceanRingTiles").intValue = 3;
            so.FindProperty("smallMetropolisCount").intValue = 0;
            so.FindProperty("smallCityCount").intValue = 1;
            so.FindProperty("smallSettlementMinCount").intValue = 2;
            so.FindProperty("smallSettlementMaxCount").intValue = 2;
            so.FindProperty("mediumMetropolisCount").intValue = 1;
            so.FindProperty("mediumCityCount").intValue = 3;
            so.FindProperty("mediumSettlementMinCount").intValue = 6;
            so.FindProperty("mediumSettlementMaxCount").intValue = 6;
            so.FindProperty("largeMetropolisCount").intValue = 2;
            so.FindProperty("largeCityCount").intValue = 5;
            so.FindProperty("largeSettlementMinCount").intValue = 13;
            so.FindProperty("largeSettlementMaxCount").intValue = 18;
            so.FindProperty("initialPlayAreaRadiusTiles").intValue = 1;
            so.FindProperty("largestCityFootprintRadiusMeters").floatValue = 960f;
            so.FindProperty("roadExitClearanceMeters").floatValue = 100f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void ApplyTerrainGridDefaults(TerrainGridProfile grid)
        {
            var so = new SerializedObject(grid);
            so.FindProperty("heightmapResolution").intValue = 513;
            so.FindProperty("alphamapResolution").intValue = 512;
            so.FindProperty("baseMapResolution").intValue = 1024;
            so.FindProperty("terrainVerticalSize").floatValue = 1000f;
            so.FindProperty("seaLevelOffsetY").floatValue = -200f;
            so.FindProperty("detailResolution").intValue = 512;
            so.FindProperty("detailSamplesPerPatch").intValue = 16;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(grid);
        }

        private static void ApplyLandformDefaults(LandformProfile landforms)
        {
            if (landforms == null) return;

            var so = new SerializedObject(landforms);
            so.FindProperty("plainsBias").floatValue = 0.22f;
            so.FindProperty("hillsAmplitude").floatValue = 175f;
            so.FindProperty("mountainAmplitude").floatValue = 520f;
            so.FindProperty("edgeBarrierDepthMeters").floatValue = 2200f;
            so.FindProperty("edgeBarrierPeakMeters").floatValue = 220f;
            so.FindProperty("edgeBarrierFalloffMeters").floatValue = 350f;
            so.FindProperty("edgeBarrierNoiseScaleMeters").floatValue = 1400f;
            so.FindProperty("edgeBarrierNoiseAmplitudeMeters").floatValue = 90f;
            so.FindProperty("edgeBarrierSeedOffset").intValue = 89;
            so.FindProperty("oceanOffshoreWidthMeters").floatValue = 220f;
            so.FindProperty("oceanShoreShelfWidthMeters").floatValue = 420f;
            so.FindProperty("oceanBeachWidthMeters").floatValue = 1200f;
            so.FindProperty("oceanBeachMaxElevationMeters").floatValue = 4.5f;
            so.FindProperty("oceanTrenchDepthMeters").floatValue = 120f;
            so.FindProperty("oceanTrenchSteepness").floatValue = 2.4f;
            so.FindProperty("oceanTrenchNoiseAmplitudeMeters").floatValue = 18f;
            so.FindProperty("oceanCoastErosionAmplitudeMeters").floatValue = 1800f;
            so.FindProperty("oceanCoastErosionScaleMeters").floatValue = 12500f;
            so.FindProperty("coastFalloffWidthMeters").floatValue = 2400f;
            so.FindProperty("inlandDryFloorMetersAboveSea").floatValue = 4f;
            so.FindProperty("inlandDryFloorBlendMeters").floatValue = 180f;
            so.FindProperty("islandNoiseScaleMeters").floatValue = 2200f;
            so.FindProperty("archipelagoNoiseScaleMeters").floatValue = 1100f;
            so.FindProperty("islandPeakThreshold").floatValue = 0.68f;
            so.FindProperty("archipelagoPeakThreshold").floatValue = 0.74f;
            so.FindProperty("landformAlgorithmVersion").intValue = 7;
            so.FindProperty("continentalnessScale").floatValue = 9000f;
            so.FindProperty("residualMountainAmplitudeMeters").floatValue = 35f;
            so.FindProperty("foothillSkirtMeters").floatValue = 850f;
            so.FindProperty("crestSharpness").floatValue = 3.1f;
            so.FindProperty("saddleDepth").floatValue = 0.55f;
            so.FindProperty("orogenWarpFraction").floatValue = 0.12f;
            so.FindProperty("structuralValleyDepthMeters").floatValue = 28f;
            so.FindProperty("microDetailAmplitudeMeters").floatValue = 12f;
            so.FindProperty("maxOrogenCoverageFraction").floatValue = 0.4f;
            so.FindProperty("minLowlandSlope12Fraction").floatValue = 0.38f;
            so.FindProperty("minPassCountPerRange").intValue = 1;
            so.FindProperty("highwayPassMaxSlopeDegrees").floatValue = 8f;
            so.FindProperty("passCorridorHalfWidthMeters").floatValue = 120f;
            so.FindProperty("cityPadAlgorithmVersion").intValue = 10;
            so.FindProperty("cityPadFlatMarginMeters").floatValue = 30f;
            so.FindProperty("cityPadPlateauSlopeMeters").floatValue = 180f;
            so.FindProperty("cityPadFalloffMinMeters").floatValue = 160f;
            so.FindProperty("cityPadFalloffMaxMeters").floatValue = 220f;
            so.FindProperty("cityPadCornerRadiusFraction").floatValue = 0.2f;
            so.FindProperty("cityPadHydrologyPruneMarginMeters").floatValue = 16f;
            so.FindProperty("cityPadApproachSlopeDegrees").floatValue = 18f;
            so.FindProperty("cityPadMaxContinuityDegrees").floatValue = 25f;
            so.FindProperty("cityPadEdgeRelaxEnabled").boolValue = true;
            so.FindProperty("cityPadEdgeRelaxMaxIterations").intValue = 40;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(landforms);
        }

        private static void ApplyWaterProfileDefaults(WorldWaterProfile water)
        {
            if (water == null) return;

            var so = new SerializedObject(water);
            if (so.FindProperty("oceanRendererPrefab").objectReferenceValue == null)
            {
                so.FindProperty("oceanRendererPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/02_Shared/Prefabs/Systems/Environment/Crest Ocean.prefab");
            }

            if (so.FindProperty("waterBodyPrefab").objectReferenceValue == null)
            {
                so.FindProperty("waterBodyPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/02_Shared/Prefabs/Systems/Environment/Crest Water Body.prefab");
            }

            if (so.FindProperty("crestOceanMaterial").objectReferenceValue == null)
            {
                so.FindProperty("crestOceanMaterial").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/02_Shared/Materials/Water/ZomberaOcean-Underwater.mat");
            }

            so.FindProperty("crestLakeSeaLevelToleranceMeters").floatValue = 0.05f;
            so.FindProperty("enableFoam").boolValue = true;
            so.FindProperty("foamStrength").floatValue = 0.35f;
            so.FindProperty("foamShoreWidthMeters").floatValue = 20f;
            so.FindProperty("riverMinimumFlowSpeedMetersPerSecond").floatValue = 0.75f;
            so.FindProperty("riverMaximumFlowSpeedMetersPerSecond").floatValue = 4f;

            const string spectraFolder =
                "Assets/03_ThirdParty/Crest/Crest-Examples/Shared/WaveSpectra/";
            if (so.FindProperty("outerSpectrum").objectReferenceValue == null)
            {
                so.FindProperty("outerSpectrum").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(spectraFolder + "WavesModerate.asset");
            }

            if (so.FindProperty("coastalSpectrum").objectReferenceValue == null)
            {
                so.FindProperty("coastalSpectrum").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(spectraFolder + "WavesCalm.asset");
            }

            so.FindProperty("outerWaveWeight").floatValue = 1f;
            so.FindProperty("outerWindSpeedKph").floatValue = 28f;
            so.FindProperty("coastalWaveWeight").floatValue = 0.35f;
            so.FindProperty("coastalWindSpeedKph").floatValue = 8f;
            so.FindProperty("coastalCalmPaddingMeters").floatValue = 400f;

            so.FindProperty("depthCacheLayers").intValue = 1;
            so.FindProperty("seaFloorGeometryLayers").intValue = 0;

            const string inlandSpectraFolder =
                "Assets/03_ThirdParty/Crest/Crest-Examples/LakesAndRivers/Settings/";
            if (so.FindProperty("lakeSpectrum").objectReferenceValue == null)
            {
                so.FindProperty("lakeSpectrum").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                        inlandSpectraFolder + "LakesAndRivers_WaveSpectrum_Lake.asset");
            }

            if (so.FindProperty("riverSpectrum").objectReferenceValue == null)
            {
                so.FindProperty("riverSpectrum").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                        inlandSpectraFolder + "LakesAndRivers_WaveSpectrum_River.asset");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(water);
        }

        private static void ApplyBiomePaletteDefaults(WorldBiomePalette biomes)
        {
            if (biomes == null) return;
            biomes.ApplyProgrammaticDefaults();
            EditorUtility.SetDirty(biomes);
        }

        /// <summary>Batch-mode entry point for regenerating biome palette defaults on disk.</summary>
        public static void RegenerateBiomePaletteAsset()
        {
            var biomes = AssetDatabase.LoadAssetAtPath<WorldBiomePalette>(
                ProfilesFolder + "/WorldBiomePalette.asset");
            if (biomes == null)
            {
                Debug.LogError("[WorldBuilderProfileAssetFactory] WorldBiomePalette.asset not found.");
                return;
            }

            ApplyBiomePaletteDefaults(biomes);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WorldBuilderProfileAssetFactory] Regenerated WorldBiomePalette ({biomes.Biomes.Count} biomes).");
        }

        [MenuItem("Tools/World/Regenerate Biome Palette Defaults")]
        public static void RegenerateBiomePaletteFromMenu() => RegenerateBiomePaletteAsset();

        private static void ApplySurfacePaletteDefaults(WorldSurfacePalette surfaces)
        {
            if (surfaces == null) return;

            const string templatePath =
                "Assets/01_Game/02_World/Terrain/Materials/Microsplat/MicroSplat.mat";
            const string keywordsPath =
                "Assets/01_Game/02_World/Terrain/Materials/Microsplat/MicroSplat_keywords.asset";
            const string propDataPath =
                "Assets/01_Game/02_World/Terrain/Materials/Microsplat/MicroSplat_propdata.asset";

            var so = new SerializedObject(surfaces);
            if (so.FindProperty("microSplatTemplateMaterial").objectReferenceValue == null)
                so.FindProperty("microSplatTemplateMaterial").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Material>(templatePath);
            if (so.FindProperty("microSplatWorldAsset").objectReferenceValue == null)
                so.FindProperty("microSplatWorldAsset").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Object>(keywordsPath);
            if (so.FindProperty("microSplatPropData").objectReferenceValue == null)
                so.FindProperty("microSplatPropData").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Object>(propDataPath);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(surfaces);
        }

        private static void ApplyEnvironmentProfileDefaults(WorldEnvironmentProfile environment)
        {
            if (environment == null) return;

            const string configPath =
                "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Profiles/Configurations/Default Enviro Configuration 3_3_2.asset";
            const string highQualityPath =
                "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Profiles/Quality/High.asset";

            var so = new SerializedObject(environment);
            if (so.FindProperty("enviroConfig").objectReferenceValue == null)
            {
                so.FindProperty("enviroConfig").objectReferenceValue =
                    AssetDatabase.LoadMainAssetAtPath(configPath);
            }

            if (so.FindProperty("enviroQuality") != null &&
                so.FindProperty("enviroQuality").objectReferenceValue == null)
            {
                so.FindProperty("enviroQuality").objectReferenceValue =
                    AssetDatabase.LoadMainAssetAtPath(highQualityPath);
            }

            SetFloatIfExists(so, "timeOfDayHours", 10f);
            SetBoolIfExists(so, "simulateTime", true);
            SetFloatIfExists(so, "cycleLengthInMinutes", 40f);
            SetEnumIfExists(so, "season", (int)WorldEnvironmentSeason.Autumn);
            SetBoolIfExists(so, "autoChangeSeason", false);
            SetBoolIfExists(so, "bindWorldAnchor", true);
            SetBoolIfExists(so, "requireUrpRenderFeature", true);
            SetStringIfExists(so, "enviroQualityName", "High");
            SetStringIfExists(so, "startingWeatherId", "Cloudy");
            SetBoolIfExists(so, "enableFog", true);
            SetBoolIfExists(so, "enableVolumetricFog", true);
            SetBoolIfExists(so, "enableVolumetricClouds", true);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(environment);
        }

        private static void SetFloatIfExists(SerializedObject so, string property, float value)
        {
            var prop = so.FindProperty(property);
            if (prop != null) prop.floatValue = value;
        }

        private static void SetBoolIfExists(SerializedObject so, string property, bool value)
        {
            var prop = so.FindProperty(property);
            if (prop != null) prop.boolValue = value;
        }

        private static void SetStringIfExists(SerializedObject so, string property, string value)
        {
            var prop = so.FindProperty(property);
            if (prop != null) prop.stringValue = value;
        }

        private static void SetEnumIfExists(SerializedObject so, string property, int value)
        {
            var prop = so.FindProperty(property);
            if (prop != null) prop.enumValueIndex = value;
        }

        private struct WorldGenerationProfileWireArgs
        {
            public WorldMapSizeSettings MapSize;
            public TerrainGridProfile TerrainGrid;
            public LandformProfile Landforms;
            public HydrologyProfile Hydrology;
            public WorldWaterProfile Water;
            public WorldBiomePalette Biomes;
            public WorldSurfacePalette Surfaces;
            public WorldNatureProfile Nature;
            public WorldPoiProfile Pois;
            public WorldEnvironmentProfile Environment;
        }

        private static void WireRoot(WorldGenerationProfile root, in WorldGenerationProfileWireArgs args)
        {
            var so = new SerializedObject(root);
            so.FindProperty("profileVersion").intValue = 2;
            so.FindProperty("mapSizeSettings").objectReferenceValue = args.MapSize;
            so.FindProperty("terrainGrid").objectReferenceValue = args.TerrainGrid;
            so.FindProperty("landforms").objectReferenceValue = args.Landforms;
            so.FindProperty("hydrology").objectReferenceValue = args.Hydrology;
            so.FindProperty("water").objectReferenceValue = args.Water;
            so.FindProperty("biomes").objectReferenceValue = args.Biomes;
            so.FindProperty("surfaces").objectReferenceValue = args.Surfaces;
            so.FindProperty("nature").objectReferenceValue = args.Nature;
            so.FindProperty("pois").objectReferenceValue = args.Pois;
            so.FindProperty("environment").objectReferenceValue = args.Environment;

            if (so.FindProperty("roadNetworkSettings").objectReferenceValue == null)
            {
                so.FindProperty("roadNetworkSettings").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<RoadNetworkSettings>(
                        "Assets/02_Shared/ScriptableObjects/World/City/CItygen/RoadNetworkSettings.asset")
                    ?? FindFirstAsset<RoadNetworkSettings>();
            }

            if (so.FindProperty("cityRegion").objectReferenceValue == null)
            {
                so.FindProperty("cityRegion").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<CityRegionAsset>(
                        "Assets/02_Shared/ScriptableObjects/World/City/CItygen/CityRegion.asset")
                    ?? FindFirstAsset<CityRegionAsset>();
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
        }

        private static T FindFirstAsset<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) return asset;
            }

            return null;
        }
    }
}
#endif
