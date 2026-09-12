using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;
using Zombera.Systems;

namespace Zombera.Characters.Work
{
    /// <summary>
    ///     Global work orchestrator: priority profiles, reservations, and RimWorld-style task selection.
    /// </summary>
    public sealed class WorkManager : MonoBehaviour
    {
        [SerializeField] [Min(0.25f)] private float fallbackPollIntervalSeconds = 0.75f;
        [SerializeField] private bool manualPrioritiesEnabled = true;

        private static WorkManager _instance;

        private readonly Dictionary<string, WorkAssignmentProfile> _profiles = new();
        private readonly Dictionary<string, string> _reservations = new();
        private readonly Dictionary<string, WorkTaskDescriptor> _activeTasksByMember = new();
        private readonly List<IWorkTaskProvider> _providers = new();
        private readonly List<WorkTaskDescriptor> _taskBuffer = new();
        private readonly List<CandidateTask> _candidateBuffer = new();

        private BuildingWorkTaskProvider _buildingProvider;
        private LootWorkTaskProvider _lootingProvider;
        private CraftingWorkTaskProvider _craftingProvider;

        public static WorkManager Instance => _instance;
        public bool ManualPrioritiesEnabled => manualPrioritiesEnabled;
        public float FallbackPollIntervalSeconds => fallbackPollIntervalSeconds;

        public event Action WorkStateChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            EnsureProviders();
        }

        private void OnEnable()
        {
            CoreEventBus.Instance?.Subscribe<SquadRosterChangedEvent>(OnSquadRosterChanged);
            CoreEventBus.Instance?.Subscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            CoreEventBus.Instance?.Subscribe<LootGeneratedEvent>(OnLootGenerated);
        }

