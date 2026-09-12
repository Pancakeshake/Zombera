#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.World.Regions;
using Random = UnityEngine.Random;

#endregion

// ReSharper disable MergeIntoPattern
// ReSharper disable MergeIntoLogicalPattern
// ReSharper disable UseNullPropagation
// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable InvertIf
// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable LoopCanBeConvertedToQuery

namespace Zombera.World.Simulation
{
    /// <summary>
    ///     Abstract distant simulation for survivor groups.
    /// </summary>
    public sealed class SurvivorManager : MonoBehaviour
    {
        [Header("Seeding")] [SerializeField] private bool autoSeedFromRegions = true;

        [SerializeField] private Vector2Int initialGroupsPerRegion = new(0, 2);
        [SerializeField] private Vector2Int initialGroupPopulation = new(2, 8);

        [Header("Simulation")] [SerializeField] [Range(0f, 1f)]
        private float migrationChancePerTick = 0.15f;

        [SerializeField] [Range(0f, 0.25f)] private float supplyDrainPerMember = 0.01f;
        [SerializeField] [Range(0f, 1f)] private float forageFactor = 0.2f;

        [Header("Debug")] [SerializeField] private bool drawSurvivorGroupGizmos = true;

        [SerializeField] private bool logSurvivorEvents;

        private readonly List<SurvivorGroup> _groups = new();
        private int _nextGroupId = 1;
        private bool _seeded;

        public IReadOnlyList<SurvivorGroup> Groups => _groups;

        private void OnDrawGizmosSelected()
        {
            if (!drawSurvivorGroupGizmos) return;

            foreach (var group in _groups)
            {
                if (group is not { memberCount: > 0 }) continue;

                var size = Mathf.Lerp(0.8f, 3.5f, Mathf.Clamp01(group.memberCount / 24f));
                Gizmos.color = new Color(0.1f, 0.5f, 1f, 0.9f);
                Gizmos.DrawCube(group.worldPosition, Vector3.one * size);
            }
        }

        public void SimulateSurvivorGroups(float deltaTime, IReadOnlyList<Region> regions, bool logFromCaller)
        {
            if (regions == null || regions.Count == 0) return;

            EnsureSeeded(regions);

            for (var i = _groups.Count - 1; i >= 0; i--)
            {
                var group = _groups[i];

                if (group is not { memberCount: > 0 })
                {
                    _groups.RemoveAt(i);
                    continue;
                }

                var region = FindRegionById(regions, group.regionId);

                if (region == null)
                {
                    var fallback = FindClosestRegion(regions, group.worldPosition);

                    if (fallback != null)
                    {
                        group.regionId = fallback.regionId;
                        region = fallback;
                    }
                }

                if (region == null) continue;

                SimulateSingleGroup(group, region, deltaTime);

                if (Random.value < migrationChancePerTick) AttemptMigration(group, region, regions, logFromCaller);

                if (group.memberCount <= 0) _groups.RemoveAt(i);
            }

            SyncGroupsToRegions(regions);
        }

        public bool TryResolveHordeEncounter(ZombieHorde horde, out string summary)
        {
            summary = null;

            if (horde is not { population: > 0 } || string.IsNullOrWhiteSpace(horde.regionId)) return false;

            var target = FindLargestGroupInRegion(horde.regionId);

            if (target is not { memberCount: > 0 }) return false;

            var hordeStrength = Mathf.Max(1f, horde.population * (0.8f + horde.aggression));
            var groupStrength = Mathf.Max(1f, target.memberCount);

            int survivorLosses;
            int zombieLosses;

            if (hordeStrength >= groupStrength)
            {
                survivorLosses = Mathf.Max(1, Mathf.RoundToInt(target.memberCount * Random.Range(0.35f, 0.75f)));
                zombieLosses = Mathf.Max(0, Mathf.RoundToInt(horde.population * Random.Range(0.05f, 0.25f)));
            }
            else
            {
                survivorLosses = Mathf.Max(0, Mathf.RoundToInt(target.memberCount * Random.Range(0.05f, 0.2f)));
                zombieLosses = Mathf.Max(1, Mathf.RoundToInt(horde.population * Random.Range(0.15f, 0.4f)));
            }

            target.memberCount = Mathf.Max(0, target.memberCount - survivorLosses);
            horde.population = Mathf.Max(0, horde.population - zombieLosses);

            if (target.memberCount <= 0) _groups.Remove(target);

            summary =
                $"Encounter in {horde.regionId}: Horde {horde.hordeId} lost {zombieLosses}, survivor group {target.groupId} lost {survivorLosses}.";

            if (logSurvivorEvents) Debug.Log($"[SurvivorManager] {summary}", this);

            return true;
        }

        public int CountMembersInRegion(string regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId)) return 0;

