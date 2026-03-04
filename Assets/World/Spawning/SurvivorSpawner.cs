using System.Collections.Generic;
using UnityEngine;
using Zombera.Systems;
using Zombera.World.Regions;
using Zombera.World.Simulation;

namespace Zombera.World.Spawning
{
    /// <summary>
    /// Region materialization bridge for survivors.
    /// </summary>
    public sealed class SurvivorSpawner : MonoBehaviour
    {
        [Header("Runtime Spawn Source")]
        [SerializeField] private SurvivorAI survivorPrefab;
        [SerializeField] private Transform runtimeParent;

        [Header("Pooling")]
        [SerializeField] private bool usePooling = true;
        [SerializeField] private int initialPoolSize = 16;

        [Header("Limits")]
        [SerializeField] private int maxRuntimeSurvivorsPerRegion = 48;

        [Header("Debug")]
        [SerializeField] private bool logSpawnActions;

        private readonly Queue<SurvivorAI> survivorPool = new Queue<SurvivorAI>();

        private void Awake()
        {
            if (usePooling)
            {
                PrewarmPool(initialPoolSize);
            }
        }

        public void SpawnForRegion(Region region, List<SurvivorGroup> groups, List<SurvivorAI> runtimeBuffer)
        {
            if (region == null || runtimeBuffer == null)
            {
                return;
            }

            int desiredCount = 0;

            if (groups != null)
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    SurvivorGroup group = groups[i];

                    if (group != null)
                    {
                        desiredCount += Mathf.Max(0, group.memberCount);
                    }
                }
            }

            int targetCount = Mathf.Clamp(desiredCount, 0, Mathf.Max(0, maxRuntimeSurvivorsPerRegion));

            while (runtimeBuffer.Count < targetCount)
            {
                SurvivorAI survivor = SpawnSingle(GetRandomPointInBounds(region.bounds));

                if (survivor == null)
                {
                    break;
                }

                runtimeBuffer.Add(survivor);
            }

            for (int i = runtimeBuffer.Count - 1; i >= targetCount; i--)
            {
                ReturnToPool(runtimeBuffer[i]);
                runtimeBuffer.RemoveAt(i);
            }

            if (logSpawnActions)
            {
                Debug.Log($"[SurvivorSpawner] Region {region.regionId} materialized {runtimeBuffer.Count} survivors.", this);
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

            for (int i = region.runtimeSurvivors.Count - 1; i >= 0; i--)
            {
                SurvivorAI survivor = region.runtimeSurvivors[i];

                if (survivor == null)
                {
                    continue;
                }

                bool alive = true;
                Zombera.Characters.UnitHealth health = survivor.GetComponent<Zombera.Characters.UnitHealth>();

                if (health != null)
                {
                    alive = !health.IsDead;
                }

                if (alive)
                {
                    aliveCount++;
                    weightedCenter += survivor.transform.position;
                }

                ReturnToPool(survivor);
            }

            region.survivorGroups.Clear();

            if (aliveCount > 0)
            {
                Vector3 center = weightedCenter / aliveCount;

                region.survivorGroups.Add(new SurvivorGroup
                {
                    groupId = 0,
                    regionId = region.regionId,
                    memberCount = aliveCount,
                    morale = Mathf.Clamp01(1f - region.dangerLevel),
                    supplies = Mathf.Clamp01(region.lootLevel),
                    worldPosition = center
                });
            }

            if (logSpawnActions)
            {
                Debug.Log($"[SurvivorSpawner] Region {region.regionId} de-materialized to {aliveCount} abstract survivors.", this);
            }
        }

        private void PrewarmPool(int count)
        {
            if (survivorPrefab == null || count <= 0)
            {
                return;
            }

            while (survivorPool.Count < count)
            {
                SurvivorAI survivor = Instantiate(survivorPrefab, transform);
                survivor.gameObject.SetActive(false);
                survivorPool.Enqueue(survivor);
            }
        }

        private SurvivorAI SpawnSingle(Vector3 position)
        {
            SurvivorAI survivor = GetOrCreateSurvivor();

            if (survivor == null)
            {
                return null;
            }

            survivor.transform.SetPositionAndRotation(position, Quaternion.identity);
            survivor.transform.SetParent(runtimeParent != null ? runtimeParent : transform);
            survivor.gameObject.SetActive(true);

            Zombera.Characters.UnitHealth health = survivor.GetComponent<Zombera.Characters.UnitHealth>();
            health?.ResetHealthToMax();

            return survivor;
        }

        private SurvivorAI GetOrCreateSurvivor()
        {
            if (usePooling && survivorPool.Count > 0)
            {
                return survivorPool.Dequeue();
            }

            if (survivorPrefab == null)
            {
                return null;
            }

            return Instantiate(survivorPrefab, runtimeParent != null ? runtimeParent : transform);
        }

        private void ReturnToPool(SurvivorAI survivor)
        {
            if (survivor == null)
            {
                return;
            }

            if (!usePooling)
            {
                Destroy(survivor.gameObject);
                return;
            }

            survivor.gameObject.SetActive(false);
            survivor.transform.SetParent(transform);
            survivorPool.Enqueue(survivor);
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