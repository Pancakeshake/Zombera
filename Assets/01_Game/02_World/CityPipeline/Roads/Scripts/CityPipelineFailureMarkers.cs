using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Runtime-safe registry of pipeline failure markers. Generation steps
    ///     record failed placements here; the editor pipeline runner draws them
    ///     in the Scene view so failures are visible as they happen.
    /// </summary>
    public static class CityPipelineFailureMarkers
    {
        public struct Marker
        {
            public Vector3 Position;
            public string Reason;
        }

        private static readonly List<Marker> Markers = new();

        public static IReadOnlyList<Marker> All => Markers;

        public static void Clear() => Markers.Clear();

        public static void Add(Vector3 position, string reason)
        {
            Markers.Add(new Marker { Position = position, Reason = reason });
        }
    }
}
