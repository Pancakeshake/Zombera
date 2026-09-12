using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Places Crest <c>WaterBody</c> volumes. AllOcean uses one full-map square matching
    ///     session/terrain bounds. Partial-ocean layouts keep edge strips with shared outer corners.
    /// </summary>
    public static class OceanWaterBodyPlacementUtility
    {
        private const float EdgePaddingMeters = 64f;
        private const float MinStripDepthMeters = 48f;
        private const float MaxInlandDepthMeters = 1400f;
        public const float DefaultOuterExtensionMeters = 3000f;

        public readonly struct EdgePlacement
        {
            public WorldMapEdgeSide Side { get; }
            public Vector3 Center { get; }
            public Vector3 Scale { get; }

            public EdgePlacement(WorldMapEdgeSide side, Vector3 center, Vector3 scale)
            {
                Side = side;
                Center = center;
                Scale = scale;
            }
        }

        /// <summary>
        ///     Single WaterBody covering <paramref name="bounds"/> exactly (perfect square when bounds are square).
        /// </summary>
        public static EdgePlacement CreateFullSquarePlacement(Rect bounds, float seaLevel)
        {
            var width = Mathf.Max(1f, bounds.width);
            var depth = Mathf.Max(1f, bounds.height);
            return new EdgePlacement(
                WorldMapEdgeSide.West,
                new Vector3(bounds.center.x, seaLevel, bounds.center.y),
                new Vector3(width, 1f, depth));
        }

        public static bool IsAllOcean(WorldMapBoundaryLayout layout) =>
            layout.West == WorldMapBoundaryKind.Ocean &&
            layout.East == WorldMapBoundaryKind.Ocean &&
            layout.South == WorldMapBoundaryKind.Ocean &&
            layout.North == WorldMapBoundaryKind.Ocean;

        /// <summary>
        ///     When the session already includes an ocean ring, Crest must not extend further.
        /// </summary>
        public static float ResolveOuterExtensionMeters(int oceanRingTiles, float configuredOuterExtensionMeters)
        {
            if (oceanRingTiles > 0)
                return 0f;
            return Mathf.Max(0f, configuredOuterExtensionMeters);
        }

        public static void CollectPlacements(
            Rect surfaceBounds,
            float seaLevel,
            WorldMapBoundaryLayout layout,
            float stripDepthMeters,
            List<EdgePlacement> results,
            float outerExtensionMeters = DefaultOuterExtensionMeters)
        {
            if (results == null)
                return;

            if (IsAllOcean(layout))
            {
                results.Add(CreateFullSquarePlacement(surfaceBounds, seaLevel));
                return;
            }

            CollectEdgePlacements(
                surfaceBounds,
                seaLevel,
                layout,
                stripDepthMeters,
                results,
                outerExtensionMeters);
        }

        public static void CollectEdgePlacements(
            Rect bounds,
            float seaLevel,
            WorldMapBoundaryLayout layout,
            float stripDepthMeters,
            List<EdgePlacement> results,
            float outerExtensionMeters = DefaultOuterExtensionMeters)
        {
            if (results == null)
                return;

            var inlandDepth = Mathf.Min(
                MaxInlandDepthMeters,
                Mathf.Max(MinStripDepthMeters, stripDepthMeters) + EdgePaddingMeters);
            var outwardDepth = Mathf.Max(0f, outerExtensionMeters);

            // Shared outer rect so N/S/E/W outer edges form one perfect square (no N/S padding skew).
            var outer = new Rect(
                bounds.xMin - outwardDepth,
                bounds.yMin - outwardDepth,
                bounds.width + outwardDepth * 2f,
                bounds.height + outwardDepth * 2f);
            var spanX = Mathf.Max(1f, outer.width);
            var spanZ = Mathf.Max(1f, outer.height);
            var centerX = outer.center.x;
            var centerZ = outer.center.y;

            if (layout.West == WorldMapBoundaryKind.Ocean)
            {
                var width = inlandDepth + outwardDepth;
                if (width < 1f)
                    width = inlandDepth;
                results.Add(new EdgePlacement(
                    WorldMapEdgeSide.West,
                    new Vector3(outer.xMin + width * 0.5f, seaLevel, centerZ),
                    new Vector3(width, 1f, spanZ)));
            }

            if (layout.East == WorldMapBoundaryKind.Ocean)
            {
                var width = inlandDepth + outwardDepth;
                if (width < 1f)
                    width = inlandDepth;
                results.Add(new EdgePlacement(
                    WorldMapEdgeSide.East,
                    new Vector3(outer.xMax - width * 0.5f, seaLevel, centerZ),
                    new Vector3(width, 1f, spanZ)));
            }

            if (layout.South == WorldMapBoundaryKind.Ocean)
            {
                var depth = inlandDepth + outwardDepth;
                if (depth < 1f)
                    depth = inlandDepth;
                results.Add(new EdgePlacement(
                    WorldMapEdgeSide.South,
                    new Vector3(centerX, seaLevel, outer.yMin + depth * 0.5f),
                    new Vector3(spanX, 1f, depth)));
            }

            if (layout.North == WorldMapBoundaryKind.Ocean)
            {
                var depth = inlandDepth + outwardDepth;
                if (depth < 1f)
                    depth = inlandDepth;
                results.Add(new EdgePlacement(
                    WorldMapEdgeSide.North,
                    new Vector3(centerX, seaLevel, outer.yMax - depth * 0.5f),
                    new Vector3(spanX, 1f, depth)));
            }
        }
    }
}
