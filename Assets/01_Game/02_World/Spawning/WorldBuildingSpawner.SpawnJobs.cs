#region

using UnityEngine;
using Zombera.BuildingSystem;
using Zombera.EasyBuildAdapter;

#endregion

namespace Zombera.World.Spawning
{
    public sealed partial class WorldBuildingSpawner
    {
        private void DrainSpawnQueue()
        {
            if (_spawnQueue.Count == 0) return;

            var budget = Mathf.Max(1, maxBuildingSpawnsPerFrame);
            for (var i = 0; i < budget && _spawnQueue.Count > 0; i++)
            {
                var job = _spawnQueue.Dequeue();
                ApplySpawnJob(job, perBuildingEasyBuildCollapse: false, notifyCollapseBatch: true);
            }
        }

        private void FlushSpawnQueueImmediate()
        {
            while (_spawnQueue.Count > 0)
            {
                var job = _spawnQueue.Dequeue();
                ApplySpawnJob(job, disableEasyBuildCollapseForSpawnedBuildings, notifyCollapseBatch: false);
            }
        }

        private void ApplySpawnJob(PendingSpawn job, bool perBuildingEasyBuildCollapse, bool notifyCollapseBatch)
        {
            var go = TakeBuildingFromPool(job.Prefab);
            var reusedFromPool = go != null;

            if (reusedFromPool)
            {
                go.transform.SetParent(job.Parent, false);
                go.transform.SetPositionAndRotation(job.Position, job.Rotation);
            }
            else
            {
                go = Instantiate(job.Prefab, job.Position, job.Rotation, job.Parent);
                go.AddComponent<PooledWorldBuilding>().SourcePrefab = job.Prefab;
            }

            AddStructuralComponentsIfNeeded(go, job.Entry);

            if (disableEasyBuildCollapseForSpawnedBuildings && perBuildingEasyBuildCollapse)
                DisableEasyBuildCollapseBehaviors(go);

            if (reusedFromPool)
                runtimePlacedStructureFixer?.ReprocessPlacedStructure(go);
            else
                runtimePlacedStructureFixer?.ProcessPlacedStructure(go);

            if (notifyCollapseBatch)
                job.CollapseBatch?.NotifySpawnCompleted();
        }

        private static void AddStructuralComponentsIfNeeded(GameObject go, BuildingSpawnEntry entry)
        {
            if (entry.ensureStructureHealth)
            {
                var health = go.GetComponent<StructureHealth>();
                if (health == null) health = go.AddComponent<StructureHealth>();
                health.SetMaxHealth(entry.structureMaxHealth, true);
            }

            if (entry.ensureBuildPiece)
            {
                var piece = go.GetComponent<BuildPiece>();
                if (piece == null) piece = go.AddComponent<BuildPiece>();
                piece.SetCategory(entry.buildPieceCategory);
            }
        }

        private void ResolveRuntimePlacedStructureFixer()
        {
            if (runtimePlacedStructureFixer != null) return;

            runtimePlacedStructureFixer = GetComponent<RuntimePlacedStructureFixer>();
            if (runtimePlacedStructureFixer == null)
                runtimePlacedStructureFixer = GetComponentInParent<RuntimePlacedStructureFixer>();

            if (runtimePlacedStructureFixer == null && autoCreateRuntimePlacedStructureFixer)
                runtimePlacedStructureFixer = gameObject.AddComponent<RuntimePlacedStructureFixer>();
        }

        private static void DisableEasyBuildCollapseBehaviors(GameObject root)
        {
            EasyBuildFacade.SetCollapseBehaviorsEnabled(root, false);
        }

        private sealed class CollapseBatchTracker
        {
            private readonly Transform _root;
            private int _remaining;

            public CollapseBatchTracker(Transform root)
            {
                _root = root;
            }

            public void IncrementPending() => _remaining++;

            public void NotifySpawnCompleted()
            {
                if (_root == null) return;
                if (--_remaining > 0) return;

                DisableEasyBuildCollapseBehaviors(_root.gameObject);
            }
        }

        private readonly struct PendingSpawn
        {
            public readonly GameObject Prefab;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Transform Parent;
            public readonly BuildingSpawnEntry Entry;
            public readonly CollapseBatchTracker CollapseBatch;

            public PendingSpawn(
                GameObject prefab,
                Vector3 position,
                Quaternion rotation,
                Transform parent,
                BuildingSpawnEntry entry,
                CollapseBatchTracker collapseBatch)
            {
                Prefab = prefab;
                Position = position;
                Rotation = rotation;
                Parent = parent;
                Entry = entry;
                CollapseBatch = collapseBatch;
            }
        }
    }
}
