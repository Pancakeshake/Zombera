using UnityEngine;

namespace Zombera.Debugging.DebugVisuals
{
    /// <summary>
    /// Draws AI detection, hearing, and attack ranges via Gizmos.
    /// </summary>
    public sealed class DetectionRadiusDrawer : MonoBehaviour, IDebugTool
    {
        [Header("Radii")]
        [SerializeField] private float detectionRadius = 12f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float hearingRadius = 8f;

        [Header("Colors")]
        [SerializeField] private Color detectionColor = Color.green;
        [SerializeField] private Color attackColor = Color.red;
        [SerializeField] private Color hearingColor = Color.yellow;

        public string ToolName => nameof(DetectionRadiusDrawer);
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

        private void OnDrawGizmos()
        {
            bool canDraw = IsToolEnabled;

            if (DebugManager.Instance != null && DebugManager.Instance.Settings != null)
            {
                canDraw &= DebugManager.Instance.Settings.showDetectionRadius;
            }

            if (!canDraw)
            {
                return;
            }

            Vector3 center = transform.position;

            Gizmos.color = detectionColor;
            Gizmos.DrawWireSphere(center, detectionRadius);

            Gizmos.color = attackColor;
            Gizmos.DrawWireSphere(center, attackRange);

            Gizmos.color = hearingColor;
            Gizmos.DrawWireSphere(center, hearingRadius);

            DrawForwardVisionCone(center);
        }

        [SerializeField] private bool showForwardVisionCone = true;
        [SerializeField, Range(0f, 180f)] private float visionConeAngleDegrees = 60f;
        [SerializeField, Min(0f)] private float visionConeRange = 12f;
        [SerializeField] private Color visionConeColor = new Color(1f, 1f, 0f, 0.4f);

        private void DrawForwardVisionCone(Vector3 center)
        {
#if UNITY_EDITOR
            if (!showForwardVisionCone || visionConeRange <= 0f)
            {
                return;
            }

            Gizmos.color = visionConeColor;
            Vector3 forward = transform.forward;
            float halfAngle = visionConeAngleDegrees * 0.5f * Mathf.Deg2Rad;
            Vector3 leftEdge = Quaternion.Euler(0f, -visionConeAngleDegrees * 0.5f, 0f) * forward * visionConeRange;
            Vector3 rightEdge = Quaternion.Euler(0f, visionConeAngleDegrees * 0.5f, 0f) * forward * visionConeRange;
            Gizmos.DrawLine(center, center + leftEdge);
            Gizmos.DrawLine(center, center + rightEdge);
            UnityEditor.Handles.color = visionConeColor;
            UnityEditor.Handles.DrawWireArc(center, Vector3.up, leftEdge.normalized, visionConeAngleDegrees, visionConeRange);
            _ = halfAngle;
#endif
        }
    }
}