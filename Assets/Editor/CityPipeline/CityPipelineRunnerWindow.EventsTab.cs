#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Simulation;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.Editor
{
    /// <summary>
    ///     Development Hub — fire and abandon building events.
    /// </summary>
    internal sealed partial class CityPipelineRunnerWindow
    {
        private void DrawEventsTab()
        {
            var manager = ResolveStateManager();
            if (manager == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a CityPrefabRoadNetworkBuilder and provision the World Builder stack.",
                    MessageType.Warning);
                return;
            }

            if (!manager.HasState)
            {
                EditorGUILayout.HelpBox("No WorldState loaded. Run the Build pipeline first.", MessageType.Info);
                return;
            }

            DrawEventsSummary(manager);
            EditorGUILayout.Space(6f);

            selectedBuildingId = EditorGUILayout.TextField(
                new GUIContent("Building Id", "Format: Building:value or just value"),
                selectedBuildingId);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use First Building"))
            {
                if (TryFindFirstBuilding(manager, out var first))
                    selectedBuildingId = first.ToString();
            }

            if (GUILayout.Button("Initialize Simulation Service"))
                InitializeSimulationService(manager);
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledGroupScope(_simulationService == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Fire"))
                    ApplyBuildingEvent(true);
                if (GUILayout.Button("Abandon"))
                    ApplyBuildingEvent(false);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawEventsSummary(WorldStateManager manager)
        {
            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            var counts = CountEntities(manager);
            EditorGUILayout.LabelField("Pending Events", counts.PendingEvents.ToString());
            EditorGUILayout.LabelField("Event History", counts.EventHistory.ToString());

            if (TryParseBuildingId(selectedBuildingId, out var id) &&
                manager.TryCopyBuilding(id, out var building))
            {
                EditorGUILayout.LabelField("Selected Building", building.id.ToString());
                EditorGUILayout.LabelField("Abandoned", building.abandoned.ToString());
                EditorGUILayout.LabelField("Fire Active", building.fire?.active == true ? "Yes" : "No");
            }
        }

        private void ApplyBuildingEvent(bool fire)
        {
            if (_simulationService == null)
            {
                EditorUtility.DisplayDialog("Events", "Initialize the simulation service first.", "OK");
                return;
            }

            if (!TryParseBuildingId(selectedBuildingId, out var buildingId))
            {
                EditorUtility.DisplayDialog("Events", "Enter a valid building id.", "OK");
                return;
            }

            var ok = fire
                ? _simulationService.FireBuildingNow(buildingId, 1f, out _, out var report)
                : _simulationService.AbandonBuildingNow(buildingId, out _, out report);

            if (!ok)
            {
                var detail = report?.Errors.Count > 0
                    ? string.Join("\n", report.Errors)
                    : "Event failed.";
                EditorUtility.DisplayDialog("Events", detail, "OK");
                return;
            }

            _sessionLog.Add((fire ? "Fired" : "Abandoned") + " building " + buildingId + ".");
            Repaint();
        }
    }
}
#endif
