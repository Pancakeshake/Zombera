using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Ocean mask flood-fill and geometric coastal strip for <see cref="HydrologySolver"/>.</summary>
    public static partial class HydrologySolver
    {
        private static readonly Vector2Int[] Cardinal =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        private readonly struct OceanSeedState
        {
            public readonly LandformField Field;
            public readonly float SeaLevel;
            public readonly Queue<int> Queue;
            public readonly bool[] Visited;
            public readonly bool[] Ocean;
            public readonly float[] DistMeters;

            public OceanSeedState(
                LandformField field,
                float seaLevel,
                Queue<int> queue,
                bool[] visited,
                bool[] ocean,
                float[] distMeters)
            {
                Field = field;
                SeaLevel = seaLevel;
                Queue = queue;
                Visited = visited;
                Ocean = ocean;
                DistMeters = distMeters;
            }
        }

        private readonly struct OceanFloodParams
        {
            public readonly float CellSize;
            public readonly float MaxInland;

            public OceanFloodParams(float cellSize, float maxInland)
            {
                CellSize = cellSize;
                MaxInland = maxInland;
            }
        }

        public static bool[] BuildOceanMask(
            LandformField field,
            float seaLevel,
            WorldMapBoundaryLayout layout,
            LandformProfile landformProfile = null)
        {
            var count = field.Width * field.Height;
            var ocean = new bool[count];
            var visited = new bool[count];
            var distMeters = new float[count];
            for (var i = 0; i < count; i++)
                distMeters[i] = float.PositiveInfinity;

            var queue = new Queue<int>(field.Width * 4);
            var cellSize = Mathf.Max(0.5f, field.CellSize);
            var maxInland = landformProfile != null
                ? landformProfile.EdgeBarrierDepthMeters * 1.4f
                : 1400f;
            var stripDepth = landformProfile != null
                ? landformProfile.EdgeBarrierDepthMeters
                : 650f;

            var seedState = new OceanSeedState(field, seaLevel, queue, visited, ocean, distMeters);
            SeedEdgeBelowSea(seedState, layout);
            FloodOceanInland(seedState, new OceanFloodParams(cellSize, maxInland));
            ApplyGeometricOceanStrip(field, seaLevel, layout, stripDepth, landformProfile, ocean);
            return ocean;
        }

        private static void FloodOceanInland(in OceanSeedState state, in OceanFloodParams flood)
        {
            var field = state.Field;
            while (state.Queue.Count > 0)
            {
                var i = state.Queue.Dequeue();
                var x = i % field.Width;
                var z = i / field.Width;
                ExpandOceanNeighbors(state, flood, x, z, state.DistMeters[i]);
            }
        }

        private static void ExpandOceanNeighbors(
            in OceanSeedState state,
            in OceanFloodParams flood,
            int x,
            int z,
            float curDist)
        {
            var field = state.Field;
            for (var n = 0; n < Cardinal.Length; n++)
            {
                var nx = x + Cardinal[n].x;
                var nz = z + Cardinal[n].y;
                if (nx < 0 || nz < 0 || nx >= field.Width || nz >= field.Height)
                    continue;

                var ni = nz * field.Width + nx;
                if (state.Visited[ni])
                    continue;

                state.Visited[ni] = true;
                if (field.WorldHeights[ni] > state.SeaLevel)
                    continue;

                var nextDist = curDist + flood.CellSize;
                if (nextDist > flood.MaxInland)
                    continue;

                state.Ocean[ni] = true;
                state.DistMeters[ni] = nextDist;
                state.Queue.Enqueue(ni);
            }
        }

        private static void ApplyGeometricOceanStrip(
            LandformField field,
            float seaLevel,
            WorldMapBoundaryLayout layout,
            float stripDepthMeters,
            LandformProfile landformProfile,
            bool[] ocean)
        {
            if (field == null || ocean == null || stripDepthMeters <= 1f)
                return;

            var bounds = new Rect(
                field.OriginXZ.x,
                field.OriginXZ.y,
                field.Width * field.CellSize,
                field.Height * field.CellSize);
            var beachMax = landformProfile != null
                ? landformProfile.OceanBeachMaxElevationMeters
                : 4f;

            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var center = field.CellCenterXZ(x, z);
                    if (!WorldMapBoundaryUtility.TryGetOceanCoastDistance(
                            center.x,
                            center.y,
                            bounds,
                            stripDepthMeters,
                            layout,
                            out _))
                        continue;

                    var i = field.Index(x, z);
                    if (field.WorldHeights[i] <= seaLevel + beachMax + 1f)
                        ocean[i] = true;
                }
            }
        }

        private static void SeedEdgeBelowSea(in OceanSeedState state, WorldMapBoundaryLayout layout)
        {
            var field = state.Field;
            for (var x = 0; x < field.Width; x++)
            {
                TrySeedOceanEdge(state, layout, x, 0);
                TrySeedOceanEdge(state, layout, x, field.Height - 1);
            }

            for (var z = 0; z < field.Height; z++)
            {
                TrySeedOceanEdge(state, layout, 0, z);
                TrySeedOceanEdge(state, layout, field.Width - 1, z);
            }
        }

        private static void TrySeedOceanEdge(
            in OceanSeedState state,
            WorldMapBoundaryLayout layout,
            int x,
            int z)
        {
            if (!WorldMapBoundaryUtility.IsOceanSeedCell(x, z, state.Field.Width, state.Field.Height, layout))
                return;

            TrySeed(state, x, z);
        }

        private static void TrySeed(in OceanSeedState state, int x, int z)
        {
            var i = state.Field.Index(x, z);
            if (state.Visited[i]) return;
            state.Visited[i] = true;
            if (state.Field.WorldHeights[i] > state.SeaLevel) return;
            state.Ocean[i] = true;
            state.DistMeters[i] = 0f;
            state.Queue.Enqueue(i);
        }
    }
}
