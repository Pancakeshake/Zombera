using System.Collections.Generic;
using UnityEngine;
using Zombera.AI.Brains;
using Zombera.World.Simulation;
using Zombera.World.Spawning;

namespace Zombera.World.Regions
{
    /// <summary>
    /// Coordinates region-level simulation ownership.
    /// Responsibilities:
    /// - map world positions to region data
    /// - convert regions between Full/Reduced/Abstract layers
    /// - spawn/despawn runtime entities from abstract region data
    /// </summary>
    public sealed class RegionManager : MonoBehaviour
    {
        [Header("Region Layout")]
        [SerializeField] private bool autoGenerateRegionGrid = true;
        [SerializeField] private Vector2Int gridSize = new Vector2Int(4, 4);
        [SerializeField] private float regionSizeMeters = 220f;
        [SerializeField] private Vector3 regionGridOrigin;
        [SerializeField] private List<Region> regions = new List<Region>();

        [Header("Runtime Conversion")]
        [SerializeField] private ZombieSpawner zombieSpawner;
        [SerializeField] private SurvivorSpawner survivorSpawner;
        [SerializeField] private float reducedLayerBrainTickInterval = 1f;

        [Header("Debug")]
        [SerializeField] private bool drawRegionBoundaries = true;

        private readonly Dictionary<UnitBrain, float> baselineBrainTicks = new Dictionary<UnitBrain, float>();

        public IReadOnlyList<Region> Regions => regions;

        public void InitializeRegions()
        {
            if (!autoGenerateRegionGrid || regions.Count > 0)
            {
                return;
            }

            GenerateRegionGrid();
        }

        public Region GetRegionAtPosition(Vector3 worldPosition)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];

                if (region != null && region.Contains(worldPosition))
                {
                    return region;
                }
            }

            return null;
        }

        public void RefreshRegionLayers(Vector3 playerPosition, float fullRange, float reducedRange)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];

                if (region == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(playerPosition, region.Center);
                WorldSimulationLayer targetLayer = WorldSimulationLayerUtility.GetLayer(distance, fullRange, reducedRange);
                SetRegionLayer(region, targetLayer);
            }
        }

        public void UpdateRegionStatsFromSimulation(HordeManager hordeManager, SurvivorManager survivorManager)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];

                if (region == null)
                {
                    continue;
                }

                int hordePopulation = hordeManager != null ? hordeManager.CountPopulationInRegion(region.regionId) : 0;
                int survivorPopulation = survivorManager != null ? survivorManager.CountMembersInRegion(region.regionId) : 0;

                region.zombiePopulation = Mathf.Max(0, hordePopulation);

                float denominator = Mathf.Max(1f, survivorPopulation + region.zombiePopulation);
                region.dangerLevel = Mathf.Clamp01(region.zombiePopulation / denominator);

                // TODO: Fold in scarcity, weather, and active event pressure.
            }
        }

        private void SetRegionLayer(Region region, WorldSimulationLayer targetLayer)
        {
            if (region.activeLayer == targetLayer)
            {
                return;
            }

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
                    ConvertRuntimeToAbstract(region);
                    break;
            }

            region.activeLayer = targetLayer;
        }

        private void EnsureRuntimeEntities(Region region)
        {
            if (region.runtimeMaterialized)
            {
                return;
            }

            zombieSpawner?.SpawnForRegion(region, region.zombiePopulation, region.runtimeZombies);
            survivorSpawner?.SpawnForRegion(region, region.survivorGroups, region.runtimeSurvivors);
            region.runtimeMaterialized = true;

            // TODO: Split materialization by nearby sub-cells to reduce spawn spikes.
        }

        private void ConvertRuntimeToAbstract(Region region)
        {
            if (!region.runtimeMaterialized)
            {
                return;
            }

            zombieSpawner?.ConvertRegionRuntimeToData(region);
            survivorSpawner?.ConvertRegionRuntimeToData(region);

            region.runtimeZombies.Clear();
            region.runtimeSurvivors.Clear();
            region.runtimeMaterialized = false;
        }

        private void ApplyFullSimulation(Region region)
        {
            for (int i = 0; i < region.runtimeZombies.Count; i++)
            {
                ConfigureRuntimeEntity(region.runtimeZombies[i] != null ? region.runtimeZombies[i].gameObject : null, false);
            }

            for (int i = 0; i < region.runtimeSurvivors.Count; i++)
            {
                ConfigureRuntimeEntity(region.runtimeSurvivors[i] != null ? region.runtimeSurvivors[i].gameObject : null, false);
            }
        }

        private void ApplyReducedSimulation(Region region)
        {
            for (int i = 0; i < region.runtimeZombies.Count; i++)
            {
                ConfigureRuntimeEntity(region.runtimeZombies[i] != null ? region.runtimeZombies[i].gameObject : null, true);
            }

            for (int i = 0; i < region.runtimeSurvivors.Count; i++)
            {
                ConfigureRuntimeEntity(region.runtimeSurvivors[i] != null ? region.runtimeSurvivors[i].gameObject : null, true);
            }

            // TODO: Move reduced entities using low-cost vector updates only.
            // TODO: Tick reduced-layer decisions on a slower scheduler.
        }

        private void ConfigureRuntimeEntity(GameObject entity, bool reduced)
        {
            if (entity == null)
            {
                return;
            }

            Animator[] animators = entity.GetComponentsInChildren<Animator>(true);

            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = !reduced;
            }

            Rigidbody[] bodies = entity.GetComponentsInChildren<Rigidbody>(true);

            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = reduced;
            }

            UnitBrain brain = entity.GetComponent<UnitBrain>();

            if (brain == null)
            {
                return;
            }

            if (!baselineBrainTicks.ContainsKey(brain))
            {
                baselineBrainTicks.Add(brain, brain.AITickInterval);
            }

            float baselineTick = baselineBrainTicks[brain];
            float targetTick = reduced ? Mathf.Max(baselineTick, reducedLayerBrainTickInterval) : baselineTick;
            brain.SetTickIntervalExternal(targetTick);

            // TODO: Disable NavMesh/pathfinding layers while in reduced simulation.
        }

        private void GenerateRegionGrid()
        {
            regions.Clear();

            for (int y = 0; y < Mathf.Max(1, gridSize.y); y++)
            {
                for (int x = 0; x < Mathf.Max(1, gridSize.x); x++)
                {
                    Vector3 center = regionGridOrigin + new Vector3((x + 0.5f) * regionSizeMeters, 0f, (y + 0.5f) * regionSizeMeters);

                    Region region = new Region
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

        private void OnDrawGizmosSelected()
        {
            if (!drawRegionBoundaries)
            {
                return;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];

                if (region == null)
                {
                    continue;
                }

                Gizmos.color = GetLayerColor(region.activeLayer);
                Gizmos.DrawWireCube(region.bounds.center, region.bounds.size);
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