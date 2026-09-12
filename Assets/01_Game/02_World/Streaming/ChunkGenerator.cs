#region

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;
using Random = System.Random;

#endregion

namespace Zombera.World
{
    /// <summary>
    ///     Generates deterministic chunk content: terrain, structures, zombies, and loot anchors.
    /// </summary>
    public sealed class ChunkGenerator : MonoBehaviour
    {
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private bool logTerrainSamples;

        [SerializeField] [Min(1)] private int chunkGenerationSteps = 8;

        // ReSharper disable once UnusedMember.Global
        public int WorldSeed => worldSeed;

        private void OnEnable() => WorldSeedFallback.Register(worldSeed);

        private void OnDisable()
        {
            if (WorldSeedFallback.RegisteredSeed == worldSeed)
                WorldSeedFallback.Clear();
        }

        public void SetWorldSeed(int seed)
        {
            worldSeed = seed;
            WorldSeedFallback.Register(worldSeed);
        }

        public WorldChunk GenerateChunk(Vector2Int coordinates, RegionDefinition region)
        {
            var chunkSeed = ComputeChunkSeed(coordinates);
            var chunk = new WorldChunk(coordinates, chunkSeed, region?.RegionId ?? "Unknown");

            GenerateTerrain(chunk, region);
            SpawnBuildings(chunk, region);
            SpawnZombies(chunk, region);
            SpawnLoot(chunk, region);

            return chunk;
        }

        /// <summary>Async variant — useful for background streaming on large worlds.</summary>
        // ReSharper disable once UnusedMember.Global
        public IEnumerator GenerateChunkAsync(Vector2Int coordinates, RegionDefinition region,
            Action<WorldChunk> onComplete)
        {
            yield return null; // yield to prevent hitching on first frame of generation
            var chunk = GenerateChunk(coordinates, region);
            onComplete?.Invoke(chunk);
        }

        public int ComputeChunkSeed(Vector2Int coordinates)
        {
            unchecked
            {
                var hash = worldSeed;
                hash = (hash * 397) ^ coordinates.x;
                hash = (hash * 397) ^ coordinates.y;
                return hash;
            }
        }

        private void GenerateTerrain(WorldChunk chunk, RegionDefinition region)
        {
            // Uses Mathf.PerlinNoise seeded by the chunk seed to compute a height value.
            // In production the height map would drive terrain tile placement.
            var heightSample = Mathf.PerlinNoise(
                (chunk.Coordinates.x + chunk.Seed % 1000) * 0.1f,
                (chunk.Coordinates.y + chunk.Seed % 1000) * 0.1f);

            var biomeScale = region?.BaseDifficulty ?? 1f;

            // Note: heightSample and biomeScale would be used here to drive tile/mesh generation.
            // For now, we suppress unused warnings as this is a placeholder for actual terrain generation.
            if (logTerrainSamples)
                Debug.Log($"Terrain for {chunk.Coordinates}: sample={heightSample}, scale={biomeScale}");
        }

        private void SpawnBuildings(WorldChunk chunk, RegionDefinition region)
        {
            // Determines building count from region density; each anchor is a candidate
            // building placement recorded in SpawnedEntityIds for deferred instantiation.
            var density = region?.RegionData != null ? Mathf.Clamp01(region.RegionData.difficulty * 0.5f) : 0.3f;
            var maxBuildings = Mathf.RoundToInt(density * 4);
            var rng = new Random(chunk.Seed ^ 0xBEEF);

            for (var i = 0; i < maxBuildings; i++)
            {
                var ox = rng.Next(0, chunkGenerationSteps);
                var oy = rng.Next(0, chunkGenerationSteps);
                chunk.SpawnedEntityIds.Add($"building_{chunk.Coordinates.x}_{chunk.Coordinates.y}_{i}_@{ox},{oy}");
            }
        }

        private static void SpawnZombies(WorldChunk chunk, RegionDefinition region)
        {
            var difficulty = region?.GetDifficulty() ?? 1f;
            var baseCount = Mathf.RoundToInt(difficulty * 5f);
            var rng = new Random(chunk.Seed ^ 0xDEAD);

            for (var i = 0; i < baseCount; i++)
                chunk.SpawnedEntityIds.Add($"zombie_{chunk.Coordinates.x}_{chunk.Coordinates.y}_{i}_t{rng.Next(0, 3)}");
        }

        private static void SpawnLoot(WorldChunk chunk, RegionDefinition region)
        {
            // Registers loot container seeds so items are generated lazily on container open.
            var lootFactor = region?.RegionData != null ? region.RegionData.difficulty : 1f;
            var containerCount = Mathf.Max(1, Mathf.RoundToInt(lootFactor * 3f));
            var rng = new Random(chunk.Seed ^ 0xF00D);

            for (var i = 0; i < containerCount; i++)
            {
                var lootRoll = rng.Next(0, int.MaxValue);
                chunk.SpawnedEntityIds.Add($"loot_{chunk.Coordinates.x}_{chunk.Coordinates.y}_{i}_seed{lootRoll}");
            }
        }
    }

    /// <summary>
    ///     Lightweight runtime representation of a streamed world chunk.
    /// </summary>
    [Serializable]
    public sealed class WorldChunk
    {
        [field: SerializeField] public Vector2Int Coordinates { get; private set; }
        [field: SerializeField] public int Seed { get; private set; }
        [field: SerializeField] public string RegionId { get; private set; }
        [field: SerializeField] public bool IsDirty { get; set; }

        public WorldChunk(Vector2Int coordinates, int seed, string regionId)
        {
            Coordinates = coordinates;
            Seed = seed;
            RegionId = regionId;
            IsDirty = false;
        }

        public List<string> SpawnedEntityIds { get; } = new();
    }
}