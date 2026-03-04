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
        private int nextHordeId;

        public int CreateHorde(IReadOnlyList<ZombieAI> zombies)
        {
            int hordeId = nextHordeId++;
            hordes[hordeId] = new List<ZombieAI>(zombies);

            // TODO: Assign leader and shared target metadata.
            return hordeId;
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
            // TODO: Propagate group movement/aggro target to all horde members.
            _ = hordeId;
            _ = targetPosition;
        }

        public void DisbandHorde(int hordeId)
        {
            hordes.Remove(hordeId);

            // TODO: Reset members to local AI behavior after disband.
        }
    }
}