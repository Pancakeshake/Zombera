#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.AI;
using Zombera.Characters;
using Zombera.Data;
using Zombera.World.Regions;
using Zombera.World.Simulation;

#endregion

namespace Zombera.World.Spawning
{
    /// <summary>
    ///     Region materialization bridge for zombies.
    ///     Converts abstract region data into runtime zombie entities and back.
    ///     (Renamed from the colliding Zombera.World.Spawning.ZombieSpawner; the
    ///     runtime pool is Zombera.AI.ZombieSpawner.)
    /// </summary>
    public sealed class RegionZombieMaterializer : MonoBehaviour
    {
        [Header("Runtime Spawn Source")] [SerializeField]
        private AI.ZombieSpawner runtimeZombieSpawner;

        [SerializeField] private ZombieType defaultZombieType;
        [SerializeField] private ZombieController zombiePrefabFallback;
        [SerializeField] private Transform runtimeParent;

        [Header("Limits")] [SerializeField] private int maxRuntimeZombiesPerRegion = 64;

        [Header("Debug")] [SerializeField] private bool logSpawnActions;

        public void SpawnForRegion(Region region, int desiredPopulation, List<ZombieController> runtimeBuffer)
        {
            if (region == null || runtimeBuffer == null) return;

            var targetCount = Mathf.Clamp(desiredPopulation, 0, Mathf.Max(0, maxRuntimeZombiesPerRegion));

            if (targetCount <= 0) return;

            while (runtimeBuffer.Count < targetCount)
            {
                var spawnPosition = GetRandomPointInBounds(region.bounds);
                var zombie = SpawnSingle(spawnPosition);

                if (zombie == null) break;

                runtimeBuffer.Add(zombie);
            }

            for (var i = runtimeBuffer.Count - 1; i >= targetCount; i--)
            {
                ReturnRuntimeZombie(runtimeBuffer[i]);
                runtimeBuffer.RemoveAt(i);
            }

            if (logSpawnActions)
                Debug.Log($"[WorldZombieSpawner] Region {region.regionId} materialized {runtimeBuffer.Count} zombies.",
                    this);
        }

        public void ConvertRegionRuntimeToData(Region region)
        {
            if (region == null) return;

            var aliveCount = 0;
            var weightedCenter = Vector3.zero;

            for (var i = region.RuntimeZombies.Count - 1; i >= 0; i--)
            {
                var zombie = region.RuntimeZombies[i];

                if (zombie == null) continue;

                var alive = true;
                var health = zombie.GetComponent<UnitHealth>();

                if (health != null) alive = !health.IsDead;

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
                var center = weightedCenter / aliveCount;

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
                Debug.Log(
                    $"[WorldZombieSpawner] Region {region.regionId} de-materialized to {aliveCount} abstract zombies.",
                    this);
        }

        private ZombieController SpawnSingle(Vector3 spawnPosition)
        {
            if (runtimeZombieSpawner != null)
            {
                var spawned = runtimeZombieSpawner.SpawnZombie(defaultZombieType, spawnPosition);

                if (spawned != null) return spawned;
            }

            if (zombiePrefabFallback == null) return null;

            var parent = runtimeParent != null ? runtimeParent : transform;
            var fallback = Instantiate(zombiePrefabFallback, spawnPosition, Quaternion.identity, parent);
            fallback.Initialize();
            return fallback;
        }

        private void ReturnRuntimeZombie(ZombieController zombie)
        {
            if (zombie == null) return;

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