using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Collects door positions ("StairSocket" children) from placed buildings and
    ///     picks the street-facing one so the Lot Terrain step can paint the slab
    ///     against the real door instead of a lot-centred guess.
    ///     (Formerly part of the removed CityDoorPathGenerator mesh-path system.)
    /// </summary>
    internal static class CityDoorAnchorUtility
    {
        internal static void CollectDoorPositions(Transform root, List<Vector3> results)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var n = child.name;
                if (n.StartsWith("StairSocket", StringComparison.OrdinalIgnoreCase))
                    results.Add(child.position);

                CollectDoorPositions(child, results);
            }
        }

        internal static Vector3? SelectStreetFacingDoor(
            List<Vector3> doors,
            Vector3 buildingPos,
            CityBuildingStreetFacingMarker marker)
        {
            if (marker == null || doors.Count == 0) return null;

            var streetDir = CityBuildingRoadFacingUtility.GetRoadNormal(marker.streetFace);
            var bestScore = float.MinValue;
            Vector3? best = null;
            for (var d = 0; d < doors.Count; d++)
            {
                var score = Vector3.Dot((doors[d] - buildingPos).normalized, streetDir);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = doors[d];
                }
            }

            return best;
        }
    }
}
