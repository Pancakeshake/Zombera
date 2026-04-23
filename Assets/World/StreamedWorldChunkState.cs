using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;

namespace Zombera.World
{
    /// <summary>
    /// In-memory procedural chunk deltas (cleared markers, entity id snapshots) merged into <see cref="GameSaveData"/>.
    /// </summary>
    public static class StreamedWorldChunkState
    {
        private static readonly Dictionary<Vector2Int, ChunkProceduralDeltaSaveData> PendingDeltas =
            new Dictionary<Vector2Int, ChunkProceduralDeltaSaveData>();

        public static void Clear()
        {
            PendingDeltas.Clear();
        }

        public static void ImportFromSave(ProceduralWorldSaveData data)
        {
            Clear();

            if (data == null || data.chunkDeltas == null)
            {
                return;
            }

            for (int i = 0; i < data.chunkDeltas.Count; i++)
            {
                ChunkProceduralDeltaSaveData delta = data.chunkDeltas[i];
                if (delta == null)
                {
                    continue;
                }

                Vector2Int key = new Vector2Int(delta.chunkX, delta.chunkZ);
                PendingDeltas[key] = CloneDelta(delta);
            }
        }

        /// <summary>Stores a snapshot of a dirty chunk for the next save (unload or autosave).</summary>
        public static void CaptureChunkState(WorldChunk chunk, string notes)
        {
            if (chunk == null || !chunk.IsDirty)
            {
                return;
            }

            ChunkProceduralDeltaSaveData delta = new ChunkProceduralDeltaSaveData
            {
                chunkX = chunk.Coordinates.x,
                chunkZ = chunk.Coordinates.y,
                cleared = false,
                notes = notes ?? string.Empty,
                entityIds = new List<string>(chunk.SpawnedEntityIds != null ? chunk.SpawnedEntityIds : new List<string>())
            };

            PendingDeltas[chunk.Coordinates] = delta;
        }

        public static void MergeIntoSave(GameSaveData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            if (saveData.ProceduralWorld == null)
            {
                saveData.ProceduralWorld = new ProceduralWorldSaveData();
            }

            saveData.ProceduralWorld.chunkDeltas.Clear();

            foreach (KeyValuePair<Vector2Int, ChunkProceduralDeltaSaveData> kvp in PendingDeltas)
            {
                saveData.ProceduralWorld.chunkDeltas.Add(CloneDelta(kvp.Value));
            }
        }

        public static void TryApplyToChunk(WorldChunk chunk)
        {
            if (chunk == null)
            {
                return;
            }

            if (!PendingDeltas.TryGetValue(chunk.Coordinates, out ChunkProceduralDeltaSaveData delta) || delta == null)
            {
                return;
            }

            if (delta.cleared)
            {
                chunk.SpawnedEntityIds?.Clear();
                return;
            }

            if (delta.entityIds == null || delta.entityIds.Count == 0)
            {
                return;
            }

            if (chunk.SpawnedEntityIds == null)
            {
                return;
            }

            for (int i = 0; i < delta.entityIds.Count; i++)
            {
                string id = delta.entityIds[i];

                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                if (!chunk.SpawnedEntityIds.Contains(id))
                {
                    chunk.SpawnedEntityIds.Add(id);
                }
            }
        }

        private static ChunkProceduralDeltaSaveData CloneDelta(ChunkProceduralDeltaSaveData source)
        {
            if (source == null)
            {
                return new ChunkProceduralDeltaSaveData();
            }

            ChunkProceduralDeltaSaveData copy = new ChunkProceduralDeltaSaveData
            {
                chunkX = source.chunkX,
                chunkZ = source.chunkZ,
                cleared = source.cleared,
                notes = source.notes,
                entityIds = new List<string>()
            };

            if (source.entityIds != null)
            {
                copy.entityIds.AddRange(source.entityIds);
            }

            return copy;
        }
    }
}
