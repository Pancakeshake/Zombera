using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Residual urban-paint erase: sparse packed-alphamap probe, dirty-rect
    ///     read-modify-write, and MicroSplat sync for reset / clear paths.
    /// </summary>
    public static partial class CityLotTerrainPainter
    {
        private struct UrbanDirtyBounds
        {
            public int MinX;
            public int MinZ;
            public int MaxX;
            public int MaxZ;
            public int AlphaW;
            public int AlphaH;

            public bool IsValid => MaxX >= MinX && MaxZ >= MinZ;

            public UrbanDirtyBounds(int alphaW, int alphaH)
            {
                AlphaW = alphaW;
                AlphaH = alphaH;
                MinX = alphaW;
                MinZ = alphaH;
                MaxX = -1;
                MaxZ = -1;
            }

            public void Expand(int ax, int az, int pad)
            {
                MinX = Mathf.Min(MinX, Mathf.Max(0, ax - pad));
                MinZ = Mathf.Min(MinZ, Mathf.Max(0, az - pad));
                MaxX = Mathf.Max(MaxX, Mathf.Min(AlphaW - 1, ax + pad));
                MaxZ = Mathf.Max(MaxZ, Mathf.Min(AlphaH - 1, az + pad));
            }
        }

        /// <summary>
        ///     Hard-erases remaining urban paint (asphalt/concrete/tile layers,
        ///     indices 13+) on active terrains. Sparse-probes packed alphamap
        ///     textures, then read-modify-writes only the dirty alphamap rect —
        ///     full-tile Get/Set on every tile was ~20s on large MapMagic grids.
        /// </summary>
        /// <returns>Wall-clock ms spent in this sweep.</returns>
        private static long EraseResidualUrbanPaint()
        {
            const int firstUrbanLayer = 13;
            const int probeStep = 8;

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var terrains = Terrain.activeTerrains;
            var touched = 0;
            var probed = 0;

            for (var t = 0; t < terrains.Length; t++)
            {
                var terrain = terrains[t];
                var td = terrain != null ? terrain.terrainData : null;
                if (td == null || td.alphamapLayers <= firstUrbanLayer ||
                    td.alphamapWidth <= 1 || td.alphamapHeight <= 1)
                    continue;

                probed++;
                if (!TryFindUrbanDirtyRect(td, firstUrbanLayer, probeStep,
                        out var xBase, out var zBase, out var dirtyW, out var dirtyH))
                    continue;

                touched++;
                EraseUrbanInAlphaRect(terrain, td, firstUrbanLayer, xBase, zBase, dirtyW, dirtyH);
            }

            Debug.Log(
                "[CityLotTerrainPainter] EraseResidualUrbanPaint probed=" + probed +
                " dirtyTiles=" + touched + " in " + watch.ElapsedMilliseconds + "ms.");
            return watch.ElapsedMilliseconds;
        }

        /// <summary>
        ///     Sparse-scans packed Unity alphamap textures for urban channels,
        ///     then tightens to an exact dirty AABB. Returns false when clean.
        /// </summary>
        private static bool TryFindUrbanDirtyRect(
            TerrainData td, int firstUrbanLayer, int probeStep,
            out int xBase, out int zBase, out int width, out int height)
        {
            xBase = zBase = width = height = 0;
            var packed = td.alphamapTextures;
            if (packed == null || packed.Length == 0)
                return false;

            var bounds = new UrbanDirtyBounds(td.alphamapWidth, td.alphamapHeight);
            var step = Mathf.Max(1, probeStep);
            var firstPacked = firstUrbanLayer / 4;

            for (var pi = firstPacked; pi < packed.Length; pi++)
                ProbePackedTextureForUrban(packed[pi], pi == firstPacked, step, ref bounds);

            if (!bounds.IsValid)
                return false;

            xBase = bounds.MinX;
            zBase = bounds.MinZ;
            width = bounds.MaxX - bounds.MinX + 1;
            height = bounds.MaxZ - bounds.MinZ + 1;
            return true;
        }

        private static void ProbePackedTextureForUrban(
            Texture2D tex, bool skipR, int step, ref UrbanDirtyBounds bounds)
        {
            if (tex == null)
                return;

            var tw = tex.width;
            var th = tex.height;
            if (tw < 1 || th < 1)
                return;

            if (!TryBuildPackedProbeView(tex, tw, th, step, skipR, bounds, out var view))
                return;

            ProbeUrbanPixels(view, ref bounds);
        }

        private struct PackedProbeView
        {
            public Unity.Collections.NativeArray<Color32> Raw;
            public Color32[] Fallback;
            public bool UseRaw;
            public int Width;
            public int Height;
            public int Step;
            public bool SkipR;
            public float ScaleX;
            public float ScaleZ;
        }

        private static float ComputePackedScale(int alphaSize, int texSize)
        {
            if (alphaSize <= 1)
                return 1f;
            return (alphaSize - 1) / (float)Mathf.Max(1, texSize - 1);
        }

        private static bool TryBuildPackedProbeView(
            Texture2D tex, int tw, int th, int step, bool skipR,
            UrbanDirtyBounds bounds, out PackedProbeView view)
        {
            // Prefer raw view — GetPixels32 allocated a full copy per tile and
            // dominated Reset on large MapMagic grids.
            view = new PackedProbeView
            {
                Width = tw,
                Height = th,
                Step = step,
                SkipR = skipR,
                ScaleX = ComputePackedScale(bounds.AlphaW, tw),
                ScaleZ = ComputePackedScale(bounds.AlphaH, th)
            };

            try
            {
                view.Raw = tex.GetRawTextureData<Color32>();
                view.UseRaw = view.Raw.IsCreated && view.Raw.Length >= tw * th;
            }
            catch (System.Exception)
            {
                view.UseRaw = false;
            }

            if (view.UseRaw)
                return true;

            view.Fallback = tex.GetPixels32();
            return view.Fallback != null && view.Fallback.Length >= tw * th;
        }

        private static void ProbeUrbanPixels(PackedProbeView view, ref UrbanDirtyBounds bounds)
        {
            for (var z = 0; z < view.Height; z += view.Step)
            {
                var row = z * view.Width;
                for (var x = 0; x < view.Width; x += view.Step)
                {
                    var c = view.UseRaw ? view.Raw[row + x] : view.Fallback[row + x];
                    if (!IsUrbanPackedColor(c, view.SkipR))
                        continue;

                    var ax = Mathf.Clamp(Mathf.RoundToInt(x * view.ScaleX), 0, bounds.AlphaW - 1);
                    var az = Mathf.Clamp(Mathf.RoundToInt(z * view.ScaleZ), 0, bounds.AlphaH - 1);
                    bounds.Expand(ax, az, view.Step);
                }
            }
        }

        private static bool IsUrbanPackedColor(Color32 c, bool skipR)
        {
            if (c.g > 5 || c.b > 5 || c.a > 5)
                return true;
            return !skipR && c.r > 5;
        }

        private static void EraseUrbanInAlphaRect(
            Terrain terrain, TerrainData td, int firstUrbanLayer,
            int xBase, int zBase, int width, int height)
        {
            var alphas = td.GetAlphamaps(xBase, zBase, width, height);
            var layerCount = td.alphamapLayers;
            var changed = false;

            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (TryEraseUrbanPixel(alphas, layerCount, firstUrbanLayer, z, x))
                        changed = true;
                }
            }

            if (!changed)
                return;

            td.SetAlphamaps(xBase, zBase, alphas);
            SyncAlphamapsToMicroSplat(terrain, alphas, xBase, zBase);
        }

        private static bool TryEraseUrbanPixel(
            float[,,] alphas, int layerCount, int firstUrbanLayer, int z, int x)
        {
            var urban = 0f;
            for (var l = firstUrbanLayer; l < layerCount; l++)
                urban += alphas[z, x, l];
            if (urban <= 0.05f)
                return false;

            var sum = 0f;
            for (var l = 0; l < layerCount; l++)
            {
                var v = ResolveErasedLayerWeight(l, firstUrbanLayer, alphas[z, x, l]);
                alphas[z, x, l] = v;
                sum += v;
            }

            NormalizeAlphamapPixel(alphas, layerCount, z, x, sum);
            return true;
        }

        private static float ResolveErasedLayerWeight(int layer, int firstUrbanLayer, float current)
        {
            if (layer == 0)
                return 1f;
            if (layer < firstUrbanLayer)
                return current;
            return 0f;
        }

        private static void NormalizeAlphamapPixel(
            float[,,] alphas, int layerCount, int z, int x, float sum)
        {
            if (sum <= 0.0001f)
                return;

            var inv = 1f / sum;
            for (var l = 0; l < layerCount; l++)
                alphas[z, x, l] *= inv;
        }
    }
}
