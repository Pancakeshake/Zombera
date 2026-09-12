#region

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

#endregion

namespace Zombera.Debugging.DebugVisuals
{
    /// <summary>
    ///     Draws AI detection, hearing, and attack ranges via Gizmos.
    /// </summary>
    public sealed class DetectionRadiusDrawer : MonoBehaviour, IDebugTool
    {
        [Header("Radii")] [SerializeField] private float detectionRadius = 12f;

        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float hearingRadius = 8f;

        [Header("Colors")] [SerializeField] private Color detectionColor = Color.green;

        [SerializeField] private Color attackColor = Color.red;
        [SerializeField] private Color hearingColor = Color.yellow;

        [SerializeField] private bool showForwardVisionCone = true;
        [SerializeField] [Range(0f, 180f)] private float visionConeAngleDegrees = 60f;
        [SerializeField] [Min(0f)] private float visionConeRange = 12f;

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        private void OnDrawGizmos()
        {
            var canDraw = IsToolEnabled;

            if (DebugManager.Instance != null && DebugManager.Instance.Settings != null)
                canDraw &= DebugManager.Instance.Settings.showDetectionRadius;

            if (!canDraw) return;

            var center = transform.position;

            Gizmos.color = detectionColor;
            Gizmos.DrawWireSphere(center, detectionRadius);

            Gizmos.color = attackColor;
            Gizmos.DrawWireSphere(center, attackRange);

            Gizmos.color = hearingColor;
            Gizmos.DrawWireSphere(center, hearingRadius);

            DrawForwardVisionCone();
        }

        public string ToolName => nameof(DetectionRadiusDrawer);
        public bool IsToolEnabled { get; private set; } = true;

        public void SetToolEnabled(bool isEnabled)
        {
            IsToolEnabled = isEnabled;
        }

        private void DrawForwardVisionCone()
        {
#if UNITY_EDITOR
            if (!showForwardVisionCone || visionConeRange <= 0f) return;

            var center = transform.position;
            var coneColor = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.color = coneColor;
            var forward = transform.forward;
            var halfAngle = visionConeAngleDegrees * 0.5f * Mathf.Deg2Rad;
            var leftEdge = Quaternion.Euler(0f, -visionConeAngleDegrees * 0.5f, 0f) * forward * visionConeRange;
            var rightEdge = Quaternion.Euler(0f, visionConeAngleDegrees * 0.5f, 0f) * forward * visionConeRange;
            Gizmos.DrawLine(center, center + leftEdge);
            Gizmos.DrawLine(center, center + rightEdge);
            Handles.color = coneColor;
            Handles.DrawWireArc(center, Vector3.up, leftEdge.normalized, visionConeAngleDegrees, visionConeRange);
            _ = halfAngle;
#endif
        }
    }
}