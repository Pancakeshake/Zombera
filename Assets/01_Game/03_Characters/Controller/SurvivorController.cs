#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Survivor behavior and recruitability profile (traits, skills, morale).
    /// </summary>
    public class SurvivorController : MonoBehaviour
    {
        private void OnEnable()
        {
            RuntimeGameplayAiRegistry.RegisterSurvivor(this);
        }

        private void OnDisable()
        {
            RuntimeGameplayAiRegistry.UnregisterSurvivor(this);
        }
        [SerializeField] private List<string> traits = new();
        [SerializeField] private Unit unit;
        [SerializeField] private UnitStats unitStats;
        [SerializeField] private int morale = 50;

        public IReadOnlyList<string> Traits => traits;
        public UnitStats Stats => unitStats;
        public int Morale => morale;
        public bool IsRecruited { get; private set; }

        protected virtual void Awake()
        {
            if (unit == null) unit = GetComponent<Unit>();

            if (unit == null) return;

            unit.SetRole(UnitRole.Survivor);
            unit.SetOptionalAI(this);
        }

        // ReSharper disable once UnusedMember.Global
        public bool EvaluateRecruitment(RecruitmentMethod method)
        {
            var baseChance = morale;

            baseChance += method switch
            {
                RecruitmentMethod.Rescue => 25,
                RecruitmentMethod.HireFromSettlement => 15,
                RecruitmentMethod.RandomWanderer => 5,
                RecruitmentMethod.PrisonerRecruitment => -15,
                _ => 0
            };

            if (traits.Contains("Loyal")) baseChance += 10;
            if (traits.Contains("Distrustful")) baseChance -= 10;
            if (traits.Contains("Desperate")) baseChance += 20;

            var roll = Random.Range(0, 100);
            return roll < Mathf.Clamp(baseChance, 5, 95);
        }

        // ReSharper disable once UnusedMember.Global
        public void MarkRecruited()
        {
            IsRecruited = true;
            unit?.SetRole(UnitRole.SquadMember);

            var squadAI = GetComponent<SquadController>();

            if (squadAI == null) squadAI = gameObject.AddComponent<SquadController>();

            squadAI.enabled = true;
            enabled = false;
        }
    }
}