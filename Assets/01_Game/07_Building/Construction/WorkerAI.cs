#region

using UnityEngine;
using UnityEngine.AI;

#endregion

// ReSharper disable Unity.PerformanceCriticalCodeInvocation
// ReSharper disable Unity.PerformanceCriticalCodeNullComparison
// ReSharper disable MemberCanBePrivate.Global

namespace Zombera.BaseBuilding
{
    /// <summary>
    ///     Worker behavior for delivering materials and advancing construction jobs.
    ///     Automatically navigates to the blueprint site before contributing work.
    /// </summary>
    public sealed class WorkerAI : MonoBehaviour
    {
        [SerializeField] private float workPerTick = 5f;
        [SerializeField] private float workerTickInterval = 0.3f;
        [SerializeField] private BaseStorage storage;
        [SerializeField] [Min(0.1f)] private float arrivalStoppingDistance = 1.8f;
        private NavMeshAgent _agent;

        private float _tickTimer;

        public ConstructionJob CurrentJob { get; private set; }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        private void Update()
        {
            if (!CurrentJob || CurrentJob.IsCompleted) return;

            // Wait until the worker has arrived at the site before doing work.
            if (!IsAtJobSite()) return;

            if (_agent != null && !_agent.isStopped) _agent.isStopped = true;

            _tickTimer += Time.deltaTime;

            if (_tickTimer < workerTickInterval) return;

            _tickTimer = 0f;
            TickWork();
        }

        // ReSharper disable once UnusedMember.Global
        public void AssignJob(ConstructionJob job)
        {
            CurrentJob = job;
            _tickTimer = 0f;

            if (!job || !job.TargetBlueprint || _agent == null || !_agent.isOnNavMesh) return;

            _agent.isStopped = false;
            _agent.SetDestination(job.TargetBlueprint.transform.position);
        }

        // ReSharper disable once UnusedMember.Global
        public void ClearJob()
        {
            CurrentJob = null;

            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        private bool IsAtJobSite()
        {
            if (!CurrentJob || !CurrentJob.TargetBlueprint) return true;
            var dist = Vector3.Distance(transform.position, CurrentJob.TargetBlueprint.transform.position);
            return dist <= arrivalStoppingDistance;
        }

        private void TickWork()
        {
            if (!CurrentJob.HasAllMaterials)
                CurrentJob.TryAutoDeliverMaterials(storage);
            else
                CurrentJob.AddWork(workPerTick);
        }
    }
}