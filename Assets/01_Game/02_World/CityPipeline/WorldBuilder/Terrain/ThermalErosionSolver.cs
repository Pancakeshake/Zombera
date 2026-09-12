using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Iterative thermal (talus) erosion over a <see cref="LandformField"/>.</summary>
    public static partial class ThermalErosionSolver
    {
        private static readonly Vector2Int[] Neighbors =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        public static void Apply(LandformField field, LandformProfile profile)
        {
            if (field == null || profile == null) return;

            var iterations = Mathf.Max(0, profile.ThermalErosionIterations);
            if (iterations == 0) return;

            ApplyRegion(
                field,
                profile.ThermalTalusAngleDegrees,
                profile.ThermalTransferFraction,
                iterations,
                new ThermalRegionScope(1, field.Width - 2, 1, field.Height - 2, null, null));
        }

        /// <summary>Full-field thermal erosion with city pad cores frozen.</summary>
        public static void Apply(
            LandformField field,
            LandformProfile profile,
            IReadOnlyList<CityFlattenPad> pads)
        {
            if (field == null || profile == null) return;

            var iterations = Mathf.Max(0, profile.ThermalErosionIterations);
            if (iterations == 0) return;

            var freeze = pads != null && pads.Count > 0
                ? BuildFreezeMask(field, pads, insetCells: 0f)
                : null;

            ApplyRegion(
                field,
                profile.ThermalTalusAngleDegrees,
                profile.ThermalTransferFraction,
                iterations,
                new ThermalRegionScope(1, field.Width - 2, 1, field.Height - 2, freeze, null));
        }

        /// <summary>
        ///     Masked pad-edge talus: only cells in pad halos move; plateau cores are frozen
        ///     (neither source nor destination). Scratch and loops are scoped to the halo AABB.
        /// </summary>
        public static void ApplyMaskedForPads(
            LandformField field,
            LandformProfile profile,
            IReadOnlyList<CityFlattenPad> pads,
            float approachSlopeDegrees)
        {
            if (field == null || profile == null || pads == null || pads.Count == 0)
                return;

            var talusDegrees = Mathf.Max(1f, approachSlopeDegrees + 5f);
            var transfer = Mathf.Clamp01(profile.ThermalTransferFraction);
            if (talusDegrees <= 0f || transfer <= 0f)
                return;

            var maxDelta = 0f;
            for (var p = 0; p < pads.Count; p++)
            {
                var pad = pads[p];
                if (pad == null) continue;
                SampleHaloMaxDelta(field, pad, ref maxDelta);
            }

            var talusPerCell = Mathf.Tan(talusDegrees * Mathf.Deg2Rad) * field.CellSize;
            if (talusPerCell <= 0.0001f)
                return;

            var iterations = Mathf.Clamp(
                Mathf.CeilToInt(2.5f * maxDelta / talusPerCell),
                0,
                48);
            if (profile.CityPadEdgeRelaxMaxIterations > 0)
                iterations = Mathf.Min(iterations, profile.CityPadEdgeRelaxMaxIterations);
            if (iterations == 0)
                return;

            var freeze = BuildFreezeMask(field, pads);
            var active = BuildActiveMask(field, pads, iterations * field.CellSize);
            if (!TryUnionActiveBounds(field, active, out var unionX0, out var unionX1, out var unionZ0, out var unionZ1))
                return;

            ApplyRegion(
                field,
                talusDegrees,
                transfer,
                iterations,
                new ThermalRegionScope(unionX0, unionX1, unionZ0, unionZ1, freeze, active));
        }

        /// <summary>Meters of influence beyond pad outer bounds after masked relax.</summary>
        public static float ResolveMaskedReachMeters(
            LandformField field,
            LandformProfile profile,
            IReadOnlyList<CityFlattenPad> pads,
            float approachSlopeDegrees)
        {
            if (field == null || profile == null || pads == null || pads.Count == 0)
                return 0f;

            var talusDegrees = Mathf.Max(1f, approachSlopeDegrees + 5f);
            var talusPerCell = Mathf.Tan(talusDegrees * Mathf.Deg2Rad) * field.CellSize;
            if (talusPerCell <= 0.0001f)
                return 0f;

            var maxDelta = 0f;
            for (var p = 0; p < pads.Count; p++)
            {
                var pad = pads[p];
                if (pad == null) continue;
                SampleHaloMaxDelta(field, pad, ref maxDelta);
            }

            var iterations = Mathf.Clamp(
                Mathf.CeilToInt(2.5f * maxDelta / talusPerCell),
                0,
                48);
            if (profile.CityPadEdgeRelaxMaxIterations > 0)
                iterations = Mathf.Min(iterations, profile.CityPadEdgeRelaxMaxIterations);
            return iterations * field.CellSize;
        }

        /// <summary>
        ///     Sparse Jacobi talus: seed cells above talus, then only reprocess movers and neighbors.
        /// </summary>
        private static void ApplyRegion(
            LandformField field,
            float talusAngleDegrees,
            float transfer,
            int iterations,
            in ThermalRegionScope region)
        {
            var talus = Mathf.Tan(talusAngleDegrees * Mathf.Deg2Rad) * field.CellSize;
            if (talus <= 0f || transfer <= 0f || iterations <= 0)
                return;
            if (region.X1 < region.X0 || region.Z1 < region.Z0)
                return;

            var heights = field.WorldHeights;
            var cellCount = heights.Length;
            var current = new List<int>(1024);
            var next = new List<int>(1024);
            var queued = new bool[cellCount];
            var delta = new float[cellCount];
            var dirty = new List<int>(1024);
            var isDirty = new bool[cellCount];
            var pass = new ThermalPassArgs(field, heights, talus, transfer, in region);

            SeedSteepCells(in pass, current, queued);
            if (current.Count == 0)
                return;

            for (var iter = 0; iter < iterations; iter++)
            {
                if (current.Count == 0)
                    break;

                ClearQueued(current, queued);
                AccumulateMoves(in pass, current, delta, dirty, isDirty);
                current.Clear();

                if (dirty.Count == 0)
                    break;

                ApplyDeltas(heights, dirty, delta, isDirty);
                EnqueueDirtyNeighborhood(field, dirty, in region, next, queued);
                dirty.Clear();

                var swap = current;
                current = next;
                next = swap;
            }
        }

        private static void SeedSteepCells(
            in ThermalPassArgs pass,
            List<int> current,
            bool[] queued)
        {
            var field = pass.Field;
            var region = pass.Region;
            for (var z = region.Z0; z <= region.Z1; z++)
            {
                for (var x = region.X0; x <= region.X1; x++)
                {
                    var index = field.Index(x, z);
                    if (!IsMovable(index, region.Freeze, region.Active))
                        continue;
                    if (FindSteepestDownhill(in pass, x, z) < 0)
                        continue;
                    Enqueue(index, current, queued);
                }
            }
        }

        private static void AccumulateMoves(
            in ThermalPassArgs pass,
            List<int> current,
            float[] delta,
            List<int> dirty,
            bool[] isDirty)
        {
            var field = pass.Field;
            var heights = pass.Heights;
            var width = field.Width;
            for (var i = 0; i < current.Count; i++)
            {
                var index = current[i];
                var z = index / width;
                var x = index - z * width;
                var maxNeighbor = FindSteepestDownhill(in pass, x, z);
                if (maxNeighbor < 0)
                    continue;

                var nIndex = field.Index(x + Neighbors[maxNeighbor].x, z + Neighbors[maxNeighbor].y);
                var move = (heights[index] - heights[nIndex] - pass.Talus) * pass.Transfer * 0.5f;
                if (move <= 0f)
                    continue;

                AddDelta(index, -move, delta, dirty, isDirty);
                AddDelta(nIndex, move, delta, dirty, isDirty);
            }
        }

        private static int FindSteepestDownhill(in ThermalPassArgs pass, int x, int z)
        {
            var field = pass.Field;
            var heights = pass.Heights;
            var region = pass.Region;
            var index = field.Index(x, z);
            var cellHeight = heights[index];
            var maxDiff = 0f;
            var maxNeighbor = -1;

            for (var n = 0; n < Neighbors.Length; n++)
            {
                var nx = x + Neighbors[n].x;
                var nz = z + Neighbors[n].y;
                if (!region.Contains(nx, nz))
                    continue;

                var nIndex = field.Index(nx, nz);
                if (!IsMovable(nIndex, region.Freeze, region.Active))
                    continue;

                var diff = cellHeight - heights[nIndex];
                if (diff <= pass.Talus || diff <= maxDiff)
                    continue;
                maxDiff = diff;
                maxNeighbor = n;
            }

            return maxNeighbor;
        }

        private static void EnqueueDirtyNeighborhood(
            LandformField field,
            List<int> dirty,
            in ThermalRegionScope region,
            List<int> next,
            bool[] queued)
        {
            var width = field.Width;
            for (var i = 0; i < dirty.Count; i++)
            {
                var index = dirty[i];
                var z = index / width;
                var x = index - z * width;
                TryEnqueueCell(field, x, z, in region, next, queued);
                for (var n = 0; n < Neighbors.Length; n++)
                {
                    TryEnqueueCell(
                        field,
                        x + Neighbors[n].x,
                        z + Neighbors[n].y,
                        in region,
                        next,
                        queued);
                }
            }
        }

        private static void TryEnqueueCell(
            LandformField field,
            int x,
            int z,
            in ThermalRegionScope region,
            List<int> next,
            bool[] queued)
        {
            if (!region.Contains(x, z))
                return;
            var index = field.Index(x, z);
            if (!IsMovable(index, region.Freeze, region.Active))
                return;
            Enqueue(index, next, queued);
        }

        private static void ApplyDeltas(
            float[] heights,
            List<int> dirty,
            float[] delta,
            bool[] isDirty)
        {
            for (var i = 0; i < dirty.Count; i++)
            {
                var index = dirty[i];
                heights[index] += delta[index];
                delta[index] = 0f;
                isDirty[index] = false;
            }
        }

        private static void AddDelta(
            int index,
            float amount,
            float[] delta,
            List<int> dirty,
            bool[] isDirty)
        {
            if (!isDirty[index])
            {
                isDirty[index] = true;
                dirty.Add(index);
            }

            delta[index] += amount;
        }

        private static void Enqueue(int index, List<int> list, bool[] queued)
        {
            if (queued[index])
                return;
            queued[index] = true;
            list.Add(index);
        }

        private static void ClearQueued(List<int> current, bool[] queued)
        {
            for (var i = 0; i < current.Count; i++)
                queued[current[i]] = false;
        }

        private static bool IsMovable(int index, bool[] freeze, bool[] active)
        {
            if (freeze != null && freeze[index])
                return false;
            if (active != null && !active[index])
                return false;
            return true;
        }

        private static bool TryUnionActiveBounds(
            LandformField field,
            bool[] active,
            out int unionX0,
            out int unionX1,
            out int unionZ0,
            out int unionZ1)
        {
            unionX0 = field.Width;
            unionX1 = 0;
            unionZ0 = field.Height;
            unionZ1 = 0;
            var any = false;

            for (var i = 0; i < active.Length; i++)
            {
                if (!active[i])
                    continue;
                var z = i / field.Width;
                var x = i - z * field.Width;
                if (x < 1 || z < 1 || x > field.Width - 2 || z > field.Height - 2)
                    continue;
                any = true;
                unionX0 = Mathf.Min(unionX0, x);
                unionZ0 = Mathf.Min(unionZ0, z);
                unionX1 = Mathf.Max(unionX1, x);
                unionZ1 = Mathf.Max(unionZ1, z);
            }

            return any;
        }
    }
}
