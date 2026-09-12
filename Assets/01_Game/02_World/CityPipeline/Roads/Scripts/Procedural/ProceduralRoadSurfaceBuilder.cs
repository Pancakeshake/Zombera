using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Tile-clipped procedural road surface builder for streaming worlds.
    /// </summary>
    public static class ProceduralRoadSurfaceBuilder
    {
        public static ProceduralCityRoadBuilder.BuildResult BuildForTile(
            Transform tileRoot,
            IReadOnlyList<RoadPolyline> roads,
            Rect tileRect,
            RoadNetworkSettings settings,
            Func<Vector2, float> resolveHeight,
            float clipMarginMeters = 1.5f)
        {
            if (tileRoot == null || roads == null || roads.Count == 0 || settings == null)
                return default;

            var expanded = ExpandRect(tileRect, clipMarginMeters, clipMarginMeters);
            var options = new ProceduralCityRoadBuildOptions
            {
                PlaceAsphalt = true,
                PlaceSidewalks = false,
                ClipRect = expanded
            };

            return ProceduralCityRoadBuilder.Build(
                tileRoot,
                roads,
                settings,
                resolveHeight,
                options);
        }

        private static Rect ExpandRect(Rect rect, float padX, float padZ)
        {
            return Rect.MinMaxRect(
                rect.xMin - padX,
                rect.yMin - padZ,
                rect.xMax + padX,
                rect.yMax + padZ);
        }
    }
}
