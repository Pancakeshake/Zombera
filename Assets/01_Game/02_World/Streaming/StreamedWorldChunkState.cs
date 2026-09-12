#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;

#endregion

// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace Zombera.World
{
    /// <summary>
    ///     In-memory procedural chunk deltas (cleared markers, entity id snapshots) merged into <see cref="GameSaveData" />.
    /// </summary>
    public static class StreamedWorldChunkState
    {
        private static readonly Dictionary<Vector2Int, ChunkProceduralDeltaSaveData> PendingDeltas = new();

        public static void Clear()
        {
            PendingDeltas.Clear();
        }

        public static void ImportFromSave(ProceduralWorldSaveData data)
        {
            if (data?.chunkDeltas is not { } chunkDeltas)
            {
                Clear();
                return;
            }

            Clear();

            foreach (var delta in chunkDeltas)
            {
                if (delta == null) continue;

                var key = new Vector2Int(delta.chunkX, delta.chunkZ);
                PendingDeltas[key] = CloneDelta(delta);
            }
        }

        /// <summary>Stores a snapshot of a dirty chunk for the next save (unload or autosave).</summary>
        public static void CaptureChunkState(WorldChunk chunk, string notes)
        {
            if (chunk is not { IsDirty: true }) return;

            var delta = new ChunkProceduralDeltaSaveData
            {
                chunkX = chunk.Coordinates.x,
                chunkZ = chunk.Coordinates.y,
                cleared = false,
                notes = notes ?? string.Empty,
                entityIds = new List<string>(chunk.SpawnedEntityIds ?? new List<string>())
            };

            PendingDeltas[chunk.Coordinates] = delta;
        }

        public static void MergeIntoSave(GameSaveData saveData)
        {
            if (saveData == null) return;

            saveData.proceduralWorld ??= new ProceduralWorldSaveData();

            saveData.proceduralWorld.chunkDeltas.Clear();

            foreach (var kvp in PendingDeltas) saveData.proceduralWorld.chunkDeltas.Add(CloneDelta(kvp.Value));
        }

        public static void TryApplyToChunk(WorldChunk chunk)
        {
            if (chunk == null) return;

            if (!PendingDeltas.TryGetValue(chunk.Coordinates, out var delta) || delta is null) return;

            if (delta.cleared)
            {
                chunk.SpawnedEntityIds?.Clear();
                return;
            }

            if (chunk.SpawnedEntityIds is null || delta.entityIds is not { Count: > 0 }) return;

            foreach (var id in delta.entityIds)
            {
                if (string.IsNullOrEmpty(id)) continue;

                if (!chunk.SpawnedEntityIds.Contains(id)) chunk.SpawnedEntityIds.Add(id);
            }
        }

        private static ChunkProceduralDeltaSaveData CloneDelta(ChunkProceduralDeltaSaveData source)
        {
            if (source == null) return new ChunkProceduralDeltaSaveData();

            var copy = new ChunkProceduralDeltaSaveData
            {
                chunkX = source.chunkX,
                chunkZ = source.chunkZ,
                cleared = source.cleared,
                notes = source.notes,
                entityIds = new List<string>()
            };

            if (source.entityIds != null) copy.entityIds.AddRange(source.entityIds);

            return copy;
        }
    }
}