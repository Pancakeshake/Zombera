#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.Editor
{
    /// <summary>
    ///     Development Hub — world-state summary, baseline capture, payload I/O, and diff.
    /// </summary>
    internal sealed partial class CityPipelineRunnerWindow
    {
        private Vector2 _scrollStateDiff;

        private void DrawStateTab()
        {
            var manager = ResolveStateManager();
            if (manager == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a CityPrefabRoadNetworkBuilder and provision the World Builder stack.",
                    MessageType.Warning);
                return;
            }

            DrawStateSummary(manager);
            EditorGUILayout.Space(6f);
            DrawBaselineControls(manager);
            EditorGUILayout.Space(6f);
            DrawPayloadControls(manager);
            EditorGUILayout.Space(6f);
            DrawCompareControls(manager);
        }

        private void DrawStateSummary(WorldStateManager manager)
        {
            EditorGUILayout.LabelField("World State Summary", EditorStyles.boldLabel);

            if (!manager.HasState)
            {
                EditorGUILayout.HelpBox(
                    "No WorldState loaded. Run the Build pipeline or load a saved payload.",
                    MessageType.Info);
                EditorGUILayout.LabelField("Availability", manager.Availability.ToString());
                return;
            }

            var counts = CountEntities(manager);
            EditorGUILayout.LabelField("Availability", manager.Availability.ToString());
            EditorGUILayout.LabelField("Revision", counts.Revision.ToString());
            EditorGUILayout.LabelField("Simulation Hour", counts.CurrentHour.ToString());
            EditorGUILayout.LabelField("Regions", counts.Regions.ToString());
            EditorGUILayout.LabelField("Settlements", counts.Settlements.ToString());
            EditorGUILayout.LabelField("Roads", counts.Roads.ToString());
            EditorGUILayout.LabelField("Districts", counts.Districts.ToString());
            EditorGUILayout.LabelField("Lots", counts.Lots.ToString());
            EditorGUILayout.LabelField("Buildings", counts.Buildings.ToString());
            EditorGUILayout.LabelField("POIs", counts.Pois.ToString());
            EditorGUILayout.LabelField("Terrain Mods", counts.TerrainMods.ToString());
            EditorGUILayout.LabelField("Pending Events", counts.PendingEvents.ToString());
            EditorGUILayout.LabelField("Event History", counts.EventHistory.ToString());

            using (new EditorGUI.DisabledGroupScope(true))
                EditorGUILayout.TextField("Current Hash", ComputeCurrentHash(manager));
        }

        private void DrawBaselineControls(WorldStateManager manager)
        {
            EditorGUILayout.LabelField("Baseline", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledGroupScope(true))
                baselineHash = EditorGUILayout.TextField("Baseline Hash", baselineHash);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture Baseline"))
                CaptureBaseline(manager);
            if (GUILayout.Button("Validate"))
                ValidateCurrentState(manager);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPayloadControls(WorldStateManager manager)
        {
            EditorGUILayout.LabelField("Test Payload", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Folder", ReportsDirectory, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Payload File", TestPayloadPath, EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Test Payload"))
                SaveTestPayload(manager);
            if (GUILayout.Button("Load Test Payload"))
                LoadTestPayload(manager);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCompareControls(WorldStateManager manager)
        {
            EditorGUILayout.LabelField("Compare", EditorStyles.boldLabel);

            if (string.IsNullOrWhiteSpace(baselineHash))
            {
                EditorGUILayout.HelpBox("Capture a baseline hash before comparing.", MessageType.Info);
                return;
            }

            if (!manager.HasState)
            {
                EditorGUILayout.HelpBox("No WorldState to compare.", MessageType.Warning);
                return;
            }

            var currentHash = ComputeCurrentHash(manager);
            var hashMatch = string.Equals(baselineHash, currentHash, StringComparison.OrdinalIgnoreCase);
            EditorGUILayout.LabelField("Current Hash", currentHash);
            EditorGUILayout.LabelField("Hash Match", hashMatch ? "Yes" : "No");

            if (GUILayout.Button("Diff Snapshot Entries"))
                DiffAgainstBaseline(manager);
        }

        private void CaptureBaseline(WorldStateManager manager)
        {
            if (!manager.HasState)
            {
                EditorUtility.DisplayDialog("Capture Baseline", "No WorldState is loaded.", "OK");
                return;
            }

            baselineHash = ComputeCurrentHash(manager);
            _sessionLog.Add("Baseline captured — hash " + ShortHash(baselineHash));
            Repaint();
        }

        private void ValidateCurrentState(WorldStateManager manager)
        {
            if (!manager.HasState)
            {
                EditorUtility.DisplayDialog("Validate", "No WorldState is loaded.", "OK");
                return;
            }

            var state = manager.CaptureCanonicalCopy();
            var report = WorldStateValidator.Validate(state);
            if (report.IsValid)
            {
                _sessionLog.Add("WorldState validation passed.");
                EditorUtility.DisplayDialog("Validate", "WorldState is valid.", "OK");
                return;
            }

            var message = report.Errors.Count > 0
                ? string.Join("\n", report.Errors)
                : "Validation failed.";
            _sessionLog.Add("WorldState validation failed — " + report.Errors.Count + " error(s).");
            EditorUtility.DisplayDialog("Validate", message, "OK");
        }

        private void SaveTestPayload(WorldStateManager manager)
        {
            if (!manager.HasState)
            {
                EditorUtility.DisplayDialog("Save Payload", "No WorldState is loaded.", "OK");
                return;
            }

            try
            {
                var payload = WorldStatePayloadCodec.Encode(manager.CaptureCanonicalCopy());
                var json = JsonUtility.ToJson(payload, true);
                File.WriteAllText(TestPayloadPath, json);
                _sessionLog.Add("Saved test payload — " + TestPayloadPath);
                EditorUtility.DisplayDialog("Save Payload", "Saved to:\n" + TestPayloadPath, "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Save Payload", "Failed: " + ex.Message, "OK");
            }
        }

        private void LoadTestPayload(WorldStateManager manager)
        {
            if (!File.Exists(TestPayloadPath))
            {
                EditorUtility.DisplayDialog("Load Payload", "Payload file not found:\n" + TestPayloadPath, "OK");
                return;
            }

            try
            {
                var json = File.ReadAllText(TestPayloadPath);
                var payload = JsonUtility.FromJson<WorldStatePayload>(json);
                if (payload == null || string.IsNullOrWhiteSpace(payload.payloadBase64))
                {
                    EditorUtility.DisplayDialog("Load Payload", "Payload file is invalid.", "OK");
                    return;
                }

                var decode = WorldStatePayloadCodec.Decode(payload.payloadBase64, expectedHashSha256: payload.hashSha256);
                if (!decode.IsValid || decode.state == null)
                {
                    var detail = decode.report?.Errors.Count > 0
                        ? string.Join("\n", decode.report.Errors)
                        : "Decode failed.";
                    EditorUtility.DisplayDialog("Load Payload", detail, "OK");
                    return;
                }

                if (!manager.TryLoad(decode.state, out var loadReport))
                {
                    var detail = loadReport?.Errors.Count > 0
                        ? string.Join("\n", loadReport.Errors)
                        : "Load failed.";
                    EditorUtility.DisplayDialog("Load Payload", detail, "OK");
                    return;
                }

                baselineHash = decode.hashSha256;
                _sessionLog.Add("Loaded test payload — hash " + ShortHash(baselineHash));
                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Load Payload", "Failed: " + ex.Message, "OK");
            }
        }

        private void DiffAgainstBaseline(WorldStateManager manager)
        {
            if (!manager.HasState)
                return;

            var currentHash = ComputeCurrentHash(manager);
            if (string.Equals(baselineHash, currentHash, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Compare", "Hashes match — no diff entries.", "OK");
                return;
            }

            if (!TryLoadBaselineSnapshot(out var baselineState))
            {
                EditorUtility.DisplayDialog(
                    "Compare",
                    "Hashes differ but no saved payload matches the baseline hash. Save a payload after capture.",
                    "OK");
                return;
            }

            var diff = WorldStateDiffer.Diff(baselineState, manager.CaptureCanonicalCopy());
            _scrollStateDiff = EditorGUILayout.BeginScrollView(_scrollStateDiff, GUILayout.Height(180f));
            EditorGUILayout.LabelField("Diff Entries: " + diff.Entries.Count, EditorStyles.boldLabel);
            for (var i = 0; i < diff.Entries.Count; i++)
            {
                var entry = diff.Entries[i];
                EditorGUILayout.LabelField(
                    entry.Kind + "  " + entry.Path,
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private bool TryLoadBaselineSnapshot(out WorldState baselineState)
        {
            baselineState = null;
            if (!File.Exists(TestPayloadPath))
                return false;

            try
            {
                var json = File.ReadAllText(TestPayloadPath);
                var payload = JsonUtility.FromJson<WorldStatePayload>(json);
                if (payload == null ||
                    !string.Equals(payload.hashSha256, baselineHash, StringComparison.OrdinalIgnoreCase))
                    return false;

                var decode = WorldStatePayloadCodec.Decode(payload.payloadBase64, expectedHashSha256: baselineHash);
                if (!decode.IsValid || decode.state == null)
                    return false;

                baselineState = decode.state;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ComputeCurrentHash(WorldStateManager manager)
        {
            if (manager == null || !manager.HasState)
                return string.Empty;

            return WorldStateHasher.ComputeHash(manager.CaptureCanonicalCopy());
        }

        private static string ShortHash(string hash) =>
            string.IsNullOrEmpty(hash) || hash.Length <= 12 ? hash ?? string.Empty : hash.Substring(0, 12) + "…";
    }
}
#endif
