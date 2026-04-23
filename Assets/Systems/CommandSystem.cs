using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Core;

namespace Zombera.Systems
{
    /// <summary>
    /// Executes player-issued squad commands such as move, attack, hold, follow, and defend.
    /// </summary>
    public sealed class CommandSystem : MonoBehaviour
    {
        [SerializeField] private FormationController formationController;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private CombatSystem combatSystem;
        [SerializeField] private float attackSearchRadius = 20f;

        private readonly List<UnitHealth> targetBuffer = new List<UnitHealth>();

        public void ExecuteCommand(SquadCommandType commandType, IReadOnlyList<SquadMember> members, Vector3 targetPosition)
        {
            EventSystem.PublishGlobal(new SquadCommandIssuedEvent
            {
                CommandType = commandType,
                TargetPosition = targetPosition,
                MemberCount = members != null ? members.Count : 0
            });

            switch (commandType)
            {
                case SquadCommandType.Move:
                    ExecuteMoveCommand(members, targetPosition);
                    break;
                case SquadCommandType.Attack:
                    ExecuteAttackCommand(members, targetPosition);
                    break;
                case SquadCommandType.HoldPosition:
                    ExecuteHoldPositionCommand(members);
                    break;
                case SquadCommandType.Follow:
                    ExecuteFollowCommand(members);
                    break;
                case SquadCommandType.Defend:
                    ExecuteDefendCommand(members, targetPosition);
                    break;
            }
        }

        public void ExecuteMoveCommand(IReadOnlyList<SquadMember> members, Vector3 destination)
        {
            if (formationController == null || members == null || members.Count == 0)
            {
                // No formation — move everyone to the same point.
                if (members == null) return;
                for (int i = 0; i < members.Count; i++)
                {
                    SquadMember member = members[i];
                    if (member == null || !member.IsAvailableForOrders()) continue;
                    member.UnitController?.MoveTo(destination);
                }
                return;
            }

            // Slot each member into a formation centered on destination.
            Vector3 groupForward = Vector3.forward;
            if (members[0]?.UnitController != null)
            {
                Vector3 toTarget = destination - members[0].UnitController.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f)
                    groupForward = toTarget.normalized;
            }

            IReadOnlyList<Vector3> slots = formationController.CalculateFormationSlots(
                destination, groupForward, members.Count);

            for (int i = 0; i < members.Count; i++)
            {
                SquadMember member = members[i];
                if (member == null || !member.IsAvailableForOrders()) continue;
                Vector3 slotPos = (i < slots.Count) ? slots[i] : destination;
                member.UnitController?.MoveTo(slotPos);
            }
        }

        public void ExecuteAttackCommand(IReadOnlyList<SquadMember> members, Vector3 focusPosition)
        {
            if (members == null)
            {
                return;
            }

            for (int i = 0; i < members.Count; i++)
            {
                SquadMember member = members[i];

                if (member == null || !member.IsAvailableForOrders())
                {
                    continue;
                }

                if (focusPosition != default)
                {
                    member.UnitController?.MoveTo(focusPosition);
                }

                targetBuffer.Clear();

                if (UnitManager.Instance != null && member.Unit != null)
                {
                    List<Unit> nearbyEnemies = UnitManager.Instance.FindNearbyEnemies(member.Unit, attackSearchRadius);

                    for (int enemyIndex = 0; enemyIndex < nearbyEnemies.Count; enemyIndex++)
                    {
                        Unit enemy = nearbyEnemies[enemyIndex];

                        if (enemy != null && enemy.Health != null && !enemy.Health.IsDead)
                        {
                            targetBuffer.Add(enemy.Health);
                        }
                    }
                }

                if (combatSystem != null)
                {
                    combatSystem.TryExecuteAttack(member.UnitCombat, targetBuffer);
                }
                else if (combatManager != null)
                {
                    combatManager.RequestAttack(member.UnitCombat, targetBuffer);
                }
                else
                {
                    member.UnitCombat?.ExecuteAttack(targetBuffer);
                }
            }
        }

        public void ExecuteHoldPositionCommand(IReadOnlyList<SquadMember> members)
        {
            if (members == null)
            {
                return;
            }

            for (int i = 0; i < members.Count; i++)
            {
                SquadMember member = members[i];

                if (member == null || !member.IsAvailableForOrders())
                {
                    continue;
                }

                member.UnitController?.Stop();

                // While holding, allow local auto-engagement:
                // mark no specific target so UnitCombat will auto-select the nearest threat.
                member.UnitCombat?.ClearMarkedTarget();
            }
        }

        public void ExecuteFollowCommand(IReadOnlyList<SquadMember> members)
        {
            if (members == null) return;

            // Resolve the leader: use the player-controlled unit if found, else the first member.
            Transform leaderTransform = null;
            if (UnitManager.Instance != null)
            {
                Unit playerUnit = UnitManager.Instance.FindFirstUnitByRole(UnitRole.Player);
                if (playerUnit != null) leaderTransform = playerUnit.transform;
            }

            for (int i = 0; i < members.Count; i++)
            {
                SquadMember member = members[i];

                if (member == null || !member.IsAvailableForOrders())
                {
                    continue;
                }

                member.FollowController?.SetFollowStyle(FollowStyle.Loose);

                // Immediately begin moving toward leader.
                if (leaderTransform != null)
                {
                    member.FollowController?.TickFollow(leaderTransform.position, leaderTransform.forward);
                }
            }
        }

        public void ExecuteDefendCommand(IReadOnlyList<SquadMember> members, Vector3 defendCenter)
        {
            formationController?.SetFormation(FormationType.DefensiveCircle);

            if (members == null)
            {
                return;
            }

            IReadOnlyList<Vector3> defendSlots = formationController != null
                ? formationController.CalculateFormationSlots(defendCenter, Vector3.forward, members.Count)
                : null;

            for (int i = 0; i < members.Count; i++)
            {
                SquadMember member = members[i];

                if (member == null || !member.IsAvailableForOrders())
                {
                    continue;
                }

                // Assign a deterministic perimeter slot and navigate to it.
                Vector3 slotPosition = (defendSlots != null && i < defendSlots.Count)
                    ? defendSlots[i]
                    : defendCenter;

                member.UnitController?.MoveTo(slotPosition);

                // Clear marked target so auto-engagement triggers from the perimeter.
                member.UnitCombat?.ClearMarkedTarget();
            }
        }

        /// <summary>
        /// When a member leaves the squad mid-command, re-issues the current follow order
        /// to the remaining roster so formation slots are recalculated cleanly.
        /// </summary>
        public void ReassignCommandsAwayFrom(SquadMember leavingMember, IReadOnlyList<SquadMember> remaining)
        {
            if (remaining == null || remaining.Count == 0)
            {
                return;
            }

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
}