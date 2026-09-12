using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Batched parallel tile CPU paint; SetAlphamaps stays on the main thread.</summary>
    public sealed partial class WorldSurfacePainter
    {
        /// <summary>Main-thread-captured tile inputs for worker CPU paint.</summary>
        public readonly struct NaturalSurfaceCpuJob
        {
            public readonly Terrain Terrain;
            public readonly TerrainData Data;
            public readonly Vector3 Origin;
            public readonly Vector3 Size;
            public readonly int Width;
            public readonly int Height;
            public readonly int Layers;
            public readonly LandformField Landforms;
            public readonly HydrologyPlan Water;
            public readonly BiomeField Biomes;
            public readonly float SeaLevel;
            public readonly List<WorldBiomeRecord> NaturalRecords;

            public NaturalSurfaceCpuJob(
                Terrain terrain,
                TerrainData data,
                Vector3 origin,
                Vector3 size,
                int width,
                int height,
                int layers,
                LandformField landforms,
                HydrologyPlan water,
                BiomeField biomes,
                float seaLevel,
                List<WorldBiomeRecord> naturalRecords)
            {
                Terrain = terrain;
                Data = data;
                Origin = origin;
                Size = size;
                Width = width;
                Height = height;
                Layers = layers;
                Landforms = landforms;
                Water = water;
                Biomes = biomes;
                SeaLevel = seaLevel;
                NaturalRecords = naturalRecords;
            }
        }

        /// <summary>CPU-filled alphamap waiting for main-thread SetAlphamaps.</summary>
        public struct NaturalSurfacePendingApply
        {
            public Terrain Terrain;
            public TerrainData Data;
            public float[,,] Map;
            public int Width;
            public int Height;
            public long LoopMs;
            public long SoftenMs;
            public long UpscaleMs;
        }

        [ThreadStatic] private static float[,,] _tlsAlphamap;
        [ThreadStatic] private static float[,,] _tlsAlphamapSoft;
        [ThreadStatic] private static float[,,] _tlsCoarse;
        [ThreadStatic] private static float[,,] _tlsCoarseSoft;
        [ThreadStatic] private static int _tlsW;
        [ThreadStatic] private static int _tlsH;
        [ThreadStatic] private static int _tlsL;
        [ThreadStatic] private static int _tlsCw;
        [ThreadStatic] private static int _tlsCh;
        [ThreadStatic] private static int _tlsCl;

        /// <summary>Builds a CPU job from a live terrain (main thread only).</summary>
        public bool TryCreateNaturalSurfaceCpuJob(
            WorldTileInfo tile,
            LandformField landforms,
            HydrologyPlan water,
            BiomeField biomes,
            out NaturalSurfaceCpuJob job)
        {
            job = default;
            if (!WorldTileInfoUtility.TryGetLiveTerrain(tile, out var terrain))
                return false;
            if (landforms == null || biomes == null || _palette == null)
                return false;

            var data = terrain.terrainData;
            if (data == null)
                return false;
            var layers = data.alphamapLayers;
            if (layers <= 0)
                return false;

            float seaLevel;
            if (_cachedSeaLevelPlan == water)
                seaLevel = _cachedSeaLevel;
            else
            {
                seaLevel = EstimateSeaLevel(water);
                _cachedSeaLevel = seaLevel;
                _cachedSeaLevelPlan = water;
            }

            var naturalRecords = _cachedNaturalRecords ??= CollectNaturalBiomeRecords(_biomePalette);
            job = new NaturalSurfaceCpuJob(
                terrain,
                data,
                terrain.transform.position,
                data.size,
                data.alphamapWidth,
                data.alphamapHeight,
                layers,
                landforms,
                water,
                biomes,
                seaLevel,
                naturalRecords);
            return true;
        }

        /// <summary>
        /// Prebinds natural-record biome indices for parallel tile workers (main thread).
        /// </summary>
        public void EnsureNaturalRecordIndicesForParallel(BiomeField biomes)
        {
            var naturalRecords = _cachedNaturalRecords ??= CollectNaturalBiomeRecords(_biomePalette);
            EnsureNaturalRecordIndices(biomes, naturalRecords);
        }

        /// <summary>
        /// Worker-safe paint into <paramref name="map"/> (no Unity SetAlphamaps).
        /// Caller must pass a dedicated buffer (TLS rentals are not visible across threads).
        /// Session landform sample cache must already be prepared on the main thread.
        /// </summary>
        public NaturalSurfacePendingApply PaintNaturalSurfaceCpu(in NaturalSurfaceCpuJob job, float[,,] map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            AlphamapSoftBlendUtility.TileWorkerMode = true;
            try
            {
                var args = new NaturalSurfacePaintArgs
                {
                    Terrain = job.Terrain,
                    Data = job.Data,
                    Map = map,
                    Width = job.Width,
                    Height = job.Height,
                    Layers = job.Layers,
                    Origin = job.Origin,
                    Size = job.Size,
                    Landforms = job.Landforms,
                    Water = job.Water,
                    Biomes = job.Biomes,
                    SeaLevel = job.SeaLevel,
                    NaturalRecords = job.NaturalRecords
                };

                long loopMs;
                long softenMs;
                long upscaleMs;
                if (_useCellResolutionPaint && job.Landforms.CellSize > 0.01f)
                    PaintCellResolutionCpu(in args, out loopMs, out softenMs, out upscaleMs);
                else
                    PaintAlphamapResolutionCpu(in args, out loopMs, out softenMs, out upscaleMs);

                return new NaturalSurfacePendingApply
                {
                    Terrain = job.Terrain,
                    Data = job.Data,
                    Map = map,
                    Width = job.Width,
                    Height = job.Height,
                    LoopMs = loopMs,
                    SoftenMs = softenMs,
                    UpscaleMs = upscaleMs
                };
            }
            finally
            {
                AlphamapSoftBlendUtility.TileWorkerMode = false;
            }
        }

        /// <summary>Main-thread SetAlphamaps + dirty mark + timing accumulate.</summary>
        public void ApplyNaturalSurfaceAlphamap(in NaturalSurfacePendingApply pending)
        {
            if (pending.Data == null || pending.Map == null)
                return;

            var sw = Stopwatch.StartNew();
            pending.Data.SetAlphamaps(0, 0, pending.Map);
            sw.Stop();
            Interlocked.Add(ref _paintSetAlphamapMs, sw.ElapsedMilliseconds);
            Interlocked.Add(ref _paintLoopMs, pending.LoopMs);
            Interlocked.Add(ref _paintSoftenMs, pending.SoftenMs);
            Interlocked.Add(ref _paintUpscaleMs, pending.UpscaleMs);
            Interlocked.Increment(ref _paintTileCount);
            MarkOrSyncPaintedTile(pending.Terrain, pending.Width, pending.Height);
        }

        private float[,,] RentAlphamapBufferThreadAware(int width, int height, int layers)
        {
            if (!AlphamapSoftBlendUtility.TileWorkerMode)
                return RentAlphamapBuffer(width, height, layers);
            EnsureTlsAlphamap(width, height, layers);
            return _tlsAlphamap;
        }

        private float[,,] RentAlphamapSoftScratchThreadAware(int width, int height, int layers)
        {
            if (!AlphamapSoftBlendUtility.TileWorkerMode)
                return RentAlphamapSoftScratch(width, height, layers);
            EnsureTlsAlphamap(width, height, layers);
            return _tlsAlphamapSoft;
        }

        private float[,,] RentCoarseAlphamapBufferThreadAware(int width, int height, int layers)
        {
            if (!AlphamapSoftBlendUtility.TileWorkerMode)
                return RentCoarseAlphamapBuffer(width, height, layers);
            EnsureTlsCoarse(width, height, layers);
            return _tlsCoarse;
        }

        private float[,,] RentCoarseAlphamapSoftScratchThreadAware(int width, int height, int layers)
        {
            if (!AlphamapSoftBlendUtility.TileWorkerMode)
                return RentCoarseAlphamapSoftScratch(width, height, layers);
            EnsureTlsCoarse(width, height, layers);
            return _tlsCoarseSoft;
        }

        private static void EnsureTlsAlphamap(int width, int height, int layers)
        {
            if (_tlsAlphamap != null && _tlsW == width && _tlsH == height && _tlsL == layers)
                return;
            _tlsAlphamap = new float[height, width, layers];
            _tlsAlphamapSoft = new float[height, width, layers];
            _tlsW = width;
            _tlsH = height;
            _tlsL = layers;
        }

        private static void EnsureTlsCoarse(int width, int height, int layers)
        {
            if (_tlsCoarse != null && _tlsCw == width && _tlsCh == height && _tlsCl == layers)
                return;
            _tlsCoarse = new float[height, width, layers];
            _tlsCoarseSoft = new float[height, width, layers];
            _tlsCw = width;
            _tlsCh = height;
            _tlsCl = layers;
        }
    }
}
