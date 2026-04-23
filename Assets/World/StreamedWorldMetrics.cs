using UnityEngine;

namespace Zombera.World
{
    /// <summary>
    /// Lightweight runtime counters for streamed MapMagic + chunk systems (debug HUD / soak logs).
    /// </summary>
    public static class StreamedWorldMetrics
    {
        public static int ActiveMapMagicTiles { get; private set; }
        public static int MapMagicTileAppliedEvents { get; private set; }
        public static int MapMagicAllCompleteEvents { get; private set; }
        public static int ChunksLoadedThisSession { get; private set; }
        public static int ChunksUnloadedThisSession { get; private set; }
        public static float LastChunkLoadMilliseconds { get; private set; }
        public static float LastNavMeshBakeMilliseconds { get; private set; }
        public static int NavMeshBakeCount { get; private set; }

        public static void ResetSession()
        {
            ActiveMapMagicTiles = 0;
            MapMagicTileAppliedEvents = 0;
            MapMagicAllCompleteEvents = 0;
            ChunksLoadedThisSession = 0;
            ChunksUnloadedThisSession = 0;
            LastChunkLoadMilliseconds = 0f;
            LastNavMeshBakeMilliseconds = 0f;
            NavMeshBakeCount = 0;
        }

        public static void SetActiveMapMagicTiles(int count)
        {
            ActiveMapMagicTiles = Mathf.Max(0, count);
        }

        public static void RecordMapMagicTileApplied()
        {
            MapMagicTileAppliedEvents++;
        }

        public static void RecordMapMagicAllComplete()
        {
            MapMagicAllCompleteEvents++;
        }

        public static void RecordChunkLoaded(System.TimeSpan elapsed)
        {
            ChunksLoadedThisSession++;
            LastChunkLoadMilliseconds = (float)elapsed.TotalMilliseconds;
        }

        public static void RecordChunkUnloaded()
        {
            ChunksUnloadedThisSession++;
        }

        public static void RecordNavMeshBakeMilliseconds(float ms)
        {
            LastNavMeshBakeMilliseconds = ms;
            NavMeshBakeCount++;
        }
    }
}
