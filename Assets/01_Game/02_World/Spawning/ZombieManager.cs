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
    /// <summary>
    ///     Coordinates zombie lifecycle, global zombie counts, and spawn requests.
    /// </summary>
    public sealed partial class ZombieManager : MonoBehaviour, IGameSystem
    {
        [Serializable]
        private sealed class WeightedZombieTypeEntry
        {
            public ZombieType zombieType;

            [Min(0f)] public float weight = 1f;
        }

        [SerializeField] private ZombieSpawner zombieSpawner;
        [SerializeField] private ZombieController defaultZombiePrefab;
        [SerializeField] private ZombieHordeManager hordeManager;
        [SerializeField] private bool autoCreateSpawnerWhenMissing = true;

        [Header("Death Handling")] [SerializeField]
        private bool keepDeadZombieCorpses = true;

        [SerializeField] [Min(0f)] private float deadZombieReturnDelaySeconds = 45f;

        [Header("Ambient Spawn Type")] [SerializeField]
        private ZombieType ambientZombieType;

        [SerializeField] private List<WeightedZombieTypeEntry> weightedAmbientZombieTypes = new();

        [Header("Ambient Spawn Frequency")] [SerializeField] [Range(0f, 1f)]
        private float ambientSpawnChancePerSimulationTick = 0.35f;

        [SerializeField] private int maxManagedZombies = 120;
        [SerializeField] private int minAmbientWaveSize = 1;
        [SerializeField] private int maxAmbientWaveSize = 4;

        [Header("High Density Scaling")] [SerializeField]
        private bool enableRealtimeAmbientSpawning = true;

        [SerializeField] [Min(0.1f)] private float ambientSpawnEvaluationIntervalSeconds = 3f;
        [SerializeField] [Min(0.02f)] private float playerPositionRefreshIntervalSeconds = 0.08f;
        [SerializeField] [Min(0.05f)] private float spawnNavMeshReadyRecheckSeconds = 0.25f;
        [SerializeField] [Min(1f)] private float guaranteedAmbientSpawnIntervalSeconds = 8f;
        [SerializeField] private bool enableDynamicSpawnBudget = true;
        [SerializeField] [Min(1)] private int minimumRuntimeZombieBudget = 160;
        [SerializeField] [Min(1)] private int maxManagedZombiesHardCap = 260;
        [SerializeField] [Min(0)] private int budgetIncreasePerStep = 45;
        [SerializeField] [Min(5f)] private float budgetStepIntervalSeconds = 90f;
        [SerializeField] [Min(1)] private int deficitPerBonusZombie = 20;
        [SerializeField] [Min(0)] private int maxDynamicWaveBonus = 4;

        [Header("Near Player Pressure")] [SerializeField]
        private bool maintainMinimumNearbyThreat = true;

        [SerializeField] [Min(0f)] private float nearbyThreatRadius = 70f;
        [SerializeField] [Min(0)] private int minimumNearbyZombieCount = 8;
        [SerializeField] [Min(1)] private int maxNearbyCatchupSpawnCount = 3;
        [SerializeField] [Min(1f)] private float nearbyCatchupSpawnDistance = 36f;
        [SerializeField] [Min(0f)] private float nearbyCatchupSpawnDistanceJitter = 18f;
        [SerializeField] [Min(0.05f)] private float nearbyThreatRecountIntervalSeconds = 0.25f;

        [Header("Periodic Horde Pulses")] [SerializeField]
        private bool enablePeriodicHordePulses = true;

        [SerializeField] [Min(5f)] private float hordePulseIntervalSeconds = 75f;
        [SerializeField] [Min(1)] private int minHordePulseSize = 4;
        [SerializeField] [Min(1)] private int maxHordePulseSize = 8;
        [SerializeField] [Min(1f)] private float hordePulseRadius = 22f;

        [Header("Runtime Spawner Bootstrap")] [SerializeField]
        private bool prewarmRuntimeSpawnerPool = true;

        [SerializeField] [Min(0)] private int runtimeSpawnerPrewarmCount = 40;
        [SerializeField] [Range(1, 64)] private int runtimeSpawnerPrewarmPerFrame = 1;
        [SerializeField] [Min(0f)] private float runtimeSpawnerPrewarmStartDelaySeconds = 3f;
        [SerializeField] private bool prewarmOnlyDuringGameplayState = true;

        [Header("Loading Screen Prewarm")]
        [SerializeField] private bool preloadSpawnerPoolDuringLoadingScreen = true;
        [SerializeField] [Min(0)] private int loadingScreenPrewarmTargetCount = 12;
        [SerializeField] [Range(1, 32)] private int loadingScreenPrewarmPerStep = 1;

        [Header("Ambient Spawn Placement")] [SerializeField]
        private bool useMapWideAmbientSpawns = true;

        [SerializeField] private Terrain ambientSpawnTerrain;
        [SerializeField] private bool useTerrainBoundsForAmbientSpawnArea = true;

        [SerializeField] private Bounds fallbackAmbientSpawnBounds =
            new(new Vector3(0f, 40f, 0f), new Vector3(1200f, 80f, 1200f));

        [SerializeField] private int ambientSpawnCenterSampleAttempts = 12;
        [SerializeField] private float minimumAmbientSpawnDistanceFromPlayer = 25f;
        [SerializeField] private bool alignAmbientSpawnCentersToTerrain = true;
        [SerializeField] private bool snapAmbientSpawnCentersToNavMesh = true;
        [SerializeField] private float ambientSpawnNavMeshSampleDistance = 24f;
        [SerializeField] private bool requireNavMeshForZombieSpawns = true;
        [SerializeField] [Min(0.5f)] private float requiredZombieSpawnNavMeshSampleDistance = 24f;
        [SerializeField] private float ambientSpawnDistanceFromPlayer = 40f;
        [SerializeField] private float ambientSpawnRadius = 12f;

        [Header("Spawn Placement Validation")] [SerializeField]
        private bool validateZombieSpawnPlacement = true;

        [SerializeField] private bool rejectZombieSpawnsInsideBuildingColliders = true;
        [SerializeField] [Min(0.1f)] private float zombieSpawnClearanceRadius = 0.45f;
        [SerializeField] [Min(0.3f)] private float zombieSpawnClearanceHeight = 1.8f;
        [SerializeField] private LayerMask zombieSpawnBlockingMask = ~0;
        [SerializeField] [Min(1)] private int zombieSpawnPlacementValidationAttempts = 6;
        [SerializeField] [Min(0f)] private float zombieSpawnValidationJitterRadius = 2.25f;
        [SerializeField] private bool requireReachablePathToPlayer = true;
        [SerializeField] private bool logSpawnPlacementDiagnostics;

        [Header("Startup Validation Spawn")] [SerializeField]
        private bool spawnValidationZombieNearPlayerOnWorldStart = true;

        [SerializeField] [Min(1f)] private float validationSpawnDistanceFromPlayer = 10f;
        [SerializeField] [Min(0f)] private float validationSpawnDistanceJitter = 2f;
        [SerializeField] private bool logValidationSpawn = true;

        private readonly HashSet<ZombieController> _activeZombies = new();
        private readonly List<ZombieController> _invalidZombieBuffer = new();
        private readonly List<ZombieController> _pendingDeadZombieReturnBuffer = new();
        private readonly Collider[] _spawnValidationOverlapBuffer = new Collider[32];
        private NavMeshPath _spawnValidationPath;
        private readonly Dictionary<ZombieController, float> _pendingDeadZombieReturns = new();
        private readonly List<Unit> _playerUnitBuffer = new();
        private readonly List<ZombieController> _spawnedZombieBatchBuffer = new();
        private bool _hasSpawnedValidationZombieNearPlayer;
        private float _initializationTime;
        private float _nextAmbientSpawnEvaluationTime;
        private float _nextGuaranteedAmbientSpawnTime;
        private float _nextHordePulseTime;
        private ZombieType _runtimeFallbackAmbientZombieType;
        private bool _runtimeSpawnerPrewarmed;
        private bool _runtimeSpawnerPrewarmQueued;
        private bool _loadingScreenPrewarmComplete;
        private bool _runtimeSpawnSuppressed;
        private int _cachedBudgetStep = -1;
        private int _cachedZombieBudget;
        private bool _hasCachedPlayerPosition;
        private Vector3 _cachedPlayerPosition;
        private float _nextPlayerPositionRefreshAt;
        private bool _cachedSpawnNavMeshReady;
        private Vector3 _cachedSpawnNavMeshReference;
        private float _nextSpawnNavMeshReadyCheckAt;
        private int _activeZombieRegistryVersion;
        private int _nearbyThreatCacheVersion = -1;
        private int _cachedNearbyThreatCount;
        private float _nextNearbyThreatRecountAt;
        private Vector3 _nearbyThreatCachedCenter;
        private float _nearbyThreatCachedRadius;
        public int ActiveZombieCount => _activeZombies.Count;
        public bool HasSpawnedValidationZombieNearPlayer => _hasSpawnedValidationZombieNearPlayer;
        public bool ValidationSpawnEnabled => spawnValidationZombieNearPlayerOnWorldStart;
        public bool IsRuntimeSpawningSuppressed => _runtimeSpawnSuppressed;
        public bool IsInitialized { get; private set; }


        public void Initialize()
        {
            if (IsInitialized) return;

            EnsureRuntimeDependenciesResolved();
            _hasSpawnedValidationZombieNearPlayer = false;
            _runtimeSpawnerPrewarmed = false;
            _runtimeSpawnerPrewarmQueued = false;
            _loadingScreenPrewarmComplete = false;
            _runtimeSpawnSuppressed = false;
            _cachedBudgetStep = -1;
            _cachedZombieBudget = 0;
            _hasCachedPlayerPosition = false;
            _cachedPlayerPosition = Vector3.zero;
            _nextPlayerPositionRefreshAt = 0f;
            _cachedSpawnNavMeshReady = false;
            _cachedSpawnNavMeshReference = Vector3.zero;
            _nextSpawnNavMeshReadyCheckAt = 0f;
            _activeZombieRegistryVersion = 0;
            _nearbyThreatCacheVersion = -1;
            _cachedNearbyThreatCount = 0;
            _nextNearbyThreatRecountAt = 0f;
            _nearbyThreatCachedCenter = Vector3.zero;
            _nearbyThreatCachedRadius = 0f;
            _initializationTime = Time.time;
            _nextAmbientSpawnEvaluationTime = _initializationTime;
            _nextGuaranteedAmbientSpawnTime = _initializationTime;
            _nextHordePulseTime = _initializationTime + Mathf.Max(5f, hordePulseIntervalSeconds);
            IsInitialized = true;
            CoreEventBus.Instance?.Subscribe<ZombieSpawnedEvent>(OnZombieSpawned);
            CoreEventBus.Instance?.Subscribe<UnitDeathEvent>(OnUnitDeath);
            CoreEventBus.Instance?.Subscribe<WorldSimulationTickEvent>(OnWorldSimulationTick);

            BootstrapFromWorldState();
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;

            IsInitialized = false;
            CoreEventBus.Instance?.Unsubscribe<ZombieSpawnedEvent>(OnZombieSpawned);
            CoreEventBus.Instance?.Unsubscribe<UnitDeathEvent>(OnUnitDeath);
            CoreEventBus.Instance?.Unsubscribe<WorldSimulationTickEvent>(OnWorldSimulationTick);
            _activeZombies.Clear();
            _pendingDeadZombieReturns.Clear();
            _pendingDeadZombieReturnBuffer.Clear();
            _spawnedZombieBatchBuffer.Clear();
            _invalidZombieBuffer.Clear();
            _playerUnitBuffer.Clear();
            _hasSpawnedValidationZombieNearPlayer = false;
            _runtimeSpawnerPrewarmed = false;
            _runtimeSpawnerPrewarmQueued = false;
            _loadingScreenPrewarmComplete = false;
            _runtimeSpawnSuppressed = false;
            _cachedBudgetStep = -1;
            _cachedZombieBudget = 0;
            _hasCachedPlayerPosition = false;
            _cachedPlayerPosition = Vector3.zero;
            _nextPlayerPositionRefreshAt = 0f;
            _cachedSpawnNavMeshReady = false;
            _cachedSpawnNavMeshReference = Vector3.zero;
            _nextSpawnNavMeshReadyCheckAt = 0f;
            _activeZombieRegistryVersion = 0;
            _nearbyThreatCacheVersion = -1;
            _cachedNearbyThreatCount = 0;
            _nextNearbyThreatRecountAt = 0f;
            _nearbyThreatCachedCenter = Vector3.zero;
            _nearbyThreatCachedRadius = 0f;
            _initializationTime = 0f;
            _nextAmbientSpawnEvaluationTime = 0f;
            _nextGuaranteedAmbientSpawnTime = 0f;
            _nextHordePulseTime = 0f;

            if (_runtimeFallbackAmbientZombieType == null) return;

            Destroy(_runtimeFallbackAmbientZombieType);
            _runtimeFallbackAmbientZombieType = null;
        }

        public ZombieController SpawnZombie(ZombieType zombieType, Vector3 worldPosition)
        {
            if (_runtimeSpawnSuppressed) return null;

            var runtimeSpawner = GetRuntimeZombieSpawner();
            if (runtimeSpawner == null) return null;

            if (!TryResolveValidatedSpawnPosition(worldPosition, out worldPosition)) return null;

            var spawned = runtimeSpawner.SpawnZombie(zombieType, worldPosition);

            if (spawned == null) return null;

            // Keep tracking robust even when EventSystem is unavailable.
            AddZombieToRegistry(spawned);
            _pendingDeadZombieReturns.Remove(spawned);

            return spawned;
        }

        public void SpawnZombieWave(ZombieType zombieType, Vector3 centerPosition, int count, float radius,
            List<ZombieController> spawnedOut = null)
        {
            if (_runtimeSpawnSuppressed) return;

            var runtimeSpawner = GetRuntimeZombieSpawner();
            if (runtimeSpawner == null) return;

            var spawnCount = Mathf.Max(0, count);
            var hasPlayerPosition = TryResolvePlayerPosition(out var playerPosition);

            for (var i = 0; i < spawnCount; i++)
            {
                var offset = Random.insideUnitCircle * Mathf.Max(0f, radius);
                var spawnPosition = centerPosition + new Vector3(offset.x, 0f, offset.y);
                var finalizedSpawnPosition = FinalizeSpawnCenter(spawnPosition, ResolveAmbientTerrain(spawnPosition));
                if (!TryResolveValidatedSpawnPosition(
                        finalizedSpawnPosition,
                        out finalizedSpawnPosition,
                        hasPlayerPosition ? playerPosition : (Vector3?)null))
                    continue;

                var spawned = runtimeSpawner.SpawnZombie(zombieType, finalizedSpawnPosition);

                if (spawned == null) continue;

                AddZombieToRegistry(spawned);
                _pendingDeadZombieReturns.Remove(spawned);
                spawnedOut?.Add(spawned);
            }
        }

        /// <summary>
        ///     Resolves the same ambient zombie type selection used by ambient waves so other
        ///     systems (for example world events) can mirror ambient spawn weighting.
        /// </summary>
        public ZombieType ResolveAmbientZombieTypeForSpawn()
        {
            return ResolveAmbientZombieType();
        }

        public bool TrySpawnValidationZombieNearPlayer(Vector3 playerPosition)
        {
            if (!IsInitialized || !spawnValidationZombieNearPlayerOnWorldStart ||
                _hasSpawnedValidationZombieNearPlayer || _runtimeSpawnSuppressed) return false;

            var spawnType = ResolveAmbientZombieType();

            if (spawnType == null) return false;

            var spawnPosition = BuildNearbySpawnPoint(playerPosition, validationSpawnDistanceFromPlayer,
                validationSpawnDistanceJitter);
            spawnPosition = FinalizeSpawnCenter(spawnPosition, ResolveAmbientTerrain(spawnPosition));
            spawnPosition = ClampValidationSpawnToPlayerNeighborhood(playerPosition, spawnPosition);
            var spawned = SpawnZombie(spawnType, spawnPosition);

            if (spawned == null) return false;

            _hasSpawnedValidationZombieNearPlayer = true;

            if (logValidationSpawn)
                DebugLogger.LogTrace(
                    LogCategory.World,
                    $"[ZombieManager] Startup validation spawn succeeded at {spawned.transform.position} near player {playerPosition}.",
                    this);

            return true;
        }

        public void TickAmbientSpawn(Vector3 playerPosition)
        {
            if (_runtimeSpawnSuppressed) return;
            if (!TryBuildAmbientSpawnPolicyContext(playerPosition, out var context)) return;
            if (TryExecuteAmbientSpawnPolicy(ref context)) MarkAmbientSpawnSuccess();
        }

        public void SetRuntimeSpawningSuppressed(bool suppressed)
        {
            _runtimeSpawnSuppressed = suppressed;

            if (!IsInitialized || suppressed) return;

            var now = Time.time;
            _nextAmbientSpawnEvaluationTime = now;
            _nextGuaranteedAmbientSpawnTime = now;
            _nextHordePulseTime = now + Mathf.Max(5f, hordePulseIntervalSeconds);
        }

        public void RegisterZombie(ZombieController zombie)
        {
            AddZombieToRegistry(zombie);
        }

        public void UnregisterZombie(ZombieController zombie)
        {
            RemoveZombieFromRegistry(zombie);
        }

        public List<ZombieController> GetActiveZombies(List<ZombieController> result = null)
        {
            return CopyActiveZombies(result);
        }

        public bool TryRunLoadingScreenPrewarmStep()
        {
            if (!preloadSpawnerPoolDuringLoadingScreen || loadingScreenPrewarmTargetCount <= 0) return true;

            var runtimeSpawner = GetRuntimeZombieSpawner();

            if (_loadingScreenPrewarmComplete) return true;
            if (runtimeSpawner == null) return true;

            var target = Mathf.Max(0, loadingScreenPrewarmTargetCount);
            var perStep = Mathf.Clamp(loadingScreenPrewarmPerStep, 1, 32);
            _loadingScreenPrewarmComplete = runtimeSpawner.PrewarmPoolStep(target, perStep);
            return _loadingScreenPrewarmComplete;
        }
    }
}
