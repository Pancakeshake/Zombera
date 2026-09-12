using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>Face / rect helpers for <see cref="CityCommercialBlockLayoutPlanner"/>.</summary>
    public static partial class CityCommercialBlockLayoutPlanner
    {
        /// <summary>The block edge nearest the city center — commercial opens toward downtown.</summary>
        private static BlockFace ResolvePrimaryFace(Rect block, Vector2 cityCenter)
        {
            var center = block.center;
            var dSouth = Vector2.Distance(cityCenter, new Vector2(center.x, block.yMin));
            var dNorth = Vector2.Distance(cityCenter, new Vector2(center.x, block.yMax));
            var dWest = Vector2.Distance(cityCenter, new Vector2(block.xMin, center.y));
            var dEast = Vector2.Distance(cityCenter, new Vector2(block.xMax, center.y));

            var min = Mathf.Min(dSouth, dNorth, dWest, dEast);
            if (Mathf.Approximately(min, dSouth)) return BlockFace.South;
            if (Mathf.Approximately(min, dNorth)) return BlockFace.North;
            if (Mathf.Approximately(min, dWest)) return BlockFace.West;
            return BlockFace.East;
        }

        private static float FrontageLength(Rect block, BlockFace face) =>
            face == BlockFace.South || face == BlockFace.North ? block.width : block.height;

        private static float CrossLength(Rect block, BlockFace face) =>
            face == BlockFace.South || face == BlockFace.North ? block.height : block.width;

        private static Rect EdgeStripRect(Rect block, BlockFace face, float depth)
        {
            return face switch
            {
                BlockFace.South => Rect.MinMaxRect(block.xMin, block.yMin, block.xMax, block.yMin + depth),
                BlockFace.North => Rect.MinMaxRect(block.xMin, block.yMax - depth, block.xMax, block.yMax),
                BlockFace.West  => Rect.MinMaxRect(block.xMin, block.yMin, block.xMin + depth, block.yMax),
                _               => Rect.MinMaxRect(block.xMax - depth, block.yMin, block.xMax, block.yMax)
            };
        }

        private static Rect InsetRectFromFace(Rect block, BlockFace face, float inset)
        {
            return face switch
            {
                BlockFace.South => Rect.MinMaxRect(block.xMin, block.yMin + inset, block.xMax, block.yMax),
                BlockFace.North => Rect.MinMaxRect(block.xMin, block.yMin, block.xMax, block.yMax - inset),
                BlockFace.West  => Rect.MinMaxRect(block.xMin + inset, block.yMin, block.xMax, block.yMax),
                _               => Rect.MinMaxRect(block.xMin, block.yMin, block.xMax - inset, block.yMax)
            };
        }

        /// <summary>Courtyard behind the front row, between the two side branches of a U court.</summary>
        private static Rect CourtCourtyardRect(Rect block, BlockFace face)
        {
            return face switch
            {
                BlockFace.South => Rect.MinMaxRect(block.xMin + StoreBandSpan, block.yMin + Walkway + FrontInset + 12f, block.xMax - StoreBandSpan, block.yMax),
                BlockFace.North => Rect.MinMaxRect(block.xMin + StoreBandSpan, block.yMin, block.xMax - StoreBandSpan, block.yMax - Walkway - FrontInset - 12f),
                BlockFace.West  => Rect.MinMaxRect(block.xMin + Walkway + FrontInset + 12f, block.yMin + StoreBandSpan, block.xMax, block.yMax - StoreBandSpan),
                _               => Rect.MinMaxRect(block.xMin, block.yMin + StoreBandSpan, block.xMax - Walkway - FrontInset - 12f, block.yMax - StoreBandSpan)
            };
        }
    }
}
