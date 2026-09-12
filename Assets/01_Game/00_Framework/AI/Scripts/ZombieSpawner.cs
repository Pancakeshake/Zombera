using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;
using Zombera.Data;
using Zombera.Characters;

namespace Zombera.AI
{
    /// <summary>
    /// Handles runtime pooling and spawning of zombie entities.
    /// Provides performance-optimized prewarming and reuse of zombie controllers.
    /// </summary>
    public sealed class ZombieSpawner : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private ZombieController zombiePrefab;
        [SerializeField] private Transform poolParent;
        [SerializeField] private bool autoInitializeOnSpawn = true;

        private readonly Queue<ZombieController> _pool = new();
        private readonly HashSet<ZombieController> _pooledLookup = new();
        private int _totalSpawnedCount;

        /// <summary>
        /// Sets the prefab used for spawning when the pool is empty or during prewarm.
        /// </summary>
        public void SetZombiePrefab(ZombieController prefab)
        {
            zombiePrefab = prefab;
        }

        /// <summary>
        /// Spawns a zombie of the specified type at the given position.
        /// If the pool is empty, a new instance is created.
        /// </summary>
        public ZombieController SpawnZombie(ZombieType zombieType, Vector3 position)
        {
            var zombie = GetFromPool();
            
            if (zombie == null)
            {
                if (zombiePrefab == null)
                {
                    Debug.LogError("[ZombieSpawner] Cannot spawn zombie: No prefab assigned.", this);
                    return null;
                }
                
                zombie = Instantiate(zombiePrefab, poolParent != null ? poolParent : transform);
            }

            // Use the shared NavMesh-safe placement helper instead of direct transform move
            UnitNavUtils.PlaceUnitOnNavMesh(zombie.gameObject, position, 8f);
            
            zombie.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            zombie.gameObject.SetActive(true);

            // Note: Applying ZombieType data to the controller would happen here if supported.
            // For now, we call Initialize as expected by the systems.
            if (autoInitializeOnSpawn)
            {
                zombie.Initialize();
            }

            // Lets ZombieManager (and any other listener) track zombies spawned by
            // callers that bypass it (e.g. region materialization).
            CoreEventBus.PublishGlobal(new ZombieSpawnedEvent
            {
                ZombieTypeId = zombieType != null ? zombieType.name : null,
                Position = zombie.transform.position,
                Zombie = zombie.gameObject
            });

            return zombie;
        }

        /// <summary>
        /// Returns a zombie controller to the pool for reuse.
        /// </summary>
        public void ReturnToPool(ZombieController zombie)
        {
            if (zombie == null) return;

            zombie.SetActive(false);
            zombie.gameObject.SetActive(false);
            zombie.transform.SetParent(poolParent != null ? poolParent : transform);

            // HashSet membership check keeps pool returns O(1) instead of O(n) Queue.Contains.
            if (_pooledLookup.Add(zombie))
            {
                _pool.Enqueue(zombie);
            }
        }

        /// <summary>
        /// Incremental prewarm step. Returns true when the target count is reached.
        /// </summary>
        public bool PrewarmPoolStep(int targetCount, int stepCount)
        {
            if (zombiePrefab == null) return true;

            int spawnedThisStep = 0;
            while (_pool.Count < targetCount && spawnedThisStep < stepCount)
            {
                var zombie = Instantiate(zombiePrefab, poolParent != null ? poolParent : transform);
                zombie.gameObject.SetActive(false);
                if (_pooledLookup.Add(zombie)) _pool.Enqueue(zombie);
                spawnedThisStep++;
            }

            return _pool.Count >= targetCount;
        }

        private ZombieController GetFromPool()
        {
            while (_pool.Count > 0)
            {
                var zombie = _pool.Dequeue();
                _pooledLookup.Remove(zombie);
                if (zombie != null) return zombie;
            }
            return null;
        }
    }
}
