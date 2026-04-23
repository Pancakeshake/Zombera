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
        public Unit NearestMedic { get; private set; }
        public Unit NearestLeader { get; private set; }
        public Unit NearestShooter { get; private set; }

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
            ClassifyAlliesByRole();
        }

        private void ClassifyAlliesByRole()
        {
            NearestMedic = null;
            NearestLeader = null;
            NearestShooter = null;

            for (int i = 0; i < allyBuffer.Count; i++)
            {
                Unit ally = allyBuffer[i];

                if (ally == null)
                {
                    continue;
                }

                switch (ally.Role)
                {
                    case UnitRole.SquadMember:
                        // Use SquadMember.RolePreference for finer-grained tagging.
                        SquadMember sm = ally.GetComponent<SquadMember>();

                        if (sm == null)
                        {
                            break;
                        }

                        if (sm.RolePreference == MemberRolePreference.Medic && NearestMedic == null)
                        {
                            NearestMedic = ally;
                        }
                        else if (sm.RolePreference == MemberRolePreference.Assault && NearestShooter == null)
                        {
                            NearestShooter = ally;
                        }

                        break;
                    case UnitRole.Player:
                        if (NearestLeader == null)
                        {
                            NearestLeader = ally;
                        }

                        break;
                }
            }
        }

        private void ResetReadings()
        {
            allyBuffer.Clear();
            NearbyAlliesCount = 0;
            NearestAlly = null;
            NearestAllyDistance = float.PositiveInfinity;
            NearestMedic = null;
            NearestLeader = null;
            NearestShooter = null;
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