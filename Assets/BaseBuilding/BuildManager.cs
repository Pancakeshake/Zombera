using System.Collections.Generic;
using UnityEngine;
using Zombera.Data;

namespace Zombera.BaseBuilding
{
    /// <summary>
    /// Coordinates base construction flow: place blueprint, assign workers, deliver materials, build, complete.
    /// </summary>
    public sealed class BuildManager : MonoBehaviour
    {
        [SerializeField] private Blueprint blueprintPrefab;
        [SerializeField] private BaseStorage baseStorage;

        private readonly List<ConstructionJob> activeJobs = new List<ConstructionJob>();

        public IReadOnlyList<ConstructionJob> ActiveJobs => activeJobs;

        public Blueprint PlaceBlueprint(BuildingData buildingData, Vector3 worldPosition)
        {
            if (blueprintPrefab == null || buildingData == null)
            {
                return null;
            }

            Blueprint blueprint = Instantiate(blueprintPrefab, worldPosition, Quaternion.identity);
            blueprint.Initialize(buildingData);

            ConstructionJob job = blueprint.GetOrCreateConstructionJob();
            activeJobs.Add(job);

            // TODO: Snap placement to grid/terrain and validate overlap rules.
            return blueprint;
        }

        public bool AssignWorker(WorkerAI worker, ConstructionJob job)
        {
            if (worker == null || job == null || job.IsCompleted)
            {
                return false;
            }

            worker.AssignJob(job);
            return true;
        }

        public void TickConstruction()
        {
            for (int i = activeJobs.Count - 1; i >= 0; i--)
            {
                ConstructionJob job = activeJobs[i];

                if (job == null || job.IsCompleted)
                {
                    activeJobs.RemoveAt(i);
                    continue;
                }

                job.TryAutoDeliverMaterials(baseStorage);
            }

            // TODO: Add job prioritization and worker assignment heuristics.
        }

        private void Update()
        {
            TickConstruction();
        }
    }
}