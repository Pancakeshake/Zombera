#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.World.Roads;
using Zombera.World.Roads.Testing;

namespace Zombera.Editor
{
    [CustomEditor(typeof(RoadTesterGenerate))]
    internal sealed class RoadTesterGenerateEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var tester = (RoadTesterGenerate)target;

            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "Runs the city math road plan on the terrain in this scene via " +
                "CityPrefabRoadNetworkBuilder. Default backend is procedural strip meshes.",
                MessageType.Info);

            EditorGUILayout.Space(4f);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.75f, 0.45f);
            const string buttonLabel = "Generate City Roads (Procedural)";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(40f)))
            {
                if (!EnsureReady(tester, out var error))
                {
                    EditorUtility.DisplayDialog("Road Tester", error, "OK");
                    return;
                }

                tester.Generate();
                EditorSceneManager.MarkSceneDirty(tester.gameObject.scene);
            }

            GUI.backgroundColor = new Color(0.55f, 0.70f, 0.95f);
            if (GUILayout.Button("Ensure Stack / Wire Refs", GUILayout.Height(28f)))
            {
                if (!EnsureReady(tester, out var error))
                    EditorUtility.DisplayDialog("Road Tester", error, "OK");
                else
                {
                    EditorUtility.SetDirty(tester);
                    EditorSceneManager.MarkSceneDirty(tester.gameObject.scene);
                    EditorUtility.DisplayDialog(
                        "Road Tester",
                        "Stack ready. City builder, settings, ground, and road content root wired.",
                        "OK");
                }
            }

            GUI.backgroundColor = prev;
        }

        internal static bool EnsureReady(RoadTesterGenerate tester, out string error)
        {
            error = null;
            if (tester == null)
            {
                error = "RoadTesterGenerate is null.";
                return false;
            }

            var builder = tester.CityBuilder;
            if (builder == null)
                builder = tester.GetComponent<CityPrefabRoadNetworkBuilder>();
            if (builder == null)
                builder = Undo.AddComponent<CityPrefabRoadNetworkBuilder>(tester.gameObject);
            tester.SetCityBuilder(builder);

            if (!CityPrefabRoadStackProvisioner.EnsureForBuilder(builder))
            {
                error = "Failed to ensure CityRoadStack / ProceduralRoadSystem.";
                return false;
            }

            WireScriptableObjects(tester, builder);
            WireGroundReference(tester, builder);

            if (!EnsureRoadContentRoot(builder, out error))
                return false;

            builder.RefreshReferences();

            EditorUtility.SetDirty(builder);
            EditorUtility.SetDirty(tester);
            return true;
        }

        private static bool EnsureRoadContentRoot(
            CityPrefabRoadNetworkBuilder builder,
            out string error)
        {
            error = null;
            if (builder == null)
            {
                error = "CityPrefabRoadNetworkBuilder is null.";
                return false;
            }

            ProceduralCityRoadBuilder.EnsureNetworkRoot(builder.transform);
            return true;
        }

        private static void WireGroundReference(RoadTesterGenerate tester, CityPrefabRoadNetworkBuilder builder)
        {
            var ground = tester.GroundReference;
            if (ground == null)
            {
                var terrain = Terrain.activeTerrain;
                if (terrain != null)
                    ground = terrain.transform;
                else
                {
                    var terrainGo = GameObject.Find("Terrain");
                    if (terrainGo != null)
                        ground = terrainGo.transform;
                }

                if (ground != null)
                    tester.SetGroundReference(ground);
            }

            if (ground == null)
                return;

            var so = new SerializedObject(builder);
            var prop = so.FindProperty("groundReference");
            if (prop == null)
                return;
            prop.objectReferenceValue = ground;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireScriptableObjects(RoadTesterGenerate tester, CityPrefabRoadNetworkBuilder builder)
        {
            var layout = tester.LayoutAsset ?? LoadFirstAsset<CityMathRoadLayoutAsset>();
            var settings = tester.RoadNetworkSettings ?? LoadFirstAsset<RoadNetworkSettings>();
            var buildConfig = tester.BuildConfig ?? LoadFirstAsset<CityBuildConfig>();

            if (layout != null)
                tester.SetLayoutAsset(layout);
            if (settings != null)
                tester.SetRoadNetworkSettings(settings);
            if (buildConfig != null)
                tester.SetBuildConfig(buildConfig);

            var so = new SerializedObject(builder);
            AssignIfEmpty(so, "layoutAsset", layout);
            AssignIfEmpty(so, "roadNetworkSettings", settings);
            AssignIfEmpty(so, "buildConfig", buildConfig);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (layout != null)
                builder.LayoutAsset = layout;
            if (buildConfig != null)
                builder.BuildConfig = buildConfig;
        }

        private static void AssignIfEmpty(SerializedObject so, string propertyName, Object value)
        {
            if (value == null)
                return;
            var prop = so.FindProperty(propertyName);
            if (prop == null)
                return;
            if (prop.objectReferenceValue == null)
                prop.objectReferenceValue = value;
        }

        private static T LoadFirstAsset<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    return asset;
            }

            return null;
        }
    }
}
#endif
