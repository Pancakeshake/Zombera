using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Lake/river/ocean rasterization helpers for <see cref="HydrologySolver"/>.</summary>
    public static partial class HydrologySolver
    {
        private static float[] _lakeBestDistScratch;
        private static int _lakeBestDistScratchSize;

        private readonly struct LakeMaskBuffers
        {
            public readonly bool[] Mask;
            public readonly float[] BestDistSq;
            public readonly int[] NearestLakeIndex;

            public LakeMaskBuffers(bool[] mask, float[] bestDistSq, int[] nearestLakeIndex)
            {
                Mask = mask;
                BestDistSq = bestDistSq;
                NearestLakeIndex = nearestLakeIndex;
            }
        }

        private readonly struct RiverStampParams
        {
            public readonly Vector2 CenterXZ;
            public readonly float RadiusSq;
            public readonly float Depth;
            public readonly bool[] ExclusionMask;

            public RiverStampParams(Vector2 centerXZ, float radiusSq, float depth, bool[] exclusionMask)
            {
                CenterXZ = centerXZ;
                RadiusSq = radiusSq;
                Depth = depth;
                ExclusionMask = exclusionMask;
            }
        }

        private static float[] RentLakeBestDistScratch(int count)
        {
            if (_lakeBestDistScratch == null || _lakeBestDistScratchSize != count)
            {
                _lakeBestDistScratch = new float[count];
                _lakeBestDistScratchSize = count;
            }

            for (var i = 0; i < count; i++)
                _lakeBestDistScratch[i] = float.PositiveInfinity;

            return _lakeBestDistScratch;
        }

        private static bool[] BuildLakeMask(
            LandformField field,
            LakeRecord[] lakes,
            HydrologyProfile profile,
            out int[] nearestLakeIndex)
        {
            var count = field.Width * field.Height;
            var mask = new bool[count];
            nearestLakeIndex = new int[count];
            for (var i = 0; i < count; i++)
                nearestLakeIndex[i] = -1;

            if (lakes == null || lakes.Length == 0)
                return mask;

            var bestDistSq = RentLakeBestDistScratch(count);
            var shore = Mathf.Max(0f, profile.ShoreBlendWidthMeters);
            var buffers = new LakeMaskBuffers(mask, bestDistSq, nearestLakeIndex);
            for (var l = 0; l < lakes.Length; l++)
            {
                var lake = lakes[l];
                if (lake == null) continue;
                StampLakeOntoMask(field, lake, l, shore, buffers);
            }

            return mask;
        }

        private static void StampLakeOntoMask(
            LandformField field,
            LakeRecord lake,
            int lakeIndex,
            float shore,
            in LakeMaskBuffers buffers)
        {
            _ = shore;
            var cells = lake.BasinCellCentersXZ;
            if (cells == null) return;
            for (var c = 0; c < cells.Length; c++)
            {
                var x = Mathf.FloorToInt((cells[c].x - field.OriginXZ.x) / field.CellSize);
                var z = Mathf.FloorToInt((cells[c].y - field.OriginXZ.y) / field.CellSize);
                if (x >= 0 && z >= 0 && x < field.Width && z < field.Height)
                    TryMarkLakeCell(field, lake, lakeIndex, 0f, buffers, x, z);
            }
        }

        private static void TryMarkLakeCell(
            LandformField field,
            LakeRecord lake,
            int lakeIndex,
            float radiusSq,
            in LakeMaskBuffers buffers,
            int x,
            int z)
        {
            var idx = field.Index(x, z);
            buffers.Mask[idx] = true;
            if (buffers.NearestLakeIndex[idx] >= 0) return;
            buffers.BestDistSq[idx] = 0f;
            buffers.NearestLakeIndex[idx] = lakeIndex;
        }

        private static void RasterizeOcean(
            LandformField field,
            HydrologyPlan plan,
            bool[] oceanMask,
            float seaLevel)
        {
            for (var i = 0; i < oceanMask.Length; i++)
            {
                if (!oceanMask[i]) continue;
                plan.WaterClass[i] = WorldWaterClass.Ocean;
                plan.SurfaceWorldY[i] = seaLevel;
                plan.DepthMeters[i] = Mathf.Max(0f, seaLevel - field.WorldHeights[i]);
            }
        }

        private static void RasterizeLakes(
            HydrologyPlan plan,
            LakeRecord[] lakes,
            bool[] lakeMask,
            int[] nearestLakeIndex)
        {
            for (var i = 0; i < lakeMask.Length; i++)
            {
                if (!lakeMask[i]) continue;
                if (plan.WaterClass[i] == WorldWaterClass.Ocean) continue;
                plan.WaterClass[i] = WorldWaterClass.Lake;

                var lakeIndex = nearestLakeIndex != null && i < nearestLakeIndex.Length
                    ? nearestLakeIndex[i]
                    : -1;
                if (lakeIndex < 0 || lakes == null || lakeIndex >= lakes.Length)
                    continue;

                var best = lakes[lakeIndex];
                if (best == null) continue;
                plan.SurfaceWorldY[i] = best.SurfaceWorldY;
                plan.DepthMeters[i] = Mathf.Max(plan.DepthMeters[i], best.MaxDepthMeters * 0.5f);
            }
        }

        private static void RasterizeRivers(
            LandformField field,
            HydrologyPlan plan,
            RiverPolyline[] rivers,
            HydrologyProfile profile,
            bool[] exclusionMask)
        {
            if (rivers == null) return;
            var shoulder = Mathf.Max(0f, profile.CarveShoulderWidthMeters);

            for (var r = 0; r < rivers.Length; r++)
            {
                var river = rivers[r];
                if (river?.PointsXZ == null) continue;
                for (var p = 0; p < river.PointsXZ.Length; p++)
                {
                    var width = p < river.WidthMeters.Length ? river.WidthMeters[p] : profile.MinRiverWidthMeters;
                    var depth = p < river.DepthMeters.Length ? river.DepthMeters[p] : profile.MinRiverDepthMeters;
                    StampRiverDisc(field, plan, river.PointsXZ[p], width * 0.5f + shoulder, depth, exclusionMask);
                }
            }
        }

        private static void StampRiverDisc(
            LandformField field,
            HydrologyPlan plan,
            Vector2 centerXZ,
            float radius,
            float depth,
            bool[] exclusionMask)
        {
            var stamp = new RiverStampParams(centerXZ, radius * radius, depth, exclusionMask);
            var minX = Mathf.FloorToInt((centerXZ.x - radius - field.OriginXZ.x) / field.CellSize);
            var maxX = Mathf.CeilToInt((centerXZ.x + radius - field.OriginXZ.x) / field.CellSize);
            var minZ = Mathf.FloorToInt((centerXZ.y - radius - field.OriginXZ.y) / field.CellSize);
            var maxZ = Mathf.CeilToInt((centerXZ.y + radius - field.OriginXZ.y) / field.CellSize);

            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                    TryStampRiverCell(field, plan, stamp, x, z);
            }
        }

        private static void TryStampRiverCell(
            LandformField field,
            HydrologyPlan plan,
            in RiverStampParams stamp,
            int x,
            int z)
        {
            if (x < 0 || z < 0 || x >= field.Width || z >= field.Height)
                return;

            var center = field.CellCenterXZ(x, z);
            var dx = center.x - stamp.CenterXZ.x;
            var dz = center.y - stamp.CenterXZ.y;
            if (dx * dx + dz * dz > stamp.RadiusSq)
                return;

            var i = field.Index(x, z);
            if (stamp.ExclusionMask != null && i < stamp.ExclusionMask.Length && stamp.ExclusionMask[i])
                return;
            if (plan.WaterClass[i] == WorldWaterClass.Ocean || plan.WaterClass[i] == WorldWaterClass.Lake)
                return;

            plan.WaterClass[i] = WorldWaterClass.River;
            plan.DepthMeters[i] = Mathf.Max(plan.DepthMeters[i], stamp.Depth);
            plan.SurfaceWorldY[i] = field.WorldHeights[i];
        }
    }
}
