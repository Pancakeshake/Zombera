using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Systems
{
    /// <summary>
    /// Calculates and applies combat formation slots for squad members.
    /// Supports Line, Wedge, and DefensiveCircle patterns.
    /// </summary>
    public sealed class FormationController : MonoBehaviour
    {
        [SerializeField] private FormationType activeFormation = FormationType.Line;
        [SerializeField, Min(0.5f)] private float slotSpacing = 2f;

        public FormationType ActiveFormation => activeFormation;
        public float SlotSpacing => slotSpacing;

        public void SetFormation(FormationType formationType)
        {
            activeFormation = formationType;
        }

        /// <summary>
        /// Returns world-space positions for <paramref name="unitCount"/> formation slots
        /// centered on <paramref name="center"/> and oriented along <paramref name="forward"/>.
        /// </summary>
        public IReadOnlyList<Vector3> CalculateFormationSlots(Vector3 center, Vector3 forward, int unitCount)
        {
            var slots = new List<Vector3>(unitCount);
            if (unitCount <= 0) return slots;

            // Build a right vector perpendicular to forward in the XZ plane.
            Vector3 fwd = forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

            float step = Mathf.Max(0.5f, slotSpacing);

            switch (activeFormation)
            {
                case FormationType.Line:
                    // Spread members side-by-side perpendicular to forward.
                    for (int i = 0; i < unitCount; i++)
                    {
                        float t = unitCount > 1 ? i / (float)(unitCount - 1) - 0.5f : 0f;
                        slots.Add(center + right * (t * step * (unitCount - 1)));
                    }
                    break;

                case FormationType.Wedge:
                    // Leading unit at front, rest form two trailing wings.
                    slots.Add(center);
                    for (int i = 1; i < unitCount; i++)
                    {
                        int row  = (i + 1) / 2;
                        float side = (i % 2 == 0) ? 1f : -1f;
                        Vector3 offset = -fwd * (row * step) + right * (side * row * step * 0.6f);
                        slots.Add(center + offset);
                    }
                    break;

                case FormationType.DefensiveCircle:
                    // Equally-spaced ring around the center point.
                    float radius = Mathf.Max(step, step * unitCount / (2f * Mathf.PI));
                    for (int i = 0; i < unitCount; i++)
                    {
                        float angle = i * Mathf.PI * 2f / unitCount;
                        slots.Add(center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius);
                    }
                    break;

                default:
                    // Fallback: stack everyone at center.
                    for (int i = 0; i < unitCount; i++)
                        slots.Add(center);
                    break;
            }

            return slots;
        }
    }

    public enum FormationType
    {
        Line,
        Wedge,
        DefensiveCircle
    }
}