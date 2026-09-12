#region

using System;
using UnityEngine;

#endregion

namespace Zombera.World
{
    /// <summary>
    ///     Lightweight runtime counters for streamed world tiles + chunk systems (debug HUD / soak logs).
    ///     First-party WorldBuilder writes ActiveStreamedTiles / ContentReadyTiles / TileAppliedForGameplayEvents.
    ///     MapMagic-named members remain as compatibility aliases.
    /// </summary>
    public static class StreamedWorldMetrics
    {
        public static int ActiveStreamedTiles { get; private set; }
        public static int ContentReadyTiles { get; private set; }
        public static int TileAppliedForGameplayEvents { get; private set; }
        public static int InitialPlayAreaCompleteEvents { get; private set; }

        public static int ActiveMapMagicTiles => ActiveStreamedTiles;
        public static int MapMagicTileAppliedEvents => TileAppliedForGameplayEvents;
        public static int MapMagicAllCompleteEvents => InitialPlayAreaCompleteEvents;

        public static int ChunksLoadedThisSession { get; private set; }
        public static int ChunksUnloadedThisSession { get; private set; }
        public static float LastChunkLoadMilliseconds { get; private set; }
        public static float LastNavMeshBakeMilliseconds { get; private set; }
        public static int NavMeshBakeCount { get; private set; }

        public static void ResetSession()
        {
            ActiveStreamedTiles = 0;
            ContentReadyTiles = 0;
            TileAppliedForGameplayEvents = 0;
            InitialPlayAreaCompleteEvents = 0;
            ChunksLoadedThisSession = 0;
            ChunksUnloadedThisSession = 0;
            LastChunkLoadMilliseconds = 0f;
            LastNavMeshBakeMilliseconds = 0f;
            NavMeshBakeCount = 0;
        }

        public static void SetActiveStreamedTiles(int count)
        {
            ActiveStreamedTiles = Mathf.Max(0, count);
        }

        public static void SetContentReadyTiles(int count)
        {
            ContentReadyTiles = Mathf.Max(0, count);
        }

        public static void RecordTileAppliedForGameplay()
        {
            TileAppliedForGameplayEvents++;
        }

        public static void RecordInitialPlayAreaComplete()
        {
            InitialPlayAreaCompleteEvents++;
        }

        public static void SetActiveMapMagicTiles(int count) => SetActiveStreamedTiles(count);

        public static void RecordMapMagicTileApplied() => RecordTileAppliedForGameplay();

        public static void RecordMapMagicAllComplete() => RecordInitialPlayAreaComplete();

        public static void RecordChunkLoaded(TimeSpan elapsed)
        {
            ChunksLoadedThisSession++;
            LastChunkLoadMilliseconds = (float)elapsed.TotalMilliseconds;
        }

        public static void RecordChunkUnloaded()
        {
            ChunksUnloadedThisSession++;
        }

        // ReSharper disable once UnusedMember.Global
        public static void RecordNavMeshBakeMilliseconds(float ms)
        {
            LastNavMeshBakeMilliseconds = ms;
            NavMeshBakeCount++;
        }
    }
}