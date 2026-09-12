using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Playable-interior and rect intersection helpers.</summary>
    public static partial class WorldMapBoundaryUtility
    {
        /// <summary>
        ///     Shrinks the map rect inward on edges that are ocean or mountain barriers,
        ///     leaving the playable interior suitable for city placement.
        /// </summary>
        public static Rect ComputePlayableInteriorRect(
            Rect mapBounds,
            WorldMapBoundaryLayout layout,
            float edgeBarrierDepthMeters,
            float extraInsetMeters = 0f)
        {
            if (mapBounds.width <= 0f || mapBounds.height <= 0f)
                return mapBounds;

            var inset = Mathf.Max(0f, edgeBarrierDepthMeters) + Mathf.Max(0f, extraInsetMeters);
            var xMin = mapBounds.xMin;
            var xMax = mapBounds.xMax;
            var yMin = mapBounds.yMin;
            var yMax = mapBounds.yMax;

            if (layout.West == WorldMapBoundaryKind.Ocean ||
                layout.West == WorldMapBoundaryKind.Mountains)
                xMin += inset;
            if (layout.East == WorldMapBoundaryKind.Ocean ||
                layout.East == WorldMapBoundaryKind.Mountains)
                xMax -= inset;
            if (layout.South == WorldMapBoundaryKind.Ocean ||
                layout.South == WorldMapBoundaryKind.Mountains)
                yMin += inset;
            if (layout.North == WorldMapBoundaryKind.Ocean ||
                layout.North == WorldMapBoundaryKind.Mountains)
                yMax -= inset;

            if (xMax <= xMin || yMax <= yMin)
                return mapBounds;

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public static Rect IntersectRects(Rect a, Rect b)
        {
            if (a.width <= 0f || a.height <= 0f)
                return b;
            if (b.width <= 0f || b.height <= 0f)
                return a;

            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin)
                return default;

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public static bool IsOceanSeedCell(
            int x,
            int z,
            int width,
            int height,
            WorldMapBoundaryLayout layout)
        {
            var onEdge = x == 0 || z == 0 || x == width - 1 || z == height - 1;
            if (!onEdge)
                return false;

            if (x == 0 && layout.West == WorldMapBoundaryKind.Ocean)
                return true;
            if (x == width - 1 && layout.East == WorldMapBoundaryKind.Ocean)
                return true;
            if (z == 0 && layout.South == WorldMapBoundaryKind.Ocean)
                return true;
            if (z == height - 1 && layout.North == WorldMapBoundaryKind.Ocean)
                return true;
            return false;
        }
    }
}
