#region

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.World.Regions;
using Zombera.World.Simulation;

#endregion

namespace Zombera.World.Spawning
{
    /// <summary>
    ///     Region materialization bridge for survivors.
    /// </summary>
    public sealed class SurvivorSpawner : MonoBehaviour
    {
        [Header("Runtime Spawn Source")] [SerializeField]
        private SurvivorController survivorPrefab;

        [SerializeField] private Transform runtimeParent;

        [Header("Pooling")] [SerializeField] private bool usePooling = true;

        [SerializeField] private int initialPoolSize = 16;

        [Header("Limits")] [SerializeField] private int maxRuntimeSurvivorsPerRegion = 48;

        [Header("Appearance Variants")] [SerializeField]
        [FormerlySerializedAs("randomizeUmaVisualsOnSpawn")]
        private bool randomizeVisualVariantsOnSpawn = true;

        [Header("Debug")] [SerializeField] private bool logSpawnActions;

        private readonly Queue<SurvivorController> _survivorPool = new();

        private void Awake()
        {
            if (usePooling) PrewarmPool(initialPoolSize);
        }

        public void SpawnForRegion(Region region, List<SurvivorGroup> groups, List<SurvivorController> runtimeBuffer)
        {
            if (region == null || runtimeBuffer == null) return;

            var desiredCount = groups?
                .Where(group => group != null)
                .Sum(group => Mathf.Max(0, group.memberCount)) ?? 0;

            var targetCount = Mathf.Clamp(desiredCount, 0, Mathf.Max(0, maxRuntimeSurvivorsPerRegion));

            while (runtimeBuffer.Count < targetCount)
            {
                var survivor = SpawnSingle(GetRandomPointInBounds(region.bounds));

                if (survivor == null) break;

                runtimeBuffer.Add(survivor);
            }

            for (var i = runtimeBuffer.Count - 1; i >= targetCount; i--)
            {
                ReturnToPool(runtimeBuffer[i]);
                runtimeBuffer.RemoveAt(i);
            }

            if (logSpawnActions)
                Debug.Log($"[SurvivorSpawner] Region {region.regionId} materialized {runtimeBuffer.Count} survivors.",
                    this);
        }

        public void ConvertRegionRuntimeToData(Region region)
        {
            if (region == null) return;

            var aliveCount = 0;
            var weightedCenter = Vector3.zero;

            for (var i = region.RuntimeSurvivors.Count - 1; i >= 0; i--)
            {
                var survivor = region.RuntimeSurvivors[i];

                if (survivor == null) continue;

                var alive = true;
                var health = survivor.GetComponent<UnitHealth>();

                if (health != null) alive = !health.IsDead;

                if (alive)
                {
                    aliveCount++;
                    weightedCenter += survivor.transform.position;
                }

                ReturnToPool(survivor);
            }

            region.survivorGroups.Clear();

            if (aliveCount <= 0)
            {
                if (logSpawnActions)
                    Debug.Log(
                        $"[SurvivorSpawner] Region {region.regionId} de-materialized to {aliveCount} abstract survivors.",
                        this);
                return;
            }

            var center = weightedCenter / aliveCount;

            region.survivorGroups.Add(new SurvivorGroup
            {
                groupId = 0,
                regionId = region.regionId,
                memberCount = aliveCount,
                supplies = Mathf.Clamp01(region.lootLevel),
                worldPosition = center
            });

            if (logSpawnActions)
                Debug.Log(
                    $"[SurvivorSpawner] Region {region.regionId} de-materialized to {aliveCount} abstract survivors.",
                    this);
        }

        private void PrewarmPool(int count)
        {
            if (survivorPrefab == null || count <= 0) return;

            while (_survivorPool.Count < count)
            {
                var survivor = Instantiate(survivorPrefab, transform);
                survivor.gameObject.SetActive(false);
                _survivorPool.Enqueue(survivor);
            }
        }

        private SurvivorController SpawnSingle(Vector3 position)
        {
            var survivor = GetOrCreateSurvivor();

            if (survivor == null) return null;

            survivor.transform.SetPositionAndRotation(position, Quaternion.identity);
            survivor.transform.SetParent(runtimeParent != null ? runtimeParent : transform);
            survivor.gameObject.SetActive(true);

            if (randomizeVisualVariantsOnSpawn) ApplyRandomizedVisualVariant(survivor.gameObject);

            var health = survivor.GetComponent<UnitHealth>();
            health?.ResetHealthToMax();

            return survivor;
        }

        private SurvivorController GetOrCreateSurvivor()
        {
            if (usePooling && _survivorPool.Count > 0) return _survivorPool.Dequeue();

            return survivorPrefab == null
                ? null
                : Instantiate(survivorPrefab, runtimeParent != null ? runtimeParent : transform);
        }

        private void ReturnToPool(SurvivorController survivor)
        {
            if (survivor == null) return;

            if (!usePooling)
            {
                Destroy(survivor.gameObject);
                return;
            }

            survivor.gameObject.SetActive(false);
            survivor.transform.SetParent(transform);
            _survivorPool.Enqueue(survivor);
        }

        private static Vector3 GetRandomPointInBounds(Bounds bounds)
        {
            return new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.center.y,
                Random.Range(bounds.min.z, bounds.max.z));
        }

        private static void ApplyRandomizedVisualVariant(GameObject survivorObject)
        {
            if (survivorObject == null) return;

            var visualSpawner = survivorObject.GetComponent<NpcAppearanceVariantSpawner>();
            if (visualSpawner == null) visualSpawner = survivorObject.AddComponent<NpcAppearanceVariantSpawner>();

            visualSpawner.ApplyRandomAppearanceNow(true);
        }
    }
}