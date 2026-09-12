using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Priority-flood depression filling seeded by ocean and map-boundary cells.</summary>
    public static class PriorityFloodSolver
    {
        private static readonly Vector2Int[] Neighbors =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        public static float[] FillDepressions(LandformField field) =>
            FillDepressionsInternal(field, oceanMask: null, layout: default, useLayoutBoundarySeed: false);

        public static float[] FillDepressions(LandformField field, bool[] oceanMask) =>
            FillDepressionsInternal(field, oceanMask, layout: default, useLayoutBoundarySeed: false);

        public static float[] FillDepressions(
            LandformField field,
            bool[] oceanMask,
            WorldMapBoundaryLayout layout) =>
            FillDepressionsInternal(field, oceanMask, layout, useLayoutBoundarySeed: true);

        private static float[] FillDepressionsInternal(
            LandformField field,
            bool[] oceanMask,
            WorldMapBoundaryLayout layout,
            bool useLayoutBoundarySeed)
        {
            if (field == null) return Array.Empty<float>();

            var count = field.Width * field.Height;
            var filled = new float[count];
            Array.Copy(field.WorldHeights, filled, count);

            var open = new SortedSet<(float Height, int Index)>(Comparer<(float, int)>.Create((a, b) =>
            {
                var cmp = a.Item1.CompareTo(b.Item1);
                return cmp != 0 ? cmp : a.Item2.CompareTo(b.Item2);
            }));
            var closed = new bool[count];

            if (useLayoutBoundarySeed)
                SeedLayoutBoundary(field, filled, open, closed, layout);
            else
                SeedAllBoundary(field, filled, open, closed);

            if (oceanMask != null && oceanMask.Length >= count)
                SeedOcean(field, filled, oceanMask, open, closed);

            while (open.Count > 0)
            {
                var current = open.Min;
                open.Remove(current);
                var index = current.Index;
                var x = index % field.Width;
                var z = index / field.Width;

                for (var n = 0; n < Neighbors.Length; n++)
                {
                    var nx = x + Neighbors[n].x;
                    var nz = z + Neighbors[n].y;
                    if (nx < 0 || nz < 0 || nx >= field.Width || nz >= field.Height) continue;

                    var ni = nz * field.Width + nx;
                    if (closed[ni]) continue;
                    closed[ni] = true;

                    var spill = Mathf.Max(filled[ni], filled[index]);
                    filled[ni] = spill;
                    open.Add((spill, ni));
                }
            }

            return filled;
        }

        private static void SeedLayoutBoundary(
            LandformField field,
            float[] filled,
            SortedSet<(float Height, int Index)> open,
            bool[] closed,
            WorldMapBoundaryLayout layout)
        {
            for (var x = 0; x < field.Width; x++)
            {
                if (WorldMapBoundaryUtility.IsOceanSeedCell(x, 0, field.Width, field.Height, layout))
                    Push(filled, open, closed, x, 0, field.Width);
                if (WorldMapBoundaryUtility.IsOceanSeedCell(x, field.Height - 1, field.Width, field.Height, layout))
                    Push(filled, open, closed, x, field.Height - 1, field.Width);
            }

            for (var z = 0; z < field.Height; z++)
            {
                if (WorldMapBoundaryUtility.IsOceanSeedCell(0, z, field.Width, field.Height, layout))
                    Push(filled, open, closed, 0, z, field.Width);
                if (WorldMapBoundaryUtility.IsOceanSeedCell(field.Width - 1, z, field.Width, field.Height, layout))
                    Push(filled, open, closed, field.Width - 1, z, field.Width);
            }
        }

        private static void SeedAllBoundary(
            LandformField field,
            float[] filled,
            SortedSet<(float Height, int Index)> open,
            bool[] closed)
        {
            for (var x = 0; x < field.Width; x++)
            {
                Push(filled, open, closed, x, 0, field.Width);
                Push(filled, open, closed, x, field.Height - 1, field.Width);
            }

            for (var z = 0; z < field.Height; z++)
            {
                Push(filled, open, closed, 0, z, field.Width);
                Push(filled, open, closed, field.Width - 1, z, field.Width);
            }
        }

        private static void SeedOcean(
            LandformField field,
            float[] filled,
            bool[] oceanMask,
            SortedSet<(float Height, int Index)> open,
            bool[] closed)
        {
            for (var i = 0; i < oceanMask.Length; i++)
            {
                if (!oceanMask[i] || closed[i]) continue;
                closed[i] = true;
                open.Add((filled[i], i));
            }

            _ = field;
        }

        private static void Push(
            float[] filled,
            SortedSet<(float Height, int Index)> open,
            bool[] closed,
            int x,
            int z,
            int width)
        {
            var i = z * width + x;
            if (closed[i]) return;
            closed[i] = true;
            open.Add((filled[i], i));
        }
    }
}
