using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zombera.AI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Data;
using Zombera.World;

namespace Zombera.Systems
{
    /// <summary>
    /// Coordinates zombie lifecycle, global zombie counts, and spawn requests.
    /// </summary>
    public sealed class ZombieManager : MonoBehaviour, IGameSystem
    {
        [SerializeField] private ZombieSpawner zombieSpawner;
        [SerializeField] private ZombieAI defaultZombiePrefab;
        [SerializeField] private HordeManager hordeManager;
        [SerializeField] private bool autoCreateSpawnerWhenMissing = true;

        [Header("Death Handling")]
        [SerializeField] private bool keepDeadZombieCorpses = true;
        [SerializeField, Min(0f)] private float deadZombieReturnDelaySeconds = 45f;

        [Header("Ambient Spawn Type")]
        [SerializeField] private ZombieType ambientZombieType;

        [Header("Ambient Spawn Frequency")]
        [SerializeField, Range(0f, 1f)] private float ambientSpawnChancePerSimulationTick = 0.35f;
        [SerializeField] private int maxManagedZombies = 200;
        [SerializeField] private int minAmbientWaveSize = 1;
        [SerializeField] private int maxAmbientWaveSize = 4;

        [Header("High Density Scaling")]
        [SerializeField] private bool enableRealtimeAmbientSpawning = true;
        [SerializeField, Min(0.1f)] private float ambientSpawnEvaluationIntervalSeconds = 2f;
        [SerializeField, Min(1f)] private float guaranteedAmbientSpawnIntervalSeconds = 5f;
        [SerializeField] private bool enableDynamicSpawnBudget = true;
        [SerializeField, Min(1)] private int minimumRuntimeZombieBudget = 320;
        [SerializeField, Min(1)] private int maxManagedZombiesHardCap = 550;
        [SerializeField, Min(0)] private int budgetIncreasePerStep = 45;
        [SerializeField, Min(5f)] private float budgetStepIntervalSeconds = 90f;
        [SerializeField, Min(1)] private int deficitPerBonusZombie = 20;
        [SerializeField, Min(0)] private int maxDynamicWaveBonus = 10;

        [Header("Near Player Pressure")]
        [SerializeField] private bool maintainMinimumNearbyThreat = true;
        [SerializeField, Min(0f)] private float nearbyThreatRadius = 70f;
        [SerializeField, Min(0)] private int minimumNearbyZombieCount = 8;
        [SerializeField, Min(1)] private int maxNearbyCatchupSpawnCount = 6;
        [SerializeField, Min(1f)] private float nearbyCatchupSpawnDistance = 36f;
        [SerializeField, Min(0f)] private float nearbyCatchupSpawnDistanceJitter = 18f;

        [Header("Periodic Horde Pulses")]
        [SerializeField] private bool enablePeriodicHordePulses = true;
        [SerializeField, Min(5f)] private float hordePulseIntervalSeconds = 40f;
        [SerializeField, Min(1)] private int minHordePulseSize = 8;
        [SerializeField, Min(1)] private int maxHordePulseSize = 16;
        [SerializeField, Min(1f)] private float hordePulseRadius = 22f;

        [Header("Runtime Spawner Bootstrap")]
        [SerializeField] private bool prewarmRuntimeSpawnerPool = true;
        [SerializeField, Min(0)] private int runtimeSpawnerPrewarmCount = 120;

        [Header("Ambient Spawn Placement")]
        [SerializeField] private bool useMapWideAmbientSpawns = true;
        [SerializeField] private Terrain ambientSpawnTerrain;
        [SerializeField] private bool useTerrainBoundsForAmbientSpawnArea = true;
        [SerializeField] private Bounds fallbackAmbientSpawnBounds = new Bounds(new Vector3(0f, 40f, 0f), new Vector3(1200f, 80f, 1200f));
        [SerializeField] private int ambientSpawnCenterSampleAttempts = 12;
        [SerializeField] private float minimumAmbientSpawnDistanceFromPlayer = 25f;
        [SerializeField] private bool alignAmbientSpawnCentersToTerrain = true;
        [SerializeField] private bool snapAmbientSpawnCentersToNavMesh = true;
        [SerializeField] private float ambientSpawnNavMeshSampleDistance = 24f;
        [SerializeField] private float ambientSpawnDistanceFromPlayer = 40f;
        [SerializeField] private float ambientSpawnRadius = 12f;

