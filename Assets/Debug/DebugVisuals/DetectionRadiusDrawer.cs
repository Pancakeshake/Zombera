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
        }

        // TODO: Add optional cone-based forward vision debug rendering.
        // TODO: Add per-state dynamic radius visualization.
    }
}