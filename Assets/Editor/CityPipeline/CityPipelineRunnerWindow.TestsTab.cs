#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Development Hub — automated Building Vertical Slice Test runner UI.
    /// </summary>
    internal sealed partial class CityPipelineRunnerWindow
    {
        private Vector2 _scrollTestIssues;
        private bool _testRunning;

        private void DrawTestsTab()
        {
            EditorGUILayout.LabelField("Automated Tests", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Building Vertical Slice Test configures Small map + seed 12345, runs the world-build " +
                "pipeline synchronously when possible, then validates state encode/decode, simulation, " +
                "and view audit. Play Mode-only domains are marked NotCovered.",
                MessageType.Info);

            using (new EditorGUI.DisabledGroupScope(_testRunning || _running))
            {
                if (GUILayout.Button("Run Building Vertical Slice Test", GUILayout.Height(32f)))
                    RunBuildingVerticalSliceTest();
            }

            if (_testRunning)
                EditorGUILayout.HelpBox("Test in progress…", MessageType.Info);

            DrawLatestTestReport();
        }

        private void RunBuildingVerticalSliceTest()
        {
            _testRunning = true;
            try
            {
                _latestTestReport = TestOrchestrator.RunBuildingVerticalSliceTest();
                if (!string.IsNullOrWhiteSpace(_latestTestReport.BaselineHash))
                    baselineHash = _latestTestReport.BaselineHash;

                _sessionLog.Add(
                    "Test '" + _latestTestReport.TestName + "' — " +
                    _latestTestReport.OverallStatus + " in " +
                    _latestTestReport.DurationMs + " ms");
            }
            finally
            {
                _testRunning = false;
                Repaint();
            }
        }

        private void DrawLatestTestReport()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Latest Report", EditorStyles.boldLabel);

            if (_latestTestReport == null)
            {
                EditorGUILayout.HelpBox("No test report yet.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("Test", _latestTestReport.TestName);
            EditorGUILayout.LabelField("Status", _latestTestReport.OverallStatus.ToString());
            EditorGUILayout.LabelField("Duration", _latestTestReport.DurationMs + " ms");
            EditorGUILayout.LabelField("Seed", _latestTestReport.WorldSeed.ToString());
            EditorGUILayout.LabelField("Map Tier", _latestTestReport.MapSizeTier);
            EditorGUILayout.LabelField("Baseline Hash", ShortHash(_latestTestReport.BaselineHash));
            EditorGUILayout.LabelField("Final Hash", ShortHash(_latestTestReport.FinalHash));

            if (!string.IsNullOrWhiteSpace(_latestTestReport.ReportPath))
            {
                EditorGUILayout.LabelField("Report Path", _latestTestReport.ReportPath, EditorStyles.miniLabel);
                if (GUILayout.Button("Ping Report File"))
                    PingReportFile(_latestTestReport.ReportPath);
            }

            DrawTestSteps(_latestTestReport);
            DrawTestIssues(_latestTestReport);
            DrawTestCoverage(_latestTestReport);
        }

        private void DrawTestSteps(WorldTestRunReport report)
        {
            if (report.Steps == null || report.Steps.Count == 0)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Steps", EditorStyles.miniBoldLabel);
            for (var i = 0; i < report.Steps.Count; i++)
            {
                var step = report.Steps[i];
                EditorGUILayout.LabelField(
                    step.Status + "  " + step.Name + "  (" + step.DurationMs + " ms)",
                    EditorStyles.miniLabel);
                if (!string.IsNullOrWhiteSpace(step.Detail))
                    EditorGUILayout.LabelField("    " + step.Detail, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawTestIssues(WorldTestRunReport report)
        {
            if (report.Issues == null || report.Issues.Count == 0)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Issues", EditorStyles.miniBoldLabel);
            _scrollTestIssues = EditorGUILayout.BeginScrollView(_scrollTestIssues, GUILayout.Height(100f));
            for (var i = 0; i < report.Issues.Count; i++)
                EditorGUILayout.LabelField("- " + report.Issues[i], EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        private void DrawTestCoverage(WorldTestRunReport report)
        {
            if (report.Coverage == null || report.Coverage.Count == 0)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Not Covered", EditorStyles.miniBoldLabel);
            for (var i = 0; i < report.Coverage.Count; i++)
                EditorGUILayout.LabelField("- " + report.Coverage[i], EditorStyles.miniLabel);
        }

        private static void PingReportFile(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(
                FileUtil.GetProjectRelativePath(path));
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }
    }
}
#endif
