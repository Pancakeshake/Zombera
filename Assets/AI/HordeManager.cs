using System.Collections.Generic;
using UnityEngine;

namespace Zombera.AI
{
    /// <summary>
    /// Groups zombies into hordes and coordinates large-scale horde behavior.
    /// </summary>
    public sealed class HordeManager : MonoBehaviour
    {
        private readonly Dictionary<int, List<ZombieAI>> hordes = new Dictionary<int, List<ZombieAI>>();
        private readonly Dictionary<int, ZombieAI> leaders = new Dictionary<int, ZombieAI>();
        private int nextHordeId;

        public int CreateHorde(IReadOnlyList<ZombieAI> zombies)
        {
            int hordeId = nextHordeId++;
            var members = new List<ZombieAI>(zombies.Count);
            for (int i = 0; i < zombies.Count; i++)
            {
                if (zombies[i] != null)
                    members.Add(zombies[i]);
            }
            hordes[hordeId] = members;

            // First non-null member becomes the leader.
            if (members.Count > 0)
                leaders[hordeId] = members[0];

            return hordeId;
        }

        public ZombieAI GetLeader(int hordeId)
        {
            leaders.TryGetValue(hordeId, out ZombieAI leader);
            return leader;
        }

        public void AddZombieToHorde(int hordeId, ZombieAI zombie)
        {
            if (zombie == null)
            {
                return;
            }

            if (!hordes.TryGetValue(hordeId, out List<ZombieAI> members))
            {
                members = new List<ZombieAI>();
                hordes[hordeId] = members;
            }

            if (!members.Contains(zombie))
            {
                members.Add(zombie);
            }
        }

        public void SetHordeTarget(int hordeId, Vector3 targetPosition)
        {
            if (!hordes.TryGetValue(hordeId, out List<ZombieAI> members))
                return;

            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null)
                    members[i].DirectToPosition(targetPosition);
            }
        }

        public void DisbandHorde(int hordeId)
        {
            // Members return to their local AI — Investigate their current position
            // so they spread out naturally rather than freezing in place.
            if (hordes.TryGetValue(hordeId, out List<ZombieAI> members))
            {
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i] != null)
                        members[i].DirectToPosition(members[i].transform.position);
                }
            }

            hordes.Remove(hordeId);
            leaders.Remove(hordeId);
        }
    }
}