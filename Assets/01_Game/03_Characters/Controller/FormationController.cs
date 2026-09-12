#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Calculates and applies combat formation slots for squad members.
    /// </summary>
    public sealed class FormationController : MonoBehaviour
    {
        [SerializeField] private FormationType activeFormation = FormationType.DefaultMovement;
        [SerializeField] [Min(0.5f)] private float slotSpacing = 1.4f;
        [SerializeField] [Min(0.5f)] private float depthSpacing = 1.4f;
        [SerializeField] [Min(0f)] private float adaptiveSpacingGain = 0.08f;
        [SerializeField] [Min(0.25f)] private float lateralBias = 1f;
        [SerializeField] [Min(0.25f)] private float depthBias = 1f;
        [SerializeField] private bool applyToWholeSquad = true;

        public FormationType ActiveFormation => activeFormation;
        public float SlotSpacing => slotSpacing;
        public float DepthSpacing => depthSpacing;
        public float LateralBias => lateralBias;
        public float DepthBias => depthBias;
        public bool ApplyToWholeSquad => applyToWholeSquad;

        public void SetFormation(FormationType formationType)
        {
            activeFormation = formationType;
        }

        public void SetSlotSpacing(float spacing)
        {
            slotSpacing = Mathf.Max(0.5f, spacing);
        }

        public void SetDepthSpacing(float spacing)
        {
            depthSpacing = Mathf.Max(0.5f, spacing);
        }

        public void SetLateralBias(float bias)
        {
            lateralBias = Mathf.Max(0.25f, bias);
        }

        public void SetDepthBias(float bias)
        {
            depthBias = Mathf.Max(0.25f, bias);
        }

        public void SetApplyToWholeSquad(bool wholeSquad)
        {
            applyToWholeSquad = wholeSquad;
        }

        public void ApplySaveData(FormationSaveData data)
        {
            if (data is not { hasData: true }) return;

            activeFormation = (FormationType)data.activeFormation;
            slotSpacing = Mathf.Max(0.5f, data.slotSpacing);
            depthSpacing = Mathf.Max(0.5f, data.depthSpacing);
            lateralBias = Mathf.Max(0.25f, data.lateralBias);
            depthBias = Mathf.Max(0.25f, data.depthBias);
            applyToWholeSquad = data.applyToWholeSquad;
        }

        public FormationSaveData CaptureSaveData()
        {
            return new FormationSaveData
            {
                hasData = true,
                activeFormation = (int)activeFormation,
                slotSpacing = slotSpacing,
                depthSpacing = depthSpacing,
                lateralBias = lateralBias,
                depthBias = depthBias,
                applyToWholeSquad = applyToWholeSquad
            };
        }

        public float GetAdaptiveSlotSpacing(int unitCount)
        {
            var step = Mathf.Max(0.5f, slotSpacing);
            if (unitCount <= 4) return step;

            return step + (unitCount - 4) * Mathf.Max(0f, adaptiveSpacingGain);
        }

        /// <summary>
        ///     Returns world-space positions for formation slots centered on <paramref name="center" />.
        /// </summary>
        public IReadOnlyList<Vector3> CalculateFormationSlots(Vector3 center, Vector3 forward, int unitCount)
        {
            var slots = new List<Vector3>(unitCount);
            if (unitCount <= 0) return slots;

            var fwd = forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();
            var right = new Vector3(fwd.z, 0f, -fwd.x);

            var step = GetAdaptiveSlotSpacing(unitCount);
            var depthStep = Mathf.Max(0.5f, depthSpacing) * depthBias;
            var lateralStep = step * lateralBias;

            switch (activeFormation)
            {
                case FormationType.DefaultMovement:
                    BuildSpread(slots, center, fwd, right, unitCount, lateralStep, depthStep);
                    break;

                case FormationType.SpearWall:
                    BuildSpearWall(slots, center, fwd, right, unitCount, lateralStep, depthStep);
                    break;

                case FormationType.Spread:
                    BuildLine(slots, center, right, unitCount, lateralStep);
                    break;

                case FormationType.Line:
                    BuildLine(slots, center, right, unitCount, lateralStep);
                    break;

                case FormationType.Wedge:
                    BuildWedge(slots, center, fwd, right, unitCount, lateralStep, depthStep);
                    break;

                case FormationType.DefensiveCircle:
                    BuildDefensiveCircle(slots, center, unitCount, lateralStep);
                    break;

                default:
                    BuildSpread(slots, center, fwd, right, unitCount, lateralStep, depthStep);
                    break;
            }

            return slots;
        }

        private static void BuildLine(List<Vector3> slots, Vector3 center, Vector3 right, int unitCount, float lateralStep)
        {
            for (var i = 0; i < unitCount; i++)
            {
                var t = unitCount > 1 ? i / (float)(unitCount - 1) - 0.5f : 0f;
                slots.Add(center + right * (t * lateralStep * Mathf.Max(1, unitCount - 1)));
            }
        }

        private static void BuildSpearWall(
            List<Vector3> slots,
            Vector3 center,
            Vector3 forward,
            Vector3 right,
            int unitCount,
            float lateralStep,
            float depthStep)
        {
            var unitsPerRow = Mathf.Clamp(Mathf.RoundToInt(3f / Mathf.Max(0.5f, lateralStep / 2f)), 1, 4);
            var placed = 0;
            var row = 0;

            while (placed < unitCount)
            {
                var inRow = Mathf.Min(unitsPerRow, unitCount - placed);
                for (var i = 0; i < inRow; i++)
                {
                    var t = inRow > 1 ? i / (float)(inRow - 1) - 0.5f : 0f;
                    var offset = right * (t * lateralStep * 0.75f) - forward * (row * depthStep);
                    slots.Add(center + offset);
                    placed++;
                }

                row++;
            }
        }

        private static void BuildSpread(
            List<Vector3> slots,
            Vector3 center,
            Vector3 forward,
            Vector3 right,
            int unitCount,
            float lateralStep,
            float depthStep)
        {
            var cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(unitCount)));
            for (var i = 0; i < unitCount; i++)
            {
                var row = i / cols;
                var col = i % cols;
                var offset = right * ((col - (cols - 1) * 0.5f) * lateralStep)
                             - forward * (row * depthStep);
                slots.Add(center + offset);
            }
        }

        private static void BuildWedge(
            List<Vector3> slots,
            Vector3 center,
            Vector3 forward,
            Vector3 right,
            int unitCount,
            float lateralStep,
            float depthStep)
        {
            slots.Add(center);
            for (var i = 1; i < unitCount; i++)
            {
                var row = (i + 1) / 2;
                var side = i % 2 == 0 ? 1f : -1f;
                var offset = -forward * (row * depthStep) + right * (side * row * lateralStep * 0.6f);
                slots.Add(center + offset);
            }
        }

        private static void BuildDefensiveCircle(List<Vector3> slots, Vector3 center, int unitCount, float step)
        {
            var radius = Mathf.Max(step, step * unitCount / (2f * Mathf.PI));
            for (var i = 0; i < unitCount; i++)
            {
                var angle = i * Mathf.PI * 2f / unitCount;
                slots.Add(center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius);
            }
        }
    }

    public enum FormationType
    {
        Line = 0,
        Wedge = 1,
        DefensiveCircle = 2,
        DefaultMovement = 3,
        SpearWall = 4,
        Spread = 5
    }
}
