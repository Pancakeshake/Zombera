using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Validates complete river branches without manufacturing disconnected fragments.</summary>
    public static class HydrologyRiverFilter
    {
        public static RiverPolyline[] Filter(
            LandformField field,
            RiverPolyline[] rivers,
            bool[] placementMask,
            HydrologyProfile profile)
        {
            return Filter(field, rivers, placementMask, null, profile);
        }

        public static RiverPolyline[] Filter(
            LandformField field,
            RiverPolyline[] rivers,
            bool[] placementMask,
            bool[] oceanMask,
            HydrologyProfile profile)
        {
            if (rivers == null || rivers.Length == 0 || field == null || placementMask == null || profile == null)
                return rivers ?? System.Array.Empty<RiverPolyline>();

            var kept = new List<RiverPolyline>(rivers.Length);
            for (var i = 0; i < rivers.Length; i++)
            {
                if (RiverFitsMask(rivers[i], field, placementMask, oceanMask))
                    kept.Add(rivers[i]);
            }
            return kept.ToArray();
        }

        public static bool RiverOverlapsMask(RiverPolyline river, LandformField field, bool[] placementMask)
        {
            if (river?.PointsXZ == null || placementMask == null || field == null)
                return false;
            for (var i = 0; i < river.PointsXZ.Length; i++)
            {
                var width = i < river.WidthMeters.Length ? river.WidthMeters[i] : 8f;
                if (DiscOverlapsMask(river.PointsXZ[i], Mathf.Max(1f, width) * 0.5f, field, placementMask))
                    return true;
            }
            return false;
        }

        private static bool RiverFitsMask(
            RiverPolyline river,
            LandformField field,
            bool[] placementMask,
            bool[] oceanMask)
        {
            if (river?.PointsXZ == null || river.PointsXZ.Length < 2)
                return false;
            for (var i = 0; i < river.PointsXZ.Length; i++)
            {
                var width = i < river.WidthMeters.Length ? river.WidthMeters[i] : 8f;
                var allowOcean = river.HasOceanMouth && i == river.PointsXZ.Length - 1;
                if (DiscOverlapsMask(river.PointsXZ[i], Mathf.Max(1f, width) * 0.5f, field, placementMask, oceanMask, allowOcean))
                    return false;
            }
            return true;
        }

        private static bool DiscOverlapsMask(
            Vector2 centerXZ,
            float radiusMeters,
            LandformField field,
            bool[] placementMask,
            bool[] oceanMask = null,
            bool allowOceanOverlap = false)
        {
            var radiusSq = radiusMeters * radiusMeters;
            var minX = Mathf.FloorToInt((centerXZ.x - radiusMeters - field.OriginXZ.x) / field.CellSize);
            var maxX = Mathf.CeilToInt((centerXZ.x + radiusMeters - field.OriginXZ.x) / field.CellSize);
            var minZ = Mathf.FloorToInt((centerXZ.y - radiusMeters - field.OriginXZ.y) / field.CellSize);
            var maxZ = Mathf.CeilToInt((centerXZ.y + radiusMeters - field.OriginXZ.y) / field.CellSize);
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (x < 0 || z < 0 || x >= field.Width || z >= field.Height)
                        continue;
                    var cell = field.CellCenterXZ(x, z);
                    var delta = cell - centerXZ;
                    if (delta.sqrMagnitude > radiusSq)
                        continue;
                    var index = field.Index(x, z);
                    if (index < placementMask.Length && placementMask[index] &&
                        !(allowOceanOverlap && oceanMask != null && index < oceanMask.Length && oceanMask[index]))
                        return true;
                }
            }
            return false;
        }
    }
}
