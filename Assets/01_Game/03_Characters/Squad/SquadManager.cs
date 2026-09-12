#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Characters;
using Zombera.Core;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Tracks all active squad members and coordinates group-level command dispatch.
    /// </summary>
    public sealed class SquadManager : MonoBehaviour
    {
        [SerializeField] private CommandSystem commandSystem;
        [SerializeField] private bool fallbackToAllMembersWhenSelectionEmpty = false;

        [Header("Move Command Guard")]
        [SerializeField] [Min(0f)] private float duplicateMoveCommandSuppressionSeconds = 0.2f;

        [SerializeField] [Min(0.1f)] private float duplicateMoveCommandPointThreshold = 0.5f;

        private readonly List<SquadMember> _commandMembersBuffer = new();
        private float _lastMoveCommandIssuedAt = -999f;
        private Vector3 _lastMoveCommandDestination;
        private readonly List<SquadMember> _selectedMembers = new();
        private bool _warnedMissingCommandSystem;
        private bool _autoProvisionedCommandSystem;

        public CommandSystem GetOrEnsureCommandSystem() => ResolveCommandSystem();

        /// <summary>
        ///     Single command-resolution path: the owning SquadManager provisions and exposes the
        ///     runtime CommandSystem. Falls back to a scene lookup only when no SquadManager exists.
        /// </summary>
        public static CommandSystem ResolveRuntimeCommandSystem()
        {
            if (HasInstance) return Instance.GetOrEnsureCommandSystem();

            var squadManager = FindFirstObjectByType<SquadManager>(FindObjectsInactive.Include);
            if (squadManager != null) return squadManager.GetOrEnsureCommandSystem();

            return FindFirstObjectByType<CommandSystem>(FindObjectsInactive.Include);
        }

        public static FormationController ResolveRuntimeFormationController()
        {
            var commandSystem = ResolveRuntimeCommandSystem();
            return commandSystem != null ? commandSystem.Formation : null;
        }

        private readonly List<SquadMember> _squadMembers = new();
        private static SquadManager _instance;
        private static bool _warnedAboutMissingInstance;

        public static bool HasInstance => _instance != null;

        public static SquadManager Instance
        {
            get => _instance;
            private set => _instance = value;
        }

        public IReadOnlyList<SquadMember> SquadMembers => _squadMembers;
        public IReadOnlyList<SquadMember> SelectedMembers => _selectedMembers;
        public bool HasSelection => _selectedMembers.Count > 0;

        public event Action<IReadOnlyList<SquadMember>> SelectionChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning(
                    $"[SquadManager] Duplicate instance detected on '{name}'. Keeping '{_instance.name}' and destroying duplicate GameObject.",
                    this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveCommandSystem();
            RefreshSquadRoster();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        public void RefreshSquadRoster()
        {
            _squadMembers.Clear();
            var members = FindObjectsByType<SquadMember>(FindObjectsSortMode.None);

            foreach (var member in members) RegisterMember(member);

            PruneSelectedMembers();
        }

        public void RegisterMember(SquadMember member)
        {
            if (member == null || _squadMembers.Contains(member)) return;

            _squadMembers.Add(member);

            CoreEventBus.PublishGlobal(new SquadRosterChangedEvent
            {
                Member = member,
                WasAdded = true
            });

            PruneSelectedMembers();
        }

        public void UnregisterMember(SquadMember member)
        {
            if (member == null) return;

            var removed = _squadMembers.Remove(member);

            if (!removed) return;

            _selectedMembers.Remove(member);
            PublishSelectionChanged();

            CoreEventBus.PublishGlobal(new SquadRosterChangedEvent
            {
                Member = member,
                WasAdded = false
            });

            // Reassign any active commands that were targeting the leaving member.
            if (ResolveCommandSystem() != null && _squadMembers.Count > 0)
                CommandSystem.ReassignCommandsAwayFrom(member, _squadMembers);
        }

        public void IssueOrder(SquadCommandType commandType, Vector3 targetPosition = default)
        {
            var targets = ResolveCommandMembers(fallbackToAllMembersWhenSelectionEmpty);
            if (targets.Count == 0) return;

            if (commandType == SquadCommandType.Move && ShouldSuppressDuplicateMoveCommand(targetPosition))
                return;

            var resolvedCommandSystem = ResolveCommandSystem();
            if (resolvedCommandSystem != null)
            {
                resolvedCommandSystem.ExecuteCommand(commandType, targets, targetPosition);
                if (commandType == SquadCommandType.Move) RecordMoveCommandIssued(targetPosition);
                return;
            }

            if (TryFallbackMoveOrder(commandType, targets, targetPosition))
                RecordMoveCommandIssued(targetPosition);
        }

        public void IssueAttackOrder(UnitHealth explicitTarget, Vector3 focusPosition = default)
        {
            var targets = ResolveCommandMembers(fallbackToAllMembersWhenSelectionEmpty);
            if (targets.Count == 0) return;

            var resolvedCommandSystem = ResolveCommandSystem();
            if (resolvedCommandSystem != null)
            {
                resolvedCommandSystem.ExecuteAttackCommand(targets, focusPosition, explicitTarget);
                return;
            }

            _ = TryFallbackAttackOrder(targets, explicitTarget, focusPosition);
        }

        public bool TryIssueOrderToSelection(SquadCommandType commandType, Vector3 targetPosition = default)
        {
            PruneSelectedMembers();
            if (_selectedMembers.Count == 0) return false;

            if (commandType == SquadCommandType.Move && ShouldSuppressDuplicateMoveCommand(targetPosition))
                return true;

            var resolvedCommandSystem = ResolveCommandSystem();
            if (resolvedCommandSystem != null)
            {
                resolvedCommandSystem.ExecuteCommand(commandType, _selectedMembers, targetPosition);
                if (commandType == SquadCommandType.Move) RecordMoveCommandIssued(targetPosition);
                return true;
            }

            if (!TryFallbackMoveOrder(commandType, _selectedMembers, targetPosition)) return false;

            if (commandType == SquadCommandType.Move) RecordMoveCommandIssued(targetPosition);
            return true;
        }

        public bool TryIssueMoveAssignmentsToSelection(
            IReadOnlyList<SquadMoveDeconfliction.GroupMoveAssignment> assignments,
            Vector3 destinationForDedup = default)
        {
            PruneSelectedMembers();
            if (_selectedMembers.Count == 0 || assignments == null || assignments.Count == 0) return false;

            if (ShouldSuppressDuplicateMoveCommand(destinationForDedup))
                return true;

            var resolvedCommandSystem = ResolveCommandSystem();
            if (resolvedCommandSystem == null) return false;

            resolvedCommandSystem.ExecuteMoveAssignments(assignments);
            RecordMoveCommandIssued(destinationForDedup);
            return true;
        }

        public bool TryIssueAttackOrderToSelection(UnitHealth explicitTarget, Vector3 focusPosition = default)
        {
            PruneSelectedMembers();
            if (_selectedMembers.Count == 0) return false;

            var resolvedCommandSystem = ResolveCommandSystem();
            if (resolvedCommandSystem != null)
            {
                resolvedCommandSystem.ExecuteAttackCommand(_selectedMembers, focusPosition, explicitTarget);
                return true;
            }

            return TryFallbackAttackOrder(_selectedMembers, explicitTarget, focusPosition);
        }

        public void SetSelectedMembers(IReadOnlyList<SquadMember> members)
        {
            _selectedMembers.Clear();

            if (members == null)
            {
                PublishSelectionChanged();
                return;
            }

            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (!IsMemberSelectable(member)) continue;
                if (!_squadMembers.Contains(member)) continue;
                if (_selectedMembers.Contains(member)) continue;

                _selectedMembers.Add(member);
            }

            PublishSelectionChanged();
        }

        public void SelectAllMembers()
        {
            _selectedMembers.Clear();
            _selectedMembers.AddRange(_squadMembers.Where(IsMemberSelectable));
            PublishSelectionChanged();
        }

        public void ClearSelection()
        {
            if (_selectedMembers.Count == 0) return;

            _selectedMembers.Clear();
            PublishSelectionChanged();
        }

        public void ToggleMemberInSelection(SquadMember member)
        {
            PruneSelectedMembers();
            if (!IsMemberSelectable(member) || !_squadMembers.Contains(member)) return;

            if (_selectedMembers.Contains(member))
                _selectedMembers.Remove(member);
            else
                _selectedMembers.Add(member);

            PublishSelectionChanged();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;
            RefreshSquadRoster();
        }

        private IReadOnlyList<SquadMember> ResolveCommandMembers(bool allowAllMembersFallback)
        {
            _commandMembersBuffer.Clear();

            if (_selectedMembers.Count > 0)
                _commandMembersBuffer.AddRange(_selectedMembers.Where(IsMemberSelectable));

            if (allowAllMembersFallback && _commandMembersBuffer.Count == 0)
                _commandMembersBuffer.AddRange(_squadMembers.Where(IsMemberSelectable));

            return _commandMembersBuffer;
        }

        private CommandSystem ResolveCommandSystem()
        {
            if (commandSystem == null) commandSystem = GetComponent<CommandSystem>();

            if (commandSystem == null)
                commandSystem = FindFirstObjectByType<CommandSystem>(FindObjectsInactive.Include);

            if (commandSystem == null && !_autoProvisionedCommandSystem)
            {
                commandSystem = gameObject.AddComponent<CommandSystem>();
                _autoProvisionedCommandSystem = true;
                Debug.Log(
                    "[SquadManager] Auto-provisioned CommandSystem on SquadManager (scene had no CommandSystem wired).",
                    this);
            }

            if (commandSystem != null)
            {
                _warnedMissingCommandSystem = false;
                return commandSystem;
            }

            if (_warnedMissingCommandSystem) return null;

            _warnedMissingCommandSystem = true;
            Debug.LogWarning(
                "[SquadManager] CommandSystem could not be resolved or provisioned. Move/Attack orders will fall back to direct member controller/combat behavior.",
                this);
            return null;
        }

        private static bool TryFallbackMoveOrder(
            SquadCommandType commandType,
            IReadOnlyList<SquadMember> members,
            Vector3 destination)
        {
            if (commandType != SquadCommandType.Move || members == null || members.Count == 0) return false;

            var forward = Vector3.forward;
            if (members[0]?.UnitController != null)
            {
                var toTarget = destination - members[0].UnitController.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f) forward = toTarget.normalized;
            }

            SquadMoveDeconfliction.IssueGroupMove(
                members,
                destination,
                forward,
                null,
                SquadMoveDeconfliction.DefaultSettings);
            return true;
        }

        private bool ShouldSuppressDuplicateMoveCommand(Vector3 targetPosition)
        {
            if (duplicateMoveCommandSuppressionSeconds <= 0f) return false;

            var elapsed = Time.time - _lastMoveCommandIssuedAt;
            if (elapsed > duplicateMoveCommandSuppressionSeconds) return false;

            var threshold = Mathf.Max(0.1f, duplicateMoveCommandPointThreshold);
            return (targetPosition - _lastMoveCommandDestination).sqrMagnitude <= threshold * threshold;
        }

        private void RecordMoveCommandIssued(Vector3 targetPosition)
        {
            _lastMoveCommandIssuedAt = Time.time;
            _lastMoveCommandDestination = targetPosition;
        }

        private static bool TryFallbackAttackOrder(
            IReadOnlyList<SquadMember> members,
            UnitHealth explicitTarget,
            Vector3 focusPosition)
        {
            if (members == null || members.Count == 0) return false;
            if (explicitTarget == null || explicitTarget.IsDead) return false;

            var attackPoint = focusPosition != default ? focusPosition : explicitTarget.transform.position;
            var executedAny = false;

            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (!IsMemberSelectable(member)) continue;

                member.UnitController?.MoveTo(attackPoint);
                member.UnitCombat?.SetMarkedTarget(explicitTarget);
                _ = member.UnitCombat?.ExecuteAttack(null);
                member.SetCommandState(SquadCommandType.Attack);
                executedAny = true;
            }

            return executedAny;
        }

        private void PruneSelectedMembers()
        {
            for (var i = _selectedMembers.Count - 1; i >= 0; i--)
            {
                var member = _selectedMembers[i];
                if (member != null && _squadMembers.Contains(member) && IsMemberSelectable(member)) continue;

                _selectedMembers.RemoveAt(i);
            }

            PublishSelectionChanged();
        }

        private static bool IsMemberSelectable(SquadMember member)
        {
            return member != null && member.IsAvailableForOrders();
        }

        private void PublishSelectionChanged()
        {
            SelectionChanged?.Invoke(_selectedMembers);
        }
    }
}