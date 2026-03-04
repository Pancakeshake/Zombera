using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Regions;

namespace Zombera.World.Simulation
{
    /// <summary>
    /// Abstract distant simulation for zombie hordes.
    /// Keeps horde data alive while regions are not fully materialized.
    /// </summary>
    public sealed class HordeManager : MonoBehaviour
    {
        [Header("Seeding")]
        [SerializeField] private bool autoSeedFromRegions = true;
        [SerializeField] private Vector2Int initialHordesPerRegion = new Vector2Int(1, 3);
        [SerializeField] private Vector2Int initialHordePopulation = new Vector2Int(6, 24);

        [Header("Simulation")]
        [SerializeField, Range(0f, 1f)] private float migrationChancePerTick = 0.2f;
        [SerializeField, Range(0f, 1f)] private float attritionFactor = 0.05f;
        [SerializeField, Range(0f, 2f)] private float growthFactor = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool drawHordeGizmos = true;
        [SerializeField] private bool logHordeEvents = false;

        private readonly List<ZombieHorde> hordes = new List<ZombieHorde>();
        private bool seeded;
        private int nextHordeId = 1;

        public IReadOnlyList<ZombieHorde> Hordes => hordes;

        public void SimulateHordes(
            float deltaTime,
            IReadOnlyList<Region> regions,
            SurvivorManager survivorManager,
            bool logFromCaller)
        {
            if (regions == null || regions.Count == 0)
            {
                return;
            }

            EnsureSeeded(regions);

            for (int i = hordes.Count - 1; i >= 0; i--)
            {
                ZombieHorde horde = hordes[i];

                if (horde == null || horde.population <= 0)
                {
                    hordes.RemoveAt(i);
                    continue;
                }

                Region region = FindRegionById(regions, horde.regionId);

                if (region == null)
                {
                    Region fallback = FindClosestRegion(regions, horde.worldPosition);

                    if (fallback != null)
                    {
                        horde.regionId = fallback.regionId;
                        region = fallback;
                    }
                }

                if (region == null)
                {
                    continue;
                }

                SimulateSingleHorde(horde, region, deltaTime);

                if (survivorManager != null && survivorManager.TryResolveHordeEncounter(horde, out string encounterSummary))
                {
                    if (ShouldLog(logFromCaller))
                    {
                        Debug.Log($"[HordeManager] {encounterSummary}", this);
                    }
                }

                if (Random.value < migrationChancePerTick)
                {
                    AttemptMigration(horde, region, regions, logFromCaller);
                }

                if (horde.population <= 0)
                {
                    hordes.RemoveAt(i);
                }
            }

            SyncHordesToRegions(regions);
        }

        public int CountPopulationInRegion(string regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId))
            {
                return 0;
            }

            int count = 0;

            for (int i = 0; i < hordes.Count; i++)
            {
                ZombieHorde horde = hordes[i];

                if (horde != null && string.Equals(horde.regionId, regionId, StringComparison.Ordinal))
                {
                    count += Mathf.Max(0, horde.population);
                }
            }

