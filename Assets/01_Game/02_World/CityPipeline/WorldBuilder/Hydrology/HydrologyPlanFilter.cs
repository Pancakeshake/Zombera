using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Removes ocean from city pad plateaus (coastal reclaim; not the grading apron).</summary>
    public static class HydrologyPlanFilter
    {
        public static void PruneCityPads(HydrologyPlan plan, IReadOnlyList<CityFlattenPad> pads)
        {
            if (plan == null || pads == null || pads.Count == 0)
                return;

            ClearWaterCellsInPads(plan, pads);
            plan.ReplaceWaterFeatures(
                Array.Empty<RiverPolyline>(),
                Array.Empty<LakeRecord>());
            HydrologyDistanceField.Compute(plan);
        }

        private static void ClearWaterCellsInPads(HydrologyPlan plan, IReadOnlyList<CityFlattenPad> pads)
        {
            for (var p = 0; p < pads.Count; p++)
            {
                var pad = pads[p];
                if (pad == null) continue;
                ClearWaterCellsInRect(plan, pad.HydrologyPruneBoundsXZ);
            }
        }

        private static void ClearWaterCellsInRect(HydrologyPlan plan, Rect outer)
        {
            var x0 = Mathf.Max(0, Mathf.FloorToInt((outer.xMin - plan.OriginXZ.x) / plan.CellSizeMeters));
            var x1 = Mathf.Min(plan.Width - 1, Mathf.CeilToInt((outer.xMax - plan.OriginXZ.x) / plan.CellSizeMeters));
            var z0 = Mathf.Max(0, Mathf.FloorToInt((outer.yMin - plan.OriginXZ.y) / plan.CellSizeMeters));
            var z1 = Mathf.Min(plan.Height - 1, Mathf.CeilToInt((outer.yMax - plan.OriginXZ.y) / plan.CellSizeMeters));

            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var center = plan.OriginXZ + new Vector2(
                        (x + 0.5f) * plan.CellSizeMeters,
                        (z + 0.5f) * plan.CellSizeMeters);
                    if (!ContainsInclusive(outer, center))
                        continue;

                    var i = plan.Index(x, z);
                    plan.WaterClass[i] = WorldWaterClass.None;
                    plan.DepthMeters[i] = 0f;
                    plan.SurfaceWorldY[i] = 0f;
                }
            }
        }

        private static bool ContainsInclusive(Rect rect, Vector2 point) =>
            point.x >= rect.xMin && point.x <= rect.xMax &&
            point.y >= rect.yMin && point.y <= rect.yMax;
    }
}
