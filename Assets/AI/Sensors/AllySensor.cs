using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.AI.Sensors
{
    /// <summary>
    /// Collects nearby allied units to support coordination and retreat scoring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AllySensor : MonoBehaviour
    {
        [SerializeField] private float allySearchRadius = 12f;
        [SerializeField] private bool includeSelf;

        private readonly List<Unit> allyBuffer = new List<Unit>();

        public int NearbyAlliesCount { get; private set; }
        public Unit NearestAlly { get; private set; }
        public float NearestAllyDistance { get; private set; } = float.PositiveInfinity;
        public IReadOnlyList<Unit> NearbyAllies => allyBuffer;

        public void Sense(Unit self)
        {
            ResetReadings();

            if (self == null || UnitManager.Instance == null)
            {
                return;
            }

            UnitManager.Instance.FindNearbyUnits(self.transform.position, allySearchRadius, allyBuffer);

            for (int i = allyBuffer.Count - 1; i >= 0; i--)
            {
                Unit candidate = allyBuffer[i];

                if (candidate == null)
                {
                    allyBuffer.RemoveAt(i);
                    continue;
                }

                if (!includeSelf && candidate == self)
                {
                    allyBuffer.RemoveAt(i);
                    continue;
                }

                if (!AreAllied(self.Role, candidate.Role))
                {
                    allyBuffer.RemoveAt(i);
                    continue;
                }

                float distance = Vector3.Distance(self.transform.position, candidate.transform.position);

                if (distance < NearestAllyDistance)
                {
                    NearestAlly = candidate;
                    NearestAllyDistance = distance;
                }
            }

            NearbyAlliesCount = allyBuffer.Count;

            // TODO: Add role tags (medic/leader/shooter) for contextual support behaviors.
        }

        private void ResetReadings()
        {
            allyBuffer.Clear();
            NearbyAlliesCount = 0;
            NearestAlly = null;
            NearestAllyDistance = float.PositiveInfinity;
        }

        private static bool AreAllied(UnitRole source, UnitRole target)
        {
            bool sourceIsHumanFaction = source == UnitRole.Player || source == UnitRole.SquadMember || source == UnitRole.Survivor;
            bool targetIsHumanFaction = target == UnitRole.Player || target == UnitRole.SquadMember || target == UnitRole.Survivor;

            if (sourceIsHumanFaction && targetIsHumanFaction)
            {
                return true;
            }

            if (source == UnitRole.Zombie && target == UnitRole.Zombie)
            {
                return true;
            }

            if (source == UnitRole.Enemy && (target == UnitRole.Enemy || target == UnitRole.Zombie))
            {
                return true;
            }

            if (source == UnitRole.Bandit && target == UnitRole.Bandit)
            {
                return true;
            }

            return false;
        }
    }
}