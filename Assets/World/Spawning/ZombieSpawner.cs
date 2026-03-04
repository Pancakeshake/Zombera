using System.Collections.Generic;
using UnityEngine;
using Zombera.Data;
using Zombera.World.Regions;
using Zombera.World.Simulation;

namespace Zombera.World.Spawning
{
    /// <summary>
    /// Region materialization bridge for zombies.
    /// Converts abstract region data into runtime zombie entities and back.
    /// </summary>
    public sealed class ZombieSpawner : MonoBehaviour
    {
        [Header("Runtime Spawn Source")]
        [SerializeField] private Zombera.AI.ZombieSpawner runtimeZombieSpawner;
        [SerializeField] private ZombieType defaultZombieType;
        [SerializeField] private Zombera.AI.ZombieAI zombiePrefabFallback;
        [SerializeField] private Transform runtimeParent;

        [Header("Limits")]
        [SerializeField] private int maxRuntimeZombiesPerRegion = 64;

        [Header("Debug")]
        [SerializeField] private bool logSpawnActions;

        public void SpawnForRegion(Region region, int desiredPopulation, List<Zombera.AI.ZombieAI> runtimeBuffer)
        {
            if (region == null || runtimeBuffer == null)
            {
                return;
            }

            int targetCount = Mathf.Clamp(desiredPopulation, 0, Mathf.Max(0, maxRuntimeZombiesPerRegion));

            if (targetCount <= 0)
            {
                return;
            }

            while (runtimeBuffer.Count < targetCount)
            {
                Vector3 spawnPosition = GetRandomPointInBounds(region.bounds);
                Zombera.AI.ZombieAI zombie = SpawnSingle(spawnPosition);

                if (zombie == null)
                {
                    break;
                }

                runtimeBuffer.Add(zombie);
            }

            for (int i = runtimeBuffer.Count - 1; i >= targetCount; i--)
            {
                ReturnRuntimeZombie(runtimeBuffer[i]);
                runtimeBuffer.RemoveAt(i);
            }

            if (logSpawnActions)
            {
                Debug.Log($"[WorldZombieSpawner] Region {region.regionId} materialized {runtimeBuffer.Count} zombies.", this);
            }
        }

        public void ConvertRegionRuntimeToData(Region region)
        {
            if (region == null)
            {
                return;
            }

            int aliveCount = 0;
            Vector3 weightedCenter = Vector3.zero;

            for (int i = region.runtimeZombies.Count - 1; i >= 0; i--)
            {
                Zombera.AI.ZombieAI zombie = region.runtimeZombies[i];

                if (zombie == null)
                {
                    continue;
                }

                bool alive = true;
                Zombera.Characters.UnitHealth health = zombie.GetComponent<Zombera.Characters.UnitHealth>();

                if (health != null)
                {
                    alive = !health.IsDead;
                }

                if (alive)
                {
                    aliveCount++;
                    weightedCenter += zombie.transform.position;
                }

                ReturnRuntimeZombie(zombie);
            }

            region.zombiePopulation = aliveCount;
            region.zombieHordes.Clear();

            if (aliveCount > 0)
            {
                Vector3 center = weightedCenter / aliveCount;

                region.zombieHordes.Add(new ZombieHorde
                {
                    hordeId = 0,
                    regionId = region.regionId,
                    population = aliveCount,
                    aggression = Mathf.Clamp01(region.dangerLevel + 0.2f),
                    worldPosition = center
                });
            }

            if (logSpawnActions)
            {
                Debug.Log($"[WorldZombieSpawner] Region {region.regionId} de-materialized to {aliveCount} abstract zombies.", this);
            }
        }

        private Zombera.AI.ZombieAI SpawnSingle(Vector3 spawnPosition)
        {
            if (runtimeZombieSpawner != null)
            {
                Zombera.AI.ZombieAI spawned = runtimeZombieSpawner.SpawnZombie(defaultZombieType, spawnPosition);

                if (spawned != null)
                {
                    return spawned;
                }
            }

            if (zombiePrefabFallback == null)
            {
                return null;
            }

            Transform parent = runtimeParent != null ? runtimeParent : transform;
            Zombera.AI.ZombieAI fallback = Instantiate(zombiePrefabFallback, spawnPosition, Quaternion.identity, parent);
            fallback.Initialize();
            return fallback;
        }

        private void ReturnRuntimeZombie(Zombera.AI.ZombieAI zombie)
        {
            if (zombie == null)
            {
                return;
            }

            if (runtimeZombieSpawner != null)
            {
                runtimeZombieSpawner.ReturnToPool(zombie);
                return;
            }

            Destroy(zombie.gameObject);
        }

        private static Vector3 GetRandomPointInBounds(Bounds bounds)
        {
            return new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.center.y,
                Random.Range(bounds.min.z, bounds.max.z));
        }
    }
}