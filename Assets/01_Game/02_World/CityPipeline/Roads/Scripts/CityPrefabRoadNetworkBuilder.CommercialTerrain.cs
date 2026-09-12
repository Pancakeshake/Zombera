using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Commercial block-layout terrain painting for
    ///     <see cref="CityPrefabRoadNetworkBuilder"/>: paints the shared walkway
    ///     + parking zones of a <see cref="CityCommercialBlockLayout"/>, plus
    ///     white stall stripes over the parking asphalt.
    /// </summary>
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        /// <summary>
        ///     Paints the shared zones of a block-level commercial layout:
        ///     walkway along the street edge, parking in front of the stores,
        ///     and stall stripes over the parking.
        /// </summary>
        private static void PaintCommercialBlockLayout(
            CityCommercialBlockLayout layout, DistrictTerrainPaintContext ctx,
            ref int paintedLots, ref int subZoneCount)
        {
            EnsureBlockLayoutZones(layout);

            var commercialLayout = ctx.TerrainLayout != null
                ? ctx.TerrainLayout.GetLayout(CityDistrictType.Commercial)
                : null;
            var walkwayLayer = commercialLayout != null ? commercialLayout.footpathTextureIndex : -1;
            var parkingLayer = commercialLayout != null ? commercialLayout.drivewayTextureIndex : -1;
            var stripeLayer = commercialLayout != null ? commercialLayout.paintStripeTextureIndex : -1;

            var zones = new List<LotSubZone>(
                layout.WalkwayZones.Count + layout.ParkingZones.Count * 8);
            for (var i = 0; i < layout.WalkwayZones.Count; i++)
            {
                if (walkwayLayer < 0)
                    break;

                zones.Add(new LotSubZone
                {
                    TextureLayerIndex = walkwayLayer,
                    Bounds = layout.WalkwayZones[i],
                    HeightOffset = 0.015f,
                    DisplayName = "CommercialWalkway",
                    Sharp = true
                });
            }

            for (var i = 0; i < layout.ParkingZones.Count; i++)
            {
                if (parkingLayer < 0)
                    break;

                var parking = layout.ParkingZones[i];
                zones.Add(new LotSubZone
                {
                    TextureLayerIndex = parkingLayer,
                    Bounds = parking,
                    HeightOffset = 0f,
                    DisplayName = "CommercialParking",
                    Sharp = true
                });

                CityCommercialParkingStripes.AppendStripes(
                    parking, layout.ParkingCarFacing, stripeLayer, zones);
            }

            if (zones.Count == 0)
                return;

            CityLotTerrainPainter.PaintLotSubZones(zones, ctx.Config, blendMeters: 0f, clipRect: ctx.CityBounds);
            subZoneCount += zones.Count;
            paintedLots++;
        }

        /// <summary>
        ///     Rehydrates walkway/parking lists when they were lost (previously
        ///     non-serialized readonly fields) by rebuilding the block plan.
        /// </summary>
        private static void EnsureBlockLayoutZones(CityCommercialBlockLayout layout)
        {
            if (layout == null || layout.ParkingZones.Count > 0)
                return;

            var marker = layout.GetComponentInParent<CityNamedAreaMarker>();
            if (marker == null)
                return;

            var localBlock = marker.BoundsXZ;
            if (localBlock.width < 8f || localBlock.height < 8f)
                return;

            var plan = CityCommercialBlockLayoutPlanner.TryBuild(
                localBlock, marker.CityCenterXZ, marker.GetInstanceID());
            if (plan == null)
                return;

            var world = marker.GetHubShiftedBoundsXZ();
            var shift = world.min - localBlock.min;
            layout.Style = plan.Style;
            layout.ParkingCarFacing = plan.ParkingCarFacing;
            layout.WalkwayZones.Clear();
            layout.ParkingZones.Clear();
            for (var i = 0; i < plan.Walkways.Count; i++)
            {
                var r = plan.Walkways[i];
                layout.WalkwayZones.Add(new Rect(r.x + shift.x, r.y + shift.y, r.width, r.height));
            }

            for (var i = 0; i < plan.Parking.Count; i++)
            {
                var r = plan.Parking[i];
                layout.ParkingZones.Add(new Rect(r.x + shift.x, r.y + shift.y, r.width, r.height));
            }
        }
    }
}
