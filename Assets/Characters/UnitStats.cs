using UnityEngine;

namespace Zombera.Characters
{
    /// <summary>
    /// Stores gameplay attributes used by combat, crafting, and AI behavior decisions.
    /// </summary>
    public sealed class UnitStats : MonoBehaviour
    {
        [Header("Core Attributes")]
        [SerializeField] private int strength = 5;
        [SerializeField] private int shooting = 5;
        [SerializeField] private int melee = 5;
        [SerializeField] private int medical = 5;
        [SerializeField] private int engineering = 5;
        [SerializeField] private int morale = 50;

        public int Strength => strength;
        public int Shooting => shooting;
        public int Melee => melee;
        public int Medical => medical;
        public int Engineering => engineering;
        public int Morale => morale;

        public int GetSkillValue(UnitSkillType skillType)
        {
            switch (skillType)
            {
                case UnitSkillType.Strength:
                    return strength;
                case UnitSkillType.Shooting:
                    return shooting;
                case UnitSkillType.Melee:
                    return melee;
                case UnitSkillType.Medical:
                    return medical;
                case UnitSkillType.Engineering:
                    return engineering;
                case UnitSkillType.Morale:
                    return morale;
                default:
                    return 0;
            }
        }

        public void ModifyMorale(int amount)
        {
            morale = Mathf.Clamp(morale + amount, 0, 100);

            // TODO: Trigger morale threshold effects (panic/boost/desertion).
        }

        public void SetSkill(UnitSkillType skillType, int value)
        {
            int clampedValue = Mathf.Clamp(value, 0, 100);

            switch (skillType)
            {
                case UnitSkillType.Strength:
                    strength = clampedValue;
                    break;
                case UnitSkillType.Shooting:
                    shooting = clampedValue;
                    break;
                case UnitSkillType.Melee:
                    melee = clampedValue;
                    break;
                case UnitSkillType.Medical:
                    medical = clampedValue;
                    break;
                case UnitSkillType.Engineering:
                    engineering = clampedValue;
                    break;
                case UnitSkillType.Morale:
                    morale = clampedValue;
                    break;
            }

            // TODO: Add progression/experience integration.
        }
    }

    public enum UnitSkillType
    {
        Strength,
        Shooting,
        Melee,
        Medical,
        Engineering,
        Morale
    }
}