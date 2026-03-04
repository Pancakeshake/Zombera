using System.Collections.Generic;
using UnityEngine;
using Zombera.AI;
using Zombera.Core;
using Zombera.Data;

namespace Zombera.Systems
{
    /// <summary>
    /// Coordinates zombie lifecycle, global zombie counts, and spawn requests.
    /// </summary>
    public sealed class ZombieManager : MonoBehaviour, IGameSystem
    {
        [SerializeField] private ZombieSpawner zombieSpawner;
        [SerializeField] private HordeManager hordeManager;
        [SerializeField] private ZombieType ambientZombieType;
        [SerializeField, Range(0f, 1f)] private float ambientSpawnChancePerSimulationTick = 0.35f;
        [SerializeField] private int maxManagedZombies = 200;
        [SerializeField] private int minAmbientWaveSize = 1;
        [SerializeField] private int maxAmbientWaveSize = 4;
        [SerializeField] private float ambientSpawnDistanceFromPlayer = 40f;
        [SerializeField] private float ambientSpawnRadius = 12f;

        private readonly HashSet<ZombieAI> activeZombies = new HashSet<ZombieAI>();

        public bool IsInitialized { get; private set; }
        public int ActiveZombieCount => activeZombies.Count;

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            IsInitialized = true;
            EventSystem.Instance?.Subscribe<ZombieSpawnedEvent>(OnZombieSpawned);
            EventSystem.Instance?.Subscribe<UnitDeathEvent>(OnUnitDeath);
            EventSystem.Instance?.Subscribe<WorldSimulationTickEvent>(OnWorldSimulationTick);

            // TODO: Bootstrap zombie populations from save/world state.
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
        }

        public ZombieAI SpawnZombie(ZombieType zombieType, Vector3 worldPosition)
        {
            if (zombieSpawner == null)
            {
                return null;
            }

            return zombieSpawner.SpawnZombie(zombieType, worldPosition);
        }

        public void SpawnZombieWave(ZombieType zombieType, Vector3 centerPosition, int count, float radius)
        {
            zombieSpawner?.SpawnWave(zombieType, centerPosition, count, radius);
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
            zombieSpawner?.ReturnToPool(zombie);
        }

        private void OnWorldSimulationTick(WorldSimulationTickEvent gameEvent)
        {
            if (!IsInitialized || zombieSpawner == null || ambientZombieType == null)
            {
                return;
            }

            if (ActiveZombieCount >= maxManagedZombies)
            {
                return;
            }

            if (Random.value > ambientSpawnChancePerSimulationTick)
            {
                return;
            }

            int minCount = Mathf.Max(1, minAmbientWaveSize);
            int maxCount = Mathf.Max(minCount, maxAmbientWaveSize);
            int count = Random.Range(minCount, maxCount + 1);

            Vector2 direction2D = Random.insideUnitCircle;

            if (direction2D.sqrMagnitude <= 0.0001f)
            {
                direction2D = Vector2.right;
            }

            direction2D.Normalize();

            Vector3 spawnCenter = gameEvent.PlayerPosition + new Vector3(direction2D.x, 0f, direction2D.y) * ambientSpawnDistanceFromPlayer;
            SpawnZombieWave(ambientZombieType, spawnCenter, count, ambientSpawnRadius);

            // TODO: Drive hordeManager composition updates during simulation pulse.
            _ = hordeManager;
        }
    }
}