using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Systems
{
    /// <summary>
    /// Survivor behavior and recruitability profile (traits, skills, morale).
    /// </summary>
    public sealed class SurvivorAI : MonoBehaviour
    {
        [SerializeField] private List<string> traits = new List<string>();
        [SerializeField] private Unit unit;
        [SerializeField] private UnitStats unitStats;
        [SerializeField] private int morale = 50;

        public IReadOnlyList<string> Traits => traits;
        public UnitStats Stats => unitStats;
        public int Morale => morale;
        public bool IsRecruited { get; private set; }

        private void Awake()
        {
            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }

            if (unit != null)
            {
                unit.SetRole(UnitRole.Survivor);
                unit.SetOptionalAI(this);
            }
        }

        public bool EvaluateRecruitment(RecruitmentMethod method)
        {
            int baseChance = morale;

            switch (method)
            {
                case RecruitmentMethod.Rescue:
                    baseChance += 25;
                    break;
                case RecruitmentMethod.HireFromSettlement:
                    baseChance += 15;
                    break;
                case RecruitmentMethod.RandomWanderer:
                    baseChance += 5;
                    break;
                case RecruitmentMethod.PrisonerRecruitment:
                    baseChance -= 15;
                    break;
            }

            int roll = Random.Range(0, 100);
            return roll < Mathf.Clamp(baseChance, 5, 95);

            // TODO: Replace random check with relationship/faction/system-driven logic.
        }

        public void ApplyMoraleChange(int amount)
        {
            morale = Mathf.Clamp(morale + amount, 0, 100);
        }

        public void MarkRecruited()
        {
            IsRecruited = true;
            unit?.SetRole(UnitRole.SquadMember);

            // TODO: Switch AI package from neutral survivor to squad behavior.
        }
    }
}