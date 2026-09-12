#if UNITY_EDITOR
using System.Diagnostics;
using UnityEditor;
using Debug = UnityEngine.Debug;
#endif
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Collapses the currently-open editor undo group in chunks while a long
    ///     road build runs. Unity's undo collapse over a huge group (especially
    ///     one containing TerrainData snapshots) can take seconds when performed
    ///     once at the end of a pipeline step; collapsing at phase boundaries
    ///     keeps each merge small and keeps the final pipeline-step collapse cheap.
    /// </summary>
    internal static class CityBuildUndoChunker
    {
        internal static void CollapseAfter(string phase)
        {
#if UNITY_EDITOR
            var sw = Stopwatch.StartNew();
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            sw.Stop();
            if (sw.ElapsedMilliseconds > 10)
                Debug.Log("[CityBuildUndoChunker] Collapsed undo group after '" + phase +
                          "': " + sw.ElapsedMilliseconds + "ms");
#endif
        }
    }
}