        private void OnDisable()
        {
            CoreEventBus.Instance?.Unsubscribe<SquadRosterChangedEvent>(OnSquadRosterChanged);
            CoreEventBus.Instance?.Unsubscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            CoreEventBus.Instance?.Unsubscribe<LootGeneratedEvent>(OnLootGenerated);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public void NotifyWorkSourcesChanged()
        {
            NotifyChanged();
        }

        private void OnSquadRosterChanged(SquadRosterChangedEvent evt)
        {
            if (!evt.WasAdded) return;
            RefreshRosterFromSquad();
        }

        private void OnBuildingCompleted(BuildingCompletedEvent evt)
        {
            _ = evt;
            NotifyWorkSourcesChanged();
        }

        private void OnLootGenerated(LootGeneratedEvent evt)
        {
            _ = evt;
            NotifyWorkSourcesChanged();
        }

        public void Configure(bool manualPriorities)
        {
            manualPrioritiesEnabled = manualPriorities;
        }

        public WorkAssignmentProfile GetOrCreateProfile(SquadMember member)
        {
            if (member == null) return null;

            member.RefreshReferences();
            var memberId = member.MemberId;
            if (string.IsNullOrWhiteSpace(memberId)) return null;

            if (_profiles.TryGetValue(memberId, out var profile)) return profile;

            profile = WorkAssignmentProfile.CreateDefault(memberId);
            _profiles[memberId] = profile;
            return profile;
        }

        public IReadOnlyDictionary<string, WorkAssignmentProfile> Profiles => _profiles;

        public void SetManualPrioritiesEnabled(bool enabled)
        {
            manualPrioritiesEnabled = enabled;
            NotifyChanged();
        }

        public void ApplyProfileToAll(WorkAssignmentProfile template)
        {
            if (template == null) return;

            foreach (var pair in _profiles)
            {
                var profile = pair.Value;
                if (profile == null) continue;

                CopyPriorities(template, profile);
            }

            NotifyChanged();
        }

        public void ResetProfileToDefaults(SquadMember member)
        {
            if (member == null) return;

            member.RefreshReferences();
            var memberId = member.MemberId;
            if (string.IsNullOrWhiteSpace(memberId)) return;

            _profiles[memberId] = WorkAssignmentProfile.CreateDefault(memberId);
            NotifyChanged();
        }

        public static bool CanMemberPerformJob(SquadMember member, WorkJobType jobType)
        {
            return CanPerformJob(member, jobType);
        }

        public bool TryAssignNextTask(SquadMember member, out WorkTaskDescriptor assignedTask)
        {
            assignedTask = default;
            if (!IsWorldSessionActive() || member == null || !member.IsAvailableForOrders()) return false;

            var profile = GetOrCreateProfile(member);
            if (profile == null) return false;

            if (_activeTasksByMember.TryGetValue(profile.memberId, out var existing) && existing.IsValid)
            {
                assignedTask = existing;
                return true;
            }

            CollectCandidates(member, profile);
            if (_candidateBuffer.Count == 0) return false;

            _candidateBuffer.Sort(CompareCandidates);

            for (var i = 0; i < _candidateBuffer.Count; i++)
            {
                var candidate = _candidateBuffer[i];
                if (!TryReserve(candidate.Task.taskId, profile.memberId)) continue;

                var reserved = candidate.Task;
                reserved.reservedMemberId = profile.memberId;
                reserved.state = WorkTaskState.Reserved;
                _activeTasksByMember[profile.memberId] = reserved;
                assignedTask = reserved;
                NotifyChanged();
                return true;
            }

            return false;
        }

        public void MarkTaskInProgress(string memberId)
        {
            if (string.IsNullOrWhiteSpace(memberId)) return;
            if (!_activeTasksByMember.TryGetValue(memberId, out var task)) return;

            task.state = WorkTaskState.InProgress;
            _activeTasksByMember[memberId] = task;
            NotifyChanged();
        }

        public void CompleteTask(string memberId, string taskId, bool failed = false)
        {
            if (string.IsNullOrWhiteSpace(memberId)) return;

            _activeTasksByMember.Remove(memberId);
            if (!string.IsNullOrWhiteSpace(taskId)) _reservations.Remove(taskId);

            NotifyChanged();
        }

        public bool TryGetActiveTask(string memberId, out WorkTaskDescriptor task)
        {
            return _activeTasksByMember.TryGetValue(memberId, out task);
        }

        public void RestoreProfiles(IReadOnlyList<WorkAssignmentProfile> profiles, bool manualEnabled)
        {
            _profiles.Clear();
            if (profiles != null)
            {
                for (var i = 0; i < profiles.Count; i++)
                {
                    var profile = profiles[i];
                    if (profile == null || string.IsNullOrWhiteSpace(profile.memberId)) continue;
                    _profiles[profile.memberId] = profile;
                }
            }

            manualPrioritiesEnabled = manualEnabled;
            _reservations.Clear();
            _activeTasksByMember.Clear();
            NotifyChanged();
        }

        public List<WorkAssignmentProfile> CaptureProfiles()
        {
            var output = new List<WorkAssignmentProfile>(_profiles.Count);
            foreach (var pair in _profiles) output.Add(pair.Value);
            return output;
        }

        public void RefreshRosterFromSquad()
        {
            if (!SquadManager.HasInstance) return;

            var members = SquadManager.Instance.SquadMembers;
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null) continue;

                GetOrCreateProfile(member);
                EnsureWorkerBrain(member);
            }
        }

        private static void EnsureWorkerBrain(SquadMember member)
        {
            if (member == null) return;
            if (member.GetComponent<WorkerBrain>() != null) return;
            member.gameObject.AddComponent<WorkerBrain>();
        }

        private void EnsureProviders()
        {
            _buildingProvider ??= new BuildingWorkTaskProvider();
            _lootingProvider ??= new LootWorkTaskProvider();
            _craftingProvider ??= new CraftingWorkTaskProvider();

            _providers.Clear();
            _providers.Add(_buildingProvider);
            _providers.Add(_lootingProvider);
            _providers.Add(_craftingProvider);
        }

        private void CollectCandidates(SquadMember member, WorkAssignmentProfile profile)
        {
            _candidateBuffer.Clear();
            _taskBuffer.Clear();

            for (var i = 0; i < _providers.Count; i++)
                _providers[i].CollectAvailableTasks(_taskBuffer);

            var memberPosition = member.transform.position;

            for (var i = 0; i < _taskBuffer.Count; i++)
            {
                var task = _taskBuffer[i];
                if (!task.IsValid) continue;
                if (_reservations.ContainsKey(task.taskId)) continue;

                var priority = profile.GetPriority(task.jobType);
                if (priority == WorkPriorityLevel.Off) continue;
                if (!CanPerformJob(member, task.jobType)) continue;
                if (!MatchesRequiredUnit(member, task)) continue;

                var score = ComputeScore(memberPosition, task, priority);
                _candidateBuffer.Add(new CandidateTask(task, priority, score));
            }
        }

        private static bool CanPerformJob(SquadMember member, WorkJobType jobType)
        {
            if (member.GetComponent<UnityEngine.AI.NavMeshAgent>() == null) return false;

            return jobType switch
            {
                WorkJobType.Building => true,
                WorkJobType.Looting => member.Unit?.Inventory != null,
                WorkJobType.Crafting => member.Unit?.Inventory != null,
                _ => false
            };
        }

        private static bool MatchesRequiredUnit(SquadMember member, WorkTaskDescriptor task)
        {
            if (string.IsNullOrWhiteSpace(task.requiredUnitId)) return true;

            var unit = member.Unit;
            return unit != null && string.Equals(unit.UnitId, task.requiredUnitId, StringComparison.Ordinal);
        }

        private static float ComputeScore(Vector3 memberPosition, WorkTaskDescriptor task, WorkPriorityLevel priority)
        {
            var priorityWeight = (int)priority * 1000f;
            var distanceWeight = Vector3.Distance(memberPosition, task.worldPosition);
            var urgencyWeight = -task.urgency * 100f;
            return priorityWeight + distanceWeight + urgencyWeight;
        }

        private static int CompareCandidates(CandidateTask a, CandidateTask b)
        {
            var priorityCompare = ((int)a.Priority).CompareTo((int)b.Priority);
            if (priorityCompare != 0) return priorityCompare;

            return a.Score.CompareTo(b.Score);
        }

        private bool TryReserve(string taskId, string memberId)
        {
            if (string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(memberId)) return false;
            if (_reservations.TryGetValue(taskId, out var owner) && owner != memberId) return false;

            _reservations[taskId] = memberId;
            return true;
        }

        private static void CopyPriorities(WorkAssignmentProfile source, WorkAssignmentProfile destination)
        {
            for (var i = 0; i < 6; i++)
            {
                var jobType = (WorkJobType)i;
                destination.SetPriority(jobType, source.GetPriority(jobType));
            }
        }

        private static bool IsWorldSessionActive()
        {
            if (!GameManagerGateway.HasInstance) return false;
            var state = GameManagerGateway.Instance.CurrentState;
            return state is GameState.LoadingWorld or GameState.Playing or GameState.Paused;
        }

        private void NotifyChanged()
        {
            WorkStateChanged?.Invoke();
        }

        private readonly struct CandidateTask
        {
            public CandidateTask(WorkTaskDescriptor task, WorkPriorityLevel priority, float score)
            {
                Task = task;
                Priority = priority;
                Score = score;
            }

            public WorkTaskDescriptor Task { get; }
            public WorkPriorityLevel Priority { get; }
            public float Score { get; }
        }
    }
}
