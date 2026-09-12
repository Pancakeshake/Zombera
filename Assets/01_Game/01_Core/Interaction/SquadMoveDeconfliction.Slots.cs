using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Systems
{
    public static partial class SquadMoveDeconfliction
    {
        // ──────────────────────────────────────────────
        //  Slot generation, deconfliction, and NavMesh grounding
        // ──────────────────────────────────────────────

        public static List<Vector3> CalculateRadialFallbackSlots(Vector3 center, Vector3 forward, int unitCount,
            float spacing)
        {
            var slots = new List<Vector3>(Mathf.Max(0, unitCount));
            if (unitCount <= 0) return slots;

            if (unitCount == 1)
            {
                slots.Add(center);
                return slots;
            }

            var ringSpacing = Mathf.Max(0.5f, spacing);
            var placed = 0;
            var ring = 0;

            while (placed < unitCount)
            {
                var ringRadius = ringSpacing * (ring + 1);
                var slotsInRing = ring == 0
                    ? 1
                    : Mathf.Max(4, Mathf.RoundToInt(2f * Mathf.PI * ringRadius / ringSpacing));

                for (var i = 0; i < slotsInRing && placed < unitCount; i++, placed++)
                {
                    if (ring == 0)
                    {
                        slots.Add(center);
                        continue;
                    }

                    var angle = i * Mathf.PI * 2f / slotsInRing;
                    var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
                    slots.Add(center + offset);
                }

                ring++;
            }

            return slots;
        }

        public static void DeconflictSlots(List<Vector3> slots, float minSeparation, int maxIterations,
            float stepMeters)
        {
            if (slots == null || slots.Count < 2) return;

            var minSep = Mathf.Max(0.25f, minSeparation);
            var minSepSqr = minSep * minSep;
            var iterations = Mathf.Max(1, maxIterations);
            var step = Mathf.Max(0.05f, stepMeters);

            for (var pass = 0; pass < iterations; pass++)
            {
                var movedAny = false;

                for (var i = 0; i < slots.Count; i++)
                {
                    for (var j = i + 1; j < slots.Count; j++)
                    {
                        var delta = slots[j] - slots[i];
                        delta.y = 0f;
                        var distSqr = delta.sqrMagnitude;
                        if (distSqr >= minSepSqr) continue;

                        Vector3 push;
                        if (distSqr < 0.0001f)
                        {
                            var angle = (i + j + pass) * 0.85f;
                            push = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        }
                        else
                        {
                            push = delta.normalized;
                        }

                        var overlap = minSep - Mathf.Sqrt(distSqr);
                        var correction = push * (overlap * 0.5f * step);
                        slots[i] -= correction;
                        slots[j] += correction;
                        movedAny = true;
                    }
                }

                if (!movedAny) break;
            }
        }

        public static int PushOutCollidingSlots(List<Vector3> slots, Vector3 center, float minSeparation,
            int maxIterations, float stepMeters)
        {
            if (slots == null || slots.Count < 2) return 0;

            var minSep = Mathf.Max(0.25f, minSeparation);
            var minSepSqr = minSep * minSep;
            var pushCount = 0;
            var step = Mathf.Max(0.1f, stepMeters);

            for (var pass = 0; pass < Mathf.Max(1, maxIterations); pass++)
            {
                var movedAny = false;

                for (var i = 0; i < slots.Count; i++)
                {
                    for (var j = i + 1; j < slots.Count; j++)
                    {
                        var delta = slots[j] - slots[i];
                        delta.y = 0f;
                        if (delta.sqrMagnitude >= minSepSqr) continue;

                        var awayFromCenter = slots[j] - center;
                        awayFromCenter.y = 0f;
                        if (awayFromCenter.sqrMagnitude < 0.0001f)
                        {
                            var angle = (i + j + pass) * 1.17f;
                            awayFromCenter = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        }

                        slots[j] += awayFromCenter.normalized * step;
                        var referenceY = slots[j].y;
                        if (UnitNavUtils.TrySampleTieredNearReferenceY(
                                slots[j] + Vector3.up * 2f,
                                referenceY,
                                out var sampled,
                                UnitNavUtils.DefaultMaxVerticalDeltaFromReference,
                                UnitNavUtils.DefaultPlacementTiers,
                                UnitNavUtils.WalkableAreaMask))
                            slots[j] = sampled;

                        pushCount++;
                        movedAny = true;
                    }
                }

                if (!movedAny) break;
            }

            return pushCount;
        }

        public static void SampleSlotsOnNavMesh(List<Vector3> slots)
        {
            EnsureSlotsGrounded(slots, slots != null && slots.Count > 0 ? slots[0] : Vector3.zero);
        }

        public static void EnsureSlotsGrounded(List<Vector3> slots, Vector3 validatedDestination)
        {
            if (slots == null) return;

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (MovementDestinationResolver.TryValidateSlotPosition(slot, validatedDestination, out var grounded))
                    slots[i] = grounded;
                else
                    slots[i] = validatedDestination;
            }
        }

        public static float ComputeMinPairwiseSeparation(IReadOnlyList<Vector3> slots)
        {
            if (slots == null || slots.Count < 2) return float.PositiveInfinity;

            var minSep = float.PositiveInfinity;
            for (var i = 0; i < slots.Count; i++)
            {
                for (var j = i + 1; j < slots.Count; j++)
                {
                    var delta = slots[j] - slots[i];
                    delta.y = 0f;
                    minSep = Mathf.Min(minSep, delta.magnitude);
                }
            }

            return minSep;
        }
    }
}
