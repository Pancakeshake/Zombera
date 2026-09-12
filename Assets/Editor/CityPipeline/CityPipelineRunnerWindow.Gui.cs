#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>
    ///     GUI for <see cref="CityPipelineRunnerWindow" /> — Development Hub tabs,
    ///     build pipeline controls, and session log.
    /// </summary>
    internal sealed partial class CityPipelineRunnerWindow : EditorWindow
    {
        private static readonly string[] HubTabLabels =
        {
            "Build",
            "State",
            "Simulation",
            "Events",
            "Streaming",
            "Tests"
        };

        private void OnGUI()
        {
            DrawHeader();
            DrawHubToolbar();
            EditorGUILayout.Space(4f);

            switch (hubTab)
            {
                case WorldDevelopmentHubTab.Build:
                    DrawBuildTab();
                    break;
                case WorldDevelopmentHubTab.State:
                    DrawStateTab();
                    break;
                case WorldDevelopmentHubTab.Simulation:
                    DrawSimulationTab();
                    break;
                case WorldDevelopmentHubTab.Events:
                    DrawEventsTab();
                    break;
                case WorldDevelopmentHubTab.Streaming:
                    DrawStreamingTab();
                    break;
                case WorldDevelopmentHubTab.Tests:
                    DrawTestsTab();
                    break;
            }
        }

        private void DrawHeader()
        {
            var header = new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = new Color(0.90f, 0.49f, 0.13f) } };
            EditorGUILayout.LabelField("World Builder — Development Hub", header);
        }

        private void DrawHubToolbar()
        {
            hubTab = (WorldDevelopmentHubTab)GUILayout.Toolbar((int)hubTab, HubTabLabels);
        }

        private void DrawBuildTab()
        {
            DrawBuildHeaderFields();
            DrawControls();
            DrawStepSections();
            DrawSessionLog();
            DrawRunTotal();
        }

        private void DrawBuildHeaderFields()
        {
            builder = (CityPrefabRoadNetworkBuilder)EditorGUILayout.ObjectField(
                "Builder", builder, typeof(CityPrefabRoadNetworkBuilder), true);
            worldProfile = (WorldGenerationProfile)EditorGUILayout.ObjectField(
                "World Profile", worldProfile, typeof(WorldGenerationProfile), false);
            editorMapTier = (WorldMapSizeTier)EditorGUILayout.EnumPopup("Map Size Tier", editorMapTier);
            DrawMapSizeTierSummary();
            editorWorldSeed = EditorGUILayout.IntField("World Seed", editorWorldSeed);
            if (worldProfile != null && GUILayout.Button("Ping Profile Asset"))
                EditorGUIUtility.PingObject(worldProfile);
        }

        private void DrawMapSizeTierSummary()
        {
            var mapSize = worldProfile != null ? worldProfile.MapSizeSettings : null;
            var tilesPerSide = mapSize != null
                ? mapSize.GetTilesPerSide(editorMapTier)
                : editorMapTier switch
                {
                    WorldMapSizeTier.Small => 4,
                    WorldMapSizeTier.Large => 16,
                    _ => 8
                };
            var tileSize = WorldMapSizeSettings.TileSizeMeters;
            var worldKm = tilesPerSide * tileSize / 1000f;
            var summary = $"Terrain grid: {tilesPerSide}×{tilesPerSide} tiles ({worldKm:F0} km²).";
            if (mapSize != null)
            {
                var ring = mapSize.GetOceanRingTiles(editorMapTier);
                var core = mapSize.GetCoreTilesPerSide(editorMapTier);
                var quotaMax = mapSize.ResolveQuota(editorMapTier, seed: 0, useMaxSettlements: true);
                var settlementRange = editorMapTier switch
                {
                    WorldMapSizeTier.Small =>
                        $"{mapSize.SmallSettlementMinCount}-{mapSize.SmallSettlementMaxCount}",
                    WorldMapSizeTier.Large =>
                        $"{mapSize.LargeSettlementMinCount}-{mapSize.LargeSettlementMaxCount}",
                    _ =>
                        $"{mapSize.MediumSettlementMinCount}-{mapSize.MediumSettlementMaxCount}"
                };
                summary +=
                    $" Core {core}×{core}, ocean ring {ring}. Hierarchy: {quotaMax.Metropolis} metropolis, {quotaMax.City} city, " +
                    $"{settlementRange} smaller settlements.";
            }

            summary += " Site scatter uses the allocated WorldTerrainGrid footprint after Allocate Terrain Grid.";
            EditorGUILayout.HelpBox(summary, MessageType.None);
        }

        private void DrawControls()
        {
            EditorGUILayout.Space(4f);

            using (new EditorGUI.DisabledGroupScope(_running))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Run All", GUILayout.Height(30f)))
                    StartRun();
                if (GUILayout.Button("Run Next", GUILayout.Height(30f)))
                    RunNextStep();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_running ? "Running…" : "Stop", GUILayout.Height(26f)))
                StopRun();
            if (GUILayout.Button("Reset Steps", GUILayout.Height(26f)))
                RebuildSteps();
            EditorGUILayout.EndHorizontal();

            DrawFastBuildFoldout();

            pauseAfterStep = EditorGUILayout.Toggle("Pause after each step", pauseAfterStep);
            stepDelaySeconds = EditorGUILayout.Slider("Pause between steps (s)", stepDelaySeconds, 0f, 3f);

            showFailureMarkers = EditorGUILayout.Toggle("Show failure markers in Scene view", showFailureMarkers);
            showScatterGizmo = EditorGUILayout.Toggle(
                new GUIContent(
                    "Show scatter zone in Scene view",
                    "Orange = allocated terrain grid. Green = city scatter zone (same footprint). Blue = placed sites."),
                showScatterGizmo);

            if (_running)
                EditorGUILayout.HelpBox("Running: " + _runningStepName, MessageType.Info);
        }

        private void DrawFastBuildFoldout()
        {
            fastBuildFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                fastBuildFoldout,
                new GUIContent(
                    "Fast Build",
                    "Iteration shortcuts for hub builds: surface paint, road quality, headless roads, and cache reuse."));
            if (fastBuildFoldout)
            {
                EditorGUI.indentLevel++;

                var newPaintMode = (SurfacePaintQualityMode)EditorGUILayout.EnumPopup(
                    new GUIContent(
                        "Surface Paint Quality",
                        "Fast: 32×32 block upscale, no soften (iteration). " +
                        "Balanced: 128×128 bilinear + SoftRadius soft edges. " +
                        "Quality: alphamap stride-2 + multi-pass soften (acceptance). " +
                        "Independent of Fast Roads. Quay rock stamps after soft paint — crop open beach for soft-edge checks."),
                    hubSurfacePaintQuality);
                if (newPaintMode != hubSurfacePaintQuality)
                    SetHubSurfacePaintQuality(newPaintMode);

                roadBuildQuality = (RoadBuildQualityMode)EditorGUILayout.EnumPopup(
                    new GUIContent(
                        "Road Build Quality",
                        "Fast Iteration uses the pre-baked city pads and highway corridors. " +
                        "Full Fidelity applies the road-stage terrain conformance pass."),
                    roadBuildQuality);

                fastRoadsMode = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Fast Roads (headless)",
                        "Runs the Roads step synchronously — no per-tick yields or Scene view repaints. " +
                        "Fastest option, but there is no live preview and Stop cannot interrupt mid-step."),
                    fastRoadsMode);

                if (builder != null)
                {
                    var newFixedSeed = EditorGUILayout.IntField(
                        new GUIContent(
                            "Fixed Region Seed",
                            "0 = normal seed flow. Set > 0 to force the same region layout and site scatter " +
                            "on every Roads run for repeatable timing comparisons."),
                        builder.FixedRegionSeedOverride);
                    if (newFixedSeed != builder.FixedRegionSeedOverride)
                    {
                        Undo.RecordObject(builder, "Change Fixed Region Seed");
                        builder.FixedRegionSeedOverride = newFixedSeed;
                    }

                    var newReuse = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Reuse Cached Roads (same seed)",
                            "Skips rebuilding roads when the region seed matches the previous build — " +
                            "fast iteration through pipeline steps. Disable when changing road or layout settings."),
                        builder.ReuseCachedRoadsOnSameSeed);
                    if (newReuse != builder.ReuseCachedRoadsOnSameSeed)
                    {
                        Undo.RecordObject(builder, "Toggle Reuse Cached Roads");
                        builder.ReuseCachedRoadsOnSameSeed = newReuse;
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawStepSections()
        {
            _scrollSteps = EditorGUILayout.BeginScrollView(_scrollSteps, GUILayout.Height(290f));
            for (var i = 0; i < _sections.Count; i++)
                DrawSection(_sections[i]);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSection(string section)
        {
            var expanded = IsSectionExpanded(section);
            var newExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, SectionHeader(section));
            if (newExpanded != expanded)
                ToggleSectionExpanded(section);
            if (newExpanded)
                DrawSectionBody(section);
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawSectionBody(string section)
        {
            using (new EditorGUI.DisabledGroupScope(_running))
            {
                if (GUILayout.Button("Run Section", GUILayout.Height(22f)))
                    RunSection(section);
            }

            for (var i = 0; i < _steps.Count; i++)
            {
                if (_steps[i].Section == section)
                    DrawStepRow(i);
            }
        }

        private void DrawStepRow(int index)
        {
            var step = _steps[index];
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledGroupScope(_running))
            {
                if (GUILayout.Button(
                        new GUIContent(
                            "▶",
                            "Incomplete: runs from the first pending step through this one. " +
                            "Next pending only if that's this step. Already done: redo this step only."),
                        GUILayout.Width(28f), GUILayout.Height(18f)))
                    RunSingle(index);
            }

            EditorGUILayout.LabelField(
                StatusPrefix(step.Status) + step.Name + DurationSuffix(step),
                StyleForStatus(step.Status));
            EditorGUILayout.EndHorizontal();

            for (var l = 0; l < step.Logs.Count; l++)
                EditorGUILayout.LabelField("    " + step.Logs[l], EditorStyles.wordWrappedMiniLabel);
        }

        private string SectionHeader(string section)
        {
            var done = 0;
            var total = 0;
            for (var i = 0; i < _steps.Count; i++)
            {
                if (_steps[i].Section != section)
                    continue;
                total++;
                if (_steps[i].Status == StepStatus.Done || _steps[i].Status == StepStatus.Warning)
                    done++;
            }

            return total == 0 ? section : $"{section}  ({done}/{total})";
        }

        private void DrawSessionLog()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Session Log", EditorStyles.boldLabel);
            _scrollLog = EditorGUILayout.BeginScrollView(_scrollLog, GUILayout.Height(150f));
            for (var i = 0; i < _sessionLog.Count; i++)
                EditorGUILayout.LabelField(_sessionLog[i], EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        private void DrawRunTotal()
        {
            if (!_trackingFullRun && _lastFullRunMs < 0)
                return;

            EditorGUILayout.Space(4f);
            var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            if (_trackingFullRun && _runStopwatch != null)
            {
                style.normal.textColor = new Color(0.95f, 0.60f, 0.15f);
                EditorGUILayout.LabelField(
                    "Full run in progress — elapsed " + FormatRunMs(_runStopwatch.ElapsedMilliseconds), style);
            }
            else
            {
                style.normal.textColor = new Color(0.30f, 0.85f, 0.35f);
                EditorGUILayout.LabelField(
                    "Full run finished — total " + FormatRunMs(_lastFullRunMs), style);
            }
        }
    }
}
#endif
