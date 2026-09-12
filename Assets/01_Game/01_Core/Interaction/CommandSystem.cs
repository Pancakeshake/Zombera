#region

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Core;
using Zombera.Debugging.DebugLogging;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Executes player-issued squad commands such as move, attack, hold, follow, and defend.
    /// </summary>
    public sealed class CommandSystem : MonoBehaviour
    {
        [SerializeField] private FormationController formationController;

        public FormationController Formation
        {
            get
            {
                EnsureFormationControllerReference();
                return formationController;
            }
        }
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private CombatSystem combatSystem;
        [SerializeField] private float attackSearchRadius = 20f;
        [SerializeField] private SquadCommandVisualizer squadCommandVisualizer;

        [Header("Group Move Deconfliction")]
        [SerializeField] [Min(0.5f)] private float groupSpacingBaseline = SquadMoveDeconfliction.DefaultGroupSpacingBaseline;

        [SerializeField] [Min(0f)] private float adaptiveSpacingGain = SquadMoveDeconfliction.DefaultAdaptiveSpacingGain;
        [SerializeField] private bool adaptiveSpacingEnabled = true;
        [SerializeField] [Min(0.5f)] private float minSeparationFloor = SquadMoveDeconfliction.DefaultMinSeparationFloor;

        [SerializeField] [Min(0.5f)] private float minSeparationMultiplier = SquadMoveDeconfliction.DefaultMinSeparationMultiplier;
        [SerializeField] [Min(1)] private int maxDeconflictionIterations = 8;
        [SerializeField] [Min(0.05f)] private float deconflictionStepMeters = 0.5f;
        [SerializeField] private bool sampleSlotsOnNavMesh = true;
        [SerializeField] private bool logMoveDeconflictionDiagnostics;

        private readonly List<UnitHealth> _targetBuffer = new();

        private void Awake()
        {
            EnsureFormationControllerReference();

            if (squadCommandVisualizer == null)
                squadCommandVisualizer = FindFirstObjectByType<SquadCommandVisualizer>(FindObjectsInactive.Include);

            if (squadCommandVisualizer == null)
                squadCommandVisualizer = gameObject.AddComponent<SquadCommandVisualizer>();
        }

        private void EnsureFormationControllerReference()
        {
            if (formationController != null) return;

            formationController = GetComponent<FormationController>();
            if (formationController != null) return;

            formationController = gameObject.AddComponent<FormationController>();
        }

        public void ExecuteCommand(SquadCommandType commandType, IReadOnlyList<SquadMember> members,
            Vector3 targetPosition)
        {
            ExecuteCommand(commandType, members, targetPosition, null);
        }

        public void ExecuteCommand(SquadCommandType commandType, IReadOnlyList<SquadMember> members,
            Vector3 targetPosition, UnitHealth explicitAttackTarget)
        {
            CoreEventBus.PublishGlobal(new SquadCommandIssuedEvent
            {
                CommandType = commandType,
                TargetPosition = targetPosition,
                MemberCount = members?.Count ?? 0
            });

            switch (commandType)
            {
                case SquadCommandType.Move:
                    ExecuteMoveCommand(members, targetPosition);
                    return;
                case SquadCommandType.Attack:
                    ExecuteAttackCommand(members, targetPosition, explicitAttackTarget, false);
                    return;
                case SquadCommandType.HoldPosition:
                    ExecuteHoldPositionCommand(members);
                    return;
                case SquadCommandType.Follow:
                    ExecuteFollowCommand(members);
                    return;
                case SquadCommandType.Defend:
                    ExecuteDefendCommand(members, targetPosition);
                    return;
                default:
                    return;
            }
        }

        public void ExecuteMoveCommand(IReadOnlyList<SquadMember> members, Vector3 destination)
        {
            if (members == null || members.Count == 0) return;

            var orderableCount = CountOrderableMembers(members);
            var groupForward = ResolveGroupForward(members, destination);
            var slots = ResolveFormationSlots(destination, groupForward, orderableCount);
            SquadMoveDeconfliction.IssueGroupMove(members, destination, groupForward, slots, BuildDeconflictionSettings());
        }

        public SquadMoveDeconfliction.Settings MoveDeconflictionSettings => BuildDeconflictionSettings();

        /// <summary>
        ///     Resolves preview slot destinations using the same pipeline as move commit.
        /// </summary>
        public bool TryResolveGroupMovePreview(
            IReadOnlyList<SquadMember> members,
            Vector3 destination,
            List<SquadMoveDeconfliction.GroupMoveAssignment> assignmentsOut)
        {
            assignmentsOut?.Clear();
            if (members == null || members.Count == 0 || assignmentsOut == null) return false;

            var orderableCount = CountOrderableMembers(members);
            if (orderableCount == 0) return false;

            var groupForward = ResolveGroupForward(members, destination);
            var slots = ResolveFormationSlots(destination, groupForward, orderableCount);
            return SquadMoveDeconfliction.TryResolveGroupMoveAssignments(
                       members,
                       destination,
                       groupForward,
                       slots,
                       BuildDeconflictionSettings(),
                       assignmentsOut) > 0;
        }

        public void ExecuteMoveAssignments(IReadOnlyList<SquadMoveDeconfliction.GroupMoveAssignment> assignments)
        {
            if (assignments == null || assignments.Count == 0) return;

            CoreEventBus.PublishGlobal(new SquadCommandIssuedEvent
            {
                CommandType = SquadCommandType.Move,
                TargetPosition = assignments[0].Destination,
                MemberCount = assignments.Count
            });

            SquadMoveDeconfliction.IssueGroupMoveAssignments(assignments, BuildDeconflictionSettings());
        }

        public void RegroupMembers(IReadOnlyList<SquadMember> members)
        {
            if (members == null || members.Count == 0) return;

            var center = ResolveGroupCenter(members);
            var orderableCount = CountOrderableMembers(members);
            var forward = ResolveGroupForward(members, center + Vector3.forward);
            var slots = ResolveFormationSlots(center, forward, orderableCount);
            SquadMoveDeconfliction.IssueGroupMove(members, center, forward, slots, BuildDeconflictionSettings());
        }

        private static int CountOrderableMembers(IReadOnlyList<SquadMember> members)
        {
            if (members == null) return 0;

            var count = 0;
            for (var i = 0; i < members.Count; i++)
            {
                if (IsOrderableMember(members[i])) count++;
            }

            return count;
        }

        private static Vector3 ResolveGroupCenter(IReadOnlyList<SquadMember> members)
        {
            var sum = Vector3.zero;
            var count = 0;
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (!IsOrderableMember(member)) continue;

                sum += member.UnitController.transform.position;
                count++;
            }

            return count > 0 ? sum / count : Vector3.zero;
        }

        private IReadOnlyList<Vector3> ResolveFormationSlots(Vector3 center, Vector3 forward, int unitCount)
        {
            if (formationController == null) return null;

            return formationController.CalculateFormationSlots(center, forward, unitCount);
        }

        private SquadMoveDeconfliction.Settings BuildDeconflictionSettings()
        {
            return new SquadMoveDeconfliction.Settings
            {
                GroupSpacingBaseline = groupSpacingBaseline,
                AdaptiveSpacingGain = adaptiveSpacingGain,
                MinSeparationFloor = minSeparationFloor,
                MinSeparationMultiplier = minSeparationMultiplier,
                MaxDeconflictionIterations = maxDeconflictionIterations,
                DeconflictionStepMeters = deconflictionStepMeters,
                AdaptiveSpacingEnabled = adaptiveSpacingEnabled,
                SampleSlotsOnNavMesh = sampleSlotsOnNavMesh,
                LogDiagnostics = logMoveDeconflictionDiagnostics
            };
        }

        private static Vector3 ResolveGroupForward(IReadOnlyList<SquadMember> members, Vector3 destination)
        {
            var groupForward = Vector3.forward;
            if (members[0]?.UnitController == null) return groupForward;

            var toTarget = destination - members[0].UnitController.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f) groupForward = toTarget.normalized;

            return groupForward;
        }

        public void ExecuteAttackCommand(IReadOnlyList<SquadMember> members, Vector3 focusPosition,
            UnitHealth explicitTarget = null, bool publishIssuedEvent = true)
        {
            if (members == null) return;

            if (publishIssuedEvent)
            {
                var targetPosition = explicitTarget != null ? explicitTarget.transform.position : focusPosition;
                CoreEventBus.PublishGlobal(new SquadCommandIssuedEvent
                {
                    CommandType = SquadCommandType.Attack,
                    TargetPosition = targetPosition,
                    MemberCount = members.Count
                });
            }

            if (explicitTarget != null && !explicitTarget.IsDead)
            {
                var targetPosition = explicitTarget.transform.position;
                var explicitApproachSlots = CalculateAttackApproachSlots(targetPosition, members.Count);
                SquadMoveDeconfliction.IssueGroupMove(
                    members,
                    targetPosition,
                    ResolveGroupForward(members, targetPosition),
                    explicitApproachSlots,
                    BuildDeconflictionSettings());

                for (var i = 0; i < members.Count; i++)
                {
                    var member = members[i];
                    if (!IsOrderableMember(member)) continue;

                    _targetBuffer.Clear();
                    _targetBuffer.Add(explicitTarget);

                    member.UnitCombat?.SetMarkedTarget(explicitTarget);
                    ExecuteMemberAttack(member);
                    member.SetCommandState(SquadCommandType.Attack);
                }

                return;
            }

            var approachSlots = CalculateAttackApproachSlots(focusPosition, members.Count);
            SquadMoveDeconfliction.IssueGroupMove(
                members,
                focusPosition,
                ResolveGroupForward(members, focusPosition),
                approachSlots,
                BuildDeconflictionSettings());

            foreach (var member in members)
            {
                if (!IsOrderableMember(member)) continue;

                CollectAttackTargets(member);
                ExecuteMemberAttack(member);
                member.SetCommandState(SquadCommandType.Attack);
            }
        }

        private static bool IsOrderableMember(SquadMember member)
        {
            return member != null && member.IsAvailableForOrders();
        }

        private static IReadOnlyList<Vector3> CalculateAttackApproachSlots(Vector3 targetPosition, int unitCount)
        {
            var slots = new List<Vector3>(Mathf.Max(0, unitCount));
            if (unitCount <= 0) return slots;

            var radius = Mathf.Max(1.35f, 0.7f + unitCount * 0.32f);

            for (var i = 0; i < unitCount; i++)
            {
                var angle = i * Mathf.PI * 2f / unitCount;
                slots.Add(targetPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }

            return slots;
        }

        private void CollectAttackTargets(SquadMember member)
        {
            _targetBuffer.Clear();

            if (UnitManager.Instance == null || member.Unit == null) return;

            var nearbyEnemies = UnitManager.Instance.FindNearbyEnemies(
                member.Unit,
                CombatTuningConfig.AttackScanRadiusOr(attackSearchRadius));
            if (nearbyEnemies == null) return;

            _targetBuffer.AddRange(nearbyEnemies
                .Where(static enemy => enemy != null && enemy.Health != null && !enemy.Health.IsDead)
                .Select(static enemy => enemy.Health));
        }

        private void ExecuteMemberAttack(SquadMember member)
        {
            if (combatSystem != null)
            {
                combatSystem.TryExecuteAttack(member.UnitCombat, _targetBuffer);
                return;
            }

            if (combatManager != null)
            {
                combatManager.RequestAttack(member.UnitCombat, _targetBuffer);
                return;
            }

            member.UnitCombat?.ExecuteAttack(_targetBuffer);
        }

        public static void ExecuteHoldPositionCommand(IReadOnlyList<SquadMember> members)
        {
            if (members == null) return;

            foreach (var member in members)
            {
                if (member == null || !member.IsAvailableForOrders()) continue;

                member.UnitController?.Stop();

                // While holding, allow local auto-engagement:
                // mark no specific target so UnitCombat will auto-select the nearest threat.
                member.UnitCombat?.ClearMarkedTarget();
                member.SetCommandState(SquadCommandType.HoldPosition);
            }
        }

        public static void ExecuteFollowCommand(IReadOnlyList<SquadMember> members)
        {
            if (members == null) return;

            // Resolve the leader: use the player-controlled unit if found, else the first member.
            Transform leaderTransform = null;
            if (UnitManager.Instance != null)
            {
                var playerUnit = UnitManager.Instance.FindFirstUnitByRole(UnitRole.Player);
                if (playerUnit != null) leaderTransform = playerUnit.transform;
            }

            foreach (var member in members)
            {
                if (member == null || !member.IsAvailableForOrders()) continue;

                member.FollowController?.SetFollowStyle(FollowStyle.Loose);

                // Immediately begin moving toward leader.
                if (leaderTransform != null)
                    member.FollowController?.TickFollow(leaderTransform.position, leaderTransform.forward);

                member.SetCommandState(SquadCommandType.Follow);
            }
        }

        public void ExecuteDefendCommand(IReadOnlyList<SquadMember> members, Vector3 defendCenter)
        {
            formationController?.SetFormation(FormationType.DefensiveCircle);

            if (members == null) return;

            var defendSlots = ResolveFormationSlots(defendCenter, Vector3.forward, CountOrderableMembers(members));
            SquadMoveDeconfliction.IssueGroupMove(
                members,
                defendCenter,
                Vector3.forward,
                defendSlots,
                BuildDeconflictionSettings());

            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || !member.IsAvailableForOrders()) continue;

                member.UnitCombat?.ClearMarkedTarget();
                member.SetCommandState(SquadCommandType.Defend);
            }
        }

        /// <summary>
        ///     When a member leaves the squad mid-command, re-issues the current follow order
        ///     to the remaining roster so formation slots are recalculated cleanly.
        /// </summary>
        public static void ReassignCommandsAwayFrom(SquadMember leavingMember, IReadOnlyList<SquadMember> remaining)
        {
            if (remaining == null || remaining.Count == 0) return;

            // Re-issue follow so survivors recompute formation without the gap.
            ExecuteFollowCommand(remaining);
        }
    }

    public enum SquadCommandType
    {
        Move,
        Attack,
        HoldPosition,
        Follow,
        Defend
    }

    /// <summary>
    ///     Renders short-lived 3D markers for squad commands (Move, Attack, Defend).
    /// </summary>
}
