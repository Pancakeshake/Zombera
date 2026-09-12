#region

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.AI;
using Zombera.AI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Data;
using Zombera.Debugging.DebugLogging;
using Zombera.World;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.Systems
{
    public sealed partial class ZombieManager
    {
        private struct AmbientSpawnPolicyContext
        {
            public Vector3 PlayerPosition;
            public ZombieType SpawnType;
            public int ZombieBudget;
            public int AvailableSlots;
        }

        private bool TryBuildAmbientSpawnPolicyContext(
            Vector3 playerPosition,
            out AmbientSpawnPolicyContext context)
        {
            context = default;

            if (!CanTickAmbientSpawn()) return false;
            if (!IsSpawnNavMeshReady(playerPosition)) return false;

            PruneInvalidActiveZombies();

            var zombieBudget = ResolveCurrentZombieBudget();
            var availableSlots = ResolveAvailableAmbientSlots(zombieBudget);
            if (availableSlots <= 0) return false;

            var spawnType = ResolveAmbientZombieType();
            if (spawnType == null) return false;

            context = new AmbientSpawnPolicyContext
            {
                PlayerPosition = playerPosition,
                SpawnType = spawnType,
                ZombieBudget = zombieBudget,
                AvailableSlots = availableSlots
            };
            return true;
        }

        private bool TryExecuteAmbientSpawnPolicy(ref AmbientSpawnPolicyContext context)
        {
            var spawnedAny = false;
            spawnedAny |= TrySpawnNearbyThreatCatchup(
                context.SpawnType,
                context.PlayerPosition,
                context.ZombieBudget,
                ref context.AvailableSlots);
            spawnedAny |= TrySpawnScheduledHordePulse(
                context.SpawnType,
                context.PlayerPosition,
                context.ZombieBudget,
                ref context.AvailableSlots);

            if (context.AvailableSlots <= 0)
                return true;

            if (!ShouldRunAmbientWaveRoll(context.ZombieBudget))
                return false;

            spawnedAny |= TrySpawnAmbientWave(
                context.SpawnType,
                context.PlayerPosition,
                context.AvailableSlots,
                context.ZombieBudget);

            return spawnedAny;
        }

        private bool CanTickAmbientSpawn()
        {
            if (!IsInitialized) return false;

            if (zombieSpawner == null && GetRuntimeZombieSpawner() == null) return false;

            return CanEvaluateAmbientSpawn();
        }

        private int ResolveAvailableAmbientSlots(int zombieBudget)
        {
            return Mathf.Max(0, zombieBudget - ActiveZombieCount);
        }

        private bool TrySpawnNearbyThreatCatchup(ZombieType spawnType, Vector3 playerPosition, int zombieBudget,
            ref int availableSlots)
        {
            if (!maintainMinimumNearbyThreat) return false;

            var nearbyDeficit = ResolveNearbyThreatDeficit(playerPosition, zombieBudget);
            if (nearbyDeficit <= 0) return false;

            var nearbySpawnCount = Mathf.Min(
                availableSlots,
                Mathf.Min(nearbyDeficit, Mathf.Max(1, maxNearbyCatchupSpawnCount)));

            if (nearbySpawnCount <= 0) return false;

            var nearbyCenter = ResolvePlayerRelativeSpawnCenter(
                playerPosition,
                nearbyCatchupSpawnDistance,
                nearbyCatchupSpawnDistanceJitter);

            if (!TrySpawnAmbientBatch(spawnType, nearbyCenter, nearbySpawnCount, ambientSpawnRadius)) return false;

            availableSlots = ResolveAvailableAmbientSlots(zombieBudget);
            return true;
        }

        private bool TrySpawnScheduledHordePulse(ZombieType spawnType, Vector3 playerPosition, int zombieBudget,
            ref int availableSlots)
        {
            if (!enablePeriodicHordePulses || availableSlots <= 0 || Time.time < _nextHordePulseTime) return false;

            var spawnedAny = false;
            var hordePulseCount = ResolveHordePulseCount(availableSlots, zombieBudget);

            if (hordePulseCount > 0)
            {
                var hordePulseCenter = ResolveAmbientSpawnCenter(playerPosition);
                spawnedAny = TrySpawnAmbientBatch(
                    spawnType,
                    hordePulseCenter,
                    hordePulseCount,
                    Mathf.Max(ambientSpawnRadius, hordePulseRadius));

                if (spawnedAny) availableSlots = ResolveAvailableAmbientSlots(zombieBudget);
            }

            _nextHordePulseTime = Time.time + Mathf.Max(5f, hordePulseIntervalSeconds);
            return spawnedAny;
        }

        private bool ShouldRunAmbientWaveRoll(int zombieBudget)
        {
            var hasNoActiveZombies = ActiveZombieCount <= 0;
            var shouldForceSpawn = hasNoActiveZombies || Time.time >= _nextGuaranteedAmbientSpawnTime;
            if (shouldForceSpawn) return true;

            var spawnChance = ResolveAmbientSpawnChance(zombieBudget);
            return Random.value <= spawnChance;
        }

        private bool TrySpawnAmbientWave(ZombieType spawnType, Vector3 playerPosition, int availableSlots,
            int zombieBudget)
        {
            var count = ResolveAmbientWaveCount(availableSlots, zombieBudget);
            if (count <= 0) return false;

            var spawnCenter = ResolveAmbientSpawnCenter(playerPosition);
            return TrySpawnAmbientBatch(spawnType, spawnCenter, count, ambientSpawnRadius);
        }

        private ZombieType ResolveAmbientZombieType()
        {
            if (TrySelectWeightedAmbientZombieType(out var weightedType)) return weightedType;

            if (ambientZombieType != null) return ambientZombieType;

            if (_runtimeFallbackAmbientZombieType != null) return _runtimeFallbackAmbientZombieType;

            _runtimeFallbackAmbientZombieType = ScriptableObject.CreateInstance<ZombieType>();
            _runtimeFallbackAmbientZombieType.name = "RuntimeAmbientZombieType";
            _runtimeFallbackAmbientZombieType.zombieTypeId = "ambient.walker";
            _runtimeFallbackAmbientZombieType.displayName = "Ambient Walker";
            _runtimeFallbackAmbientZombieType.baseHealth = 50f;
            _runtimeFallbackAmbientZombieType.moveSpeed = 1.6f;
            _runtimeFallbackAmbientZombieType.attackDamage = 8f;
            _runtimeFallbackAmbientZombieType.defaultAiTickInterval = 0.4f;

            DebugLogger.LogWarning(
                LogCategory.World,
                "ZombieManager ambientZombieType is unassigned. Using runtime fallback zombie type.",
                this);

            return _runtimeFallbackAmbientZombieType;
        }

        private bool TrySelectWeightedAmbientZombieType(out ZombieType selectedZombieType)
        {
            selectedZombieType = null;

            if (weightedAmbientZombieTypes == null || weightedAmbientZombieTypes.Count == 0) return false;

            var totalWeight = 0f;

            foreach (var entry in weightedAmbientZombieTypes)
            {
                if (entry?.zombieType == null) continue;

                var weight = Mathf.Max(0f, entry.weight);

                if (weight <= 0f) continue;

                totalWeight += weight;
            }

            if (totalWeight <= 0f) return false;

            var roll = Random.value * totalWeight;

            foreach (var entry in weightedAmbientZombieTypes)
            {
                if (entry?.zombieType == null) continue;

                var weight = Mathf.Max(0f, entry.weight);

                if (weight <= 0f) continue;

                roll -= weight;

                if (roll > 0f) continue;

                selectedZombieType = entry.zombieType;
                return true;
            }

            // Fallback to first valid weighted entry if floating-point drift keeps roll above zero.
            foreach (var entry in weightedAmbientZombieTypes)
                if (entry?.zombieType != null && entry.weight > 0f)
                {
                    selectedZombieType = entry.zombieType;
                    return true;
                }

            return false;
        }

        private Vector3 ResolveAmbientSpawnCenter(Vector3 playerPosition)
        {
            if (useMapWideAmbientSpawns && TryGetRandomPointAcrossMap(playerPosition, out var mapPoint))
                return mapPoint;

            return ResolvePlayerRelativeSpawnCenter(playerPosition);
        }

        private Vector3 ResolvePlayerRelativeSpawnCenter(Vector3 playerPosition)
        {
            return ResolvePlayerRelativeSpawnCenter(playerPosition, ambientSpawnDistanceFromPlayer, 0f);
        }

        private Vector3 ResolvePlayerRelativeSpawnCenter(Vector3 playerPosition, float distanceFromPlayer,
            float distanceJitter)
        {
            var spawnCenter = BuildNearbySpawnPoint(playerPosition, distanceFromPlayer, distanceJitter);
            var terrain = ResolveAmbientTerrain(spawnCenter);
            return FinalizeSpawnCenter(spawnCenter, terrain);
        }

        private bool CanEvaluateAmbientSpawn()
        {
            if (!enableRealtimeAmbientSpawning) return true;

            var now = Time.time;

            if (now < _nextAmbientSpawnEvaluationTime) return false;

            _nextAmbientSpawnEvaluationTime = now + Mathf.Max(0.1f, ambientSpawnEvaluationIntervalSeconds);
            return true;
        }

        private int ResolveCurrentZombieBudget()
        {
            var baseBudget = Mathf.Max(1, maxManagedZombies);

            if (!enableDynamicSpawnBudget) return baseBudget;

            baseBudget = Mathf.Max(baseBudget, minimumRuntimeZombieBudget);
            var hardCap = Mathf.Max(baseBudget, maxManagedZombiesHardCap);

            var elapsed = Mathf.Max(0f, Time.time - _initializationTime);
            var stepCount = budgetStepIntervalSeconds > 0.01f
                ? Mathf.FloorToInt(elapsed / Mathf.Max(1f, budgetStepIntervalSeconds))
                : 0;
            if (stepCount == _cachedBudgetStep && _cachedZombieBudget > 0)
                return _cachedZombieBudget;

            var scaledBudget = baseBudget + Mathf.Max(0, budgetIncreasePerStep) * Mathf.Max(0, stepCount);
            var resolvedBudget = Mathf.Clamp(scaledBudget, baseBudget, hardCap);

            _cachedBudgetStep = stepCount;
            _cachedZombieBudget = resolvedBudget;
            return resolvedBudget;
        }

        private float ResolveAmbientSpawnChance(int zombieBudget)
        {
            var baseChance = Mathf.Clamp01(ambientSpawnChancePerSimulationTick);

            if (!enableDynamicSpawnBudget || zombieBudget <= 0) return baseChance;

            var deficit = Mathf.Max(0, zombieBudget - ActiveZombieCount);
            var pressure = Mathf.Clamp01(deficit / (float)zombieBudget);
            return Mathf.Clamp01(baseChance + pressure * 0.55f);
        }

        private int ResolveAmbientWaveCount(int availableSlots, int zombieBudget)
        {
            var minCount = Mathf.Max(1, minAmbientWaveSize);
            var maxCount = Mathf.Max(minCount, maxAmbientWaveSize);
            var count = Random.Range(minCount, maxCount + 1);

            if (!enableDynamicSpawnBudget) return Mathf.Clamp(count, 1, Mathf.Max(1, availableSlots));

            var deficit = Mathf.Max(0, zombieBudget - ActiveZombieCount);
            var divisor = Mathf.Max(1, deficitPerBonusZombie);
            var bonus = Mathf.Clamp(deficit / divisor, 0, Mathf.Max(0, maxDynamicWaveBonus));
            count += bonus;

            return Mathf.Clamp(count, 1, Mathf.Max(1, availableSlots));
        }

        private int ResolveHordePulseCount(int availableSlots, int zombieBudget)
        {
            var minCount = Mathf.Max(1, minHordePulseSize);
            var maxCount = Mathf.Max(minCount, maxHordePulseSize);
            var count = Random.Range(minCount, maxCount + 1);

            if (!enableDynamicSpawnBudget) return Mathf.Clamp(count, 1, Mathf.Max(1, availableSlots));

            var deficit = Mathf.Max(0, zombieBudget - ActiveZombieCount);
            var divisor = Mathf.Max(1, deficitPerBonusZombie);
            var bonus = Mathf.Clamp(deficit / divisor, 0, Mathf.Max(0, maxDynamicWaveBonus));
            count += bonus;

            return Mathf.Clamp(count, 1, Mathf.Max(1, availableSlots));
        }

        private int ResolveNearbyThreatDeficit(Vector3 playerPosition, int zombieBudget)
        {
            var desiredNearbyCount = Mathf.Clamp(minimumNearbyZombieCount, 0, Mathf.Max(0, zombieBudget));

            if (desiredNearbyCount <= 0) return 0;

            var nearbyCount = ResolveNearbyThreatCount(playerPosition, Mathf.Max(0f, nearbyThreatRadius));
            return Mathf.Max(0, desiredNearbyCount - nearbyCount);
        }

        private int ResolveNearbyThreatCount(Vector3 centerPosition, float radius)
        {
            if (radius <= 0f) return 0;

            var now = Time.time;
            var centerDelta = centerPosition - _nearbyThreatCachedCenter;
            centerDelta.y = 0f;
            var centerMovedFar = centerDelta.sqrMagnitude > 4f;
            var radiusChanged = Mathf.Abs(radius - _nearbyThreatCachedRadius) > 0.5f;
            var cacheExpired = now >= _nextNearbyThreatRecountAt;
            var registryChanged = _nearbyThreatCacheVersion != _activeZombieRegistryVersion;

            if (!cacheExpired && !centerMovedFar && !radiusChanged && !registryChanged)
                return _cachedNearbyThreatCount;

            _cachedNearbyThreatCount = CountActiveZombiesNearPosition(centerPosition, radius);
            _nearbyThreatCachedCenter = centerPosition;
            _nearbyThreatCachedRadius = radius;
            _nearbyThreatCacheVersion = _activeZombieRegistryVersion;
            _nextNearbyThreatRecountAt = now + Mathf.Max(0.05f, nearbyThreatRecountIntervalSeconds);

            return _cachedNearbyThreatCount;
        }

        private int CountActiveZombiesNearPosition(Vector3 centerPosition, float radius)
        {
            if (radius <= 0f) return 0;

            var radiusSqr = radius * radius;
            var count = 0;
            var cx = centerPosition.x;
            var cz = centerPosition.z;

            foreach (var zombie in _activeZombies)
            {
                if (zombie == null) continue;

                var p = zombie.transform.position;
                var dx = p.x - cx;
                var dz = p.z - cz;
                if (dx * dx + dz * dz <= radiusSqr) count++;
            }

            return count;
        }

        private bool TrySpawnAmbientBatch(ZombieType spawnType, Vector3 centerPosition, int count, float radius)
        {
            if (spawnType == null || count <= 0) return false;

            _spawnedZombieBatchBuffer.Clear();
            SpawnZombieWave(spawnType, centerPosition, count, radius, _spawnedZombieBatchBuffer);

            if (_spawnedZombieBatchBuffer.Count <= 0) return false;

            // Register dense waves as hordes so downstream AI systems can coordinate movement.
            var runtimeHordeManager = hordeManager ?? GetRuntimeHordeManager();
            if (runtimeHordeManager != null && spawnType.hordeAffinity > 0f)
                runtimeHordeManager.CreateHorde(_spawnedZombieBatchBuffer);

            _spawnedZombieBatchBuffer.Clear();
            return true;
        }

        private void MarkAmbientSpawnSuccess()
        {
            _nextGuaranteedAmbientSpawnTime = Time.time + Mathf.Max(1f, guaranteedAmbientSpawnIntervalSeconds);
        }

        private bool TryResolvePlayerPosition(out Vector3 playerPosition)
        {
            var now = Time.time;
            if (_hasCachedPlayerPosition && now < _nextPlayerPositionRefreshAt)
            {
                playerPosition = _cachedPlayerPosition;
                return true;
            }

            playerPosition = Vector3.zero;

            if (UnitManager.Instance == null)
            {
                _hasCachedPlayerPosition = false;
                return false;
            }

            var playerUnits = UnitManager.Instance.GetUnitsByRole(UnitRole.Player, _playerUnitBuffer);

            if (playerUnits is not { Count: > 0 } || playerUnits[0] == null)
            {
                _hasCachedPlayerPosition = false;
                return false;
            }

            playerPosition = playerUnits[0].transform.position;

            _cachedPlayerPosition = playerPosition;
            _hasCachedPlayerPosition = true;
            _nextPlayerPositionRefreshAt = now + Mathf.Max(0.02f, playerPositionRefreshIntervalSeconds);
            return true;
        }
    }
}