        [Header("Startup Validation Spawn")]
        [SerializeField] private bool spawnValidationZombieNearPlayerOnWorldStart = true;
        [SerializeField, Min(1f)] private float validationSpawnDistanceFromPlayer = 10f;
        [SerializeField, Min(0f)] private float validationSpawnDistanceJitter = 2f;
        [SerializeField] private bool logValidationSpawn = true;

        private readonly HashSet<ZombieAI> activeZombies = new HashSet<ZombieAI>();
        private readonly Dictionary<ZombieAI, float> pendingDeadZombieReturns = new Dictionary<ZombieAI, float>();
        private readonly List<ZombieAI> pendingDeadZombieReturnBuffer = new List<ZombieAI>();
        private readonly List<ZombieAI> spawnedZombieBatchBuffer = new List<ZombieAI>();
        private readonly List<ZombieAI> invalidZombieBuffer = new List<ZombieAI>();
        private readonly List<Unit> playerUnitBuffer = new List<Unit>();
        private ZombieType runtimeFallbackAmbientZombieType;
        private bool hasSpawnedValidationZombieNearPlayer;
        private bool runtimeSpawnerPrewarmed;
        private float initializationTime;
        private float nextAmbientSpawnEvaluationTime;
        private float nextGuaranteedAmbientSpawnTime;
        private float nextHordePulseTime;

        public bool IsInitialized { get; private set; }
        public int ActiveZombieCount => activeZombies.Count;

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            ResolveRuntimeReferences();
            hasSpawnedValidationZombieNearPlayer = false;
            runtimeSpawnerPrewarmed = false;
            initializationTime = Time.time;
            nextAmbientSpawnEvaluationTime = initializationTime;
            nextGuaranteedAmbientSpawnTime = initializationTime;
            nextHordePulseTime = initializationTime + Mathf.Max(5f, hordePulseIntervalSeconds);
            IsInitialized = true;
            EventSystem.Instance?.Subscribe<ZombieSpawnedEvent>(OnZombieSpawned);
            EventSystem.Instance?.Subscribe<UnitDeathEvent>(OnUnitDeath);
            EventSystem.Instance?.Subscribe<WorldSimulationTickEvent>(OnWorldSimulationTick);

            BootstrapFromWorldState();
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
            EventSystem.Instance?.Unsubscribe<ZombieSpawnedEvent>(OnZombieSpawned);
            EventSystem.Instance?.Unsubscribe<UnitDeathEvent>(OnUnitDeath);
            EventSystem.Instance?.Unsubscribe<WorldSimulationTickEvent>(OnWorldSimulationTick);
            activeZombies.Clear();
            pendingDeadZombieReturns.Clear();
            pendingDeadZombieReturnBuffer.Clear();
            spawnedZombieBatchBuffer.Clear();
            invalidZombieBuffer.Clear();
            playerUnitBuffer.Clear();
            hasSpawnedValidationZombieNearPlayer = false;
            runtimeSpawnerPrewarmed = false;
            initializationTime = 0f;
            nextAmbientSpawnEvaluationTime = 0f;
            nextGuaranteedAmbientSpawnTime = 0f;
            nextHordePulseTime = 0f;

            if (runtimeFallbackAmbientZombieType != null)
            {
                Destroy(runtimeFallbackAmbientZombieType);
                runtimeFallbackAmbientZombieType = null;
            }
        }

        private void Update()
        {
            if (!IsInitialized)
            {
                return;
            }

            ProcessPendingDeadZombieReturns();

            if (enableRealtimeAmbientSpawning && TryResolvePlayerPosition(out Vector3 playerPosition))
            {
                TickAmbientSpawn(playerPosition);
            }
        }

        public ZombieAI SpawnZombie(ZombieType zombieType, Vector3 worldPosition)
        {
            ResolveRuntimeReferences();

            if (zombieSpawner == null)
            {
                return null;
            }

            ZombieAI spawned = zombieSpawner.SpawnZombie(zombieType, worldPosition);

            if (spawned != null)
            {
                // Keep tracking robust even when EventSystem is unavailable.
                activeZombies.Add(spawned);
                pendingDeadZombieReturns.Remove(spawned);
            }

            return spawned;
        }

        public void SpawnZombieWave(ZombieType zombieType, Vector3 centerPosition, int count, float radius,
            List<ZombieAI> spawnedOut = null)
        {
            ResolveRuntimeReferences();

            if (zombieSpawner == null)
            {
                return;
            }

            int spawnCount = Mathf.Max(0, count);

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, radius);
                Vector3 spawnPosition = centerPosition + new Vector3(offset.x, 0f, offset.y);
                Vector3 finalizedSpawnPosition = FinalizeSpawnCenter(spawnPosition, ResolveAmbientTerrain(spawnPosition));
                ZombieAI spawned = zombieSpawner.SpawnZombie(zombieType, finalizedSpawnPosition);