            return count;
        }

        public void RegisterOrUpdateRegionHorde(string regionId, int population, Vector3 worldPosition)
        {
            if (string.IsNullOrWhiteSpace(regionId) || population <= 0)
            {
                return;
            }

            ZombieHorde existing = FindLargestHordeInRegion(regionId);

            if (existing == null)
            {
                hordes.Add(new ZombieHorde
                {
                    hordeId = nextHordeId++,
                    regionId = regionId,
                    population = population,
                    aggression = Random.Range(0.45f, 1f),
                    worldPosition = worldPosition
                });

                return;
            }

            existing.population = Mathf.Max(existing.population, population);
            existing.worldPosition = worldPosition;
        }

        private void EnsureSeeded(IReadOnlyList<Region> regions)
        {
            if (seeded)
            {
                return;
            }

            if (!autoSeedFromRegions)
            {
                seeded = true;
                return;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];

                if (region == null)
                {
                    continue;
                }

                if (region.zombieHordes != null && region.zombieHordes.Count > 0)
                {
                    for (int j = 0; j < region.zombieHordes.Count; j++)
                    {
                        ZombieHorde source = region.zombieHordes[j];

                        if (source == null || source.population <= 0)
                        {
                            continue;
                        }

                        source.hordeId = source.hordeId <= 0 ? nextHordeId++ : source.hordeId;
                        source.regionId = string.IsNullOrWhiteSpace(source.regionId) ? region.regionId : source.regionId;
                        hordes.Add(source);
                        nextHordeId = Mathf.Max(nextHordeId, source.hordeId + 1);
                    }

                    continue;
                }

                int hordeCount = Random.Range(
                    Mathf.Min(initialHordesPerRegion.x, initialHordesPerRegion.y),
                    Mathf.Max(initialHordesPerRegion.x, initialHordesPerRegion.y) + 1);

                for (int h = 0; h < hordeCount; h++)
                {
                    int population = Random.Range(
                        Mathf.Min(initialHordePopulation.x, initialHordePopulation.y),
                        Mathf.Max(initialHordePopulation.x, initialHordePopulation.y) + 1);

                    Vector3 spawnPosition = GetRandomPointInBounds(region.bounds);

                    hordes.Add(new ZombieHorde
                    {
                        hordeId = nextHordeId++,
                        regionId = region.regionId,
                        population = population,
                        aggression = Random.Range(0.35f, 1f),
                        worldPosition = spawnPosition
                    });
                }
            }

            seeded = true;
        }

        private void SimulateSingleHorde(ZombieHorde horde, Region region, float deltaTime)
        {
            float growth = region.dangerLevel * growthFactor * deltaTime;
            float attrition = Mathf.Max(0f, (1f - region.dangerLevel) * attritionFactor * deltaTime);

            int growthCount = Mathf.RoundToInt(growth * Mathf.Max(1, horde.population));
            int attritionCount = Mathf.RoundToInt(attrition * Mathf.Max(1, horde.population));
            horde.population = Mathf.Max(0, horde.population + growthCount - attritionCount);

            Vector2 drift = UnityEngine.Random.insideUnitCircle;
            float driftDistance = Mathf.Lerp(2f, 10f, horde.aggression) * Mathf.Max(0.1f, deltaTime * 0.2f);
            Vector3 nextPosition = horde.worldPosition + new Vector3(drift.x, 0f, drift.y) * driftDistance;

            horde.worldPosition = ClampToBounds(region.bounds, nextPosition);
        }

        private void AttemptMigration(ZombieHorde horde, Region currentRegion, IReadOnlyList<Region> regions, bool logFromCaller)
        {
            Region target = FindMigrationTarget(currentRegion, regions);

            if (target == null)
            {
                return;
            }

            horde.regionId = target.regionId;
            horde.worldPosition = ClampToBounds(target.bounds, horde.worldPosition);

            if (ShouldLog(logFromCaller))
            {
                Debug.Log($"[HordeManager] Horde {horde.hordeId} migrated {currentRegion.regionId} -> {target.regionId}", this);
            }
        }

        private static Region FindMigrationTarget(Region currentRegion, IReadOnlyList<Region> regions)
        {
            Region best = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < regions.Count; i++)
            {
                Region candidate = regions[i];

                if (candidate == null || candidate == currentRegion)
                {
                    continue;
                }

                float centerDistance = Vector3.Distance(currentRegion.Center, candidate.Center);
                float maxNeighborDistance = Mathf.Max(currentRegion.bounds.size.x, currentRegion.bounds.size.z) * 1.6f;

                if (centerDistance > maxNeighborDistance)
                {
                    continue;
                }

                float score = candidate.dangerLevel - (centerDistance * 0.0015f);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private void SyncHordesToRegions(IReadOnlyList<Region> regions)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];

                if (region != null)
                {
                    region.zombieHordes.Clear();
                }
            }

            for (int i = 0; i < hordes.Count; i++)
            {
                ZombieHorde horde = hordes[i];

                if (horde == null || horde.population <= 0)
                {
                    continue;
                }

                Region region = FindRegionById(regions, horde.regionId);

                if (region != null)
                {
                    region.zombieHordes.Add(horde);
                }
            }
        }

        private ZombieHorde FindLargestHordeInRegion(string regionId)
        {
            ZombieHorde largest = null;

            for (int i = 0; i < hordes.Count; i++)
            {
                ZombieHorde horde = hordes[i];

                if (horde == null || !string.Equals(horde.regionId, regionId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (largest == null || horde.population > largest.population)
                {
                    largest = horde;
                }
            }

            return largest;
        }

        private static Region FindRegionById(IReadOnlyList<Region> regions, string regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId) || regions == null)
            {
                return null;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];

                if (region != null && string.Equals(region.regionId, regionId, StringComparison.Ordinal))
                {
                    return region;
                }
            }

            return null;
        }

        private static Region FindClosestRegion(IReadOnlyList<Region> regions, Vector3 worldPosition)
        {
            Region closest = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];

                if (region == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(worldPosition, region.Center);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closest = region;
                }
            }

            return closest;
        }

        private static Vector3 ClampToBounds(Bounds bounds, Vector3 worldPosition)
        {
            return new Vector3(
                Mathf.Clamp(worldPosition.x, bounds.min.x, bounds.max.x),
                bounds.center.y,
                Mathf.Clamp(worldPosition.z, bounds.min.z, bounds.max.z));
        }

        private static Vector3 GetRandomPointInBounds(Bounds bounds)
        {
            return new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.center.y,
                Random.Range(bounds.min.z, bounds.max.z));
        }

        private bool ShouldLog(bool logFromCaller)
        {
            return logHordeEvents || logFromCaller;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawHordeGizmos)
            {
                return;
            }

            for (int i = 0; i < hordes.Count; i++)
            {
                ZombieHorde horde = hordes[i];

                if (horde == null || horde.population <= 0)
                {
                    continue;
                }

                float size = Mathf.Lerp(1f, 5f, Mathf.Clamp01(horde.population / 60f));
                Gizmos.color = new Color(0.8f, 0.1f, 0.1f, 0.9f);
                Gizmos.DrawSphere(horde.worldPosition, size);
            }
        }
    }

    [Serializable]
    public sealed class ZombieHorde
    {
        public int hordeId;
        public string regionId;
        [Min(0)] public int population = 10;
        [Range(0f, 1f)] public float aggression = 0.6f;
        public Vector3 worldPosition;
    }
}