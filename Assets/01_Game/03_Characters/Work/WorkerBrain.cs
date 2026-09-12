using UnityEngine;
using UnityEngine.AI;
using Zombera.BaseBuilding;
using Zombera.Systems;

namespace Zombera.Characters.Work
{
    /// <summary>
    ///     Per-squad-member autonomous worker that requests tasks from <see cref="WorkManager" />.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class WorkerBrain : MonoBehaviour
    {
        [SerializeField] private SquadMember squadMember;
        [SerializeField] private WorkerAI workerAI;
        [SerializeField] private NavMeshAgent navAgent;
        [SerializeField] [Min(0.25f)] private float pollIntervalSeconds = 0.75f;
        [SerializeField] [Min(0.5f)] private float interactionRange = 2.2f;

        private WorkTaskDescriptor _activeTask;
        private float _pollTimer;
        private string _memberId = string.Empty;
    }
}