                if (spawned != null)
                {
                    activeZombies.Add(spawned);
                    pendingDeadZombieReturns.Remove(spawned);
                    spawnedOut?.Add(spawned);
                }
            }
        }

        public bool TrySpawnValidationZombieNearPlayer(Vector3 playerPosition)
        {
            if (!IsInitialized || !spawnValidationZombieNearPlayerOnWorldStart || hasSpawnedValidationZombieNearPlayer)
            {
                return false;
            }

            ZombieType spawnType = ResolveAmbientZombieType();

            if (spawnType == null)
            {
                return false;
            }

            Vector3 spawnPosition = BuildNearbySpawnPoint(playerPosition, validationSpawnDistanceFromPlayer, validationSpawnDistanceJitter);
            spawnPosition = FinalizeSpawnCenter(spawnPosition, ResolveAmbientTerrain(spawnPosition));
            spawnPosition = ClampValidationSpawnToPlayerNeighborhood(playerPosition, spawnPosition);
            ZombieAI spawned = SpawnZombie(spawnType, spawnPosition);

            if (spawned == null)
            {
                return false;
            }

            hasSpawnedValidationZombieNearPlayer = true;

            if (logValidationSpawn)
            {
                Debug.Log($"[ZombieManager] Startup validation spawn succeeded at {spawned.transform.position} near player {playerPosition}.", this);
            }

            return true;
        }

        public void TickAmbientSpawn(Vector3 playerPosition)
        {
            if (!IsInitialized)
            {
                return;
            }

            ResolveRuntimeReferences();

            if (zombieSpawner == null)
            {
                return;
            }

            if (!CanEvaluateAmbientSpawn())
            {
                return;
            }

            PruneInvalidActiveZombies();

            int zombieBudget = ResolveCurrentZombieBudget();
            int availableSlots = Mathf.Max(0, zombieBudget - ActiveZombieCount);

            if (availableSlots <= 0)
            {
                return;
            }

            ZombieType spawnType = ResolveAmbientZombieType();

            if (spawnType == null)
            {
                return;
            }

            bool spawnedAny = false;

            if (maintainMinimumNearbyThreat && availableSlots > 0)
            {
                int nearbyDeficit = ResolveNearbyThreatDeficit(playerPosition, zombieBudget);

                if (nearbyDeficit > 0)
                {
                    int nearbySpawnCount = Mathf.Min(
                        availableSlots,
                        Mathf.Min(nearbyDeficit, Mathf.Max(1, maxNearbyCatchupSpawnCount)));

                    if (nearbySpawnCount > 0)
                    {
                        Vector3 nearbyCenter = ResolvePlayerRelativeSpawnCenter(
                            playerPosition,
                            nearbyCatchupSpawnDistance,
                            nearbyCatchupSpawnDistanceJitter);

                        if (TrySpawnAmbientBatch(spawnType, nearbyCenter, nearbySpawnCount, ambientSpawnRadius))
                        {
                            spawnedAny = true;
                            availableSlots = Mathf.Max(0, zombieBudget - ActiveZombieCount);
                        }
                    }
                }
            }

            if (enablePeriodicHordePulses && availableSlots > 0 && Time.time >= nextHordePulseTime)
            {
                int hordePulseCount = ResolveHordePulseCount(availableSlots, zombieBudget);

                if (hordePulseCount > 0)
                {
                    Vector3 hordePulseCenter = ResolveAmbientSpawnCenter(playerPosition);

                    if (TrySpawnAmbientBatch(
                            spawnType,
                            hordePulseCenter,
                            hordePulseCount,
                            Mathf.Max(ambientSpawnRadius, hordePulseRadius)))
                    {
                        spawnedAny = true;
                        availableSlots = Mathf.Max(0, zombieBudget - ActiveZombieCount);
                    }
                }

                nextHordePulseTime = Time.time + Mathf.Max(5f, hordePulseIntervalSeconds);
            }

            if (availableSlots <= 0)
            {
                if (spawnedAny)
                {
                    MarkAmbientSpawnSuccess();
                }

                return;
            }

            bool hasNoActiveZombies = ActiveZombieCount <= 0;
            bool shouldForceSpawn = hasNoActiveZombies || Time.time >= nextGuaranteedAmbientSpawnTime;

            if (!shouldForceSpawn)
            {
                float spawnChance = ResolveAmbientSpawnChance(zombieBudget);

                if (Random.value > spawnChance)
                {
                    return;
                }
            }

            int count = ResolveAmbientWaveCount(availableSlots, zombieBudget);

            if (count <= 0)
            {
                return;
            }

            Vector3 spawnCenter = ResolveAmbientSpawnCenter(playerPosition);

            if (TrySpawnAmbientBatch(spawnType, spawnCenter, count, ambientSpawnRadius))
            {
                spawnedAny = true;
            }

            if (spawnedAny)
            {
                MarkAmbientSpawnSuccess();
            }
        }

        public void RegisterZombie(ZombieAI zombie)
        {
            if (zombie == null)
            {
                return;
            }

            activeZombies.Add(zombie);
        }

        public void UnregisterZombie(ZombieAI zombie)
        {
            if (zombie == null)
            {
                return;
            }

            activeZombies.Remove(zombie);
        }

        public List<ZombieAI> GetActiveZombies(List<ZombieAI> result = null)
        {
            List<ZombieAI> zombies = result ?? new List<ZombieAI>(activeZombies.Count);
            zombies.Clear();

            foreach (ZombieAI zombie in activeZombies)
            {
                if (zombie != null)
                {
                    zombies.Add(zombie);
                }
            }

            return zombies;
        }

        private void OnZombieSpawned(ZombieSpawnedEvent gameEvent)
        {
            RegisterZombie(gameEvent.Zombie);
            pendingDeadZombieReturns.Remove(gameEvent.Zombie);
        }

        private void OnUnitDeath(UnitDeathEvent gameEvent)
        {
            if (gameEvent.Role != Characters.UnitRole.Zombie)
            {
                return;
            }

            ZombieAI zombie = gameEvent.UnitObject != null ? gameEvent.UnitObject.GetComponent<ZombieAI>() : null;

            if (zombie == null)
            {
                return;
            }

            UnregisterZombie(zombie);

            if (!keepDeadZombieCorpses)
            {
                ReturnZombieToPoolOrDestroy(zombie);
                return;
            }

            float returnDelay = Mathf.Max(0f, deadZombieReturnDelaySeconds);

            if (returnDelay <= 0f)
            {
                // Keep corpse indefinitely unless explicitly cleaned up elsewhere.
                pendingDeadZombieReturns.Remove(zombie);
                return;
            }

            pendingDeadZombieReturns[zombie] = Time.time + returnDelay;
        }

        private void OnWorldSimulationTick(WorldSimulationTickEvent gameEvent)
        {
            TickAmbientSpawn(gameEvent.PlayerPosition);
        }

        private void ResolveRuntimeReferences()
        {
            if (zombieSpawner == null)
            {
                zombieSpawner = FindFirstObjectByType<ZombieSpawner>();

                if (zombieSpawner == null && autoCreateSpawnerWhenMissing)
                {
                    GameObject runtimeSpawner = new GameObject("RuntimeZombieSpawner");
                    runtimeSpawner.transform.SetParent(transform, false);
                    zombieSpawner = runtimeSpawner.AddComponent<ZombieSpawner>();
                }
            }

            if (zombieSpawner != null && defaultZombiePrefab != null)
            {
                zombieSpawner.SetZombiePrefab(defaultZombiePrefab);
            }

            if (zombieSpawner != null && prewarmRuntimeSpawnerPool && !runtimeSpawnerPrewarmed && runtimeSpawnerPrewarmCount > 0)
            {
                zombieSpawner.PrewarmPool(runtimeSpawnerPrewarmCount);
                runtimeSpawnerPrewarmed = true;
            }

            if (hordeManager == null)
            {
                hordeManager = FindFirstObjectByType<HordeManager>();
            }
        }

        /// <summary>
        /// Rebuilds the active zombie registry from any ZombieAI instances already present
        /// in the scene (placed in-editor or restored by a save load).
        /// </summary>
        private void BootstrapFromWorldState()
        {
            ZombieAI[] sceneZombies = FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);

            for (int i = 0; i < sceneZombies.Length; i++)
            {
                ZombieAI zombie = sceneZombies[i];

                if (zombie != null && !activeZombies.Contains(zombie))
                {
                    activeZombies.Add(zombie);
                }
            }

            if (sceneZombies.Length > 0)
            {
                Debug.Log($"[ZombieManager] Bootstrapped {sceneZombies.Length} zombie(s) from world state.");
            }
        }

        private void ProcessPendingDeadZombieReturns()
        {
            if (pendingDeadZombieReturns.Count == 0)
            {
                return;
            }

            pendingDeadZombieReturnBuffer.Clear();

            foreach (KeyValuePair<ZombieAI, float> pending in pendingDeadZombieReturns)
            {
                ZombieAI zombie = pending.Key;

                if (zombie == null)
                {
                    pendingDeadZombieReturnBuffer.Add(zombie);
                    continue;
                }

                Zombera.Characters.UnitHealth health = zombie.GetComponent<Zombera.Characters.UnitHealth>();
                if (health != null && !health.IsDead)
                {
                    pendingDeadZombieReturnBuffer.Add(zombie);
                    continue;
                }

                if (Time.time < pending.Value)
                {
                    continue;
                }

                pendingDeadZombieReturnBuffer.Add(zombie);
                ReturnZombieToPoolOrDestroy(zombie);
            }

            for (int i = 0; i < pendingDeadZombieReturnBuffer.Count; i++)
            {
                pendingDeadZombieReturns.Remove(pendingDeadZombieReturnBuffer[i]);
            }

            pendingDeadZombieReturnBuffer.Clear();
        }

        private void ReturnZombieToPoolOrDestroy(ZombieAI zombie)
        {
            if (zombie == null)
            {
                return;
            }

            if (zombieSpawner != null)
            {
                zombieSpawner.ReturnToPool(zombie);
                return;
            }

            Destroy(zombie.gameObject);
        }

        private ZombieType ResolveAmbientZombieType()
        {
            if (ambientZombieType != null)
            {
                return ambientZombieType;
            }

            if (runtimeFallbackAmbientZombieType == null)
            {
                runtimeFallbackAmbientZombieType = ScriptableObject.CreateInstance<ZombieType>();
                runtimeFallbackAmbientZombieType.name = "RuntimeAmbientZombieType";
                runtimeFallbackAmbientZombieType.zombieTypeId = "ambient.walker";
                runtimeFallbackAmbientZombieType.displayName = "Ambient Walker";
                runtimeFallbackAmbientZombieType.baseHealth = 50f;
                runtimeFallbackAmbientZombieType.moveSpeed = 1.6f;
                runtimeFallbackAmbientZombieType.attackDamage = 8f;
                runtimeFallbackAmbientZombieType.defaultAiTickInterval = 0.4f;

                Debug.LogWarning("ZombieManager ambientZombieType is unassigned. Using runtime fallback zombie type.", this);
            }

            return runtimeFallbackAmbientZombieType;
        }

        private Vector3 ResolveAmbientSpawnCenter(Vector3 playerPosition)
        {
            if (useMapWideAmbientSpawns && TryGetRandomPointAcrossMap(playerPosition, out Vector3 mapPoint))
            {
                return mapPoint;
            }

            return ResolvePlayerRelativeSpawnCenter(playerPosition);
        }

        private Vector3 ResolvePlayerRelativeSpawnCenter(Vector3 playerPosition)
        {
            return ResolvePlayerRelativeSpawnCenter(playerPosition, ambientSpawnDistanceFromPlayer, 0f);
        }

        private Vector3 ResolvePlayerRelativeSpawnCenter(Vector3 playerPosition, float distanceFromPlayer, float distanceJitter)
        {
            Vector3 spawnCenter = BuildNearbySpawnPoint(playerPosition, distanceFromPlayer, distanceJitter);
            Terrain terrain = ResolveAmbientTerrain(spawnCenter);
            return FinalizeSpawnCenter(spawnCenter, terrain);
        }

        private bool CanEvaluateAmbientSpawn()
        {
            if (!enableRealtimeAmbientSpawning)
            {
                return true;
            }

            float now = Time.time;

            if (now < nextAmbientSpawnEvaluationTime)
            {
                return false;
            }

            nextAmbientSpawnEvaluationTime = now + Mathf.Max(0.1f, ambientSpawnEvaluationIntervalSeconds);
            return true;
        }

        private int ResolveCurrentZombieBudget()
        {
            int baseBudget = Mathf.Max(1, maxManagedZombies);

            if (!enableDynamicSpawnBudget)
            {
                return baseBudget;
            }

            baseBudget = Mathf.Max(baseBudget, minimumRuntimeZombieBudget);
            int hardCap = Mathf.Max(baseBudget, maxManagedZombiesHardCap);

            float elapsed = Mathf.Max(0f, Time.time - initializationTime);
            int stepCount = budgetStepIntervalSeconds > 0.01f
                ? Mathf.FloorToInt(elapsed / Mathf.Max(1f, budgetStepIntervalSeconds))
                : 0;
            int scaledBudget = baseBudget + Mathf.Max(0, budgetIncreasePerStep) * Mathf.Max(0, stepCount);

            return Mathf.Clamp(scaledBudget, baseBudget, hardCap);
        }

        private float ResolveAmbientSpawnChance(int zombieBudget)
        {
            float baseChance = Mathf.Clamp01(ambientSpawnChancePerSimulationTick);

            if (!enableDynamicSpawnBudget || zombieBudget <= 0)
            {
                return baseChance;
            }

            int deficit = Mathf.Max(0, zombieBudget - ActiveZombieCount);
            float pressure = Mathf.Clamp01(deficit / (float)zombieBudget);
            return Mathf.Clamp01(baseChance + pressure * 0.55f);
        }

        private int ResolveAmbientWaveCount(int availableSlots, int zombieBudget)
        {
            int minCount = Mathf.Max(1, minAmbientWaveSize);
            int maxCount = Mathf.Max(minCount, maxAmbientWaveSize);
            int count = Random.Range(minCount, maxCount + 1);

            if (enableDynamicSpawnBudget)
            {
                int deficit = Mathf.Max(0, zombieBudget - ActiveZombieCount);
                int divisor = Mathf.Max(1, deficitPerBonusZombie);
                int bonus = Mathf.Clamp(deficit / divisor, 0, Mathf.Max(0, maxDynamicWaveBonus));
                count += bonus;
            }

            return Mathf.Clamp(count, 1, Mathf.Max(1, availableSlots));
        }

        private int ResolveHordePulseCount(int availableSlots, int zombieBudget)
        {
            int minCount = Mathf.Max(1, minHordePulseSize);
            int maxCount = Mathf.Max(minCount, maxHordePulseSize);
            int count = Random.Range(minCount, maxCount + 1);

            if (enableDynamicSpawnBudget)
            {
                int deficit = Mathf.Max(0, zombieBudget - ActiveZombieCount);
                int divisor = Mathf.Max(1, deficitPerBonusZombie);
                int bonus = Mathf.Clamp(deficit / divisor, 0, Mathf.Max(0, maxDynamicWaveBonus));
                count += bonus;
            }

            return Mathf.Clamp(count, 1, Mathf.Max(1, availableSlots));
        }

        private int ResolveNearbyThreatDeficit(Vector3 playerPosition, int zombieBudget)
        {
            int desiredNearbyCount = Mathf.Clamp(minimumNearbyZombieCount, 0, Mathf.Max(0, zombieBudget));

            if (desiredNearbyCount <= 0)
            {
                return 0;
            }

            int nearbyCount = CountActiveZombiesNearPosition(playerPosition, Mathf.Max(0f, nearbyThreatRadius));
            return Mathf.Max(0, desiredNearbyCount - nearbyCount);
        }

        private int CountActiveZombiesNearPosition(Vector3 centerPosition, float radius)
        {
            if (radius <= 0f)
            {
                return 0;
            }

            int count = 0;
            float radiusSqr = radius * radius;

            foreach (ZombieAI zombie in activeZombies)
            {
                if (zombie == null)
                {
                    continue;
                }

                Vector3 offset = zombie.transform.position - centerPosition;
                offset.y = 0f;

                if (offset.sqrMagnitude <= radiusSqr)
                {
                    count++;
                }
            }

            return count;
        }

        private bool TrySpawnAmbientBatch(ZombieType spawnType, Vector3 centerPosition, int count, float radius)
        {
            if (spawnType == null || count <= 0)
            {
                return false;
            }

            spawnedZombieBatchBuffer.Clear();
            SpawnZombieWave(spawnType, centerPosition, count, radius, spawnedZombieBatchBuffer);

            if (spawnedZombieBatchBuffer.Count <= 0)
            {
                return false;
            }

            // Register dense waves as hordes so downstream AI systems can coordinate movement.
            if (hordeManager != null && spawnType.hordeAffinity > 0f)
            {
                hordeManager.CreateHorde(spawnedZombieBatchBuffer);
            }

            spawnedZombieBatchBuffer.Clear();
            return true;
        }

        private Vector3 ClampValidationSpawnToPlayerNeighborhood(Vector3 playerPosition, Vector3 candidate)
        {
            Vector3 planarOffset = candidate - playerPosition;
            planarOffset.y = 0f;

            float maxAllowedDistance = Mathf.Max(
                validationSpawnDistanceFromPlayer + Mathf.Max(0f, validationSpawnDistanceJitter) + 12f,
                24f);

            if (planarOffset.sqrMagnitude <= maxAllowedDistance * maxAllowedDistance)
            {
                return candidate;
            }

            Vector3 fallbackCandidate = BuildNearbySpawnPoint(playerPosition, validationSpawnDistanceFromPlayer, validationSpawnDistanceJitter);

            if (TrySampleGroundFromPhysics(fallbackCandidate, out float groundY))
            {
                fallbackCandidate.y = groundY;
            }

            Vector3 navSampleOrigin = fallbackCandidate + Vector3.up * 2f;
            float sampleDistance = Mathf.Max(8f, ambientSpawnNavMeshSampleDistance);
            const int walkableAreaMask = 1 << 0;

            if (NavMesh.SamplePosition(navSampleOrigin, out NavMeshHit navHit, sampleDistance, walkableAreaMask) ||
                NavMesh.SamplePosition(navSampleOrigin, out navHit, sampleDistance, NavMesh.AllAreas))
            {
                fallbackCandidate = navHit.position;
            }

            if (logValidationSpawn)
            {
                Debug.LogWarning(
                    $"[ZombieManager] Validation spawn candidate was too far from player ({planarOffset.magnitude:F1}m). Clamped to {fallbackCandidate}.",
                    this);
            }

            return fallbackCandidate;
        }

        private void MarkAmbientSpawnSuccess()
        {
            nextGuaranteedAmbientSpawnTime = Time.time + Mathf.Max(1f, guaranteedAmbientSpawnIntervalSeconds);
        }

        private bool TryResolvePlayerPosition(out Vector3 playerPosition)
        {
            playerPosition = Vector3.zero;

            if (UnitManager.Instance == null)
            {
                return false;
            }

            List<Unit> playerUnits = UnitManager.Instance.GetUnitsByRole(UnitRole.Player, playerUnitBuffer);

            if (playerUnits.Count <= 0 || playerUnits[0] == null)
            {
                return false;
            }

            playerPosition = playerUnits[0].transform.position;
            return true;
        }

        private void PruneInvalidActiveZombies()
        {
            if (activeZombies.Count <= 0)
            {
                return;
            }

            invalidZombieBuffer.Clear();

            foreach (ZombieAI zombie in activeZombies)
            {
                if (zombie == null)
                {
                    invalidZombieBuffer.Add(zombie);
                }
            }

            for (int i = 0; i < invalidZombieBuffer.Count; i++)
            {
                activeZombies.Remove(invalidZombieBuffer[i]);
            }

            invalidZombieBuffer.Clear();
        }

        private static Vector3 BuildNearbySpawnPoint(Vector3 playerPosition, float distanceFromPlayer, float distanceJitter)
        {
            Vector2 direction2D = Random.insideUnitCircle;

            if (direction2D.sqrMagnitude <= 0.0001f)
            {
                direction2D = Vector2.right;
            }

            direction2D.Normalize();
            float spawnDistance = Mathf.Max(1f, distanceFromPlayer) + Random.Range(0f, Mathf.Max(0f, distanceJitter));
            return playerPosition + new Vector3(direction2D.x, 0f, direction2D.y) * spawnDistance;
        }

        private bool TryGetRandomPointAcrossMap(Vector3 playerPosition, out Vector3 spawnPoint)
        {
            spawnPoint = playerPosition;

            if (!TryResolveAmbientSpawnBounds(playerPosition, out Bounds spawnBounds, out Terrain terrain))
            {
                return false;
            }

            int attempts = Mathf.Max(1, ambientSpawnCenterSampleAttempts);
            float minimumDistance = Mathf.Max(0f, minimumAmbientSpawnDistanceFromPlayer);
            float minimumDistanceSqr = minimumDistance * minimumDistance;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Vector3 candidate = new Vector3(
                    Random.Range(spawnBounds.min.x, spawnBounds.max.x),
                    spawnBounds.center.y,
                    Random.Range(spawnBounds.min.z, spawnBounds.max.z));

                candidate = FinalizeSpawnCenter(candidate, terrain);

                if (minimumDistanceSqr > 0f)
                {
                    Vector3 toPlayer = candidate - playerPosition;
                    toPlayer.y = 0f;

                    if (toPlayer.sqrMagnitude < minimumDistanceSqr)
                    {
                        continue;
                    }
                }

                spawnPoint = candidate;
                return true;
            }

            return false;
        }

        private bool TryResolveAmbientSpawnBounds(Vector3 playerPosition, out Bounds bounds, out Terrain terrain)
        {
            terrain = ResolveAmbientTerrain(playerPosition);

            if (useTerrainBoundsForAmbientSpawnArea && terrain != null && terrain.terrainData != null)
            {
                Vector3 terrainSize = terrain.terrainData.size;

                if (terrainSize.x > 0.01f && terrainSize.z > 0.01f)
                {
                    Vector3 terrainPosition = terrain.GetPosition();
                    bounds = new Bounds(
                        terrainPosition + new Vector3(terrainSize.x * 0.5f, terrainSize.y * 0.5f, terrainSize.z * 0.5f),
                        terrainSize);

                    return true;
                }
            }

            if (fallbackAmbientSpawnBounds.size.x > 0.01f && fallbackAmbientSpawnBounds.size.z > 0.01f)
            {
                bounds = fallbackAmbientSpawnBounds;
                return true;
            }

            bounds = default;
            return false;
        }

        private Vector3 FinalizeSpawnCenter(Vector3 candidate, Terrain terrainHint)
        {
            Terrain terrain = terrainHint != null ? terrainHint : ResolveAmbientTerrain(candidate);
            bool hasSurfaceHeight = false;
            float surfaceY = candidate.y;

            if (alignAmbientSpawnCentersToTerrain && terrain != null && terrain.terrainData != null)
            {
                Vector3 terrainOrigin = terrain.GetPosition();
                Vector3 terrainSize = terrain.terrainData.size;

                float minX = terrainOrigin.x;
                float maxX = terrainOrigin.x + terrainSize.x;
                float minZ = terrainOrigin.z;
                float maxZ = terrainOrigin.z + terrainSize.z;

                float clampedX = Mathf.Clamp(candidate.x, minX, maxX);
                float clampedZ = Mathf.Clamp(candidate.z, minZ, maxZ);

                candidate.x = clampedX;
                candidate.z = clampedZ;

                surfaceY = terrain.SampleHeight(new Vector3(clampedX, terrainOrigin.y, clampedZ)) + terrainOrigin.y;
                candidate.y = surfaceY;
                hasSurfaceHeight = true;
            }

            if (TrySampleGroundFromPhysics(candidate, out float physicsGroundY))
            {
                if (!hasSurfaceHeight || physicsGroundY > surfaceY)
                {
                    surfaceY = physicsGroundY;
                    hasSurfaceHeight = true;
                }

                candidate.y = surfaceY;
            }

            if (snapAmbientSpawnCentersToNavMesh)
            {
                Vector3 sampleOrigin = candidate + Vector3.up * 2f;
                float sampleDistance = Mathf.Max(0.5f, ambientSpawnNavMeshSampleDistance);
                const int walkableAreaMask = 1 << 0;


                bool hasNavMeshHit =
                    NavMesh.SamplePosition(sampleOrigin, out NavMeshHit navHit, sampleDistance, walkableAreaMask) ||
                    NavMesh.SamplePosition(sampleOrigin, out navHit, sampleDistance, NavMesh.AllAreas);

                if (hasNavMeshHit)
                {
                    // Ignore stale/old navmesh hits that sit below the sampled ground surface.
                    if (!hasSurfaceHeight || navHit.position.y >= surfaceY - 0.25f)
                    {
                        candidate = navHit.position;
                    }
                }
            }

            if (hasSurfaceHeight && candidate.y < surfaceY - 0.05f)
            {
                candidate.y = surfaceY;
            }

            return candidate;
        }

        private static bool TrySampleGroundFromPhysics(Vector3 worldPosition, out float groundY)
        {
            Vector3 rayOrigin = worldPosition + Vector3.up * 1200f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2600f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
                return true;
            }

            groundY = worldPosition.y;
            return false;
        }

        private Terrain ResolveAmbientTerrain(Vector3 referencePosition)
        {
            Terrain resolvedTerrain = TerrainResolver.ResolveTerrainForPosition(referencePosition, ambientSpawnTerrain);

            if (resolvedTerrain != null)
            {
                ambientSpawnTerrain = resolvedTerrain;
            }

            return resolvedTerrain;
        }
    }
}