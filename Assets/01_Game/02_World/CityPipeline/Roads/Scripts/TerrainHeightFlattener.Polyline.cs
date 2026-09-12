using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>Per-call stats from a polyline heightmap flatten.</summary>
    public readonly struct PolylineFlattenStats
    {
        public readonly int SamplesChanged;
        public readonly float MaxAbsDeltaMeters;

        public PolylineFlattenStats(int samplesChanged, float maxAbsDeltaMeters)
        {
            SamplesChanged = samplesChanged;
            MaxAbsDeltaMeters = maxAbsDeltaMeters;
        }

        public static PolylineFlattenStats Empty => new(0, 0f);

        public PolylineFlattenStats Combine(in PolylineFlattenStats other) =>
            new(
                SamplesChanged + other.SamplesChanged,
                Mathf.Max(MaxAbsDeltaMeters, other.MaxAbsDeltaMeters));
    }

    /// <summary>Optional knobs for corridor flatten writes.</summary>
    public readonly struct PolylineFlattenOptions
    {
        public readonly PolylineFlattenMode Mode;
        public readonly float BedClearanceMeters;
        public readonly bool SyncGpu;
        public readonly System.Func<Vector2, bool> SkipSample;
        public readonly float MaxTerrainDeltaMeters;

        public PolylineFlattenOptions(
            PolylineFlattenMode mode = PolylineFlattenMode.Blend,
            float bedClearanceMeters = 0f,
            bool syncGpu = true,
            System.Func<Vector2, bool> skipSample = null,
            float maxTerrainDeltaMeters = 0f)
        {
            Mode = mode;
            BedClearanceMeters = bedClearanceMeters;
            SyncGpu = syncGpu;
            SkipSample = skipSample;
            MaxTerrainDeltaMeters = maxTerrainDeltaMeters;
        }

        public static PolylineFlattenOptions Default => new();
    }

    /// <summary>Polyline corridor flatten path for TerrainHeightFlattener.</summary>
    public static partial class TerrainHeightFlattener
    {
        private struct HeightmapWindow
        {
            public float[,] Heights;
            public int Width;
            public int Height;
            public int StartX;
            public int StartZ;
            public int HeightRes;
            public Vector3 Origin;
            public Vector3 Size;
        }

        private readonly struct SampleEvalResult
        {
            public readonly float Next;
            public readonly float DeltaMeters;

            public SampleEvalResult(float next, float deltaMeters)
            {
                Next = next;
                DeltaMeters = deltaMeters;
            }
        }

        private readonly struct PolylineCoreArgs
        {
            public readonly Terrain Terrain;
            public readonly PolylineHeightProfile Profile;
            public readonly Rect SampleBounds;
            public readonly float HalfWidth;
            public readonly float BlendMeters;
            public readonly PolylineFlattenOptions Options;

            public PolylineCoreArgs(
                Terrain terrain,
                PolylineHeightProfile profile,
                Rect sampleBounds,
                float halfWidth,
                float blendMeters,
                in PolylineFlattenOptions options)
            {
                Terrain = terrain;
                Profile = profile;
                SampleBounds = sampleBounds;
                HalfWidth = halfWidth;
                BlendMeters = blendMeters;
                Options = options;
            }
        }

        private readonly struct SpikePassArgs
        {
            public readonly HeightmapWindow Window;
            public readonly PolylineHeightProfile Profile;
            public readonly float HalfWidth;
            public readonly float BlendMeters;
            public readonly float BedClearanceMeters;
            public readonly System.Func<Vector2, bool> SkipSample;

            public SpikePassArgs(
                in HeightmapWindow window,
                PolylineHeightProfile profile,
                float halfWidth,
                float blendMeters,
                float bedClearanceMeters,
                System.Func<Vector2, bool> skipSample)
            {
                Window = window;
                Profile = profile;
                HalfWidth = halfWidth;
                BlendMeters = blendMeters;
                BedClearanceMeters = bedClearanceMeters;
                SkipSample = skipSample;
            }
        }

        /// <summary>
        ///     Flattens a polyline corridor toward heights sampled along the path (highways).
        /// </summary>
        public static PolylineFlattenStats FlattenPolyline(
            Terrain terrain,
            IReadOnlyList<Vector2> pointsXZ,
            float halfWidthMeters,
            float blendMeters,
            System.Func<Vector2, float> sampleTargetWorldY)
        {
            if (terrain == null || terrain.terrainData == null || pointsXZ == null || pointsXZ.Count < 2 ||
                sampleTargetWorldY == null)
                return PolylineFlattenStats.Empty;

            var profile = PolylineHeightProfile.Build(pointsXZ, sampleTargetWorldY);
            return FlattenPolyline(terrain, profile, halfWidthMeters, blendMeters);
        }

        /// <summary>
        ///     Flattens a polyline corridor using a pre-sampled height profile along the path.
        /// </summary>
        public static PolylineFlattenStats FlattenPolyline(
            Terrain terrain,
            PolylineHeightProfile profile,
            float halfWidthMeters,
            float blendMeters) =>
            FlattenPolyline(terrain, profile, halfWidthMeters, blendMeters, PolylineFlattenOptions.Default);

        /// <summary>
        ///     Flattens a polyline corridor using a pre-sampled height profile along the path.
        /// </summary>
        public static PolylineFlattenStats FlattenPolyline(
            Terrain terrain,
            PolylineHeightProfile profile,
            float halfWidthMeters,
            float blendMeters,
            in PolylineFlattenOptions options)
        {
            if (terrain == null || terrain.terrainData == null || profile == null || !profile.IsValid)
                return PolylineFlattenStats.Empty;

            var bounds = profile.BoundsXZ;
            var radius = Mathf.Max(0.5f, halfWidthMeters);
            var blend = Mathf.Max(0f, blendMeters);
            var expanded = Rect.MinMaxRect(
                bounds.xMin - radius - blend,
                bounds.yMin - radius - blend,
                bounds.xMax + radius + blend,
                bounds.yMax + radius + blend);

            return FlattenPolylineCore(new PolylineCoreArgs(
                terrain, profile, expanded, radius, blend, options));
        }

        public static void SyncTerrainGpu(Terrain terrain)
        {
            if (terrain?.terrainData == null)
                return;

#if UNITY_2019_1_OR_NEWER
            terrain.terrainData.SyncHeightmap();
#endif
            terrain.Flush();
        }

        private static PolylineFlattenStats FlattenPolylineCore(in PolylineCoreArgs args)
        {
            var terrain = args.Terrain;
            var terrainData = terrain.terrainData;
            var origin = terrain.transform.position;
            var size = terrainData.size;
            if (size.x <= 0.01f || size.z <= 0.01f || size.y <= 0.01f)
                return PolylineFlattenStats.Empty;

            if (!TryResolveHeightmapWindow(args.SampleBounds, origin, size, terrainData.heightmapResolution,
                    out var region))
                return PolylineFlattenStats.Empty;

            var heights = terrainData.GetHeights(region.StartX, region.StartZ, region.Width, region.Height);
            var window = new HeightmapWindow
            {
                Heights = heights,
                Width = region.Width,
                Height = region.Height,
                StartX = region.StartX,
                StartZ = region.StartZ,
                HeightRes = terrainData.heightmapResolution,
                Origin = origin,
                Size = size
            };
            var sampleStats = ApplyPolylineSamples(window, args);

            var samplesChanged = sampleStats.SamplesChanged;
            var maxAbsDelta = sampleStats.MaxAbsDeltaMeters;

            if (args.Options.Mode == PolylineFlattenMode.HighwayCorridor)
            {
                var spike = ApplyHighwaySpikeCutPass(new SpikePassArgs(
                    window,
                    args.Profile,
                    args.HalfWidth,
                    args.BlendMeters,
                    args.Options.BedClearanceMeters,
                    args.Options.SkipSample));
                samplesChanged += spike.SamplesChanged;
                maxAbsDelta = Mathf.Max(maxAbsDelta, spike.MaxAbsDeltaMeters);
            }

            if (samplesChanged <= 0)
                return PolylineFlattenStats.Empty;

            PrepareHeightmapWrite(terrain, heights, region.StartX, region.StartZ, region.Width, region.Height);
            terrainData.SetHeightsDelayLOD(region.StartX, region.StartZ, heights);
            if (args.Options.SyncGpu)
                SyncTerrainGpu(terrain);

            return new PolylineFlattenStats(samplesChanged, maxAbsDelta);
        }

        private readonly struct HeightmapRegion
        {
            public readonly int StartX;
            public readonly int StartZ;
            public readonly int Width;
            public readonly int Height;

            public HeightmapRegion(int startX, int startZ, int width, int height)
            {
                StartX = startX;
                StartZ = startZ;
                Width = width;
                Height = height;
            }
        }

        private static bool TryResolveHeightmapWindow(
            Rect sampleBounds,
            Vector3 origin,
            Vector3 size,
            int heightRes,
            out HeightmapRegion region)
        {
            region = default;
            var regionMinX = Mathf.Max(sampleBounds.xMin, origin.x);
            var regionMaxX = Mathf.Min(sampleBounds.xMax, origin.x + size.x);
            var regionMinZ = Mathf.Max(sampleBounds.yMin, origin.z);
            var regionMaxZ = Mathf.Min(sampleBounds.yMax, origin.z + size.z);
            if (regionMaxX <= regionMinX || regionMaxZ <= regionMinZ || heightRes <= 1)
                return false;

            if (!TryBuildSampleRegion(regionMinX, regionMaxX, origin.x, size.x, heightRes, out var startX, out var width) ||
                !TryBuildSampleRegion(regionMinZ, regionMaxZ, origin.z, size.z, heightRes, out var startZ, out var height))
                return false;

            region = new HeightmapRegion(startX, startZ, width, height);
            return true;
        }

        private static PolylineFlattenStats ApplyPolylineSamples(
            in HeightmapWindow window,
            in PolylineCoreArgs args)
        {
            var samplesChanged = 0;
            var maxAbsDelta = 0f;
            var clearance = Mathf.Max(0f, args.Options.BedClearanceMeters);
            var invHeight = 1f / Mathf.Max(0.01f, window.Size.y);

            for (var z = 0; z < window.Height; z++)
            {
                for (var x = 0; x < window.Width; x++)
                {
                    if (!TryEvaluatePolylineSample(window, z, x, args, clearance, invHeight, out var result))
                        continue;

                    if (result.DeltaMeters > maxAbsDelta)
                        maxAbsDelta = result.DeltaMeters;
                    window.Heights[z, x] = result.Next;
                    samplesChanged++;
                }
            }

            return new PolylineFlattenStats(samplesChanged, maxAbsDelta);
        }

        private static bool TryEvaluatePolylineSample(
            in HeightmapWindow window,
            int z,
            int x,
            in PolylineCoreArgs args,
            float clearance,
            float invHeight,
            out SampleEvalResult result)
        {
            result = default;
            var worldPoint = HeightmapWorldPoint(
                window.Origin, window.Size, window.HeightRes, window.StartX + x, window.StartZ + z);
            if (args.Options.SkipSample != null && args.Options.SkipSample(worldPoint))
                return false;

            var dist = args.Profile.QueryNearest(worldPoint, out _, out var pathHeight);
            var weight = ComputePolylineFlattenWeight(dist, args.HalfWidth, args.BlendMeters);
            if (weight <= 0f)
                return false;

            var current = window.Heights[z, x];
            var currentWorldY = current * window.Size.y + window.Origin.y;
            var targetWorldY = pathHeight - clearance;
            var maxDelta = args.Options.MaxTerrainDeltaMeters;
            if (maxDelta > 0f)
            {
                targetWorldY = Mathf.Clamp(
                    targetWorldY,
                    currentWorldY - maxDelta,
                    currentWorldY + maxDelta);
            }

            var targetNormalized = Mathf.Clamp01((targetWorldY - window.Origin.y) * invHeight);
            var next = ResolveCorridorLerp(
                args.Options.Mode == PolylineFlattenMode.HighwayCorridor,
                current,
                targetNormalized,
                weight,
                currentWorldY > targetWorldY,
                dist <= args.HalfWidth);
            if (Mathf.Abs(next - current) <= 0.00001f)
                return false;

            result = new SampleEvalResult(next, Mathf.Abs(next - current) * window.Size.y);
            return true;
        }

        private static float ResolveCorridorLerp(
            bool highwayCorridor,
            float current,
            float targetNormalized,
            float weight,
            bool cuttingDown,
            bool insideCarriageway)
        {
            if (!highwayCorridor)
                return Mathf.Lerp(current, targetNormalized, weight);

            if (cuttingDown)
                return Mathf.Lerp(current, targetNormalized, weight);
            if (insideCarriageway)
                return Mathf.Lerp(current, targetNormalized, Mathf.Max(weight, 0.85f));
            return Mathf.Lerp(current, targetNormalized, weight);
        }

        private static PolylineFlattenStats ApplyHighwaySpikeCutPass(in SpikePassArgs args)
        {
            var invHeight = 1f / Mathf.Max(0.01f, args.Window.Size.y);
            var clearance = Mathf.Max(0f, args.BedClearanceMeters);
            var samplesChanged = 0;
            var maxAbsDelta = 0f;

            for (var pass = 0; pass < 2; pass++)
            {
                for (var z = 0; z < args.Window.Height; z++)
                {
                    for (var x = 0; x < args.Window.Width; x++)
                    {
                        if (!TryCutSpikeSample(args, z, x, clearance, invHeight, out var deltaMeters))
                            continue;

                        if (deltaMeters > maxAbsDelta)
                            maxAbsDelta = deltaMeters;
                        samplesChanged++;
                    }
                }
            }

            return new PolylineFlattenStats(samplesChanged, maxAbsDelta);
        }

        private static bool TryCutSpikeSample(
            in SpikePassArgs args,
            int z,
            int x,
            float clearance,
            float invHeight,
            out float deltaMeters)
        {
            deltaMeters = 0f;
            var window = args.Window;
            var worldPoint = HeightmapWorldPoint(
                window.Origin, window.Size, window.HeightRes, window.StartX + x, window.StartZ + z);
            if (args.SkipSample != null && args.SkipSample(worldPoint))
                return false;

            var dist = args.Profile.QueryNearest(worldPoint, out _, out var pathHeight);
            if (ComputePolylineFlattenWeight(dist, args.HalfWidth, args.BlendMeters) <= 0f)
                return false;

            var targetWorldY = pathHeight - clearance;
            var current = window.Heights[z, x];
            var currentWorldY = current * window.Size.y + window.Origin.y;
            if (currentWorldY <= targetWorldY + 0.05f)
                return false;

            if (!HasLowerNeighbor(window, x, z, targetWorldY))
                return false;

            var targetNormalized = Mathf.Clamp01((targetWorldY - window.Origin.y) * invHeight);
            deltaMeters = Mathf.Abs(targetNormalized - current) * window.Size.y;
            window.Heights[z, x] = targetNormalized;
            return true;
        }

        private static bool HasLowerNeighbor(
            in HeightmapWindow window,
            int x,
            int z,
            float targetWorldY)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                        continue;

                    var nx = x + dx;
                    var nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= window.Width || nz >= window.Height)
                        continue;

                    var neighborWorldY = window.Heights[nz, nx] * window.Size.y + window.Origin.y;
                    if (neighborWorldY <= targetWorldY + 0.1f)
                        return true;
                }
            }

            return false;
        }

        private static Vector2 HeightmapWorldPoint(
            Vector3 origin,
            Vector3 size,
            int heightRes,
            int mapX,
            int mapZ) =>
            new(
                origin.x + (mapX / (float)(heightRes - 1)) * size.x,
                origin.z + (mapZ / (float)(heightRes - 1)) * size.z);

        private static float ComputePolylineFlattenWeight(float distance, float halfWidth, float blendMeters)
        {
            if (distance <= halfWidth)
                return 1f;
            if (blendMeters <= 0.01f)
                return 0f;

            var linear = 1f - Mathf.Clamp01((distance - halfWidth) / blendMeters);
            return SmoothBlendCurve(linear);
        }

        private static float SmoothBlendCurve(float linearWeight)
        {
            var t = Mathf.Clamp01(linearWeight);
            return t * t * (3f - 2f * t);
        }
    }
}
