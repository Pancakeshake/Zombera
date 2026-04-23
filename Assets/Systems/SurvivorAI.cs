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

            // Trait-based modifiers.
            if (traits.Contains("Loyal"))      baseChance += 10;
            if (traits.Contains("Distrustful")) baseChance -= 10;
            if (traits.Contains("Desperate"))  baseChance += 20;

            int roll = Random.Range(0, 100);
            return roll < Mathf.Clamp(baseChance, 5, 95);
        }

        public void ApplyMoraleChange(int amount)
        {
            morale = Mathf.Clamp(morale + amount, 0, 100);
        }

        public void MarkRecruited()
        {
            IsRecruited = true;
            unit?.SetRole(UnitRole.SquadMember);

            // Switch to squad-driven AI by disabling survivor autonomy and
            // letting the SquadAI/FollowController take over from here.
            SquadAI squadAI = GetComponent<SquadAI>();

            if (squadAI == null)
            {
                squadAI = gameObject.AddComponent<SquadAI>();
            }

            squadAI.enabled = true;
            enabled = false; // Survivor autonomy yields to squad package.
        }
    }
}