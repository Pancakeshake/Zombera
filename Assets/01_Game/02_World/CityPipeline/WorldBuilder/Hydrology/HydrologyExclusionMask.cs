using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Blocked-cell mask for river/lake placement (oceans, mountains, coast strips).</summary>
    public static class HydrologyExclusionMask
    {
        private const float MountainElevNormThreshold = 0.55f;
        private const float MountainSlopeDegreesThreshold = 22f;
        private const float MountainAbsoluteElevationMeters = 420f;
        private const float MountainElevNormDivisor = 720f;

        /// <summary>Packed args for <see cref="IsCellBlocked"/> (Sonar S107).</summary>
        public readonly struct CellBlockedParams
        {
            public LandformField Field { get; init; }
            public int X { get; init; }
            public int Z { get; init; }
            public float SeaLevel { get; init; }
            public WorldMapBoundaryLayout Layout { get; init; }
            public Rect Bounds { get; init; }
            public float EdgeDepth { get; init; }
            public bool[] OceanMask { get; init; }
            public int CellIndex { get; init; }
        }

        public static bool[] Build(
            LandformField field,
            float seaLevel,
            WorldMapSession session,
            LandformProfile landformProfile,
            bool[] oceanMask)
        {
            if (field == null)
                return System.Array.Empty<bool>();

            var count = field.Width * field.Height;
            var blocked = new bool[count];
            var layout = WorldMapBoundaryLayout.Resolve(session, landformProfile);
            var bounds = session.WorldBoundsXZ;
            var edgeDepth = landformProfile != null
                ? landformProfile.EdgeBarrierDepthMeters
                : 650f;

            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var i = field.Index(x, z);
                    if (IsCellBlocked(new CellBlockedParams
                        {
                            Field = field,
                            X = x,
                            Z = z,
                            SeaLevel = seaLevel,
                            Layout = layout,
                            Bounds = bounds,
                            EdgeDepth = edgeDepth,
                            OceanMask = oceanMask,
                            CellIndex = i
                        }))
                        blocked[i] = true;
                }
            }

            return blocked;
        }

        public static bool[] BuildRiverPlacementMask(
            LandformField field,
            float seaLevel,
            WorldMapSession session,
            LandformProfile landformProfile,
            HydrologyProfile hydrologyProfile,
            bool[] oceanMask)
        {
            var blocked = Build(field, seaLevel, session, landformProfile, oceanMask);
            if (field == null || hydrologyProfile == null)
                return blocked;

            var layout = WorldMapBoundaryLayout.Resolve(session, landformProfile);
            var bounds = session.WorldBoundsXZ;
            var edgeDepth = landformProfile != null ? landformProfile.EdgeBarrierDepthMeters : 650f;
            var coastBlocked = new bool[blocked.Length];
            var obstacleBlocked = new bool[blocked.Length];
            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var index = field.Index(x, z);
                    var isOcean = IsOceanCell(field, x, z, oceanMask, index, bounds, edgeDepth, layout);
                    coastBlocked[index] = isOcean;
                    obstacleBlocked[index] = blocked[index] && !isOcean;
                }
            }

            var coast = Dilate(field, coastBlocked, hydrologyProfile.RiverPlacementCoastBufferMeters);
            var obstacles = Dilate(field, obstacleBlocked, hydrologyProfile.RiverPlacementObstacleBufferMeters);
            for (var i = 0; i < blocked.Length; i++)
                blocked[i] = coast[i] || obstacles[i];
            return blocked;
        }

        public static bool[] Dilate(LandformField field, bool[] source, float radiusMeters)
        {
            if (field == null || source == null || source.Length == 0)
                return source ?? System.Array.Empty<bool>();
            if (radiusMeters <= 0f)
                return (bool[])source.Clone();

            var passes = Mathf.CeilToInt(radiusMeters / Mathf.Max(0.01f, field.CellSize));
            var current = (bool[])source.Clone();
            var next = new bool[current.Length];

            for (var pass = 0; pass < passes; pass++)
            {
                System.Array.Copy(current, next, current.Length);
                for (var z = 0; z < field.Height; z++)
                {
                    for (var x = 0; x < field.Width; x++)
                    {
                        var i = field.Index(x, z);
                        if (!current[i])
                            continue;

                        StampNeighborBlock(field, next, x + 1, z);
                        StampNeighborBlock(field, next, x - 1, z);
                        StampNeighborBlock(field, next, x, z + 1);
                        StampNeighborBlock(field, next, x, z - 1);
                        StampNeighborBlock(field, next, x + 1, z + 1);
                        StampNeighborBlock(field, next, x - 1, z + 1);
                        StampNeighborBlock(field, next, x + 1, z - 1);
                        StampNeighborBlock(field, next, x - 1, z - 1);
                    }
                }

                var swap = current;
                current = next;
                next = swap;
            }

            return current;
        }

        private static bool IsOceanCell(
            LandformField field,
            int x,
            int z,
            bool[] oceanMask,
            int index,
            Rect bounds,
            float edgeDepth,
            WorldMapBoundaryLayout layout)
        {
            if (oceanMask != null && index < oceanMask.Length && oceanMask[index])
                return true;

            var center = field.CellCenterXZ(x, z);
            return WorldMapBoundaryUtility.TryGetEdgeBarrier(
                center.x,
                center.y,
                bounds,
                edgeDepth,
                layout,
                out var kind) && kind == WorldMapBoundaryKind.Ocean;
        }

        private static void StampNeighborBlock(LandformField field, bool[] blocked, int x, int z)
        {
            if (x < 0 || z < 0 || x >= field.Width || z >= field.Height)
                return;
            blocked[field.Index(x, z)] = true;
        }

        public static bool IsCellBlocked(in CellBlockedParams args)
        {
            if (args.OceanMask != null && args.CellIndex < args.OceanMask.Length && args.OceanMask[args.CellIndex])
                return true;

            var center = args.Field.CellCenterXZ(args.X, args.Z);
            if (WorldMapBoundaryUtility.TryGetEdgeBarrier(
                    center.x,
                    center.y,
                    args.Bounds,
                    args.EdgeDepth,
                    args.Layout,
                    out var boundaryKind))
            {
                if (boundaryKind == WorldMapBoundaryKind.Mountains)
                    return true;
                if (boundaryKind == WorldMapBoundaryKind.Ocean)
                    return true;
            }

            return IsInteriorMountain(args.Field, args.X, args.Z, args.SeaLevel);
        }

        private static bool IsInteriorMountain(LandformField field, int x, int z, float seaLevel)
        {
            if (!field.TryGetHeight(x, z, out var elev))
                return false;

            var slope = LandformFieldSampling.EstimateSlopeDegrees(field, x, z);
            var elevNorm = Mathf.Clamp01((elev - (seaLevel - 20f)) / MountainElevNormDivisor);

            // High + steep only — absolute height alone must not wipe all riverable lowlands
            // after taller orogen / continental amplitudes.
            if (elev > seaLevel + MountainAbsoluteElevationMeters && slope > MountainSlopeDegreesThreshold)
                return true;

            return elevNorm > MountainElevNormThreshold && slope > MountainSlopeDegreesThreshold;
        }
    }
}
