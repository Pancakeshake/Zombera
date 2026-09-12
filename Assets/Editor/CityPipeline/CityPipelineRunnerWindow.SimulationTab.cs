#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Simulation;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.Editor
{
    /// <summary>
    ///     Development Hub — simulation clock advance via <see cref="WorldStateSimulationService"/>.
    /// </summary>
    internal sealed partial class CityPipelineRunnerWindow
    {
        private WorldStateSimulationService _simulationService;

        private void DrawSimulationTab()
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

            DrawSimulationSummary(manager);
            EditorGUILayout.Space(6f);

            if (GUILayout.Button("Initialize Simulation Service"))
                InitializeSimulationService(manager);

            using (new EditorGUI.DisabledGroupScope(_simulationService == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+1 Hour"))
                    AdvanceSimulation(1);
                if (GUILayout.Button("+1 Day"))
                    AdvanceSimulation(24);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+7 Days"))
                    AdvanceSimulation(24 * 7);
                if (GUILayout.Button("+30 Days"))
                    AdvanceSimulation(24 * 30);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("+1 Year"))
                    AdvanceSimulation(24 * 365);
            }
        }

        private void DrawSimulationSummary(WorldStateManager manager)
        {
            EditorGUILayout.LabelField("Simulation", EditorStyles.boldLabel);
            var counts = CountEntities(manager);
            EditorGUILayout.LabelField("Current Hour", counts.CurrentHour.ToString());
            EditorGUILayout.LabelField("Pending Events", counts.PendingEvents.ToString());
            EditorGUILayout.LabelField("Event History", counts.EventHistory.ToString());
            EditorGUILayout.LabelField(
                "Service",
                _simulationService != null ? "Initialized" : "Not initialized");
        }

        private void InitializeSimulationService(WorldStateManager manager)
        {
            _simulationService = new WorldStateSimulationService(manager);
            _sessionLog.Add("Simulation service initialized.");
            Repaint();
        }

        private void AdvanceSimulation(int hours)
        {
            if (_simulationService == null)
            {
                EditorUtility.DisplayDialog("Simulation", "Initialize the simulation service first.", "OK");
                return;
            }

            if (!_simulationService.AdvanceHours(hours, out var changes, out var report))
            {
                var detail = report?.Errors.Count > 0
                    ? string.Join("\n", report.Errors)
                    : "Advance failed.";
                EditorUtility.DisplayDialog("Simulation", detail, "OK");
                return;
            }

            var delta = changes?.UpdatedEntityIds?.Count ?? 0;
            _sessionLog.Add("Advanced simulation +" + hours + "h — updated " + delta + " entit(y/ies).");
            Repaint();
        }
    }
}
#endif
