#region

using System.Collections.Generic;
using System.Text;
using UnityEngine;

#endregion

namespace Zombera.Debugging.DebugVisuals
{
    // ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
    // ReSharper disable LoopCanBeConvertedToQuery
    /// <summary>
    ///     Draws debug navigation paths using Debug.DrawLine.
    /// </summary>
    public sealed class PathDebugDrawer : MonoBehaviour, IDebugTool
    {
        [SerializeField] private Color pathColor = Color.cyan;
        [SerializeField] private float lineDuration;

        private readonly Dictionary<int, List<Vector3>> _pathsById = new();

        private void Update()
        {
            if (!IsToolEnabled) return;

            var showPathfinding = DebugManager.Instance == null || DebugManager.Instance.Settings == null ||
                                  DebugManager.Instance.Settings.showPathfinding;

            if (!showPathfinding) return;

            foreach (var entry in _pathsById)
            {
                var path = entry.Value;

                if (path == null || path.Count < 2) continue;

                for (var i = 0; i < path.Count - 1; i++)
                    Debug.DrawLine(path[i], path[i + 1], pathColor, lineDuration);
            }
        }

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public string ToolName => nameof(PathDebugDrawer);
        public bool IsToolEnabled { get; private set; } = true;

        public void SetToolEnabled(bool isEnabled)
        {
            IsToolEnabled = isEnabled;
        }

        public void SetPath(int pathId, IReadOnlyList<Vector3> pathPoints)
        {
            var points = GetOrCreatePath(pathId);
            points.Clear();

            if (pathPoints == null) return;

            points.AddRange(pathPoints);
        }

        public void ClearPath(int pathId)
        {
            _pathsById.Remove(pathId);
        }

        [ContextMenu("Paths/Clear All")]
        // ReSharper disable once UnusedMember.Global
        public void ClearAllPaths()
        {
            _pathsById.Clear();
        }

        private List<Vector3> GetOrCreatePath(int pathId)
        {
            if (_pathsById.TryGetValue(pathId, out var points)) return points;

            points = new List<Vector3>();
            _pathsById[pathId] = points;
            return points;
        }

        /// <summary>Exports all active path data as a JSON-compatible string for offline diagnostics.</summary>
        [ContextMenu("Paths/Log Snapshot")]
        // ReSharper disable once UnusedMember.Global
        public string ExportPathSnapshot()
        {
            var sb = new StringBuilder();
            sb.Append("[");
            var sep = "";

            foreach (var entry in _pathsById)
            {
                sb.Append(sep);
                sep = ",";
                sb.Append($"{{\"id\":{entry.Key},\"points\":{entry.Value.Count}}}");
            }

            sb.Append("]");
            var snapshot = sb.ToString();
            Debug.Log($"[PathDebugDrawer] {snapshot}", this);
            return snapshot;
        }
    }
}