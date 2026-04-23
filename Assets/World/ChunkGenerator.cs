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
        [SerializeField] private bool logTerrainSamples = false;

        public int WorldSeed => worldSeed;

        public void SetWorldSeed(int seed)
        {
            worldSeed = seed;
        }

        public WorldChunk GenerateChunk(Vector2Int coordinates, RegionDefinition region)
        {
            int chunkSeed = ComputeChunkSeed(coordinates);
            WorldChunk chunk = new WorldChunk(coordinates, chunkSeed, region?.RegionId ?? "Unknown");

            GenerateTerrain(chunk, region);
            SpawnBuildings(chunk, region);
            SpawnZombies(chunk, region);
            SpawnLoot(chunk, region);

            return chunk;
        }

        /// <summary>Async variant — useful for background streaming on large worlds.</summary>
        public System.Collections.IEnumerator GenerateChunkAsync(Vector2Int coordinates, RegionDefinition region, System.Action<WorldChunk> onComplete)
        {
            yield return null; // yield to prevent hitching on first frame of generation
            WorldChunk chunk = GenerateChunk(coordinates, region);
            onComplete?.Invoke(chunk);
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
            // Uses Mathf.PerlinNoise seeded by the chunk seed to compute a height value.
            // In production the height map would drive terrain tile placement.
            float heightSample = Mathf.PerlinNoise(
                (chunk.Coordinates.x + chunk.Seed % 1000) * 0.1f,
                (chunk.Coordinates.y + chunk.Seed % 1000) * 0.1f);

            float biomeScale = region?.BaseDifficulty ?? 1f;
            
            // Note: heightSample and biomeScale would be used here to drive tile/mesh generation.
            // For now, we suppress unused warnings as this is a placeholder for actual terrain generation.
            if (logTerrainSamples)
            {
                UnityEngine.Debug.Log($"Terrain for {chunk.Coordinates}: sample={heightSample}, scale={biomeScale}");
            }
        }

        private void SpawnBuildings(WorldChunk chunk, RegionDefinition region)
        {
            // Determines building count from region density; each anchor is a candidate
            // building placement recorded in SpawnedEntityIds for deferred instantiation.
            float density = region?.RegionData != null ? Mathf.Clamp01(region.RegionData.Difficulty * 0.5f) : 0.3f;
            int maxBuildings = Mathf.RoundToInt(density * 4);
            System.Random rng = new System.Random(chunk.Seed ^ 0xBEEF);

            for (int i = 0; i < maxBuildings; i++)
            {
                int ox = rng.Next(0, chunkGenerationSteps);
                int oy = rng.Next(0, chunkGenerationSteps);
                chunk.SpawnedEntityIds.Add($"building_{chunk.Coordinates.x}_{chunk.Coordinates.y}_{i}_@{ox},{oy}");
            }
        }

        private void SpawnZombies(WorldChunk chunk, RegionDefinition region)
        {
            float difficulty = region?.GetDifficulty() ?? 1f;
            int baseCount = Mathf.RoundToInt(difficulty * 5f);
            System.Random rng = new System.Random(chunk.Seed ^ 0xDEAD);

            for (int i = 0; i < baseCount; i++)
            {
                chunk.SpawnedEntityIds.Add($"zombie_{chunk.Coordinates.x}_{chunk.Coordinates.y}_{i}_t{rng.Next(0, 3)}");
            }
        }

        private void SpawnLoot(WorldChunk chunk, RegionDefinition region)
        {
            // Registers loot container seeds so items are generated lazily on container open.
            float lootFactor = region?.RegionData != null ? region.RegionData.Difficulty : 1f;
            int containerCount = Mathf.Max(1, Mathf.RoundToInt(lootFactor * 3f));
            System.Random rng = new System.Random(chunk.Seed ^ 0xF00D);

            for (int i = 0; i < containerCount; i++)
            {
                int lootRoll = rng.Next(0, int.MaxValue);
                chunk.SpawnedEntityIds.Add($"loot_{chunk.Coordinates.x}_{chunk.Coordinates.y}_{i}_seed{lootRoll}");
            }
        }

        [SerializeField, Min(1)] private int chunkGenerationSteps = 8;
    }

    /// <summary>
    /// Lightweight runtime representation of a streamed world chunk.
    /// </summary>
    [Serializable]
    public sealed class WorldChunk
    {
        [field: SerializeField] public Vector2Int Coordinates { get; private set; }
        [field: SerializeField] public int Seed { get; private set; }
        [field: SerializeField] public string RegionId { get; private set; }
        [field: SerializeField] public bool IsDirty { get; set; }
        public List<string> SpawnedEntityIds { get; } = new List<string>();

        public WorldChunk(Vector2Int coordinates, int seed, string regionId)
        {
            Coordinates = coordinates;
            Seed = seed;
            RegionId = regionId;
            IsDirty = false;
        }
    }
}