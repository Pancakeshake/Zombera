using System;
using UnityEngine;
using UnityEngine.AI;
using Zombera.BaseBuilding;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Inventory.Crafting;
using Zombera.Systems;

namespace Zombera.Characters.Work
{
    public sealed partial class WorkerBrain
    {
        private void ExecuteBuildingTask(WorkTaskDescriptor task)

        {

            var job = BuildingWorkTaskProvider.ResolveJob(task);

            if (job == null)

            {

                WorkManager.Instance?.CompleteTask(_memberId, task.taskId, failed: true);

                _activeTask = default;

                return;

            }



            if (workerAI == null) workerAI = GetComponent<WorkerAI>();

            if (workerAI == null) workerAI = gameObject.AddComponent<WorkerAI>();



            workerAI.AssignJob(job);

        }



        private void ExecuteLootTask(WorkTaskDescriptor task)

        {

            var container = LootWorkTaskProvider.ResolveContainer(task);

            if (container == null)

            {

                WorkManager.Instance?.CompleteTask(_memberId, task.taskId, failed: true);

                _activeTask = default;

                return;

            }



            MoveTowards(container.transform.position);

        }



        private void ExecuteCraftingTask(WorkTaskDescriptor task)

        {

            var entry = CraftingWorkTaskProvider.ResolveEntry(task);

            if (entry == null)

            {

                WorkManager.Instance?.CompleteTask(_memberId, task.taskId, failed: true);

                _activeTask = default;

                return;

            }



            var unit = squadMember.Unit;

            if (unit != null && string.IsNullOrWhiteSpace(entry.crafterUnitId))

                entry.crafterUnitId = unit.UnitId;



            if (task.worldPosition != Vector3.zero)

                MoveTowards(task.worldPosition);

        }



        private void MonitorActiveTask()

        {

            if (!_activeTask.IsValid)

            {

                ReleaseActiveTask();

                return;

            }



            switch (_activeTask.jobType)

            {

                case WorkJobType.Building:

                    MonitorBuildingTask();

                    break;

                case WorkJobType.Looting:

                    MonitorLootTask();

                    break;

                case WorkJobType.Crafting:

                    MonitorCraftingTask();

                    break;

            }

        }



        private void MonitorBuildingTask()

        {

            var job = BuildingWorkTaskProvider.ResolveJob(_activeTask);

            if (job == null || job.IsCompleted)

            {

                workerAI?.ClearJob();

                WorkManager.Instance?.CompleteTask(_memberId, _activeTask.taskId);

                _activeTask = default;

            }

        }



        private void MonitorLootTask()

        {

            var container = LootWorkTaskProvider.ResolveContainer(_activeTask);

            if (container == null || !container.HasLootRemaining())

            {

                StopNavigation();

                WorkManager.Instance?.CompleteTask(_memberId, _activeTask.taskId);

                _activeTask = default;

                return;

            }



            if (!IsWithinRange(container.transform.position))

            {

                MoveTowards(container.transform.position);

                return;

            }



            StopNavigation();

            container.OpenContainer();

            var inventory = squadMember.Unit?.Inventory;

            if (inventory != null) container.TransferAllTo(inventory);



            if (!container.HasLootRemaining())

            {

                WorkManager.Instance?.CompleteTask(_memberId, _activeTask.taskId);

                _activeTask = default;

            }

        }



        private void MonitorCraftingTask()

        {

            var entry = CraftingWorkTaskProvider.ResolveEntry(_activeTask);

            if (entry == null

                || entry.status is CraftingStatus.Complete or CraftingStatus.Failed or CraftingStatus.Cancelled)

            {

                StopNavigation();

                WorkManager.Instance?.CompleteTask(_memberId, _activeTask.taskId);

                _activeTask = default;

                return;

            }



            if (_activeTask.worldPosition != Vector3.zero && !IsWithinRange(_activeTask.worldPosition))

            {

                MoveTowards(_activeTask.worldPosition);

                return;

            }



            StopNavigation();

        }



        private void MoveTowards(Vector3 destination)

        {

            // Prefer the unit's central movement path so workers don't bypass UnitController.

            var controller = squadMember != null && squadMember.Unit != null ? squadMember.Unit.Controller : null;

            if (controller != null)

            {

                controller.MoveTo(destination);

                return;

            }

            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();

            if (navAgent == null || !navAgent.isOnNavMesh) return;



            navAgent.isStopped = false;

            navAgent.SetDestination(destination);

        }



        private void StopNavigation()

        {

            var controller = squadMember != null && squadMember.Unit != null ? squadMember.Unit.Controller : null;

            if (controller != null)

            {

                controller.Stop();

                return;

            }

            if (navAgent != null && navAgent.isOnNavMesh) navAgent.isStopped = true;

        }



        private bool IsWithinRange(Vector3 targetPosition)

        {

            return Vector3.Distance(transform.position, targetPosition) <= interactionRange;

        }



        private void ReleaseActiveTask()

        {

            if (!_activeTask.IsValid) return;



            workerAI?.ClearJob();

            StopNavigation();

            WorkManager.Instance?.CompleteTask(_memberId, _activeTask.taskId);

            _activeTask = default;

        }



        private static bool IsWorldSessionActive()
        {
            return WorldSessionGate.IsWorldSessionActive;
        }
    }
}
