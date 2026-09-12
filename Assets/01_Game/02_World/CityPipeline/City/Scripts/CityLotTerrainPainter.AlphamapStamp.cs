using System.Collections.Generic;
using JBooth.MicroSplat;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Alphamap dirty-region flush and zone stamping for
    ///     <see cref="CityLotTerrainPainter"/>. MicroSplat sync lives in
    ///     <c>CityLotTerrainPainter.ControlSync.cs</c>.
    /// </summary>
    public static partial class CityLotTerrainPainter
    {
        private readonly struct AlphamapGrid
        {
            public readonly Vector3 Origin;
            public readonly Vector3 Size;
            public readonly int Width;
            public readonly int Height;

            public AlphamapGrid(Vector3 origin, Vector3 size, int width, int height)
            {
                Origin = origin;
                Size = size;
                Width = width;
                Height = height;
            }
        }

        private readonly struct AlphamapDirtyRect
        {
            public readonly int XBase;
            public readonly int ZBase;
            public readonly int Width;
            public readonly int Height;

            public AlphamapDirtyRect(int xBase, int zBase, int width, int height)
            {
                XBase = xBase;
                ZBase = zBase;
                Width = width;
                Height = height;
            }
        }

        private readonly struct ZoneStampContext
        {
            public readonly float[,,] Alphas;
            public readonly int AlphaLayers;
            public readonly AlphamapGrid Grid;
            public readonly int AlphaBaseX;
            public readonly int AlphaBaseZ;

            public ZoneStampContext(
                float[,,] alphas, int alphaLayers, AlphamapGrid grid,
                int alphaBaseX, int alphaBaseZ)
            {
                Alphas = alphas;
                AlphaLayers = alphaLayers;
                Grid = grid;
                AlphaBaseX = alphaBaseX;
                AlphaBaseZ = alphaBaseZ;
            }
        }

        private static void FlushTerrainZonePaints(
            Terrain terrain, List<PendingZonePaint> zones,
            ref long getMs, ref long stampMs, ref long setMs, ref long syncMs)
        {
            EnsureMicroSplatMaterial(terrain);

            var td = terrain.terrainData;
            var alphaW = td.alphamapWidth;
            var alphaH = td.alphamapHeight;
            var alphaLayers = td.alphamapLayers;
            if (alphaW <= 1 || alphaH <= 1 || alphaLayers <= 0 || zones.Count == 0) return;

            var grid = new AlphamapGrid(terrain.transform.position, td.size, alphaW, alphaH);
            if (!TryComputeDirtyAlphaRegion(zones, grid, out var dirty))
                return;

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var alphas = td.GetAlphamaps(dirty.XBase, dirty.ZBase, dirty.Width, dirty.Height);
            getMs += watch.ElapsedMilliseconds;

            // Soft yards first, then absolute sharp stamps overwrite hardscape.
            // No sharp-mask allocation — absolute stamps make soft-into-sharp moot.
            watch.Restart();
            var ctx = new ZoneStampContext(alphas, alphaLayers, grid, dirty.XBase, dirty.ZBase);
            StampPendingZones(ctx, zones, sharpPass: false);
            StampPendingZones(ctx, zones, sharpPass: true);
            stampMs += watch.ElapsedMilliseconds;

            watch.Restart();
            td.SetAlphamaps(dirty.XBase, dirty.ZBase, alphas);
            ForcePointFilterAlphamaps(td);
            setMs += watch.ElapsedMilliseconds;

            watch.Restart();
            SyncAlphamapsToMicroSplat(terrain, alphas, dirty.XBase, dirty.ZBase);
            syncMs += watch.ElapsedMilliseconds;
        }

        /// <summary>
        ///     Union of all pending zone rects in alphamap indices (plus 1-texel pad).
        ///     City paint usually covers a fraction of a 1 km tile — full-tile
        ///     Get/SetAlphamaps was the Lot Terrain bottleneck at 1024/2048.
        /// </summary>
        private static bool TryComputeDirtyAlphaRegion(
            List<PendingZonePaint> zones, AlphamapGrid grid, out AlphamapDirtyRect dirty)
        {
            dirty = default;
            var minX = grid.Width;
            var minZ = grid.Height;
            var maxX = -1;
            var maxZ = -1;

            for (var i = 0; i < zones.Count; i++)
            {
                if (!TryExpandDirtyBounds(zones[i], grid, ref minX, ref minZ, ref maxX, ref maxZ))
                    continue;
            }

            if (maxX < minX || maxZ < minZ)
                return false;

            // One-texel pad so Soft edge falloff / texel-center sampling stays valid.
            minX = Mathf.Max(0, minX - 1);
            minZ = Mathf.Max(0, minZ - 1);
            maxX = Mathf.Min(grid.Width - 1, maxX + 1);
            maxZ = Mathf.Min(grid.Height - 1, maxZ + 1);

            var width = maxX - minX + 1;
            var height = maxZ - minZ + 1;
            if (width <= 0 || height <= 0)
                return false;

            dirty = new AlphamapDirtyRect(minX, minZ, width, height);
            return true;
        }

        private static bool TryExpandDirtyBounds(
            PendingZonePaint zone, AlphamapGrid grid,
            ref int minX, ref int minZ, ref int maxX, ref int maxZ)
        {
            var expanded = Rect.MinMaxRect(
                zone.Rect.xMin - zone.Blend, zone.Rect.yMin - zone.Blend,
                zone.Rect.xMax + zone.Blend, zone.Rect.yMax + zone.Blend);
            if (!TryBuildSampleRegion(expanded.xMin, expanded.xMax,
                    grid.Origin.x, grid.Size.x, grid.Width, out var ax, out var aw))
                return false;
            if (!TryBuildSampleRegion(expanded.yMin, expanded.yMax,
                    grid.Origin.z, grid.Size.z, grid.Height, out var az, out var ah))
                return false;

            minX = Mathf.Min(minX, ax);
            minZ = Mathf.Min(minZ, az);
            maxX = Mathf.Max(maxX, ax + aw - 1);
            maxZ = Mathf.Max(maxZ, az + ah - 1);
            return true;
        }

        private static void ForcePointFilterAlphamaps(TerrainData td)
        {
            var textures = td.alphamapTextures;
            if (textures == null) return;
            for (var i = 0; i < textures.Length; i++)
            {
                var tex = textures[i];
                if (tex == null) continue;
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
            }
        }

        private static void StampPendingZones(
            ZoneStampContext ctx, List<PendingZonePaint> zones, bool sharpPass)
        {
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone.Layer < 0 || zone.Layer >= ctx.AlphaLayers) continue;
                var isSharp = zone.Sharp || zone.HardErase;
                if (isSharp != sharpPass) continue;
                StampZoneIntoAlphamaps(ctx, zone);
            }
        }

        private static void StampZoneIntoAlphamaps(ZoneStampContext ctx, PendingZonePaint zone)
        {
            var expandedRect = Rect.MinMaxRect(
                zone.Rect.xMin - zone.Blend, zone.Rect.yMin - zone.Blend,
                zone.Rect.xMax + zone.Blend, zone.Rect.yMax + zone.Blend);

            if (!TryBuildSampleRegion(expandedRect.xMin, expandedRect.xMax,
                    ctx.Grid.Origin.x, ctx.Grid.Size.x, ctx.Grid.Width, out var ax, out var aw))
                return;
            if (!TryBuildSampleRegion(expandedRect.yMin, expandedRect.yMax,
                    ctx.Grid.Origin.z, ctx.Grid.Size.z, ctx.Grid.Height, out var az, out var ah))
                return;

            StampZonePixels(ctx, zone, ax, aw, az, ah);
        }

        private static void StampZonePixels(
            ZoneStampContext ctx, PendingZonePaint zone,
            int ax, int aw, int az, int ah)
        {
            var absoluteSharp = zone.Sharp && !zone.HardErase;
            var invH = 1f / (ctx.Grid.Height - 1);
            var subH = ctx.Alphas.GetLength(0);

            for (var z = 0; z < ah; z++)
            {
                var pz = az + z;
                var lz = pz - ctx.AlphaBaseZ;
                if ((uint)lz >= (uint)subH) continue;
                var wz = ctx.Grid.Origin.z + pz * invH * ctx.Grid.Size.z;
                StampZoneRow(ctx, zone, ax, aw, lz, wz, absoluteSharp);
            }
        }

        private static void StampZoneRow(
            ZoneStampContext ctx, PendingZonePaint zone,
            int ax, int aw, int lz, float wz, bool absoluteSharp)
        {
            var invW = 1f / (ctx.Grid.Width - 1);
            var subW = ctx.Alphas.GetLength(1);

            for (var x = 0; x < aw; x++)
            {
                var px = ax + x;
                var lx = px - ctx.AlphaBaseX;
                if ((uint)lx >= (uint)subW) continue;

                var wx = ctx.Grid.Origin.x + px * invW * ctx.Grid.Size.x;
                var w = ComputeZoneWeight(wx, wz, zone.Rect, zone.Blend);
                if (w <= 0.001f) continue;

                if (absoluteSharp)
                    StampPixelAbsolute(ctx.Alphas, ctx.AlphaLayers, zone.Layer, lz, lx);
                else
                    StampPixel(ctx.Alphas, ctx.AlphaLayers, zone.Layer, lz, lx, w, zone.HardErase);
            }
        }

        private static float ComputeZoneWeight(
            float worldX, float worldZ, Rect zone, float blendMeters)
        {
            var dxMin = worldX - zone.xMin;
            var dxMax = zone.xMax - worldX;
            var dzMin = worldZ - zone.yMin;
            var dzMax = zone.yMax - worldZ;
            var minDist = Mathf.Min(dxMin, dxMax, dzMin, dzMax);
            if (minDist >= 0f) return 1f;

            // Zero blend = crisp binary edge (driveways / slabs): full weight
            // inside the rect, nothing outside — no 0.1 m falloff floor.
            if (blendMeters <= 0.01f) return 0f;

            var t = Mathf.Clamp01(-minDist / blendMeters);
            return 1f - t;
        }

        /// <summary>
        ///     Binary splat write for driveways / slabs / door paths: target layer
        ///     is exactly 1 and every other layer is 0 — no lerp residual.
        /// </summary>
        private static void StampPixelAbsolute(
            float[,,] alphas, int layerCount, int targetLayer, int z, int x)
        {
            for (var l = 0; l < layerCount; l++)
                alphas[z, x, l] = l == targetLayer ? 1f : 0f;
        }

        private static void StampPixel(
            float[,,] alphas, int layerCount, int targetLayer, int z, int x, float weight, bool hardErase = false)
        {
            // Clear / full-weight stamps fully erase non-target layers. Partial
            // soft stamps keep a damped fade so yard seams blend.
            var damp = hardErase || weight >= 0.999f ? 1f : 0.85f;
            var sum = 0f;
            for (var l = 0; l < layerCount; l++)
            {
                var v = alphas[z, x, l];
                v = l == targetLayer
                    ? Mathf.Lerp(v, 1f, weight)
                    : Mathf.Lerp(v, 0f, weight * damp);
                alphas[z, x, l] = v;
                sum += v;
            }
            if (sum > 0.0001f)
            {
                var inv = 1f / sum;
                for (var l = 0; l < layerCount; l++)
                    alphas[z, x, l] *= inv;
            }
        }

        private static bool TryBuildSampleRegion(
            float worldMin, float worldMax,
            float terrainOrigin, float terrainSize,
            int resolution, out int start, out int length)
        {
            start = 0;
            length = 0;
            if (terrainSize <= 0.01f || resolution <= 1) return false;
            var normMin = Mathf.Clamp01((worldMin - terrainOrigin) / terrainSize);
            var normMax = Mathf.Clamp01((worldMax - terrainOrigin) / terrainSize);
            var minIdx = Mathf.Clamp(Mathf.FloorToInt(normMin * (resolution - 1)), 0, resolution - 1);
            var maxIdx = Mathf.Clamp(Mathf.CeilToInt(normMax * (resolution - 1)), 0, resolution - 1);
            if (maxIdx < minIdx) return false;
            start = minIdx;
            length = maxIdx - minIdx + 1;
            return length > 0;
        }
    }
}
