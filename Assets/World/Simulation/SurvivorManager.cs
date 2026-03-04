using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Regions;

namespace Zombera.World.Simulation
{
    /// <summary>
    /// Abstract distant simulation for survivor groups.
    /// </summary>
    public sealed class SurvivorManager : MonoBehaviour
    {
        [Header("Seeding")]
        [SerializeField] private bool autoSeedFromRegions = true;
        [SerializeField] private Vector2Int initialGroupsPerRegion = new Vector2Int(0, 2);
        [SerializeField] private Vector2Int initialGroupPopulation = new Vector2Int(2, 8);

        [Header("Simulation")]
        [SerializeField, Range(0f, 1f)] private float migrationChancePerTick = 0.15f;
        [SerializeField, Range(0f, 0.25f)] private float supplyDrainPerMember = 0.01f;
        [SerializeField, Range(0f, 1f)] private float forageFactor = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool drawSurvivorGroupGizmos = true;
        [SerializeField] private bool logSurvivorEvents = false;

        private readonly List<SurvivorGroup> groups = new List<SurvivorGroup>();
        private bool seeded;
        private int nextGroupId = 1;

        public IReadOnlyList<SurvivorGroup> Groups => groups;

        public void SimulateSurvivorGroups(float deltaTime, IReadOnlyList<Region> regions, bool logFromCaller)
        {
            if (regions == null || regions.Count == 0)
            {
                return;
            }

            EnsureSeeded(regions);

            for (int i = groups.Count - 1; i >= 0; i--)
            {
                SurvivorGroup group = groups[i];

                if (group == null || group.memberCount <= 0)
                {
                    groups.RemoveAt(i);
                    continue;
                }

                Region region = FindRegionById(regions, group.regionId);

                if (region == null)
                {
                    Region fallback = FindClosestRegion(regions, group.worldPosition);

                    if (fallback != null)
                    {
                        group.regionId = fallback.regionId;
                        region = fallback;
                    }
                }

                if (region == null)
                {
                    continue;
                }

                SimulateSingleGroup(group, region, deltaTime);

                if (Random.value < migrationChancePerTick)
                {
                    AttemptMigration(group, region, regions, logFromCaller);
                }

                if (group.memberCount <= 0)
                {
                    groups.RemoveAt(i);
                }
            }

            SyncGroupsToRegions(regions);
        }

        public bool TryResolveHordeEncounter(ZombieHorde horde, out string summary)
        {
            summary = null;

            if (horde == null || horde.population <= 0 || string.IsNullOrWhiteSpace(horde.regionId))
            {
                return false;
            }

            SurvivorGroup target = FindLargestGroupInRegion(horde.regionId);

            if (target == null || target.memberCount <= 0)
            {
                return false;
            }

            float hordeStrength = Mathf.Max(1f, horde.population * (0.8f + horde.aggression));
            float groupStrength = Mathf.Max(1f, target.memberCount * (0.7f + target.morale));

            int survivorLosses;
            int zombieLosses;

            if (hordeStrength >= groupStrength)
            {
                survivorLosses = Mathf.Max(1, Mathf.RoundToInt(target.memberCount * Random.Range(0.35f, 0.75f)));
                zombieLosses = Mathf.Max(0, Mathf.RoundToInt(horde.population * Random.Range(0.05f, 0.25f)));
                target.morale = Mathf.Clamp01(target.morale - 0.15f);
            }
            else
            {
                survivorLosses = Mathf.Max(0, Mathf.RoundToInt(target.memberCount * Random.Range(0.05f, 0.2f)));
                zombieLosses = Mathf.Max(1, Mathf.RoundToInt(horde.population * Random.Range(0.15f, 0.4f)));
                target.morale = Mathf.Clamp01(target.morale + 0.05f);
            }

            target.memberCount = Mathf.Max(0, target.memberCount - survivorLosses);
            horde.population = Mathf.Max(0, horde.population - zombieLosses);

            if (target.memberCount <= 0)
            {
                groups.Remove(target);
            }

            summary = $"Encounter in {horde.regionId}: Horde {horde.hordeId} lost {zombieLosses}, survivor group {target.groupId} lost {survivorLosses}.";

            if (logSurvivorEvents)
            {
                Debug.Log($"[SurvivorManager] {summary}", this);
            }

            return true;
        }

        public int CountMembersInRegion(string regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId))
            {
                return 0;
            }

            int count = 0;

            for (int i = 0; i < groups.Count; i++)
            {
                SurvivorGroup group = groups[i];

                if (group != null && string.Equals(group.regionId, regionId, StringComparison.Ordinal))
                {
                    count += Mathf.Max(0, group.memberCount);
                }
            }

