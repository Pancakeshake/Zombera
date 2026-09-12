using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.City
{
    /// <summary>
    ///     Derives buildable lot bounds inside a district block by reserving footpath corridors
    ///     on street-facing edges only (shared inter-block edges stay flush).
    /// </summary>
    public static class CityLotBoundsUtility
    {
        public static Rect ComputeLotBoundsRect(
            Rect districtBounds,
            IReadOnlyList<CityNamedArea> allAreas,
            CityNamedArea self,
            RoadNetworkSettings settings)
        {
            var corridorInset = CityMathBlockLayoutGenerator.ResolveFootpathCorridorInsetMeters(settings);
            if (corridorInset <= 0f || self == null)
                return districtBounds;

            var xMin = districtBounds.xMin;
            var xMax = districtBounds.xMax;
            var yMin = districtBounds.yMin;
            var yMax = districtBounds.yMax;

            if (IsStreetFacingEdge(districtBounds, allAreas, self, new Vector2(xMin, yMin), new Vector2(xMax, yMin)))
                yMin += corridorInset;

            if (IsStreetFacingEdge(districtBounds, allAreas, self, new Vector2(xMin, yMax), new Vector2(xMax, yMax)))
                yMax -= corridorInset;

            if (IsStreetFacingEdge(districtBounds, allAreas, self, new Vector2(xMin, yMin), new Vector2(xMin, yMax)))
                xMin += corridorInset;

            if (IsStreetFacingEdge(districtBounds, allAreas, self, new Vector2(xMax, yMin), new Vector2(xMax, yMax)))
                xMax -= corridorInset;

            if (xMax - xMin < 1f || yMax - yMin < 1f)
                return districtBounds;

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public static CityNamedArea FromMarker(CityNamedAreaMarker marker)
        {
            if (marker == null)
                return null;

            return new CityNamedArea
            {
                id = marker.AreaId,
                displayName = marker.DisplayName,
                clusterName = marker.ClusterName,
                districtType = marker.DistrictType,
                boundsXZ = marker.BoundsXZ,
                areaSquareMeters = marker.AreaSquareMeters,
                roundedCorners = marker.RoundedCorners,
                outlineXZ = marker.OutlineXZ ?? System.Array.Empty<Vector2>()
            };
        }

        public static CityNamedArea FromMarkerHubShifted(CityNamedAreaMarker marker)
        {
            if (marker == null)
                return null;

            var shift = marker.GetHubShiftXZ();
            var bounds = marker.GetHubShiftedBoundsXZ();
            var outline = marker.OutlineXZ;
            Vector2[] shiftedOutline;
            if (outline != null && outline.Length >= 3)
            {
                shiftedOutline = new Vector2[outline.Length];
                for (var i = 0; i < outline.Length; i++)
                    shiftedOutline[i] = outline[i] + shift;
            }
            else
            {
                shiftedOutline = new[]
                {
                    new Vector2(bounds.xMin, bounds.yMin),
                    new Vector2(bounds.xMax, bounds.yMin),
                    new Vector2(bounds.xMax, bounds.yMax),
                    new Vector2(bounds.xMin, bounds.yMax)
                };
            }

            return new CityNamedArea
            {
                id = marker.AreaId,
                displayName = marker.DisplayName,
                clusterName = marker.ClusterName,
                districtType = marker.DistrictType,
                boundsXZ = bounds,
                centerXZ = bounds.center,
                areaSquareMeters = marker.AreaSquareMeters,
                roundedCorners = marker.RoundedCorners,
                outlineXZ = shiftedOutline
            };
        }

        public static List<CityNamedArea> CollectFromAreasRoot(Transform areasRoot)
        {
            var list = new List<CityNamedArea>();
            if (areasRoot == null)
                return list;

            for (var i = 0; i < areasRoot.childCount; i++)
            {
                var area = FromMarker(areasRoot.GetChild(i).GetComponent<CityNamedAreaMarker>());
                if (area != null)
                    list.Add(area);
            }

            return list;
        }

        public static Rect ComputeLotBoundsFromMarker(
            CityNamedAreaMarker marker,
            IReadOnlyList<CityNamedArea> allAreas,
            RoadNetworkSettings settings)
        {
            if (marker == null)
                return default;

            var self = FromMarkerHubShifted(marker);
            return ComputeLotBoundsRect(marker.GetHubShiftedBoundsXZ(), allAreas, self, settings);
        }

        private static bool IsStreetFacingEdge(
            Rect districtBounds,
            IReadOnlyList<CityNamedArea> allAreas,
            CityNamedArea self,
            Vector2 edgeStart,
            Vector2 edgeEnd)
        {
            if (allAreas == null || allAreas.Count == 0)
                return true;

            for (var i = 0; i < allAreas.Count; i++)
            {
                var other = allAreas[i];
                if (other == null || other.id == self.id)
                    continue;

                if (CityBlockEdgeAdjacencyUtility.IsSharedStreetBlockEdge(
                        districtBounds, other.boundsXZ, edgeStart, edgeEnd))
                    return false;
            }

            return true;
        }
    }
}
