#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Inventory;
using Zombera.Systems;

#endregion

// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace Zombera.World
{
    /// <summary>
    ///     Coordinates world loot spawn point registration and container placement.
    /// </summary>
    public sealed class LootSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject defaultLootContainerPrefab;
        [SerializeField] private Transform lootRoot;
        [SerializeField] private List<Transform> prototypeSpawnPoints = new();
        [SerializeField] private bool randomizeYawOnSpawn = true;
        [SerializeField] private bool clearExistingOnPrime;
        [SerializeField] private LootManager lootManager;

        private readonly List<GameObject> _activeLootContainers = new();
        private readonly Queue<GameObject> _containerPool = new();
        private bool _hasPrimedPrototypeLoot;
        public bool HasCompletedInitialObjectBootstrap { get; private set; }

        private void Awake()
        {
            if (lootManager == null) lootManager = FindFirstObjectByType<LootManager>();
        }

        public void PrimePrototypeLoot()
        {
            if (defaultLootContainerPrefab == null || prototypeSpawnPoints.Count <= 0)
            {
                HasCompletedInitialObjectBootstrap = true;
                return;
            }

            if (_hasPrimedPrototypeLoot)
            {
                if (!clearExistingOnPrime)
                {
                    HasCompletedInitialObjectBootstrap = true;
                    return;
                }

                ClearSpawnedLoot();
            }

            foreach (var point in prototypeSpawnPoints)
            {
                if (point == null) continue;

                SpawnLootAt(point.position);
            }

            _hasPrimedPrototypeLoot = true;
            HasCompletedInitialObjectBootstrap = true;
        }

        public void SpawnLootAt(Vector3 worldPosition)
        {
            if (defaultLootContainerPrefab == null) return;

            var parent = lootRoot != null ? lootRoot : transform;
            var rotation = randomizeYawOnSpawn
                ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                : Quaternion.identity;

            var spawnedContainer = TakeFromPool();
            if (spawnedContainer != null)
            {
                spawnedContainer.transform.SetParent(parent);
                spawnedContainer.transform.SetPositionAndRotation(worldPosition, rotation);
                spawnedContainer.SetActive(true);
            }
            else
            {
                spawnedContainer = Instantiate(defaultLootContainerPrefab, worldPosition, rotation, parent);
            }

            _activeLootContainers.Add(spawnedContainer);

            if (spawnedContainer.TryGetComponent(out LootContainer lootContainer))
            {
                lootContainer.ResetForReuse();
                if (lootManager != null) lootManager.RegisterContainer(lootContainer);
            }
        }

        private GameObject TakeFromPool()
        {
            while (_containerPool.Count > 0)
            {
                var pooled = _containerPool.Dequeue();
                if (pooled != null) return pooled;
            }

            return null;
        }

        // ReSharper disable once UnusedMember.Global
        public void RegisterSpawnPoint(Transform spawnPoint)
        {
            if (spawnPoint == null || prototypeSpawnPoints.Contains(spawnPoint)) return;

            prototypeSpawnPoints.Add(spawnPoint);
        }

        public void ClearSpawnedLoot()
        {
            for (var i = _activeLootContainers.Count - 1; i >= 0; i--)
            {
                var containerObject = _activeLootContainers[i];

                if (containerObject == null)
                {
                    _activeLootContainers.RemoveAt(i);
                    continue;
                }

                if (lootManager != null && containerObject.TryGetComponent(out LootContainer lootContainer))
                    lootManager.UnregisterContainer(lootContainer);

                // Pool instead of destroy: containers are reset on reuse in SpawnLootAt.
                containerObject.SetActive(false);
                _containerPool.Enqueue(containerObject);
                _activeLootContainers.RemoveAt(i);
            }

            _hasPrimedPrototypeLoot = false;
        }

        public void TickNearPlayer(Vector3 playerPosition)
        {
            // Activate loot containers within spawn radius that have not yet been primed.
            foreach (var lootContainer in _activeLootContainers)
            {
                if (lootContainer == null) continue;

                var dist = Vector3.Distance(lootContainer.transform.position, playerPosition);
                lootContainer.SetActive(dist < 60f);
            }
        }
    }
}