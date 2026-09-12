using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Tile collection helpers for hydrology-scoped terrain rebakes.</summary>
    public static class HydrologyTileScope
    {
        private const float DefaultMarginMeters = 48f;

        public static void CollectTilesWithWater(
            HydrologyPlan plan,
            WorldMapSession session,
            List<WorldTileCoord> buffer,
            float marginMeters = DefaultMarginMeters)
        {
            buffer.Clear();
            if (plan?.WaterClass == null || plan.Width <= 0 || plan.Height <= 0)
                return;

            var hasWater = false;
            var minX = float.PositiveInfinity;
            var minZ = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxZ = float.NegativeInfinity;
            var origin = plan.OriginXZ;
            var cellSize = plan.CellSizeMeters;

            for (var z = 0; z < plan.Height; z++)
            {
                for (var x = 0; x < plan.Width; x++)
                {
                    if (plan.WaterClass[plan.Index(x, z)] == WorldWaterClass.None)
                        continue;

                    hasWater = true;
                    var centerX = origin.x + (x + 0.5f) * cellSize;
                    var centerZ = origin.y + (z + 0.5f) * cellSize;
                    minX = Mathf.Min(minX, centerX);
                    minZ = Mathf.Min(minZ, centerZ);
                    maxX = Mathf.Max(maxX, centerX);
                    maxZ = Mathf.Max(maxZ, centerZ);
                }
            }

            if (!hasWater)
                return;

            var bounds = Rect.MinMaxRect(
                minX - marginMeters,
                minZ - marginMeters,
                maxX + marginMeters,
                maxZ + marginMeters);
            WorldBuildScopeUtility.CollectIntersecting(bounds, session, buffer);
        }
    }
}
