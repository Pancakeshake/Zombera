#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Regions;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.World.Simulation
{
    // ReSharper disable LoopCanBeConvertedToQuery
    // ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
    // ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
    // ReSharper disable MergeIntoPattern
    // ReSharper disable MergeIntoLogicalPattern
    // ReSharper disable UseNullPropagation
    // ReSharper disable ConvertIfStatementToSwitchStatement
    // ReSharper disable InvertIf
    // ReSharper disable ForCanBeConvertedToForeach
    /// <summary>
    ///     Abstract distant simulation for zombie hordes.
    ///     Keeps horde data alive while regions are not fully materialized.
    /// </summary>
    public sealed class HordeManager : MonoBehaviour
    {
        [Header("Seeding")] [SerializeField] private bool autoSeedFromRegions = true;

        [SerializeField] private Vector2Int initialHordesPerRegion = new(1, 3);
        [SerializeField] private Vector2Int initialHordePopulation = new(6, 24);

        [Header("Simulation")] [SerializeField] [Range(0f, 1f)]
        private float migrationChancePerTick = 0.2f;

        [SerializeField] [Range(0f, 1f)] private float attritionFactor = 0.05f;
        [SerializeField] [Range(0f, 2f)] private float growthFactor = 0.1f;

        [Header("Debug")] [SerializeField] private bool drawHordeGizmos = true;

        [SerializeField] private bool logHordeEvents;

        private readonly List<ZombieHorde> _hordes = new();
        private int _nextHordeId = 1;
        private bool _seeded;

        public IReadOnlyList<ZombieHorde> Hordes => _hordes;

        private void OnDrawGizmosSelected()
        {
            if (!drawHordeGizmos) return;

            foreach (var horde in _hordes)
            {
                if (horde is not { population: > 0 }) continue;

                var size = Mathf.Lerp(1f, 5f, Mathf.Clamp01(horde.population / 60f));
                Gizmos.color = new Color(0.8f, 0.1f, 0.1f, 0.9f);
                Gizmos.DrawSphere(horde.worldPosition, size);
            }
        }

        public void SimulateHordes(
            float deltaTime,
            IReadOnlyList<Region> regions,
            SurvivorManager survivorManager,
            bool logFromCaller)
        {
            if (regions == null || regions.Count == 0) return;

            EnsureSeeded(regions);

            for (var i = _hordes.Count - 1; i >= 0; i--)
            {
                var horde = _hordes[i];

                if (horde is not { population: > 0 })
                {
                    _hordes.RemoveAt(i);
                    continue;
                }

                var region = FindRegionById(regions, horde.regionId);

                if (region == null)
                {
                    var fallback = FindClosestRegion(regions, horde.worldPosition);

                    if (fallback != null)
                    {
                        horde.regionId = fallback.regionId;
                        region = fallback;
                    }
                }

                if (region == null) continue;

                SimulateSingleHorde(horde, region, deltaTime);

                if (survivorManager?.TryResolveHordeEncounter(horde, out var encounterSummary) == true &&
                    ShouldLog(logFromCaller))
                    Debug.Log($"[HordeManager] {encounterSummary}", this);

                if (Random.value < migrationChancePerTick) AttemptMigration(horde, region, regions, logFromCaller);

                if (horde.population <= 0) _hordes.RemoveAt(i);
            }

            SyncHordesToRegions(regions);
        }

        public int CountPopulationInRegion(string regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId)) return 0;

            var count = 0;

            foreach (var horde in _hordes)
            {
                if (horde != null && string.Equals(horde.regionId, regionId, StringComparison.Ordinal))
                    count += Mathf.Max(0, horde.population);
            }

            return count;
        }

        // ReSharper disable once UnusedMember.Global
        public void RegisterOrUpdateRegionHorde(string regionId, int population, Vector3 worldPosition)
        {
            if (string.IsNullOrWhiteSpace(regionId) || population <= 0) return;

            var existing = FindLargestHordeInRegion(regionId);

            if (existing == null)
            {
                _hordes.Add(new ZombieHorde
                {
                    hordeId = _nextHordeId++,
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
            if (_seeded) return;

            if (!autoSeedFromRegions)
            {
                _seeded = true;
                return;
            }

            foreach (var region in regions)
            {
                if (region == null) continue;

                if (region is { zombieHordes: { Count: > 0 } })
                {
                    foreach (var source in region.zombieHordes)
                    {
                        if (source is not { population: > 0 }) continue;

                        source.hordeId = source.hordeId <= 0 ? _nextHordeId++ : source.hordeId;
                        source.regionId = string.IsNullOrWhiteSpace(source.regionId)
                            ? region.regionId
                            : source.regionId;
                        _hordes.Add(source);
                        _nextHordeId = Mathf.Max(_nextHordeId, source.hordeId + 1);
                    }

                    continue;
                }

                var hordeCount = Random.Range(
                    Mathf.Min(initialHordesPerRegion.x, initialHordesPerRegion.y),
                    Mathf.Max(initialHordesPerRegion.x, initialHordesPerRegion.y) + 1);

                for (var h = 0; h < hordeCount; h++)
                {
                    var population = Random.Range(
                        Mathf.Min(initialHordePopulation.x, initialHordePopulation.y),
                        Mathf.Max(initialHordePopulation.x, initialHordePopulation.y) + 1);

                    var spawnPosition = GetRandomPointInBounds(region.bounds);

                    _hordes.Add(new ZombieHorde
                    {
                        hordeId = _nextHordeId++,
                        regionId = region.regionId,
                        population = population,
                        aggression = Random.Range(0.35f, 1f),
                        worldPosition = spawnPosition
                    });
                }
            }

            _seeded = true;
        }

        private void SimulateSingleHorde(ZombieHorde horde, Region region, float deltaTime)
        {
            var growth = region.dangerLevel * growthFactor * deltaTime;
            var attrition = Mathf.Max(0f, (1f - region.dangerLevel) * attritionFactor * deltaTime);

            var growthCount = Mathf.RoundToInt(growth * Mathf.Max(1, horde.population));
            var attritionCount = Mathf.RoundToInt(attrition * Mathf.Max(1, horde.population));
            horde.population = Mathf.Max(0, horde.population + growthCount - attritionCount);

            var drift = Random.insideUnitCircle;
            var driftDistance = Mathf.Lerp(2f, 10f, horde.aggression) * Mathf.Max(0.1f, deltaTime * 0.2f);
            var nextPosition = horde.worldPosition + new Vector3(drift.x, 0f, drift.y) * driftDistance;

            horde.worldPosition = ClampToBounds(region.bounds, nextPosition);
        }

        private void AttemptMigration(ZombieHorde horde, Region currentRegion, IReadOnlyList<Region> regions,
            bool logFromCaller)
        {
            var target = FindMigrationTarget(currentRegion, regions);

            if (target == null) return;

            horde.regionId = target.regionId;
            horde.worldPosition = ClampToBounds(target.bounds, horde.worldPosition);

            if (ShouldLog(logFromCaller))
                Debug.Log(
                    $"[HordeManager] Horde {horde.hordeId} migrated {currentRegion.regionId} -> {target.regionId}",
                    this);
        }

        private static Region FindMigrationTarget(Region currentRegion, IReadOnlyList<Region> regions)
        {
            Region best = null;
            var bestScore = float.NegativeInfinity;

            for (var i = 0; i < regions.Count; i++)
            {
                var candidate = regions[i];

                if (candidate == null || candidate == currentRegion) continue;

                var centerDistance = Vector3.Distance(currentRegion.Center, candidate.Center);
                var maxNeighborDistance = Mathf.Max(currentRegion.bounds.size.x, currentRegion.bounds.size.z) * 1.6f;

                if (centerDistance > maxNeighborDistance) continue;

                var score = candidate.dangerLevel - centerDistance * 0.0015f;

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
            foreach (var region in regions)
            {
                if (region != null) region.zombieHordes.Clear();
            }

            foreach (var horde in _hordes)
            {
                if (horde is not { population: > 0 }) continue;

                var region = FindRegionById(regions, horde.regionId);

                if (region != null) region.zombieHordes.Add(horde);
            }
        }

        private ZombieHorde FindLargestHordeInRegion(string regionId)
        {
            ZombieHorde largest = null;

            foreach (var horde in _hordes)
            {
                if (horde == null || !string.Equals(horde.regionId, regionId, StringComparison.Ordinal)) continue;

                if (largest == null || horde.population > largest.population) largest = horde;
            }

            return largest;
        }

        private static Region FindRegionById(IReadOnlyList<Region> regions, string regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId) || regions == null) return null;

            for (var i = 0; i < regions.Count; i++)
            {
                var region = regions[i];

                if (region != null && string.Equals(region.regionId, regionId, StringComparison.Ordinal)) return region;
            }

            return null;
        }

        private static Region FindClosestRegion(IReadOnlyList<Region> regions, Vector3 worldPosition)
        {
            Region closest = null;
            var bestDistance = float.PositiveInfinity;

            for (var i = 0; i < regions.Count; i++)
            {
                var region = regions[i];

                if (region == null) continue;

                var distance = Vector3.Distance(worldPosition, region.Center);

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