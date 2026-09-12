#region

using System;
using System.Diagnostics;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#endregion

namespace Zombera.Debugging
{
    /// <summary>
    ///     Lightweight, opt-in performance tracing for world/session startup.
    ///     Uses Profiler markers (visible in Unity Profiler) and optional logs (visible in Console/player log).
    /// </summary>
    public static class PerfTrace
    {
        /// <summary>
        ///     Toggle startup timing logs without changing callsites.
        /// </summary>
        public static bool Enabled { get; set; }

        public static Scope Measure(string label, Object context = null, bool log = true)
        {
            return new Scope(label, context, log);
        }

        public readonly struct Scope : IDisposable
        {
            private readonly string _label;
            private readonly Object _context;
            private readonly bool _log;
            private readonly long _startTicks;

            public Scope(string label, Object context, bool log)
            {
                _label = string.IsNullOrWhiteSpace(label) ? "PerfTrace" : label;
                _context = context;
                _log = log;
                _startTicks = Stopwatch.GetTimestamp();
                Profiler.BeginSample(_label, _context);
            }

            public void Dispose()
            {
                Profiler.EndSample();

                if (!Enabled || !_log) return;

                var end = Stopwatch.GetTimestamp();
                var ms = (end - _startTicks) * 1000.0 / Stopwatch.Frequency;
                Debug.Log($"[PerfTrace] {_label} took {ms:0.0} ms", _context);
            }
        }
    }
}