            return count;
        }

        public void RegisterOrUpdateRegionGroup(string regionId, int memberCount, Vector3 worldPosition)
        {
            if (string.IsNullOrWhiteSpace(regionId) || memberCount <= 0)
            {
                return;
            }

            SurvivorGroup existing = FindLargestGroupInRegion(regionId);

            if (existing == null)
            {
                groups.Add(new SurvivorGroup
                {
                    groupId = nextGroupId++,
                    regionId = regionId,
                    memberCount = memberCount,
                    morale = Random.Range(0.35f, 0.9f),
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

                if (region.survivorGroups != null && region.survivorGroups.Count > 0)
                {
                    for (int j = 0; j < region.survivorGroups.Count; j++)
                    {
                        SurvivorGroup source = region.survivorGroups[j];

                        if (source == null || source.memberCount <= 0)
                        {
                            continue;
                        }

                        source.groupId = source.groupId <= 0 ? nextGroupId++ : source.groupId;
                        source.regionId = string.IsNullOrWhiteSpace(source.regionId) ? region.regionId : source.regionId;
                        groups.Add(source);
                        nextGroupId = Mathf.Max(nextGroupId, source.groupId + 1);
                    }

                    continue;
                }

                int groupCount = Random.Range(
                    Mathf.Min(initialGroupsPerRegion.x, initialGroupsPerRegion.y),
                    Mathf.Max(initialGroupsPerRegion.x, initialGroupsPerRegion.y) + 1);

                for (int g = 0; g < groupCount; g++)
                {
                    int population = Random.Range(
                        Mathf.Min(initialGroupPopulation.x, initialGroupPopulation.y),
                        Mathf.Max(initialGroupPopulation.x, initialGroupPopulation.y) + 1);

                    groups.Add(new SurvivorGroup
                    {
                        groupId = nextGroupId++,
                        regionId = region.regionId,
                        memberCount = population,
                        morale = Random.Range(0.3f, 0.9f),
                        supplies = Random.Range(0.3f, 1f),
                        worldPosition = GetRandomPointInBounds(region.bounds)
                    });
                }
            }

            seeded = true;
        }

        private void SimulateSingleGroup(SurvivorGroup group, Region region, float deltaTime)
        {
            float drain = group.memberCount * supplyDrainPerMember * deltaTime;
            group.supplies = Mathf.Clamp01(group.supplies - drain);

            float forage = forageFactor * region.lootLevel * Mathf.Max(0.1f, deltaTime * 0.25f);
            group.supplies = Mathf.Clamp01(group.supplies + forage);

            float moraleShift = (group.supplies - region.dangerLevel) * 0.15f * Mathf.Max(0.1f, deltaTime * 0.25f);
            group.morale = Mathf.Clamp01(group.morale + moraleShift);

            if (group.supplies <= 0.05f)
            {
                int starvationLoss = Mathf.Max(1, Mathf.RoundToInt(group.memberCount * 0.1f));
                group.memberCount = Mathf.Max(0, group.memberCount - starvationLoss);
                group.morale = Mathf.Clamp01(group.morale - 0.1f);
            }

            Vector2 drift = Random.insideUnitCircle;
            Vector3 nextPosition = group.worldPosition + new Vector3(drift.x, 0f, drift.y) * Mathf.Lerp(1.5f, 7f, 1f - group.morale);
            group.worldPosition = ClampToBounds(region.bounds, nextPosition);
        }

        private void AttemptMigration(SurvivorGroup group, Region currentRegion, IReadOnlyList<Region> regions, bool logFromCaller)
        {
            Region target = FindSaferNeighbor(currentRegion, regions);

            if (target == null)
            {
                return;
            }

            group.regionId = target.regionId;
            group.worldPosition = ClampToBounds(target.bounds, group.worldPosition);

            if (logSurvivorEvents || logFromCaller)
            {
                Debug.Log($"[SurvivorManager] Group {group.groupId} migrated {currentRegion.regionId} -> {target.regionId}", this);
            }
        }

        private void SyncGroupsToRegions(IReadOnlyList<Region> regions)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];

                if (region != null)
                {
                    region.survivorGroups.Clear();
                }
            }

            for (int i = 0; i < groups.Count; i++)
            {
                SurvivorGroup group = groups[i];

                if (group == null || group.memberCount <= 0)
                {
                    continue;
                }

                Region region = FindRegionById(regions, group.regionId);

                if (region != null)
                {
                    region.survivorGroups.Add(group);
                }
            }
        }

        private SurvivorGroup FindLargestGroupInRegion(string regionId)
        {
            SurvivorGroup largest = null;

            for (int i = 0; i < groups.Count; i++)
            {
                SurvivorGroup group = groups[i];

                if (group == null || !string.Equals(group.regionId, regionId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (largest == null || group.memberCount > largest.memberCount)
                {
                    largest = group;
                }
            }

            return largest;
        }

        private static Region FindSaferNeighbor(Region currentRegion, IReadOnlyList<Region> regions)
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

                float safetyScore = (1f - candidate.dangerLevel) + candidate.lootLevel - (centerDistance * 0.0015f);

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

        private void OnDrawGizmosSelected()
        {
            if (!drawSurvivorGroupGizmos)
            {
                return;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                SurvivorGroup group = groups[i];

                if (group == null || group.memberCount <= 0)
                {
                    continue;
                }

                float size = Mathf.Lerp(0.8f, 3.5f, Mathf.Clamp01(group.memberCount / 24f));
                Gizmos.color = new Color(0.1f, 0.5f, 1f, 0.9f);
                Gizmos.DrawCube(group.worldPosition, Vector3.one * size);
            }
        }
    }

    [Serializable]
    public sealed class SurvivorGroup
    {
        public int groupId;
        public string regionId;
        [Min(0)] public int memberCount = 4;
        [Range(0f, 1f)] public float morale = 0.6f;
        [Range(0f, 1f)] public float supplies = 0.65f;
        public Vector3 worldPosition;
    }
}