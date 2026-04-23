using UnityEngine;
using UnityEngine.AI;

namespace Zombera.BaseBuilding
{
    /// <summary>
    /// Worker behavior for delivering materials and advancing construction jobs.
    /// Automatically navigates to the blueprint site before contributing work.
    /// </summary>
    public sealed class WorkerAI : MonoBehaviour
    {
        [SerializeField] private float workPerTick = 5f;
        [SerializeField] private float workerTickInterval = 0.3f;
        [SerializeField] private BaseStorage storage;
        [SerializeField, Min(0.1f)] private float arrivalStoppingDistance = 1.8f;

        public ConstructionJob CurrentJob { get; private set; }

        private float tickTimer;
        private NavMeshAgent _agent;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        public void AssignJob(ConstructionJob job)
        {
            CurrentJob = job;
            tickTimer = 0f;

            if (job != null && job.TargetBlueprint != null && _agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(job.TargetBlueprint.transform.position);
            }
        }

        public void ClearJob()
        {
            CurrentJob = null;

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
            }
        }

        private void Update()
        {
            if (CurrentJob == null || CurrentJob.IsCompleted)
            {
                return;
            }

            // Wait until the worker has arrived at the site before doing work.
            if (!IsAtJobSite())
            {
                return;
            }

            if (_agent != null && !_agent.isStopped)
            {
                _agent.isStopped = true;
            }

            tickTimer += Time.deltaTime;

            if (tickTimer < workerTickInterval)
            {
                return;
            }

            tickTimer = 0f;
            TickWork();
        }

        private bool IsAtJobSite()
        {
            if (CurrentJob?.TargetBlueprint == null) return true;
            float dist = Vector3.Distance(transform.position, CurrentJob.TargetBlueprint.transform.position);
            return dist <= arrivalStoppingDistance;
        }

        private void TickWork()
        {
            if (!CurrentJob.HasAllMaterials)
            {
                CurrentJob.TryAutoDeliverMaterials(storage);
            }
            else
            {
                CurrentJob.AddWork(workPerTick);
            }
        }
    }
}