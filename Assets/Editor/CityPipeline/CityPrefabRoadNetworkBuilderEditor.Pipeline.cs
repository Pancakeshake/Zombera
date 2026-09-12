#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    internal sealed partial class CityPrefabRoadNetworkBuilderEditor : UnityEditor.Editor
    {
        private static void DrawPipelineSteps(CityPrefabRoadNetworkBuilder builder)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Pipeline Steps", EditorStyles.boldLabel);

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Step-by-step generation now lives in the World Builder window — grouped "
                + "into phase sections with per-section and per-step run buttons.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open World Builder — Development Hub", GUILayout.Height(30f)))
                CityPipelineRunnerWindow.ShowFor(builder);
            EditorGUILayout.EndHorizontal();

            // ── Toggles ──
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Toggles", EditorStyles.boldLabel);

            var buildConfig = builder.BuildConfig;
            bool showFills;
            if (buildConfig != null)
            {
                showFills = buildConfig.showDistrictFillColors;
                var newVal = EditorGUILayout.Toggle("District Fill Colors", showFills);
                if (newVal != showFills)
                {
                    Undo.RecordObject(buildConfig, "Toggle District Fill Colors");
                    buildConfig.showDistrictFillColors = newVal;
                    EditorUtility.SetDirty(buildConfig);
                    builder.SyncDistrictFillVisibility();
                    MarkDirty(builder);
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.Toggle("District Fill Colors", false);
                EditorGUILayout.HelpBox("Assign a CityBuildConfig to enable this toggle.", MessageType.Info);
            }

            var regionMode = builder.RegionModeEnabled;
            var newRegionMode = EditorGUILayout.Toggle("Multi-City (Region)", regionMode);
            if (newRegionMode != regionMode)
            {
                Undo.RecordObject(builder, "Toggle Multi-City Region");
                builder.RegionModeEnabled = newRegionMode;
                MarkDirty(builder);
            }
        }
    }
}
#endif