using System.IO;
using UnityEditor;
using UnityEngine;
using Zombera.World;
using Zombera.World.City;
using Zombera.World.Roads;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    public static class CityAreaRuntimeConfigBakeTool
    {
        private const string ConfigAssetPath = "Assets/02_Shared/ScriptableObjects/World/CityAreaRuntimeConfig.asset";
        private const string ResourcesConfigPath = "Assets/Resources/World/CityAreaRuntimeConfig.asset";
        private const string RoadNetworkSettingsPath = "Assets/02_Shared/ScriptableObjects/World/RoadNetworkSettings.asset";
        private const string StreamedCityCatalogPath = "Assets/Resources/World/StreamedCityCatalog.asset";

        [MenuItem("Tools/World/City/Bake City Area Runtime Config From Hub", priority = -500)]
        public static void BakeFromHubScene()
        {
            StreamedCityBuilderQuickTool.RebuildStreamedCityCatalogMenu();

            EnsureConfigAssetExists(out var config);
            if (config == null)
            {
                Debug.LogError("[CityAreaRuntimeConfigBakeTool] Could not create CityAreaRuntimeConfig asset.");
                return;
            }

            config.ApplyHubDefaults();

            config.roadNetworkSettings =
                AssetDatabase.LoadAssetAtPath<RoadNetworkSettings>(RoadNetworkSettingsPath);
            config.buildingCatalog =
                AssetDatabase.LoadAssetAtPath<StreamedCityCatalog>(StreamedCityCatalogPath);
            config.spawnCityRoadMeshes = true;
            config.waitForMainTerrainBeforeBuild = true;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            MirrorConfigToResources(config);
            Debug.Log("[CityAreaRuntimeConfigBakeTool] CityAreaRuntimeConfig baked to " + ConfigAssetPath);
        }

        public static CityAreaRuntimeConfig EnsureConfigAssetExists(out CityAreaRuntimeConfig config)
        {
            config = AssetDatabase.LoadAssetAtPath<CityAreaRuntimeConfig>(ConfigAssetPath);
            if (config != null)
                return config;

            var folder = Path.GetDirectoryName(ConfigAssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            config = ScriptableObject.CreateInstance<CityAreaRuntimeConfig>();
            config.ApplyHubDefaults();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            MirrorConfigToResources(config);
            return config;
        }

        private static void MirrorConfigToResources(CityAreaRuntimeConfig source)
        {
            if (source == null)
                return;

            var resourcesFolder = Path.GetDirectoryName(ResourcesConfigPath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(resourcesFolder) && !AssetDatabase.IsValidFolder(resourcesFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                if (!AssetDatabase.IsValidFolder("Assets/Resources/World"))
                    AssetDatabase.CreateFolder("Assets/Resources", "World");
            }

            var existing = AssetDatabase.LoadAssetAtPath<CityAreaRuntimeConfig>(ResourcesConfigPath);
            if (existing == null)
            {
                var copy = Object.Instantiate(source);
                AssetDatabase.CreateAsset(copy, ResourcesConfigPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, existing);
                EditorUtility.SetDirty(existing);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
