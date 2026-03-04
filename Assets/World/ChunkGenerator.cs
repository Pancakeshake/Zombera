using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World
{
    /// <summary>
    /// Generates deterministic chunk content: terrain, structures, zombies, and loot anchors.
    /// </summary>
    public sealed class ChunkGenerator : MonoBehaviour
    {
        [SerializeField] private int worldSeed = 12345;

        public WorldChunk GenerateChunk(Vector2Int coordinates, RegionDefinition region)
        {
            int chunkSeed = ComputeChunkSeed(coordinates);
            WorldChunk chunk = new WorldChunk(coordinates, chunkSeed, region != null ? region.RegionId : "Unknown");

            GenerateTerrain(chunk, region);
            SpawnBuildings(chunk, region);
            SpawnZombies(chunk, region);
            SpawnLoot(chunk, region);

            // TODO: Integrate async generation path for large-scale streaming.

            return chunk;
        }

        public int ComputeChunkSeed(Vector2Int coordinates)
        {
            unchecked
            {
                int hash = worldSeed;
                hash = (hash * 397) ^ coordinates.x;
                hash = (hash * 397) ^ coordinates.y;
                return hash;
            }
        }

        private void GenerateTerrain(WorldChunk chunk, RegionDefinition region)
        {
            // TODO: Use noise/heightmap strategy per biome/region.
            _ = chunk;
            _ = region;
        }

        private void SpawnBuildings(WorldChunk chunk, RegionDefinition region)
        {
            // TODO: Place building anchors from region density rules.
            _ = chunk;
            _ = region;
        }

        private void SpawnZombies(WorldChunk chunk, RegionDefinition region)
        {
            // TODO: Spawn baseline zombie populations by difficulty/time.
            _ = chunk;
            _ = region;
        }

        private void SpawnLoot(WorldChunk chunk, RegionDefinition region)
        {
            // TODO: Register loot container seeds (generate items on open).
            _ = chunk;
            _ = region;
        }
    }

    /// <summary>
    /// Lightweight runtime representation of a streamed world chunk.
    /// </summary>
    [Serializable]
    public sealed class WorldChunk
    {
        public Vector2Int Coordinates;
        public int Seed;
        public string RegionId;
        public bool IsDirty;
        public List<string> SpawnedEntityIds;

        public WorldChunk(Vector2Int coordinates, int seed, string regionId)
        {
            Coordinates = coordinates;
            Seed = seed;
            RegionId = regionId;
            IsDirty = false;
            SpawnedEntityIds = new List<string>();
        }
    }
}