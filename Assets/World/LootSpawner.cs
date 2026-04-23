using System.Collections.Generic;
using UnityEngine;
using Zombera.Inventory;
using Zombera.Systems;

namespace Zombera.World
{
    /// <summary>
    /// Coordinates world loot spawn point registration and container placement.
    /// </summary>
    public sealed class LootSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject defaultLootContainerPrefab;
        [SerializeField] private Transform lootRoot;
        [SerializeField] private List<Transform> prototypeSpawnPoints = new List<Transform>();
        [SerializeField] private bool randomizeYawOnSpawn = true;
        [SerializeField] private bool clearExistingOnPrime;
        [SerializeField] private LootManager lootManager;

        private readonly List<GameObject> activeLootContainers = new List<GameObject>();
        private bool hasPrimedPrototypeLoot;

        private void Awake()
        {
            if (lootManager == null)
            {
                lootManager = FindFirstObjectByType<LootManager>();
            }
        }

        public void PrimePrototypeLoot()
        {
            if (defaultLootContainerPrefab == null || prototypeSpawnPoints.Count <= 0)
            {
                return;
            }

            if (hasPrimedPrototypeLoot)
            {
                if (!clearExistingOnPrime)
                {
                    return;
                }

                ClearSpawnedLoot();
            }

            for (int i = 0; i < prototypeSpawnPoints.Count; i++)
            {
                Transform point = prototypeSpawnPoints[i];

                if (point == null)
                {
                    continue;
                }

                SpawnLootAt(point.position);
            }

            hasPrimedPrototypeLoot = true;
        }

        public void SpawnLootAt(Vector3 worldPosition)
        {
            if (defaultLootContainerPrefab == null)
            {
                return;
            }

            Transform parent = lootRoot != null ? lootRoot : transform;
            Quaternion rotation = randomizeYawOnSpawn
                ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                : Quaternion.identity;

            GameObject spawnedContainer = Instantiate(defaultLootContainerPrefab, worldPosition, rotation, parent);
            activeLootContainers.Add(spawnedContainer);

            if (lootManager != null && spawnedContainer.TryGetComponent(out LootContainer lootContainer))
            {
                lootManager.RegisterContainer(lootContainer);
            }
        }

        public void RegisterSpawnPoint(Transform spawnPoint)
        {
            if (spawnPoint == null || prototypeSpawnPoints.Contains(spawnPoint))
            {
                return;
            }

            prototypeSpawnPoints.Add(spawnPoint);
        }

        public void ClearSpawnedLoot()
        {
            for (int i = activeLootContainers.Count - 1; i >= 0; i--)
            {
                GameObject containerObject = activeLootContainers[i];

                if (containerObject == null)
                {
                    activeLootContainers.RemoveAt(i);
                    continue;
                }

                if (lootManager != null && containerObject.TryGetComponent(out LootContainer lootContainer))
                {
                    lootManager.UnregisterContainer(lootContainer);
                }

                Destroy(containerObject);
                activeLootContainers.RemoveAt(i);
            }

            hasPrimedPrototypeLoot = false;
        }

        public void TickNearPlayer(Vector3 playerPosition)
        {
            // Activate loot containers within spawn radius that have not yet been primed.
            for (int i = 0; i < activeLootContainers.Count; i++)
            {
                if (activeLootContainers[i] == null)
                {
                    continue;
                }

                float dist = Vector3.Distance(activeLootContainers[i].transform.position, playerPosition);
                activeLootContainers[i].SetActive(dist < 60f);
            }
        }
    }
}