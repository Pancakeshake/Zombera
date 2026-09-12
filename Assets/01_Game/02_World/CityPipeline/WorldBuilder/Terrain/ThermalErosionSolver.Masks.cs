using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Pad halo freeze/active masks and rect helpers for thermal erosion.</summary>
    public static partial class ThermalErosionSolver
    {
        private static bool[] BuildActiveMask(
            LandformField field,
            IReadOnlyList<CityFlattenPad> pads,
            float reachMeters)
        {
            var active = new bool[field.WorldHeights.Length];
            for (var p = 0; p < pads.Count; p++)
            {
                var pad = pads[p];
                if (pad == null) continue;
                MarkActiveHalo(field, Expand(pad.OuterBoundsXZ, reachMeters), active);
            }

            return active;
        }

        private static void MarkActiveHalo(LandformField field, Rect outer, bool[] active)
        {
            WorldToCellRect(field, outer, out var x0, out var x1, out var z0, out var z1);
            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var center = field.CellCenterXZ(x, z);
                    if (!ContainsInclusive(outer, center))
                        continue;
                    active[field.Index(x, z)] = true;
                }
            }
        }

        private static bool[] BuildFreezeMask(LandformField field, IReadOnlyList<CityFlattenPad> pads)
        {
            return BuildFreezeMask(field, pads, insetCells: 1.5f);
        }

        /// <summary>
        ///     Freeze pad plateaus. Use insetCells=0 for planning-field core freeze during full erode.
        /// </summary>
        private static bool[] BuildFreezeMask(
            LandformField field,
            IReadOnlyList<CityFlattenPad> pads,
            float insetCells)
        {
            var freeze = new bool[field.WorldHeights.Length];
            var inset = field.CellSize * Mathf.Max(0f, insetCells);
            for (var p = 0; p < pads.Count; p++)
            {
                var pad = pads[p];
                if (pad == null) continue;

                var inner = inset > 0.01f ? Shrink(pad.PlateauBoundsXZ, inset) : pad.PlateauBoundsXZ;
                if (inner.width <= 0f || inner.height <= 0f)
                    continue;

                MarkFrozenPlateau(field, inner, freeze);
            }

            return freeze;
        }

        private static void MarkFrozenPlateau(LandformField field, Rect inner, bool[] freeze)
        {
            WorldToCellRect(field, inner, out var x0, out var x1, out var z0, out var z1);
            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var center = field.CellCenterXZ(x, z);
                    if (!ContainsInclusive(inner, center))
                        continue;
                    freeze[field.Index(x, z)] = true;
                }
            }
        }

        private static bool ContainsInclusive(Rect rect, Vector2 point) =>
            point.x >= rect.xMin && point.x <= rect.xMax &&
            point.y >= rect.yMin && point.y <= rect.yMax;

        private static Rect Shrink(Rect rect, float meters) =>
            Rect.MinMaxRect(
                rect.xMin + meters,
                rect.yMin + meters,
                rect.xMax - meters,
                rect.yMax - meters);

        private static void SampleHaloMaxDelta(LandformField field, CityFlattenPad pad, ref float maxDelta)
        {
            var outer = pad.OuterBoundsXZ;
            WorldToCellRect(field, outer, out var x0, out var x1, out var z0, out var z1);
            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var center = field.CellCenterXZ(x, z);
                    if (ContainsInclusive(pad.PlateauBoundsXZ, center))
                        continue;
                    maxDelta = Mathf.Max(
                        maxDelta,
                        Mathf.Abs(field.WorldHeights[field.Index(x, z)] - pad.TargetHeightWorldY));
                }
            }
        }

        private static void WorldToCellRect(
            LandformField field,
            Rect bounds,
            out int x0,
            out int x1,
            out int z0,
            out int z1)
        {
            x0 = Mathf.Max(0, Mathf.FloorToInt((bounds.xMin - field.OriginXZ.x) / field.CellSize));
            x1 = Mathf.Min(field.Width - 1, Mathf.CeilToInt((bounds.xMax - field.OriginXZ.x) / field.CellSize));
            z0 = Mathf.Max(0, Mathf.FloorToInt((bounds.yMin - field.OriginXZ.y) / field.CellSize));
            z1 = Mathf.Min(field.Height - 1, Mathf.CeilToInt((bounds.yMax - field.OriginXZ.y) / field.CellSize));
        }

        private static Rect Expand(Rect rect, float meters) =>
            Rect.MinMaxRect(rect.xMin - meters, rect.yMin - meters, rect.xMax + meters, rect.yMax + meters);
    }
}
