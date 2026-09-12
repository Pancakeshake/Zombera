#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    [CustomEditor(typeof(CityPrefabRoadNetworkBuilder))]
    internal sealed partial class CityPrefabRoadNetworkBuilderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var builder = (CityPrefabRoadNetworkBuilder)target;
            builder.RefreshReferences();

            DrawHubHeader();
            DrawPipelineSteps(builder);
            DrawRegionSection(builder);

            serializedObject.Update();
            DrawScriptableObjectSection(serializedObject, builder);
            DrawScriptsSection(serializedObject);
            DrawSceneReferencesSection(serializedObject);
            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawHubHeader()
        {
            var hubStyle = new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = new Color(0.90f, 0.49f, 0.13f) } };
            EditorGUILayout.LabelField("World Builder Hub", hubStyle);
        }

        private static void DrawScriptsSection(SerializedObject serializedObject)
        {
            EditorGUILayout.Space(8f);
            var headingStyle = new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = new Color(0.90f, 0.49f, 0.13f) } };
            EditorGUILayout.LabelField("Scripts", headingStyle);

            var descStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                { normal = { textColor = DescriptionColor } };

            EditorGUILayout.Space(6f);
            var roadSystemProp = serializedObject.FindProperty("proceduralRoadSystem");
            if (roadSystemProp != null)
            {
                EditorGUILayout.PropertyField(roadSystemProp, new GUIContent("Procedural Road System"));
                EditorGUILayout.LabelField("Handles road mesh generation, junction placement, and EasyRoads integration.", descStyle);
            }

        }

        private static void DrawSceneReferencesSection(SerializedObject serializedObject)
        {
            EditorGUILayout.Space(8f);
            var headingStyle = new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = new Color(0.90f, 0.49f, 0.13f) } };
            EditorGUILayout.LabelField("Scene References", headingStyle);

            var descStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                { normal = { textColor = DescriptionColor } };

            EditorGUILayout.Space(6f);
            var groundRefProp = serializedObject.FindProperty("groundReference");
            if (groundRefProp != null)
            {
                EditorGUILayout.PropertyField(groundRefProp, new GUIContent("Ground Reference"));
                EditorGUILayout.LabelField("Raycast origin for terrain height sampling. Falls back to transform position when null.", descStyle);
            }
        }

        private static void MarkDirty(CityPrefabRoadNetworkBuilder builder)
        {
            EditorUtility.SetDirty(builder);
            var scene = builder.gameObject.scene;
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
