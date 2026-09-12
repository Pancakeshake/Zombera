#region

using System.Collections.Generic;
using UnityEngine;

#endregion

// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace Zombera.AI
{
    /// <summary>
    ///     Groups zombies into hordes and coordinates large-scale horde behavior.
    /// </summary>
    public class ZombieHordeManager : MonoBehaviour
    {
        private readonly Dictionary<int, List<ZombieController>> _hordes = new();
        private readonly Dictionary<int, ZombieController> _leaders = new();
        private int _nextHordeId;

        public int CreateHorde(IReadOnlyList<ZombieController> zombies)
        {
            var hordeId = _nextHordeId++;
            var members = new List<ZombieController>(zombies.Count);
            foreach (var z in zombies)
                if (z != null)
                    members.Add(z);

            _hordes[hordeId] = members;

            if (members.Count > 0) _leaders[hordeId] = members[0];

            return hordeId;
        }

        public ZombieController GetLeader(int hordeId)
        {
            _leaders.TryGetValue(hordeId, out var leader);
            return leader;
        }

        public void AddZombieToHorde(int hordeId, ZombieController zombie)
        {
            if (zombie == null) return;

            if (!_hordes.TryGetValue(hordeId, out var members))
            {
                members = new List<ZombieController>();
                _hordes[hordeId] = members;
            }

            if (!members.Contains(zombie)) members.Add(zombie);
        }

        public void SetHordeTarget(int hordeId, Vector3 targetPosition)
        {
            if (!_hordes.TryGetValue(hordeId, out var members)) return;

            foreach (var member in members)
                if (member != null)
                    member.DirectToPosition(targetPosition);
        }

        // ReSharper disable once UnusedMember.Global
        public void DisbandHorde(int hordeId)
        {
            if (_hordes.TryGetValue(hordeId, out var members))
                foreach (var member in members)
                    if (member != null)
                        member.DirectToPosition(member.transform.position);

            _hordes.Remove(hordeId);
            _leaders.Remove(hordeId);
        }
    }
}