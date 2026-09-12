using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Aggregated paint-phase timings for hub perf diagnosis.</summary>
    public sealed partial class WorldSurfacePainter
    {
        private static readonly ProfilerMarker PaintLoopMarker = new("WorldSurfacePainter.PaintLoop");
        private static readonly ProfilerMarker UpscaleMarker = new("WorldSurfacePainter.Upscale");
        private static readonly ProfilerMarker SoftenMarker = new("WorldSurfacePainter.Soften");
        private static readonly ProfilerMarker SetAlphamapMarker = new("WorldSurfacePainter.SetAlphamaps");

        private long _paintLoopMs;
        private long _paintUpscaleMs;
        private long _paintSoftenMs;
        private long _paintSetAlphamapMs;
        private long _paintBindMs;
        private long _paintTileCount;
        private readonly Stopwatch _paintPhaseWatch = new();

        public void ResetPaintTimingAccumulators()
        {
            _paintLoopMs = 0;
            _paintUpscaleMs = 0;
            _paintSoftenMs = 0;
            _paintSetAlphamapMs = 0;
            _paintBindMs = 0;
            _paintTileCount = 0;
        }

        public void AddBindTimingMs(long ms) => _paintBindMs += ms;

        public void LogPaintTimingSummary(string context)
        {
            if (_paintTileCount <= 0 && _paintBindMs <= 0)
                return;

            Debug.Log(
                "[WorldSurfacePainter] paintTiming context=" + context +
                " mode=" + _configuredPaintMode +
                " tiles=" + _paintTileCount +
                " cellPaint=" + _useCellResolutionPaint +
                " coarseRes=" + _coarsePaintResolution +
                " fastSampling=" + _fastPaintSampling +
                " stride=" + _paintTexelStride +
                " skipSoften=" + _skipAlphamapSoften +
                " parallelTiles=batch" +
                " bind=" + _paintBindMs + "ms" +
                " paintLoop=" + _paintLoopMs + "ms" +
                " upscale=" + _paintUpscaleMs + "ms" +
                " soften=" + _paintSoftenMs + "ms" +
                " setAlphamap=" + _paintSetAlphamapMs + "ms" +
                " sum=" + (_paintBindMs + _paintLoopMs + _paintUpscaleMs + _paintSoftenMs + _paintSetAlphamapMs) + "ms");
        }

        private void BeginPaintPhase()
        {
            _paintPhaseWatch.Restart();
        }

        private long EndPaintPhaseMs()
        {
            _paintPhaseWatch.Stop();
            return _paintPhaseWatch.ElapsedMilliseconds;
        }
    }
}
