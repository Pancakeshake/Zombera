using UnityEngine;

namespace Zombera.BaseBuilding
{
    /// <summary>
    /// Worker behavior for delivering materials and advancing construction jobs.
    /// </summary>
    public sealed class WorkerAI : MonoBehaviour
    {
        [SerializeField] private float workPerTick = 5f;
        [SerializeField] private float workerTickInterval = 0.3f;

        public ConstructionJob CurrentJob { get; private set; }

        private float tickTimer;

        public void AssignJob(ConstructionJob job)
        {
            CurrentJob = job;
            tickTimer = 0f;

            // TODO: Start navigation toward job site.
        }

        public void ClearJob()
        {
            CurrentJob = null;

            // TODO: Return worker to idle or new assignment queue.
        }

        private void Update()
        {
            if (CurrentJob == null || CurrentJob.IsCompleted)
            {
                return;
            }

            tickTimer += Time.deltaTime;

            if (tickTimer < workerTickInterval)
            {
                return;
            }

            tickTimer = 0f;
            TickWork();
        }

        private void TickWork()
        {
            CurrentJob.AddWork(workPerTick);

            // TODO: Split between delivering materials and building based on job phase.
        }
    }
}