using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Commercial block-layout wiring for <see cref="CityPrefabResidentialLotBuilder"/>:
    ///     turns a block-level store plan into store lots + a
    ///     <see cref="CityCommercialBlockLayout"/> component carrying the shared
    ///     walkway / parking paint zones.
    /// </summary>
    internal static partial class CityPrefabResidentialLotBuilder
    {
        /// <summary>
        ///     Commercial block-level store layout: U / L / strip store lots plus a
        ///     block-layout component for the terrain step. Returns null when the
        ///     block is too small — the caller then uses the legacy per-lot path.
        /// </summary>
        private static CityCommercialBlockLayout TryBuildCommercialBlockLayout(
            CityNamedAreaMarker marker, Rect block, System.Random rng,
            GameObject lotsRoot, List<LotPlacement> lots)
        {
            var plan = CityCommercialBlockLayoutPlanner.TryBuild(block, marker.CityCenterXZ, rng.Next());
            if (plan == null || plan.StoreLots.Count == 0)
                return null;

            for (var i = 0; i < plan.StoreLots.Count; i++)
            {
                var spec = plan.StoreLots[i];
                lots.Add(new LotPlacement
                {
                    Bounds = spec.Rect,
                    OverrideFace = spec.Face,
                    OverrideKind = spec.Kind
                });
            }

            // Zone rects are block-local (BoundsXZ space); translate into the
            // hub-shifted world space the terrain painter works in.
            var shift = marker.GetHubShiftedBoundsXZ().min - block.min;
            var layout = lotsRoot.AddComponent<CityCommercialBlockLayout>();
            layout.Style = plan.Style;
            layout.ParkingCarFacing = plan.ParkingCarFacing;
            for (var i = 0; i < plan.Walkways.Count; i++)
            {
                var rect = plan.Walkways[i];
                layout.WalkwayZones.Add(new Rect(rect.x + shift.x, rect.y + shift.y, rect.width, rect.height));
            }
            for (var i = 0; i < plan.Parking.Count; i++)
            {
                var rect = plan.Parking[i];
                layout.ParkingZones.Add(new Rect(rect.x + shift.x, rect.y + shift.y, rect.width, rect.height));
            }

            return layout;
        }
    }
}
