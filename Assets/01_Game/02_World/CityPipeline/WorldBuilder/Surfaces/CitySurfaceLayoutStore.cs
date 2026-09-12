using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Thread-safe registry of planned city layouts. Runtime services register
    ///     layouts on the main thread before tiles are requested; the MapMagic graph
    ///     node and future runtime builders read them from worker threads during
    ///     tile bake / generation.
    /// </summary>
    public static class CitySurfaceLayoutStore
    {
        private static readonly object Gate = new object();
        private static readonly List<CitySurfaceLayout> Layouts = new List<CitySurfaceLayout>();

        public static int Count
        {
            get
            {
                lock (Gate)
                    return Layouts.Count;
            }
        }

        /// <summary>Registers a layout, replacing any previous layout with the same seed.</summary>
        public static void Register(CitySurfaceLayout layout)
        {
            if (layout == null)
                return;

            lock (Gate)
            {
                for (var i = 0; i < Layouts.Count; i++)
                {
                    if (Layouts[i].seed != layout.seed)
                        continue;
                    Layouts[i] = layout;
                    return;
                }

                Layouts.Add(layout);
            }
        }

        public static void Unregister(int seed)
        {
            lock (Gate)
            {
                for (var i = 0; i < Layouts.Count; i++)
                {
                    if (Layouts[i].seed == seed)
                    {
                        Layouts.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        public static void Clear()
        {
            lock (Gate)
                Layouts.Clear();
        }

        public static CitySurfaceLayout Find(int seed)
        {
            lock (Gate)
            {
                for (var i = 0; i < Layouts.Count; i++)
                {
                    if (Layouts[i].seed == seed)
                        return Layouts[i];
                }
            }

            return null;
        }

        /// <summary>Copies every zone overlapping <paramref name="worldRect"/> into <paramref name="results"/>.</summary>
        public static void CollectZones(Rect worldRect, List<CitySurfaceZone> results)
        {
            lock (Gate)
            {
                for (var l = 0; l < Layouts.Count; l++)
                {
                    var zones = Layouts[l].zones;
                    for (var z = 0; z < zones.Count; z++)
                    {
                        var bounds = zones[z].bounds;
                        if (worldRect.Overlaps(bounds))
                            results.Add(zones[z]);
                    }
                }
            }
        }
    }
}
