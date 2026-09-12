using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        /// <summary>
        ///     Door-aware lot terrain helpers: resolves the placed building per lot so
        ///     the painted slab matches the real footprint and door anchor.
        /// </summary>
        private struct PlacedBuildingAnchor
        {
            public Rect Footprint;
            public Vector2 Door;
            public bool HasDoor;
        }

        private static List<PlacedBuildingAnchor> CollectPlacedBuildingAnchors(Transform areaTransform)
        {
            var result = new List<PlacedBuildingAnchor>();
            if (areaTransform == null)
                return result;

            var placed = areaTransform.Find(CityDistrictLotPlacement.PlacedBuildingsContainerName);
            if (placed == null)
                return result;

            for (var i = 0; i < placed.childCount; i++)
            {
                var marker = placed.GetChild(i).GetComponent<CityBuildingStreetFacingMarker>();
                if (marker == null ||
                    marker.footprintXZ.width < 0.5f || marker.footprintXZ.height < 0.5f)
                    continue;

                result.Add(new PlacedBuildingAnchor
                {
                    Footprint = marker.footprintXZ,
                    Door = new Vector2(marker.doorAnchorWorld.x, marker.doorAnchorWorld.z),
                    HasDoor = marker.hasDoorAnchor
                });
            }

            return result;
        }

        private static CityPrefabResidentialLotBuilder.LotBuildingAnchor? FindBuildingAnchorForLot(
            Rect lotRect, List<PlacedBuildingAnchor> anchors)
        {
            if (anchors == null || anchors.Count == 0)
                return null;

            // Overlap match — Contains(center) misses buildings whose AABB center
            // sits just outside the lot mesh while most of the house is still on it,
            // which then fell through to the oversized geometric slab.
            var bestIndex = -1;
            var bestOverlap = 0f;
            for (var i = 0; i < anchors.Count; i++)
            {
                var overlap = OverlapAreaXZ(lotRect, anchors[i].Footprint);
                if (overlap <= bestOverlap)
                    continue;
                bestOverlap = overlap;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return null;

            var footprint = anchors[bestIndex].Footprint;
            var minOverlap = Mathf.Min(4f, footprint.width * footprint.height * 0.2f);
            if (bestOverlap < minOverlap)
                return null;

            var anchor = anchors[bestIndex];
            return new CityPrefabResidentialLotBuilder.LotBuildingAnchor(
                anchor.Footprint, anchor.Door, anchor.HasDoor);
        }

        private static float OverlapAreaXZ(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            var w = xMax - xMin;
            var h = yMax - yMin;
            if (w <= 0f || h <= 0f)
                return 0f;
            return w * h;
        }
    }
}
