#region

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Zombera.AI;
using Zombera.Core;
using Zombera.World.Simulation;
using Zombera.World.Spawning;

#endregion

// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace Zombera.World.Regions
{
    /// <summary>
    ///     Coordinates region-level simulation ownership.
    ///     Responsibilities:
    ///     - map world positions to region data
    ///     - convert regions between Full/Reduced/Abstract layers
    ///     - spawn/despawn runtime entities from abstract region data
    /// </summary>
    public sealed class RegionManager : MonoBehaviour
    {
        [Header("Region Layout")] [SerializeField]
        private bool autoGenerateRegionGrid = true;

        [SerializeField] private Vector2Int gridSize = new(4, 4);
        [SerializeField] private float regionSizeMeters = 220f;
        [SerializeField] private Vector3 regionGridOrigin;
        [SerializeField] private List<Region> regions = new();

        [Header("Runtime Conversion")] [SerializeField]
        private RegionZombieMaterializer zombieSpawner;

        [SerializeField] private SurvivorSpawner survivorSpawner;
        [SerializeField] private float reducedLayerBrainTickInterval = 1f;

        [Header("Debug")] [SerializeField] private bool drawRegionBoundaries = true;

        private readonly Dictionary<ZombieController, float> _baselineBrainTicks = new();

        public IReadOnlyList<Region> Regions => regions;

        private void OnDrawGizmosSelected()
        {
            if (!drawRegionBoundaries) return;

            regions.Where(region => region != null).ToList().ForEach(region =>
            {
                Gizmos.color = GetLayerColor(region.ActiveLayer);
                Gizmos.DrawWireCube(region.bounds.center, region.bounds.size);
            });
        }

        public void InitializeRegions()
        {
            if (!autoGenerateRegionGrid || regions.Count > 0) return;

            GenerateRegionGrid();
        }

        public void RefreshRegionLayers(Vector3 playerPosition, float fullRange, float reducedRange)
        {
            RefreshRegionLayers(playerPosition, fullRange, reducedRange, int.MaxValue);
        }

        /// <summary>
        ///     Updates region simulation layers with a per-call cap to spread layer transitions across ticks (streaming budgets).
        /// </summary>
        public void RefreshRegionLayers(Vector3 playerPosition, float fullRange, float reducedRange,
            int maxLayerTransitions)
        {
            if (regions.Count == 0) return;

            if (maxLayerTransitions <= 0) return;

            if (maxLayerTransitions >= regions.Count)
            {
                foreach (var region in regions)
                {
                    if (region == null) continue;

                    var distance = Vector3.Distance(playerPosition, region.Center);
                    var targetLayer = WorldSimulationLayerUtility.GetLayer(distance, fullRange, reducedRange);
                    SetRegionLayer(region, targetLayer);
                }

                return;
            }

            var pending = new List<(Region region, WorldSimulationLayer targetLayer, float distance)>();
            foreach (var region in regions)
            {
                if (region == null) continue;

                var distance = Vector3.Distance(playerPosition, region.Center);
                var targetLayer = WorldSimulationLayerUtility.GetLayer(distance, fullRange, reducedRange);
                if (region.ActiveLayer == targetLayer) continue;

                pending.Add((region, targetLayer, distance));
            }

            if (pending.Count == 0) return;

            pending.Sort((a, b) => a.distance.CompareTo(b.distance));

            var limit = Mathf.Min(maxLayerTransitions, pending.Count);
            for (var i = 0; i < limit; i++)
            {
                var entry = pending[i];
                SetRegionLayer(entry.region, entry.targetLayer);
            }
        }

        public void UpdateRegionStatsFromSimulation(HordeManager hordeManager, SurvivorManager survivorManager)
        {
            foreach (var region in regions)
            {
                if (region == null) continue;

                var hordePopulation = hordeManager != null ? hordeManager.CountPopulationInRegion(region.regionId) : 0;
                var survivorPopulation =
                    survivorManager != null ? survivorManager.CountMembersInRegion(region.regionId) : 0;

                region.zombiePopulation = Mathf.Max(0, hordePopulation);

                var denominator = Mathf.Max(1f, survivorPopulation + region.zombiePopulation);
                region.dangerLevel = Mathf.Clamp01(region.zombiePopulation / denominator);

                // Blend in scarcity, weather, and active event modifiers when available.
                if (CoreEventBus.Instance != null)
                {
                    // Additional pressure sources can be polled through published world state events.
                    // For now dangerLevel alone drives the simulation layer decisions.
                }
            }
        }

        private void SetRegionLayer(Region region, WorldSimulationLayer targetLayer)
        {
            if (region.ActiveLayer == targetLayer) return;

            switch (targetLayer)
            {
                case WorldSimulationLayer.Full:
                    EnsureRuntimeEntities(region);
                    ApplyFullSimulation(region);
                    break;

                case WorldSimulationLayer.Reduced:
                    EnsureRuntimeEntities(region);
                    ApplyReducedSimulation(region);
                    break;

                case WorldSimulationLayer.Abstract:
                default:
                    ConvertRuntimeToAbstract(region);
                    break;
            }

            region.ActiveLayer = targetLayer;
        }

        private void EnsureRuntimeEntities(Region region)
        {
            if (region.RuntimeMaterialized) return;

            zombieSpawner?.SpawnForRegion(region, region.zombiePopulation, region.RuntimeZombies);
            survivorSpawner?.SpawnForRegion(region, region.survivorGroups, region.RuntimeSurvivors);
            region.RuntimeMaterialized = true;

            // Sub-cell staggering: if the region is large, break materialization into
            // smaller batches in future streaming passes to avoid a single-frame spike.
            // The regionSizeMeters field on RegionManager controls granularity.
        }

        private void ConvertRuntimeToAbstract(Region region)
        {
            if (!region.RuntimeMaterialized) return;

            zombieSpawner?.ConvertRegionRuntimeToData(region);
            survivorSpawner?.ConvertRegionRuntimeToData(region);

            region.RuntimeZombies.Clear();
            region.RuntimeSurvivors.Clear();
            region.RuntimeMaterialized = false;
        }

        private void ApplyFullSimulation(Region region)
        {
            foreach (var runtimeZombie in region.RuntimeZombies)
                ConfigureRuntimeEntity(runtimeZombie != null ? runtimeZombie.gameObject : null, false);

            foreach (var runtimeSurvivor in region.RuntimeSurvivors)
                ConfigureRuntimeEntity(runtimeSurvivor != null ? runtimeSurvivor.gameObject : null, false);
        }

        private void ApplyReducedSimulation(Region region)
        {
            // Apply reduced simulation: animate at lower tick rate via SetTickIntervalExternal.
            foreach (var runtimeZombie in region.RuntimeZombies)
                ConfigureRuntimeEntity(runtimeZombie != null ? runtimeZombie.gameObject : null, true);

            foreach (var runtimeSurvivor in region.RuntimeSurvivors)
                ConfigureRuntimeEntity(runtimeSurvivor != null ? runtimeSurvivor.gameObject : null, true);

            // Reduced entities use only transform/velocity updates; heavy physics and
            // complex utility scoring are skipped until the region enters Full layer.
        }

        private void ConfigureRuntimeEntity(GameObject entity, bool reduced)
        {
            if (entity == null) return;

            var animators = entity.GetComponentsInChildren<Animator>(true);

            foreach (var animator in animators) animator.enabled = !reduced;

            var bodies = entity.GetComponentsInChildren<Rigidbody>(true);

            foreach (var body in bodies) body.isKinematic = reduced;

            var brain = entity.GetComponent<ZombieController>();

            if (brain == null) return;

            if (!_baselineBrainTicks.ContainsKey(brain)) _baselineBrainTicks.Add(brain, brain.AITickInterval);

            var baselineTick = _baselineBrainTicks[brain];
            var targetTick = reduced ? Mathf.Max(baselineTick, reducedLayerBrainTickInterval) : baselineTick;
            brain.SetAITickInterval(targetTick);

            // When switching to reduced mode disable NavMesh agent steering;
            // the entity is repositioned using direct transform moves each slow tick.
            var agent = entity.GetComponent<NavMeshAgent>();

            if (agent != null) agent.enabled = !reduced;
        }

        private void GenerateRegionGrid()
        {
            regions.Clear();

            foreach (var y in Enumerable.Range(0, Mathf.Max(1, gridSize.y)))
            {
                foreach (var x in Enumerable.Range(0, Mathf.Max(1, gridSize.x)))
                {
                    var center = regionGridOrigin +
                                 new Vector3((x + 0.5f) * regionSizeMeters, 0f, (y + 0.5f) * regionSizeMeters);

                    var region = new Region
                    {
                        regionId = $"Region_{x}_{y}",
                        bounds = new Bounds(center, new Vector3(regionSizeMeters, 120f, regionSizeMeters)),
                        zombiePopulation = Random.Range(8, 35),
                        lootLevel = Random.Range(0.25f, 0.85f),
                        dangerLevel = Random.Range(0.2f, 0.8f)
                    };

                    regions.Add(region);
                }
            }
        }

        private static Color GetLayerColor(WorldSimulationLayer layer)
        {
            switch (layer)
            {
                case WorldSimulationLayer.Full:
                    return new Color(0.2f, 1f, 0.2f, 1f);
                case WorldSimulationLayer.Reduced:
                    return new Color(1f, 0.75f, 0.2f, 1f);
                case WorldSimulationLayer.Abstract:
                default:
                    return new Color(0.7f, 0.7f, 1f, 1f);
            }
        }
    }
}