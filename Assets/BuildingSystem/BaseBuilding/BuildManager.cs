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

        [Header("Placement Snap")]
        [SerializeField] private bool snapPositionToGrid = true;
        [SerializeField, Min(0.1f)] private float gridSize = 2f;
        [SerializeField] private bool snapRotation = true;
        [SerializeField, Min(1f)] private float rotationSnapDegrees = 90f;

        [Header("Placement Validation")]
        [SerializeField] private LayerMask structureLayerMask;
        [SerializeField, Min(0f)] private float overlapCheckHalfExtent = 0.9f;

        private readonly List<ConstructionJob> activeJobs = new List<ConstructionJob>();

        public IReadOnlyList<ConstructionJob> ActiveJobs => activeJobs;
        public BaseStorage BaseStorage => baseStorage;

        public Blueprint PlaceBlueprint(BuildingData buildingData, Vector3 worldPosition)
        {
            return PlaceBlueprint(buildingData, worldPosition, Quaternion.identity);
        }

        public Blueprint PlaceBlueprint(BuildingData buildingData, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (blueprintPrefab == null || buildingData == null)
            {
                return null;
            }

            Vector3 snappedPosition = GetSnappedPosition(worldPosition);
            Quaternion snappedRotation = GetSnappedRotation(worldRotation);

            // Reject placement if a structure already occupies the target cell.
            if (structureLayerMask != 0)
            {
                float half = Mathf.Max(0.01f, overlapCheckHalfExtent);
                Vector3 halfExtents = new Vector3(half, 2f, half);
                Collider[] hits = Physics.OverlapBox(snappedPosition, halfExtents, snappedRotation, structureLayerMask);
                if (hits.Length > 0)
                {
                    return null;
                }
            }

            Blueprint blueprint = Instantiate(blueprintPrefab, snappedPosition, snappedRotation);
            blueprint.Initialize(buildingData);

            ConstructionJob job = blueprint.GetOrCreateConstructionJob();
            activeJobs.Add(job);

            return blueprint;
        }

        public Vector3 GetSnappedPosition(Vector3 worldPosition)
        {
            if (!snapPositionToGrid)
            {
                return worldPosition;
            }

            float step = Mathf.Max(0.1f, gridSize);
            worldPosition.x = Mathf.Round(worldPosition.x / step) * step;
            worldPosition.z = Mathf.Round(worldPosition.z / step) * step;
            return worldPosition;
        }

        public Quaternion GetSnappedRotation(Quaternion worldRotation)
        {
            if (!snapRotation)
            {
                return worldRotation;
            }

            float snappedYaw = GetSnappedYaw(worldRotation.eulerAngles.y);
            return Quaternion.Euler(0f, snappedYaw, 0f);
        }

        public float GetSnappedYaw(float yawDegrees)
        {
            if (!snapRotation)
            {
                return yawDegrees;
            }

            float step = Mathf.Max(1f, rotationSnapDegrees);
            return Mathf.Round(yawDegrees / step) * step;
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

        /// <summary>
        /// Assigns <paramref name="worker"/> to the highest-priority pending job.
        /// Priority: jobs nearest completion (most work done relative to required) first.
        /// Returns false if no incomplete jobs are available.
        /// </summary>
        public bool AssignWorkerToBestJob(WorkerAI worker)
        {
            if (worker == null) return false;

            ConstructionJob best = null;
            float bestProgress = -1f;

            for (int i = 0; i < activeJobs.Count; i++)
            {
                ConstructionJob job = activeJobs[i];
                if (job == null || job.IsCompleted) continue;

                // Prefer jobs with most work already done (nearly complete first).
                float progress = job.WorkProgress;
                if (progress > bestProgress)
                {
                    bestProgress = progress;
                    best = job;
                }
            }

            if (best == null) return false;
            worker.AssignJob(best);
            return true;
        }

        public void TickConstruction()
        {
            // Cull completed jobs and auto-deliver materials for any remaining.
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
        }

        private void Update()
        {
            TickConstruction();
        }
    }
}