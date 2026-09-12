using System.Threading.Tasks;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Parallelism switches for tile workers (avoid nested Parallel.For thrash).</summary>
    public static partial class AlphamapSoftBlendUtility
    {
        /// <summary>
        /// When true on the current thread, row/column loops run serially and soften/upscale
        /// use thread-local scratch (safe for parallel tile paint).
        /// </summary>
        [System.ThreadStatic]
        private static bool _tileWorkerMode;

        public static bool TileWorkerMode
        {
            get => _tileWorkerMode;
            set => _tileWorkerMode = value;
        }

        private static void ForRows(int count, System.Action<int> body)
        {
            if (count <= 0 || body == null)
                return;
            if (_tileWorkerMode)
            {
                for (var i = 0; i < count; i++)
                    body(i);
                return;
            }

            Parallel.For(0, count, body);
        }
    }
}
