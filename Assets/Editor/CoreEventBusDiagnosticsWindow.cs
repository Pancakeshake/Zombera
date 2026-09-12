#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zombera.Core;

namespace Zombera.EditorTools
{
    public sealed class CoreEventBusDiagnosticsWindow : EditorWindow
    {
        private const int MaxRows = 2000;

        private readonly List<EventTrafficSample> _rows = new(MaxRows);
        private readonly Dictionary<string, int> _publishCounts = new(StringComparer.Ordinal);

        private bool _captureTraffic = true;
        private bool _autoScroll = true;
        private Vector2 _scroll;
        private string _searchFilter = string.Empty;

        [MenuItem("Tools/Utilities/Diagnostics/Core Event Bus Traffic", priority = -500)]
        private static void Open()
        {
            var window = GetWindow<CoreEventBusDiagnosticsWindow>();
            window.titleContent = new GUIContent("CoreEventBus Traffic");
            window.minSize = new Vector2(760f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            CoreEventBus.GlobalDiagnosticTrafficEnabled = true;
            CoreEventBus.EventTrafficObserved += HandleTrafficObserved;
            CoreEventBus.ClearDiagnosticHistory();
            _rows.Clear();
            _publishCounts.Clear();
            Repaint();
        }

        private void OnDisable()
        {
            CoreEventBus.EventTrafficObserved -= HandleTrafficObserved;
            CoreEventBus.GlobalDiagnosticTrafficEnabled = false;
        }

        private void HandleTrafficObserved(EventTrafficSample sample)
        {
            if (!_captureTraffic) return;

            _rows.Add(sample);
            if (_rows.Count > MaxRows)
                _rows.RemoveRange(0, _rows.Count - MaxRows);

            if (string.Equals(sample.Operation, "Publish", StringComparison.Ordinal))
            {
                if (_publishCounts.TryGetValue(sample.EventTypeName, out var current))
                    _publishCounts[sample.EventTypeName] = current + 1;
                else
                    _publishCounts[sample.EventTypeName] = 1;
            }

            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawStatus();
            DrawTopPublishers();
            DrawTrafficTable();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _captureTraffic = GUILayout.Toggle(_captureTraffic, "Capture", EditorStyles.toolbarButton,
                    GUILayout.Width(70f));
                _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll", EditorStyles.toolbarButton,
                    GUILayout.Width(90f));

                GUILayout.Space(8f);
                GUILayout.Label("Search", GUILayout.Width(44f));
                _searchFilter = GUILayout.TextField(_searchFilter ?? string.Empty, EditorStyles.toolbarTextField,
                    GUILayout.MinWidth(140f));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                {
                    _rows.Clear();
                    _publishCounts.Clear();
                    CoreEventBus.ClearDiagnosticHistory();
                }
            }
        }

        private void DrawStatus()
        {
            var modeText = Application.isPlaying ? "Play Mode" : "Edit Mode";
            var busText = CoreEventBus.Instance != null
                ? "Bus: present"
                : "Bus: not found (enter play mode)";

            EditorGUILayout.HelpBox(
                modeText + " | " + busText + " | Captured rows: " + _rows.Count,
                MessageType.Info);
        }

        private void DrawTopPublishers()
        {
            if (_publishCounts.Count == 0) return;

            EditorGUILayout.LabelField("Top Published Events", EditorStyles.boldLabel);

            var top = _publishCounts
                .OrderByDescending(static pair => pair.Value)
                .Take(5)
                .ToArray();

            for (var i = 0; i < top.Length; i++)
                EditorGUILayout.LabelField(top[i].Key + ": " + top[i].Value);

            EditorGUILayout.Space(8f);
        }

        private void DrawTrafficTable()
        {
            EditorGUILayout.LabelField("Traffic", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Time", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
                GUILayout.Label("Frame", EditorStyles.miniBoldLabel, GUILayout.Width(52f));
                GUILayout.Label("Operation", EditorStyles.miniBoldLabel, GUILayout.Width(92f));
                GUILayout.Label("Event", EditorStyles.miniBoldLabel, GUILayout.Width(210f));
                GUILayout.Label("Listeners", EditorStyles.miniBoldLabel, GUILayout.Width(64f));
                GUILayout.Label("Queue", EditorStyles.miniBoldLabel, GUILayout.Width(50f));
                GUILayout.Label("Mode", EditorStyles.miniBoldLabel, GUILayout.Width(56f));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (!PassesFilter(row, _searchFilter)) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(row.RealtimeSinceStartup.ToString("0.00"), GUILayout.Width(70f));
                    GUILayout.Label(row.Frame.ToString(), GUILayout.Width(52f));
                    GUILayout.Label(row.Operation, GUILayout.Width(92f));
                    GUILayout.Label(row.EventTypeName, GUILayout.Width(210f));
                    GUILayout.Label(row.ListenerCount.ToString(), GUILayout.Width(64f));
                    GUILayout.Label(row.QueueDepth.ToString(), GUILayout.Width(50f));
                    GUILayout.Label(row.QueuedMode ? "Queued" : "Now", GUILayout.Width(56f));
                }
            }

            if (_autoScroll && Event.current.type == EventType.Repaint)
                _scroll.y = float.MaxValue;

            EditorGUILayout.EndScrollView();
        }

        private static bool PassesFilter(EventTrafficSample sample, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;

            return sample.EventTypeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                   || sample.Operation.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

#endif
