#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>
    ///     Minimal road stack for isolated city-prefab authoring scenes (no WorldManager / MapMagic).
    /// </summary>
    internal static class CityPrefabRoadStackProvisioner
    {
        private const string SettingsAssetPath = "Assets/02_Shared/ScriptableObjects/World/RoadNetworkSettings.asset";
        private const string StackChildName = "CityRoadStack";

        public static bool EnsureForBuilder(CityPrefabRoadNetworkBuilder builder)
        {
            if (builder == null) return false;

            var scene = builder.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            var stackRoot = builder.transform.Find(StackChildName);
            if (stackRoot == null)
            {
                var stackObject = new GameObject(StackChildName);
                Undo.RegisterCreatedObjectUndo(stackObject, "Create City Road Stack");
                stackObject.transform.SetParent(builder.transform, false);
                stackRoot = stackObject.transform;
            }

            var roadSystem = stackRoot.GetComponent<ProceduralRoadSystem>();
            if (roadSystem == null)
            {
                roadSystem = Undo.AddComponent<ProceduralRoadSystem>(stackRoot.gameObject);
            }

            var settings = LoadRoadNetworkSettings();
            ConfigureProceduralRoadSystem(roadSystem, settings);
            AssignBuilderReferences(builder, roadSystem, settings);

            EditorUtility.SetDirty(roadSystem);
            EditorUtility.SetDirty(builder);
            EditorSceneManager.MarkSceneDirty(scene);
            return roadSystem != null;
        }

        public static void SetupHubScene(CityPrefabRoadNetworkBuilder hub)
        {
            if (hub == null) return;
            EnsureForBuilder(hub);
            hub.RefreshReferences();
        }

        private static RoadNetworkSettings LoadRoadNetworkSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<RoadNetworkSettings>(SettingsAssetPath);
            if (settings != null) return settings;

            var guids = AssetDatabase.FindAssets("t:RoadNetworkSettings");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                settings = AssetDatabase.LoadAssetAtPath<RoadNetworkSettings>(path);
                if (settings != null) return settings;
            }

            return null;
        }

        private static void ConfigureProceduralRoadSystem(ProceduralRoadSystem roadSystem, RoadNetworkSettings settings)
        {
            if (roadSystem == null) return;

            Undo.RecordObject(roadSystem, "Configure City Prefab Road System");

            var serialized = new SerializedObject(roadSystem);
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.FindProperty("autoGenerateOnTileApply").boolValue = false;
            serialized.FindProperty("autoResolveReferences").boolValue = false;
            serialized.FindProperty("enableEditorPinnedTileAuthoringMode").boolValue = false;
            serialized.FindProperty("tileStreamBridge").objectReferenceValue = null;
            serialized.FindProperty("roadGameplayService").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBuilderReferences(
            CityPrefabRoadNetworkBuilder builder,
            ProceduralRoadSystem roadSystem,
            RoadNetworkSettings settings)
        {
            var serialized = new SerializedObject(builder);
            serialized.FindProperty("proceduralRoadSystem").objectReferenceValue = roadSystem;
            serialized.FindProperty("roadNetworkSettings").objectReferenceValue = settings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
