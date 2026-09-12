#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    public static class CityStreetscapeConfigMigration
    {
        private const string NewConfigPath = "Assets/02_Shared/ScriptableObjects/World/CityStreetscapeConfig.asset";

        [MenuItem("Zombera/Migration/Create City Streetscape Config")]
        public static void Create()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(NewConfigPath);
            if (existing != null)
            {
                Debug.Log($"[Migration] CityStreetscapeConfig already exists at {NewConfigPath}");
                return;
            }

            // Old SOs reference deleted types — create fresh default.
            var config = ScriptableObject.CreateInstance<World.Roads.CityStreetscapeConfig>();

            AssetDatabase.CreateAsset(config, NewConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Migration] Created {NewConfigPath}. Open it in the Inspector to assign prefabs.");
        }
    }
}
#endif
