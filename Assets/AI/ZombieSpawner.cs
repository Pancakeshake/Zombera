using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;
using Zombera.Data;

namespace Zombera.AI
{
    /// <summary>
    /// Responsible for spawning zombies from type data and regional difficulty rules.
    /// </summary>
    public sealed class ZombieSpawner : MonoBehaviour
    {
        [SerializeField] private ZombieAI zombiePrefab;
        [SerializeField] private bool useObjectPooling = true;
        [SerializeField] private int initialPoolSize = 24;

        private readonly Queue<ZombieAI> zombiePool = new Queue<ZombieAI>();
        private readonly HashSet<ZombieAI> activeZombies = new HashSet<ZombieAI>();

        private void Awake()
        {
            if (useObjectPooling)
            {
                PrewarmPool(initialPoolSize);
            }
        }

        public void PrewarmPool(int count)
        {
            if (zombiePrefab == null || count <= 0)
            {
                return;
            }

            while (zombiePool.Count < count)
            {
                ZombieAI zombie = Instantiate(zombiePrefab, transform);
                zombie.gameObject.SetActive(false);
                zombiePool.Enqueue(zombie);
            }
        }

        public ZombieAI SpawnZombie(ZombieType zombieType, Vector3 position)
        {
            if (zombiePrefab == null)
            {
                return null;
            }

            ZombieAI zombie = GetOrCreateZombie();

            zombie.transform.SetPositionAndRotation(position, Quaternion.identity);
            zombie.gameObject.SetActive(true);
            zombie.Initialize();
            activeZombies.Add(zombie);

            EventSystem.PublishGlobal(new ZombieSpawnedEvent
            {
                ZombieTypeId = zombieType != null ? zombieType.zombieTypeId : string.Empty,
                Position = position,
                Zombie = zombie
            });

            // TODO: Apply zombieType stats/behavior profile on spawned zombie.
            // TODO: Register spawned instance with HordeManager/world chunk ownership.
            _ = zombieType;

            return zombie;
        }

        public void SpawnWave(ZombieType zombieType, Vector3 centerPosition, int count, float radius)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 spawnPosition = centerPosition + new Vector3(offset.x, 0f, offset.y);
                SpawnZombie(zombieType, spawnPosition);
            }

            // TODO: Add pooled spawn path for large wave counts.
        }

        public void ReturnToPool(ZombieAI zombie)
        {
            if (zombie == null)
            {
                return;
            }

            activeZombies.Remove(zombie);

            if (!useObjectPooling)
            {
                Destroy(zombie.gameObject);
                return;
            }

            zombie.SetActive(false);
            zombie.transform.SetParent(transform);
            zombie.gameObject.SetActive(false);
            zombiePool.Enqueue(zombie);
        }

        private ZombieAI GetOrCreateZombie()
        {
            if (useObjectPooling && zombiePool.Count > 0)
            {
                return zombiePool.Dequeue();
            }

            return Instantiate(zombiePrefab);
        }
    }
}