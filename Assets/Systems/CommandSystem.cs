using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;

namespace Zombera.Systems
{
    /// <summary>
    /// Executes player-issued squad commands such as move, attack, hold, follow, and defend.
    /// </summary>
    public sealed class CommandSystem : MonoBehaviour
    {
        [SerializeField] private FormationController formationController;

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
            // TODO: Route attack command through targeting and threat map.
            _ = members;
            _ = focusPosition;
        }

        public void ExecuteHoldPositionCommand(IReadOnlyList<SquadMember> members)
        {
            // TODO: Freeze movement and allow engagement within hold radius.
            _ = members;
        }

        public void ExecuteFollowCommand(IReadOnlyList<SquadMember> members)
        {
            // TODO: Set follow behavior active and bind leader reference.
            _ = members;
        }

        public void ExecuteDefendCommand(IReadOnlyList<SquadMember> members, Vector3 defendCenter)
        {
            formationController?.SetFormation(FormationType.DefensiveCircle);

            // TODO: Assign defensive slots and maintain perimeter around defendCenter.
            _ = members;
            _ = defendCenter;
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