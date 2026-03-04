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
            for (int i = 0; i < members.Count; i++)
            {
                SquadMember member = members[i];

                if (member == null || !member.IsAvailableForOrders())
                {
                    continue;
                }

                member.UnitController?.MoveTo(destination);
            }

            // TODO: Support per-member slotting for ordered group move.
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
            }

            // TODO: Allow local auto-engagement while maintaining hold radius.
        }

        public void ExecuteFollowCommand(IReadOnlyList<SquadMember> members)
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

                member.FollowController?.SetFollowStyle(FollowStyle.Loose);
            }

            // TODO: Bind active leader target for follow execution.
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

                Vector3 slotPosition = defendCenter;

                if (defendSlots != null && i < defendSlots.Count)
                {
                    slotPosition = defendSlots[i];
                }

                member.UnitController?.MoveTo(slotPosition);
            }

            // TODO: Assign defensive slots and maintain perimeter around defendCenter.
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