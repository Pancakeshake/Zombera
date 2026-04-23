using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Debugging.DebugVisuals
{
    /// <summary>
    /// Draws debug navigation paths using Debug.DrawLine.
    /// </summary>
    public sealed class PathDebugDrawer : MonoBehaviour, IDebugTool
    {
        [SerializeField] private Color pathColor = Color.cyan;
        [SerializeField] private float lineDuration = 0f;

        private readonly Dictionary<int, List<Vector3>> pathsById = new Dictionary<int, List<Vector3>>();

        public string ToolName => nameof(PathDebugDrawer);
        public bool IsToolEnabled { get; private set; } = true;

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public void SetToolEnabled(bool enabled)
        {
            IsToolEnabled = enabled;
        }

        public void SetPath(int pathId, IReadOnlyList<Vector3> pathPoints)
        {
            List<Vector3> points = GetOrCreatePath(pathId);
            points.Clear();

            if (pathPoints == null)
            {
                return;
            }

            for (int i = 0; i < pathPoints.Count; i++)
            {
                points.Add(pathPoints[i]);
            }
        }

        public void ClearPath(int pathId)
        {
            pathsById.Remove(pathId);
        }

        public void ClearAllPaths()
        {
            pathsById.Clear();
        }

        private void Update()
        {
            if (!IsToolEnabled)
            {
                return;
            }

            bool showPathfinding = DebugManager.Instance == null || DebugManager.Instance.Settings == null || DebugManager.Instance.Settings.showPathfinding;

            if (!showPathfinding)
            {
                return;
            }

            foreach (KeyValuePair<int, List<Vector3>> entry in pathsById)
            {
                List<Vector3> path = entry.Value;

                for (int i = 0; i < path.Count - 1; i++)
                {
                    Debug.DrawLine(path[i], path[i + 1], pathColor, lineDuration);
                }
            }
        }

        private List<Vector3> GetOrCreatePath(int pathId)
        {
            if (!pathsById.TryGetValue(pathId, out List<Vector3> points))
            {
                points = new List<Vector3>();
                pathsById[pathId] = points;
            }

            return points;
        }

        // Per-agent color registry: pathId → Color.
        private readonly Dictionary<int, Color> agentColors = new Dictionary<int, Color>();

        private static readonly Color[] colorPalette =
        {
            Color.cyan, Color.yellow, Color.green, Color.magenta, new Color(1f, 0.5f, 0f)
        };

        public void SetAgentColor(int pathId, Color color)
        {
            agentColors[pathId] = color;
        }

        private Color GetAgentColor(int pathId)
        {
            return agentColors.TryGetValue(pathId, out Color c) ? c : pathColor;
        }

        /// <summary>Exports all active path data as a JSON-compatible string for offline diagnostics.</summary>
        public string ExportPathSnapshot()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("[");
            bool first = true;

            foreach (KeyValuePair<int, List<Vector3>> entry in pathsById)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append($"{{\"id\":{entry.Key},\"points\":{entry.Value.Count}}}");
            }

            sb.Append("]");
            return sb.ToString();
        }
    }
}