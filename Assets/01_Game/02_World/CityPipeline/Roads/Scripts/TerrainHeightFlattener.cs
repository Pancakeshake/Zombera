using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public enum PolylineFlattenMode
    {
        Blend = 0,
        HighwayCorridor = 1
    }

    /// <summary>
    ///     Flattens terrain heightmaps under axis-aligned world XZ rectangles with optional edge blend.
    /// </summary>
    public static partial class TerrainHeightFlattener
    {
        private struct FlattenArgs
        {
            public float InnerMinX, InnerMaxX, InnerMinZ, InnerMaxZ;
            public float OuterMinX, OuterMaxX, OuterMinZ, OuterMaxZ;
            public float SampleMinX, SampleMaxX, SampleMinZ, SampleMaxZ;
            public float BlendDistance;
            public float TargetWorldY;
        }

        private static System.Func<Rect, bool> _protectedWaterBoundsTest;
        private static System.Func<Vector2, float, float> _protectedGroundHeightClamp;
        private static Rect _modifiedWorldBoundsXZ;
        private static bool _hasModifiedWorldBounds;

        /// <summary>
        ///     Installs the single protected inland-water rule shared by every road-stack heightmap
        ///     write. Both delegates are method groups on <c>CityPrefabRoadNetworkBuilder</c>, so the
        ///     clearance rule lives in exactly one place (<c>ResolveProtectedGroundHeight</c>).
        ///     Pass nulls to disable protection.
        /// </summary>
        public static void SetProtectedWaterWriteGuard(
            System.Func<Rect, bool> boundsOverlapTest,
            System.Func<Vector2, float, float> groundHeightClamp)
        {
            _protectedWaterBoundsTest = boundsOverlapTest;
            _protectedGroundHeightClamp = groundHeightClamp;
        }

        /// <summary>
        ///     Final guard applied to a heightmap window immediately before
        ///     <c>SetHeightsDelayLOD</c>: clamps every cell inside protected inland water down to the
        ///     bed height and records the written world bounds for depth-cache refresh.
        /// </summary>
        /// <returns>Number of cells lowered by the water-bed clamp.</returns>
        public static int PrepareHeightmapWrite(
            Terrain terrain,
            float[,] heights,
            int startX,
            int startZ,
            int width,
            int height)
        {
            if (terrain?.terrainData == null || heights == null || width <= 0 || height <= 0)
                return 0;

            var terrainData = terrain.terrainData;
            var origin = terrain.transform.position;
            var size = terrainData.size;
            var resolution = terrainData.heightmapResolution;
            if (resolution <= 1 || size.y <= 0.01f)
                return 0;

            var worldBoundsXZ = ResolveHeightmapWindowWorldRect(
                origin, size, resolution, startX, startZ, width, height);
            MarkModifiedWorldBoundsXZ(worldBoundsXZ);
            return ClampProtectedWaterBedCells(
                heights, origin, size, resolution, startX, startZ, width, height, worldBoundsXZ);
        }

        /// <summary>
        ///     Union of world XZ bounds written by infrastructure heightmap writes since the previous
        ///     consume, then reset. Crest's sea-floor binder unions this into the ocean depth cache.
        /// </summary>
        public static bool ConsumeModifiedWorldBoundsXZ(out Rect boundsXZ)
        {
            boundsXZ = _modifiedWorldBoundsXZ;
            var hasBounds = _hasModifiedWorldBounds && boundsXZ.width > 0f && boundsXZ.height > 0f;
            ResetModifiedWorldBoundsXZ();
            return hasBounds;
        }

        private static void ResetModifiedWorldBoundsXZ()
        {
            _modifiedWorldBoundsXZ = default;
            _hasModifiedWorldBounds = false;
        }

        private static void MarkModifiedWorldBoundsXZ(Rect boundsXZ)
        {
            if (boundsXZ.width <= 0f || boundsXZ.height <= 0f)
                return;

            _modifiedWorldBoundsXZ = _hasModifiedWorldBounds
                ? UnionWorldRect(_modifiedWorldBoundsXZ, boundsXZ)
                : boundsXZ;
            _hasModifiedWorldBounds = true;
        }

        private static Rect UnionWorldRect(Rect a, Rect b) =>
            Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax));

        private static int ClampProtectedWaterBedCells(
            float[,] heights,
            Vector3 origin,
            Vector3 size,
            int resolution,
            int startX,
            int startZ,
            int width,
            int height,
            Rect worldBoundsXZ)
        {
            var clamp = _protectedGroundHeightClamp;
            if (clamp == null)
                return 0;
            if (_protectedWaterBoundsTest != null && !_protectedWaterBoundsTest(worldBoundsXZ))
                return 0;

            var lowered = 0;
            var invHeight = 1f / size.y;
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var worldPoint = HeightmapWorldPoint(origin, size, resolution, startX + x, startZ + z);
                    var currentWorldY = heights[z, x] * size.y + origin.y;
                    var clampedWorldY = clamp(worldPoint, currentWorldY);
                    if (clampedWorldY >= currentWorldY - 0.0001f)
                        continue;

                    heights[z, x] = Mathf.Clamp01((clampedWorldY - origin.y) * invHeight);
                    lowered++;
                }
            }

            return lowered;
        }

        private static Rect ResolveHeightmapWindowWorldRect(
            Vector3 origin,
            Vector3 size,
            int resolution,
            int startX,
            int startZ,
            int width,
            int height)
        {
            var min = HeightmapWorldPoint(origin, size, resolution, startX, startZ);
            var max = HeightmapWorldPoint(origin, size, resolution, startX + width - 1, startZ + height - 1);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static void FlattenRect(
            Terrain terrain,
            Rect worldBoundsXZ,
            float targetWorldY,
            float paddingMeters = 0f,
            float blendMeters = 2f)
        {
            if (terrain == null || terrain.terrainData == null) return;

            var coreMinX = worldBoundsXZ.xMin - Mathf.Max(0f, paddingMeters);
            var coreMaxX = worldBoundsXZ.xMax + Mathf.Max(0f, paddingMeters);
            var coreMinZ = worldBoundsXZ.yMin - Mathf.Max(0f, paddingMeters);
            var coreMaxZ = worldBoundsXZ.yMax + Mathf.Max(0f, paddingMeters);

            var blend = Mathf.Max(0f, blendMeters);
            FlattenRectCore(
                terrain,
                new FlattenArgs
                {
                    InnerMinX = coreMinX, InnerMaxX = coreMaxX,
                    InnerMinZ = coreMinZ, InnerMaxZ = coreMaxZ,
                    OuterMinX = coreMinX - blend, OuterMaxX = coreMaxX + blend,
                    OuterMinZ = coreMinZ - blend, OuterMaxZ = coreMaxZ + blend,
                    SampleMinX = coreMinX - blend, SampleMaxX = coreMaxX + blend,
                    SampleMinZ = coreMinZ - blend, SampleMaxZ = coreMaxZ + blend,
                    BlendDistance = blend, TargetWorldY = targetWorldY
                });
        }

        public static void FlattenRects(
            IReadOnlyList<Terrain> terrains,
            IReadOnlyList<Rect> worldBounds,
            float targetWorldY,
            float paddingMeters = 0f,
            float blendMeters = 2f)
        {
            if (terrains == null || worldBounds == null) return;

            for (var r = 0; r < worldBounds.Count; r++)
            {
                var rect = worldBounds[r];
                for (var t = 0; t < terrains.Count; t++)
                    FlattenRect(terrains[t], rect, targetWorldY, paddingMeters, blendMeters);
            }
        }

        /// <summary>
        ///     Full flatten inside <paramref name="innerBoundsXZ" />, linear falloff between inner (yellow)
        ///     and outer (blue) bounds, optional blend past the outer edge into natural terrain.
        /// </summary>
        public static void FlattenNestedRects(
            Terrain terrain,
            Rect innerBoundsXZ,
            Rect outerBoundsXZ,
            float targetWorldY,
            float innerPaddingMeters = 0f,
            float outerEdgeBlendMeters = 0f)
        {
            if (terrain == null || terrain.terrainData == null) return;

            if (RectsApproximatelyEqual(innerBoundsXZ, outerBoundsXZ))
            {
                FlattenRect(terrain, innerBoundsXZ, targetWorldY, innerPaddingMeters, outerEdgeBlendMeters);
                return;
            }

            var padding = Mathf.Max(0f, innerPaddingMeters);
            var innerMinX = innerBoundsXZ.xMin - padding;
            var innerMaxX = innerBoundsXZ.xMax + padding;
            var innerMinZ = innerBoundsXZ.yMin - padding;
            var innerMaxZ = innerBoundsXZ.yMax + padding;

            var outerMinX = outerBoundsXZ.xMin;
            var outerMaxX = outerBoundsXZ.xMax;
            var outerMinZ = outerBoundsXZ.yMin;
            var outerMaxZ = outerBoundsXZ.yMax;

            var outerBlend = Mathf.Max(0f, outerEdgeBlendMeters);

            var args = new FlattenArgs
            {
                InnerMinX = innerMinX, InnerMaxX = innerMaxX,
                InnerMinZ = innerMinZ, InnerMaxZ = innerMaxZ,
                OuterMinX = outerMinX, OuterMaxX = outerMaxX,
                OuterMinZ = outerMinZ, OuterMaxZ = outerMaxZ,
                SampleMinX = outerMinX - outerBlend, SampleMaxX = outerMaxX + outerBlend,
                SampleMinZ = outerMinZ - outerBlend, SampleMaxZ = outerMaxZ + outerBlend,
                BlendDistance = outerBlend, TargetWorldY = targetWorldY
            };
            FlattenNestedRectsCore(terrain, args);
        }

        private static void FlattenRectCore(Terrain terrain, FlattenArgs a)
        {
            var terrainData = terrain.terrainData;
            var origin = terrain.transform.position;
            var size = terrainData.size;
            if (size.x <= 0.01f || size.z <= 0.01f || size.y <= 0.01f) return;

            var regionMinX = Mathf.Max(a.SampleMinX, origin.x);
            var regionMaxX = Mathf.Min(a.SampleMaxX, origin.x + size.x);
            var regionMinZ = Mathf.Max(a.SampleMinZ, origin.z);
            var regionMaxZ = Mathf.Min(a.SampleMaxZ, origin.z + size.z);

            if (regionMaxX <= regionMinX || regionMaxZ <= regionMinZ) return;

            var heightRes = terrainData.heightmapResolution;
            if (heightRes <= 1) return;

            if (!TryBuildSampleRegion(regionMinX, regionMaxX, origin.x, size.x, heightRes, out var startX,
                    out var width)) return;
            if (!TryBuildSampleRegion(regionMinZ, regionMaxZ, origin.z, size.z, heightRes, out var startZ,
                    out var height)) return;

            var heights = terrainData.GetHeights(startX, startZ, width, height);
            var targetNormalized = Mathf.Clamp01((a.TargetWorldY - origin.y) / Mathf.Max(0.01f, size.y));

            float WeightFunc(float wx, float wz) =>
                ComputeFlattenWeight(wx, wz, a.InnerMinX, a.InnerMaxX, a.InnerMinZ, a.InnerMaxZ, a.BlendDistance);

            if (!ProcessHeightmap(heights, origin, size, heightRes, startX, startZ, width, height,
                    targetNormalized, WeightFunc))
                return;

            PrepareHeightmapWrite(terrain, heights, startX, startZ, width, height);
            terrainData.SetHeightsDelayLOD(startX, startZ, heights);
#if UNITY_2019_1_OR_NEWER
            terrainData.SyncHeightmap();
#endif
            terrain.Flush();
        }

        private static void FlattenNestedRectsCore(Terrain terrain, FlattenArgs a)
        {
            var terrainData = terrain.terrainData;
            var origin = terrain.transform.position;
            var size = terrainData.size;
            if (size.x <= 0.01f || size.z <= 0.01f || size.y <= 0.01f) return;

            var regionMinX = Mathf.Max(a.SampleMinX, origin.x);
            var regionMaxX = Mathf.Min(a.SampleMaxX, origin.x + size.x);
            var regionMinZ = Mathf.Max(a.SampleMinZ, origin.z);
            var regionMaxZ = Mathf.Min(a.SampleMaxZ, origin.z + size.z);

            if (regionMaxX <= regionMinX || regionMaxZ <= regionMinZ) return;

            var heightRes = terrainData.heightmapResolution;
            if (heightRes <= 1) return;

            if (!TryBuildSampleRegion(regionMinX, regionMaxX, origin.x, size.x, heightRes, out var startX,
                    out var width)) return;
            if (!TryBuildSampleRegion(regionMinZ, regionMaxZ, origin.z, size.z, heightRes, out var startZ,
                    out var height)) return;

            var heights = terrainData.GetHeights(startX, startZ, width, height);
            var targetNormalized = Mathf.Clamp01((a.TargetWorldY - origin.y) / Mathf.Max(0.01f, size.y));

            float WeightFunc(float wx, float wz)
            {
                var nested = ComputeNestedRectWeight(wx, wz, a);
                if (nested <= 0f) return 0f;
                if (a.BlendDistance <= 0.01f) return nested;
                var fade = ComputeFlattenWeight(wx, wz, a.OuterMinX, a.OuterMaxX, a.OuterMinZ, a.OuterMaxZ, a.BlendDistance);
                return nested * fade;
            }

            if (!ProcessHeightmap(heights, origin, size, heightRes, startX, startZ, width, height,
                    targetNormalized, WeightFunc))
                return;

            PrepareHeightmapWrite(terrain, heights, startX, startZ, width, height);
            terrainData.SetHeightsDelayLOD(startX, startZ, heights);
#if UNITY_2019_1_OR_NEWER
            terrainData.SyncHeightmap();
#endif
            terrain.Flush();
        }

        /// <returns>true if any height was changed</returns>
        private static bool ProcessHeightmap(
            float[,] heights,
            Vector3 origin,
            Vector3 size,
            int heightRes,
            int startX,
            int startZ,
            int width,
            int height,
            float targetNormalized,
            System.Func<float, float, float> computeWeight)
        {
            var changed = false;

            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var worldX = origin.x + ((startX + x) / (float)(heightRes - 1)) * size.x;
                    var worldZ = origin.z + ((startZ + z) / (float)(heightRes - 1)) * size.z;

                    var weight = computeWeight(worldX, worldZ);
                    if (weight <= 0f) continue;

                    var current = heights[z, x];
                    var next = Mathf.Lerp(current, targetNormalized, weight);
                    if (Mathf.Abs(next - current) <= 0.00001f) continue;

                    heights[z, x] = next;
                    changed = true;
                }
            }

            return changed;
        }

        private static float ComputeNestedRectWeight(float worldX, float worldZ, FlattenArgs a)
        {
            if (worldX < a.OuterMinX || worldX > a.OuterMaxX || worldZ < a.OuterMinZ || worldZ > a.OuterMaxZ)
                return 0f;

            var weightX = ComputeAxisNestedWeight(worldX, a.InnerMinX, a.InnerMaxX, a.OuterMinX, a.OuterMaxX);
            var weightZ = ComputeAxisNestedWeight(worldZ, a.InnerMinZ, a.InnerMaxZ, a.OuterMinZ, a.OuterMaxZ);
            return Mathf.Min(weightX, weightZ);
        }

        private static float ComputeAxisNestedWeight(
            float coord,
            float innerMin,
            float innerMax,
            float outerMin,
            float outerMax)
        {
            if (coord >= innerMin && coord <= innerMax)
                return 1f;

            if (coord < innerMin)
            {
                var band = innerMin - outerMin;
                if (band <= 0.01f) return 0f;
                return SmoothBlendCurve(Mathf.Clamp01((coord - outerMin) / band));
            }

            var upperBand = outerMax - innerMax;
            if (upperBand <= 0.01f) return 0f;
            return SmoothBlendCurve(Mathf.Clamp01((outerMax - coord) / upperBand));
        }

        private static bool RectsApproximatelyEqual(Rect a, Rect b)
        {
            const float epsilon = 0.05f;
            return Mathf.Abs(a.xMin - b.xMin) < epsilon
                   && Mathf.Abs(a.xMax - b.xMax) < epsilon
                   && Mathf.Abs(a.yMin - b.yMin) < epsilon
                   && Mathf.Abs(a.yMax - b.yMax) < epsilon;
        }

        private static float ComputeFlattenWeight(
            float worldX,
            float worldZ,
            float coreMinX,
            float coreMaxX,
            float coreMinZ,
            float coreMaxZ,
            float blendDistance)
        {
            var dx = 0f;
            if (worldX < coreMinX) dx = coreMinX - worldX;
            else if (worldX > coreMaxX) dx = worldX - coreMaxX;

            var dz = 0f;
            if (worldZ < coreMinZ) dz = coreMinZ - worldZ;
            else if (worldZ > coreMaxZ) dz = worldZ - coreMaxZ;

            var edgeDist = Mathf.Sqrt(dx * dx + dz * dz);
            if (edgeDist <= 0f) return 1f;
            if (blendDistance <= 0.01f) return 0f;

            var linear = 1f - Mathf.Clamp01(edgeDist / blendDistance);
            return SmoothBlendCurve(linear);
        }

        private static bool TryBuildSampleRegion(
            float worldMin,
            float worldMax,
            float terrainOrigin,
            float terrainSize,
            int resolution,
            out int start,
            out int length)
        {
            start = 0;
            length = 0;

            if (terrainSize <= 0.01f || resolution <= 1) return false;

            var normMin = Mathf.Clamp01((worldMin - terrainOrigin) / terrainSize);
            var normMax = Mathf.Clamp01((worldMax - terrainOrigin) / terrainSize);
            var minIndex = Mathf.Clamp(Mathf.FloorToInt(normMin * (resolution - 1)), 0, resolution - 1);
            var maxIndex = Mathf.Clamp(Mathf.CeilToInt(normMax * (resolution - 1)), 0, resolution - 1);
            if (maxIndex < minIndex) return false;

            start = minIndex;
            length = maxIndex - minIndex + 1;
            return length > 0;
        }
    }
}
