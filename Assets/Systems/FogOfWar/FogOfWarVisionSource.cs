using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Systems
{
    /// <summary>
    /// Perception-driven vision cone source used by the fog-of-war system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FogOfWarVisionSource : MonoBehaviour
    {
        private static readonly List<FogOfWarVisionSource> ActiveSourceRegistry = new List<FogOfWarVisionSource>();

        [Header("Source")]
        [SerializeField] private bool sourceEnabled = true;
        [SerializeField] private Unit unit;
        [SerializeField] private UnitStats unitStats;
        [SerializeField] private Transform eyePoint;
        [SerializeField, Min(0f)] private float eyeHeightOffsetMeters = 1.6f;

        [Header("Perception")]
        [SerializeField] private bool useManualPerceptionLevel = true;
        [SerializeField, Range(1, 100)] private int manualPerceptionLevel = 1;
        [SerializeField] private UnitSkillType fallbackUnitSkillForPerception = UnitSkillType.Scavenging;

        [Header("Vision Curve")]
        [SerializeField, Min(0.1f)] private float minimumVisionRangeMeters = 25f;
        [SerializeField, Min(0.1f)] private float maximumVisionRangeMeters = 100f;
        [SerializeField, Range(1f, 360f)] private float minimumVisionAngleDegrees = 180f;
        [SerializeField, Range(1f, 360f)] private float maximumVisionAngleDegrees = 270f;

        [Header("Checks")]
        [SerializeField] private bool flattenVerticalForAngleChecks = true;
        [SerializeField, Min(0f)] private float targetRadiusPaddingMeters = 0.6f;

        [Header("Debug Gizmos")]
        [SerializeField] private bool drawVisionGizmo = true;
        [SerializeField] private bool drawVisionGizmoWhenNotSelected;
        [SerializeField, Min(6)] private int gizmoArcSegments = 36;
        [SerializeField] private Color gizmoConeColor = new Color(0.2f, 0.9f, 0.35f, 0.85f);
        [SerializeField] private Color gizmoDisabledColor = new Color(0.9f, 0.2f, 0.2f, 0.85f);

        public static IReadOnlyList<FogOfWarVisionSource> ActiveSources => ActiveSourceRegistry;

        public bool IsOperational => sourceEnabled && enabled && gameObject.activeInHierarchy;

        public int CurrentPerceptionLevel => ResolvePerceptionLevel();

        public float CurrentVisionRangeMeters
        {
            get
            {
                float t = ResolvePerceptionLerpT();
                return Mathf.Lerp(minimumVisionRangeMeters, maximumVisionRangeMeters, t);
            }
        }

        public float CurrentVisionAngleDegrees
        {
            get
            {
                float t = ResolvePerceptionLerpT();
                return Mathf.Lerp(minimumVisionAngleDegrees, maximumVisionAngleDegrees, t);
            }
        }

        public float MaximumVisionRangeMeters => Mathf.Max(minimumVisionRangeMeters, maximumVisionRangeMeters);

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            if (!FogOfWarRuntimeConfig.FeatureEnabled)
            {
                if (Application.isPlaying)
                {
                    Destroy(this);
                }
                else
                {
                    enabled = false;
                }

                return;
            }

            if (!ActiveSourceRegistry.Contains(this))
            {
                ActiveSourceRegistry.Add(this);
            }

            ResolveReferences();
        }

        private void OnDisable()
        {
            ActiveSourceRegistry.Remove(this);
        }

        private void OnDrawGizmos()
        {
            if (!drawVisionGizmoWhenNotSelected)
            {
                return;
            }

            DrawVisionGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (drawVisionGizmoWhenNotSelected)
            {
                return;
            }

            DrawVisionGizmo();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            minimumVisionRangeMeters = Mathf.Max(0.1f, minimumVisionRangeMeters);
            maximumVisionRangeMeters = Mathf.Max(minimumVisionRangeMeters, maximumVisionRangeMeters);
            minimumVisionAngleDegrees = Mathf.Clamp(minimumVisionAngleDegrees, 1f, 360f);
            maximumVisionAngleDegrees = Mathf.Clamp(maximumVisionAngleDegrees, minimumVisionAngleDegrees, 360f);
            targetRadiusPaddingMeters = Mathf.Max(0f, targetRadiusPaddingMeters);

            ResolveReferences();
        }
#endif

        public bool OwnsTarget(FogOfWarTarget target)
        {
            if (target == null)
            {
                return false;
            }

            Transform targetTransform = target.transform;
            return targetTransform == transform
                || targetTransform.IsChildOf(transform)
                || transform.IsChildOf(targetTransform);
        }

        public bool CanSee(FogOfWarTarget target, bool requireLineOfSight, LayerMask occlusionMask)
        {
            if (!IsOperational || target == null)
            {
                return false;
            }

            Vector3 eyePosition = GetEyePosition();
            Vector3 targetPosition = target.VisibilityPosition;

            if (!IsWithinVisionCone(eyePosition, targetPosition))
            {
                return false;
            }

            if (requireLineOfSight && !HasLineOfSightToTarget(target, eyePosition, targetPosition, occlusionMask))
            {
                return false;
            }

            return true;
        }

        private bool IsWithinVisionCone(Vector3 eyePosition, Vector3 targetPosition)
        {
            Vector3 toTarget = targetPosition - eyePosition;
            Vector3 forward = transform.forward;

            if (flattenVerticalForAngleChecks)
            {
                toTarget.y = 0f;
                forward.y = 0f;
            }

            float distanceToTarget = toTarget.magnitude;
            if (distanceToTarget <= 0.0001f)
            {
                return true;
            }

            if (distanceToTarget > CurrentVisionRangeMeters + targetRadiusPaddingMeters)
            {
                return false;
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            float halfAngle = CurrentVisionAngleDegrees * 0.5f;
            float angle = Vector3.Angle(forward, toTarget / distanceToTarget);
            return angle <= halfAngle;
        }

        private bool HasLineOfSightToTarget(
            FogOfWarTarget target,
            Vector3 eyePosition,
            Vector3 targetPosition,
            LayerMask occlusionMask)
        {
            Vector3 ray = targetPosition - eyePosition;
            float distance = ray.magnitude;
            if (distance <= 0.0001f)
            {
                return true;
            }

            Vector3 direction = ray / distance;
            RaycastHit[] hits = Physics.RaycastAll(
                eyePosition,
                direction,
                distance,
                occlusionMask,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
            {
                return true;
            }

            Array.Sort(hits, CompareHitDistance);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || IsOwnCollider(collider))
                {
                    continue;
                }

                return target.ContainsCollider(collider);
            }

            return true;
        }

        private static int CompareHitDistance(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }

        private bool IsOwnCollider(Collider collider)
        {
            Transform colliderTransform = collider.transform;
            return colliderTransform == transform || colliderTransform.IsChildOf(transform);
        }

        private Vector3 GetEyePosition()
        {
            if (eyePoint != null)
            {
                return eyePoint.position;
            }

            return transform.position + Vector3.up * eyeHeightOffsetMeters;
        }

        private int ResolvePerceptionLevel()
        {
            if (useManualPerceptionLevel)
            {
                return Mathf.Clamp(manualPerceptionLevel, UnitStats.MinSkillLevel, UnitStats.MaxSkillLevel);
            }

            if (unitStats != null)
            {
                return Mathf.Clamp(
                    unitStats.GetSkillLevel(fallbackUnitSkillForPerception),
                    UnitStats.MinSkillLevel,
                    UnitStats.MaxSkillLevel);
            }

            return Mathf.Clamp(manualPerceptionLevel, UnitStats.MinSkillLevel, UnitStats.MaxSkillLevel);
        }

        private float ResolvePerceptionLerpT()
        {
            int currentLevel = ResolvePerceptionLevel();
            int minLevel = UnitStats.MinSkillLevel;
            int maxLevel = UnitStats.MaxSkillLevel;

            if (maxLevel <= minLevel)
            {
                return 0f;
            }

            return Mathf.Clamp01((currentLevel - minLevel) / (float)(maxLevel - minLevel));
        }

        private void ResolveReferences()
        {
            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }

            if (unitStats == null)
            {
                unitStats = unit != null ? unit.Stats : GetComponent<UnitStats>();
            }
        }

        private void DrawVisionGizmo()
        {
            if (!drawVisionGizmo)
            {
                return;
            }

            Vector3 eyePosition = GetEyePosition();
            float visionRange = CurrentVisionRangeMeters;
            float visionAngle = CurrentVisionAngleDegrees;
            float halfAngle = visionAngle * 0.5f;

            Vector3 forward = transform.forward;
            if (flattenVerticalForAngleChecks)
            {
                forward.y = 0f;
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            Vector3 rotationAxis = flattenVerticalForAngleChecks ? Vector3.up : transform.up;
            if (rotationAxis.sqrMagnitude <= 0.0001f)
            {
                rotationAxis = Vector3.up;
            }
            else
            {
                rotationAxis.Normalize();
            }

            Gizmos.color = IsOperational ? gizmoConeColor : gizmoDisabledColor;
            Gizmos.DrawWireSphere(eyePosition, 0.15f);

            if (visionAngle >= 359f)
            {
                Gizmos.DrawWireSphere(eyePosition, visionRange);
                return;
            }

            int segmentCount = Mathf.Max(6, gizmoArcSegments);
            Vector3 previousDirection = Quaternion.AngleAxis(-halfAngle, rotationAxis) * forward;
            Vector3 previousPoint = eyePosition + previousDirection * visionRange;

            Gizmos.DrawLine(eyePosition, previousPoint);

            for (int segment = 1; segment <= segmentCount; segment++)
            {
                float t = segment / (float)segmentCount;
                float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
                Vector3 currentDirection = Quaternion.AngleAxis(currentAngle, rotationAxis) * forward;
                Vector3 currentPoint = eyePosition + currentDirection * visionRange;

                Gizmos.DrawLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }

            Gizmos.DrawLine(eyePosition, previousPoint);
            Gizmos.DrawLine(eyePosition, eyePosition + forward * visionRange);
        }
    }
}