            return _groups
                .Where(group => group != null && string.Equals(group.regionId, regionId, StringComparison.Ordinal))
                .Sum(group => Mathf.Max(0, group.memberCount));
        }

        // ReSharper disable once UnusedMember.Global
        public void RegisterOrUpdateRegionGroup(string regionId, int memberCount, Vector3 worldPosition)
        {
            if (string.IsNullOrWhiteSpace(regionId) || memberCount <= 0) return;

            var existing = FindLargestGroupInRegion(regionId);

            if (existing == null)
            {
                _groups.Add(new SurvivorGroup
                {
                    groupId = _nextGroupId++,
                    regionId = regionId,
                    memberCount = memberCount,
                    supplies = Random.Range(0.25f, 1f),
                    worldPosition = worldPosition
                });

                return;
            }

            existing.memberCount = Mathf.Max(existing.memberCount, memberCount);
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

                if (region is { survivorGroups: { Count: > 0 } })
                {
                    foreach (var source in region.survivorGroups)
                    {
                        if (source is not { memberCount: > 0 }) continue;

                        source.groupId = source.groupId <= 0 ? _nextGroupId++ : source.groupId;
                        source.regionId = string.IsNullOrWhiteSpace(source.regionId)
                            ? region.regionId
                            : source.regionId;
                        _groups.Add(source);
                        _nextGroupId = Mathf.Max(_nextGroupId, source.groupId + 1);
                    }

                    continue;
                }

                var groupCount = Random.Range(
                    Mathf.Min(initialGroupsPerRegion.x, initialGroupsPerRegion.y),
                    Mathf.Max(initialGroupsPerRegion.x, initialGroupsPerRegion.y) + 1);

                for (var g = 0; g < groupCount; g++)
                {
                    var population = Random.Range(
                        Mathf.Min(initialGroupPopulation.x, initialGroupPopulation.y),
                        Mathf.Max(initialGroupPopulation.x, initialGroupPopulation.y) + 1);

                    _groups.Add(new SurvivorGroup
                    {
                        groupId = _nextGroupId++,
                        regionId = region.regionId,
                        memberCount = population,
                        supplies = Random.Range(0.3f, 1f),
                        worldPosition = GetRandomPointInBounds(region.bounds)
                    });
                }
            }

            _seeded = true;
        }

        private void SimulateSingleGroup(SurvivorGroup group, Region region, float deltaTime)
        {
            var drain = group.memberCount * supplyDrainPerMember * deltaTime;
            group.supplies = Mathf.Clamp01(group.supplies - drain);

            var forage = forageFactor * region.lootLevel * Mathf.Max(0.1f, deltaTime * 0.25f);
            group.supplies = Mathf.Clamp01(group.supplies + forage);

            if (group.supplies <= 0.05f)
            {
                var starvationLoss = Mathf.Max(1, Mathf.RoundToInt(group.memberCount * 0.1f));
                group.memberCount = Mathf.Max(0, group.memberCount - starvationLoss);
            }

            var drift = Random.insideUnitCircle;
            var nextPosition = group.worldPosition + new Vector3(drift.x, 0f, drift.y) * 4f;
            group.worldPosition = ClampToBounds(region.bounds, nextPosition);
        }

        private void AttemptMigration(SurvivorGroup group, Region currentRegion, IReadOnlyList<Region> regions,
            bool logFromCaller)
        {
            var target = FindSaferNeighbor(currentRegion, regions);

            if (target == null) return;

            group.regionId = target.regionId;
            group.worldPosition = ClampToBounds(target.bounds, group.worldPosition);

            if (logSurvivorEvents || logFromCaller)
                Debug.Log(
                    $"[SurvivorManager] Group {group.groupId} migrated {currentRegion.regionId} -> {target.regionId}",
                    this);
        }

        private void SyncGroupsToRegions(IReadOnlyList<Region> regions)
        {
            foreach (var region in regions)
            {
                if (region != null) region.survivorGroups.Clear();
            }

            foreach (var group in _groups)
            {
                if (group is not { memberCount: > 0 }) continue;

                var region = FindRegionById(regions, group.regionId);

                if (region != null) region.survivorGroups.Add(group);
            }
        }

        private SurvivorGroup FindLargestGroupInRegion(string regionId)
        {
            SurvivorGroup largest = null;

            foreach (var group in _groups)
            {
                if (group == null || !string.Equals(group.regionId, regionId, StringComparison.Ordinal)) continue;

                if (largest == null || group.memberCount > largest.memberCount) largest = group;
            }

            return largest;
        }

        private static Region FindSaferNeighbor(Region currentRegion, IReadOnlyList<Region> regions)
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

                var safetyScore = 1f - candidate.dangerLevel + candidate.lootLevel - centerDistance * 0.0015f;

                if (safetyScore > bestScore)
                {
                    bestScore = safetyScore;
                    best = candidate;
                }
            }

            return best;
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
    }

    [Serializable]
    public sealed class SurvivorGroup
    {
        public int groupId;
        public string regionId;
        [Min(0)] public int memberCount = 4;
        [Range(0f, 1f)] public float supplies = 0.65f;
        public Vector3 worldPosition;
    }
}