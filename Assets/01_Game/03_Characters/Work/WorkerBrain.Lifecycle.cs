using UnityEngine;
using UnityEngine.AI;
using Zombera.BaseBuilding;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;

namespace Zombera.Characters.Work
{
    public sealed partial class WorkerBrain
    {
        private void Awake()
        {
            if (squadMember == null) squadMember = GetComponent<SquadMember>();
            if (workerAI == null) workerAI = GetComponent<WorkerAI>();
            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            if (squadMember != null)
            {
                squadMember.RefreshReferences();
                _memberId = squadMember.MemberId;
            }

            CoreEventBus.Instance?.Subscribe<SquadCommandIssuedEvent>(OnSquadCommandIssued);

            if (WorkManager.Instance != null)
                WorkManager.Instance.WorkStateChanged += OnWorkStateChanged;
        }

        private void OnDisable()
        {
            CoreEventBus.Instance?.Unsubscribe<SquadCommandIssuedEvent>(OnSquadCommandIssued);

            if (WorkManager.Instance != null)
                WorkManager.Instance.WorkStateChanged -= OnWorkStateChanged;

            ReleaseActiveTask();
        }

        private void OnWorkStateChanged()
        {
            if (!_activeTask.IsValid) _pollTimer = 0f;
        }

        private void Update()
        {
            if (!IsWorldSessionActive() || squadMember == null || !squadMember.IsAvailableForOrders()) return;
            if (!ShouldRunAutonomousWork()) return;

            if (_activeTask.IsValid)
            {
                MonitorActiveTask();
                return;
            }

            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = ResolvePollInterval();

            TryAcquireTask();
        }

        private float ResolvePollInterval()
        {
            return WorkManager.Instance != null
                ? WorkManager.Instance.FallbackPollIntervalSeconds
                : pollIntervalSeconds;
        }

        private bool ShouldRunAutonomousWork()
        {
            if (squadMember.CurrentState is SquadUnitState.Attacking or SquadUnitState.Moving or SquadUnitState.Following)
                return false;

            return squadMember.CurrentState == SquadUnitState.Idle;
        }

        private void OnSquadCommandIssued(SquadCommandIssuedEvent evt)
        {
            if (evt.CommandType == SquadCommandType.Move
                || evt.CommandType == SquadCommandType.Attack
                || evt.CommandType == SquadCommandType.HoldPosition
                || evt.CommandType == SquadCommandType.Defend)
            {
                ReleaseActiveTask();
            }
        }

        private void TryAcquireTask()
        {
            var manager = WorkManager.Instance;
            if (manager == null) return;

            if (!manager.TryAssignNextTask(squadMember, out var task) || !task.IsValid) return;

            _activeTask = task;
            manager.MarkTaskInProgress(_memberId);

            switch (task.jobType)
            {
                case WorkJobType.Building:
                    ExecuteBuildingTask(task);
                    break;
                case WorkJobType.Looting:
                    ExecuteLootTask(task);
                    break;
                case WorkJobType.Crafting:
                    ExecuteCraftingTask(task);
                    break;
                default:
                    manager.CompleteTask(_memberId, task.taskId, failed: true);
                    _activeTask = default;
                    break;
            }
        }
    }
}
