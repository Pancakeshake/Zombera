using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Roads
{
    /// <summary>Per-road buffer writes and paint helpers for <see cref="RoadTerrainStamper"/>.</summary>
    public static partial class RoadTerrainStamper
    {
        private struct StampAlphaCtx
        {
            public float[,,] Alphas;
            public int Layers, RoadLayer;
            public int CenterX, CenterZ, RadiusX, RadiusZ;
            public float Falloff;
        }

        private static bool TryBuildUnionStamp(
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            Rect tileQueryRect,
            Rect terrainRect,
            bool alphamapOnlyForHighways,
            out Rect unionRect,
            out bool anyHeights,
            out bool anyAlphas)
        {
            unionRect = default;
            anyHeights = false;
            anyAlphas = false;
            var found = false;

            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                var radius = ResolveStampRadius(road, settings);
                var expanded = ExpandRect(road.BoundsXZ, radius, radius);
                if (!TryIntersectRect(tileQueryRect, expanded, out var stampQuery) ||
                    !TryIntersectRect(stampQuery, terrainRect, out var terrainStamp))
                    continue;

                unionRect = found ? UnionRect(unionRect, terrainStamp) : terrainStamp;
                found = true;
                anyAlphas = true;
                if (!(alphamapOnlyForHighways && road.roadClass == RoadClass.Highway))
                    anyHeights = true;
            }

            return found;
        }

        private static void StampOneRoadIntoBuffers(
            RoadPolyline road,
            RoadNetworkSettings settings,
            Rect tileQueryRect,
            Rect terrainRect,
            Vector3 origin,
            Vector3 size,
            int heightRes,
            int alphaW,
            int alphaH,
            int alphaLayers,
            int roadLayer,
            int heightStartX,
            int heightStartZ,
            int alphaStartX,
            int alphaStartZ,
            float[,] heights,
            float[,,] alphas,
            IReadOnlyList<WaterCrossing> crossings,
            bool writeHeights)
        {
            var width = Mathf.Max(0.5f, road.widthMeters);
            var radius = ResolveStampRadius(road, settings);
            var falloff = Mathf.Clamp01(settings.terrainBlendFalloff);
            var expanded = ExpandRect(road.BoundsXZ, radius, radius);
            if (!TryIntersectRect(tileQueryRect, expanded, out var stampQuery) ||
                !TryIntersectRect(stampQuery, terrainRect, out var terrainStamp))
                return;

            var ctx = new StampSampleCtx
            {
                TerrainOriginX = origin.x,
                TerrainOriginZ = origin.z,
                TerrainSize = size,
                WidthMeters = width,
                SampleStep = Mathf.Clamp(width * 0.35f, 3f, 10f),
                TerrainStampRect = terrainStamp,
                HeightRes = heightRes,
                AlphaW = alphaW,
                AlphaH = alphaH,
                HeightRadiusX = Mathf.CeilToInt(radius / Mathf.Max(0.01f, size.x) * (heightRes - 1)),
                HeightRadiusZ = Mathf.CeilToInt(radius / Mathf.Max(0.01f, size.z) * (heightRes - 1)),
                AlphaRadiusX = Mathf.CeilToInt(radius / Mathf.Max(0.01f, size.x) * (alphaW - 1)),
                AlphaRadiusZ = Mathf.CeilToInt(radius / Mathf.Max(0.01f, size.z) * (alphaH - 1)),
                Falloff = falloff,
                Heights = heights,
                Alphas = alphas,
                RoadLayer = roadLayer,
                AlphaLayers = alphaLayers,
                HeightStartX = heightStartX,
                HeightStartZ = heightStartZ,
                AlphaStartX = alphaStartX,
                AlphaStartZ = alphaStartZ,
                SkipBridgeSpans = crossings,
                SkipRadiusMeters = radius + 2f,
                RoadId = road.id,
                WriteHeights = writeHeights && heights != null
            };

            SampleAlongPolyline(road.pointsXZ, ctx.SampleStep, tileQueryRect, p => ProcessStampSample(ctx, p));
        }

        private static float ResolveStampRadius(RoadPolyline road, RoadNetworkSettings settings)
        {
            var width = Mathf.Max(0.5f, road.widthMeters);
            var shoulder = Mathf.Max(0f, settings.terrainShoulderMeters);
            return width * 0.5f + shoulder;
        }

        private static Rect UnionRect(Rect a, Rect b) =>
            Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        private static void ProcessStampSample(StampSampleCtx ctx, Vector2 p)
        {
            var wx = p.x;
            var wz = p.y;
            if (!ctx.TerrainStampRect.Contains(new Vector2(wx, wz))) return;
            if (IsInsideSkippedBridgeSpan(ctx, p)) return;
            if (MountainTunnelBuildCache.IsCoreSkip(p, ctx.RoadId, ctx.SkipRadiusMeters)) return;

            var localX = (wx - ctx.TerrainOriginX) / Mathf.Max(0.01f, ctx.TerrainSize.x);
            var localZ = (wz - ctx.TerrainOriginZ) / Mathf.Max(0.01f, ctx.TerrainSize.z);
            if (localX < 0f || localX > 1f || localZ < 0f || localZ > 1f) return;

            if (ctx.WriteHeights)
            {
                var targetNorm = SampleHeightBufferBilinear(
                    ctx.Heights, ctx.HeightStartX, ctx.HeightStartZ, ctx.HeightRes, localX, localZ);
                var hcx = Mathf.RoundToInt(localX * (ctx.HeightRes - 1)) - ctx.HeightStartX;
                var hcz = Mathf.RoundToInt(localZ * (ctx.HeightRes - 1)) - ctx.HeightStartZ;
                StampCircleHeights(
                    ctx.Heights, hcx, hcz, ctx.HeightRadiusX, ctx.HeightRadiusZ, targetNorm, ctx.Falloff);
            }

            if (ctx.Alphas == null) return;

            var acx = Mathf.RoundToInt(localX * (ctx.AlphaW - 1)) - ctx.AlphaStartX;
            var acz = Mathf.RoundToInt(localZ * (ctx.AlphaH - 1)) - ctx.AlphaStartZ;
            StampCircleAlphas(new StampAlphaCtx
            {
                Alphas = ctx.Alphas,
                Layers = ctx.AlphaLayers,
                RoadLayer = ctx.RoadLayer,
                CenterX = acx,
                CenterZ = acz,
                RadiusX = ctx.AlphaRadiusX,
                RadiusZ = ctx.AlphaRadiusZ,
                Falloff = ctx.Falloff
            });
        }

        /// <summary>Bilinear sample from the in-buffer heightmap (avoids Terrain.SampleHeight).</summary>
        private static float SampleHeightBufferBilinear(
            float[,] heights,
            int heightStartX,
            int heightStartZ,
            int heightRes,
            float localX,
            float localZ)
        {
            var width = heights.GetLength(1);
            var height = heights.GetLength(0);
            var fx = localX * (heightRes - 1) - heightStartX;
            var fz = localZ * (heightRes - 1) - heightStartZ;
            var x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, width - 1);
            var z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, height - 1);
            var x1 = Mathf.Min(x0 + 1, width - 1);
            var z1 = Mathf.Min(z0 + 1, height - 1);
            var tx = Mathf.Clamp01(fx - x0);
            var tz = Mathf.Clamp01(fz - z0);
            var h00 = heights[z0, x0];
            var h10 = heights[z0, x1];
            var h01 = heights[z1, x0];
            var h11 = heights[z1, x1];
            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
        }

        private static bool IsInsideSkippedBridgeSpan(StampSampleCtx ctx, Vector2 p)
        {
            var crossings = ctx.SkipBridgeSpans;
            if (crossings == null || crossings.Count == 0)
                return false;

            var pad = Mathf.Max(1f, ctx.SkipRadiusMeters);
            for (var i = 0; i < crossings.Count; i++)
            {
                var c = crossings[i];
                if (c.Policy != WaterCrossingPolicy.Bridge && c.Policy != WaterCrossingPolicy.Causeway)
                    continue;

                var dist = DistancePointToSegment(p, c.EntryXZ, c.ExitXZ);
                if (dist <= pad)
                    return true;
            }

            return false;
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
                return Vector2.Distance(p, a);

            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            var proj = a + ab * t;
            return Vector2.Distance(p, proj);
        }

        private static Rect ExpandRect(Rect rect, float expandX, float expandY) =>
            Rect.MinMaxRect(rect.xMin - expandX, rect.yMin - expandY, rect.xMax + expandX, rect.yMax + expandY);

        private static bool TryIntersectRect(Rect a, Rect b, out Rect intersection)
        {
            var minX = Mathf.Max(a.xMin, b.xMin);
            var minY = Mathf.Max(a.yMin, b.yMin);
            var maxX = Mathf.Min(a.xMax, b.xMax);
            var maxY = Mathf.Min(a.yMax, b.yMax);

            if (maxX <= minX || maxY <= minY)
            {
                intersection = default;
                return false;
            }

            intersection = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private static bool TryBuildSampleRegion(float worldMin, float worldMax, float terrainOrigin, float terrainSize,
            int resolution, out int start, out int length)
        {
            start = length = 0;
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

        private static void SampleAlongPolyline(IReadOnlyList<Vector2> pointsXZ, float stepMeters, Rect tileRect,
            Action<Vector2> onSample)
        {
            if (pointsXZ == null || pointsXZ.Count < 2 || onSample == null) return;

            for (var i = 1; i < pointsXZ.Count; i++)
            {
                var a = pointsXZ[i - 1];
                var b = pointsXZ[i];
                var segLen = Vector2.Distance(a, b);
                if (segLen < 0.001f) continue;

                var segBounds = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x),
                    Mathf.Max(a.y, b.y));
                if (!segBounds.Overlaps(tileRect, true)) continue;

                var steps = Mathf.Max(1, Mathf.CeilToInt(segLen / Mathf.Max(0.5f, stepMeters)));
                for (var s = 0; s <= steps; s++)
                {
                    var t = s / (float)steps;
                    onSample(Vector2.Lerp(a, b, t));
                }
            }
        }

        private static void StampCircleHeights(
            float[,] heights, int centerX, int centerZ,
            int radiusX, int radiusZ, float targetHeight, float falloff)
        {
            var width = heights.GetLength(1);
            var height = heights.GetLength(0);
            if (width <= 0 || height <= 0) return;

            var rx = Mathf.Max(1, radiusX);
            var rz = Mathf.Max(1, radiusZ);
            var x0 = Mathf.Clamp(centerX - rx, 0, width - 1);
            var x1 = Mathf.Clamp(centerX + rx, 0, width - 1);
            var z0 = Mathf.Clamp(centerZ - rz, 0, height - 1);
            var z1 = Mathf.Clamp(centerZ + rz, 0, height - 1);

            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var dx = (x - centerX) / (float)rx;
                    var dz = (z - centerZ) / (float)rz;
                    var d2 = dx * dx + dz * dz;
                    if (d2 > 1f) continue;

                    var d = Mathf.Sqrt(d2);
                    var w = SmoothStampWeight(d, falloff);
                    heights[z, x] = Mathf.Lerp(heights[z, x], targetHeight, w);
                }
            }
        }

        private static void StampCircleAlphas(StampAlphaCtx a)
        {
            var width = a.Alphas.GetLength(1);
            var height = a.Alphas.GetLength(0);
            if (width <= 0 || height <= 0) return;

            var rx = Mathf.Max(1, a.RadiusX);
            var rz = Mathf.Max(1, a.RadiusZ);
            var x0 = Mathf.Clamp(a.CenterX - rx, 0, width - 1);
            var x1 = Mathf.Clamp(a.CenterX + rx, 0, width - 1);
            var z0 = Mathf.Clamp(a.CenterZ - rz, 0, height - 1);
            var z1 = Mathf.Clamp(a.CenterZ + rz, 0, height - 1);

            for (var z = z0; z <= z1; z++)
                for (var x = x0; x <= x1; x++)
                    StampAlphaPixel(a, x, z, rx, rz);
        }

        private static void StampAlphaPixel(StampAlphaCtx a, int x, int z, float rx, float rz)
        {
            var dx = (x - a.CenterX) / (float)rx;
            var dz = (z - a.CenterZ) / (float)rz;
            var d2 = dx * dx + dz * dz;
            if (d2 > 1f) return;

            var wgt = SmoothStampWeight(Mathf.Sqrt(d2), a.Falloff);
            var sum = 0f;
            for (var l = 0; l < a.Layers; l++)
            {
                var v = a.Alphas[z, x, l];
                v = l == a.RoadLayer ? Mathf.Lerp(v, 1f, wgt) : Mathf.Lerp(v, 0f, wgt * 0.65f);
                a.Alphas[z, x, l] = v;
                sum += v;
            }

            if (sum > 0.0001f)
            {
                var inv = 1f / sum;
                for (var l = 0; l < a.Layers; l++) a.Alphas[z, x, l] *= inv;
            }
        }

        private static float SmoothStampWeight(float normalizedDistance01, float falloff)
        {
            var t = Mathf.Clamp01(normalizedDistance01);
            if (t <= falloff) return 1f;
            var u = Mathf.InverseLerp(falloff, 1f, t);
            return 1f - (u * u * (3f - 2f * u));
        }

        private static int ResolveRoadLayerIndex(TerrainData td)
        {
            if (td == null) return -1;
            var layers = td.terrainLayers;
            if (layers == null) return -1;

            for (var i = 0; i < layers.Length; i++)
            {
                if (layers[i]?.name.IndexOf("road", StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }

            return -1;
        }
    }
}
