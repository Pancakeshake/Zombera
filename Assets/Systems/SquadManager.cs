using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Systems
{
    /// <summary>
    /// Tracks all active squad members and coordinates group-level command dispatch.
    /// </summary>
    public sealed class SquadManager : MonoBehaviour
    {
        [SerializeField] private CommandSystem commandSystem;

        private readonly List<SquadMember> squadMembers = new List<SquadMember>();

        public IReadOnlyList<SquadMember> SquadMembers => squadMembers;

        public void RegisterMember(SquadMember member)
        {
            if (member == null || squadMembers.Contains(member))
            {
                return;
            }

            squadMembers.Add(member);

            // TODO: Broadcast squad roster update to UI/system listeners.
        }

        public void UnregisterMember(SquadMember member)
        {
            if (member == null)
            {
                return;
            }

            squadMembers.Remove(member);

            // TODO: Handle active command reassignment when member leaves.
        }

        public void IssueOrder(SquadCommandType commandType, Vector3 targetPosition = default)
        {
            commandSystem?.ExecuteCommand(commandType, squadMembers, targetPosition);
        }

        public SquadMember GetMemberById(string memberId)
        {
            for (int i = 0; i < squadMembers.Count; i++)
            {
                if (squadMembers[i].MemberId == memberId)
                {
                    return squadMembers[i];
                }
            }

            return null;
        }
    }
}