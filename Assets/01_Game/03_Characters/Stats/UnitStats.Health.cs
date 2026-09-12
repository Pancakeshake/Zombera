using UnityEngine;

namespace Zombera.Characters
{
    public sealed partial class UnitStats
    {
        public void SetStrengthBaseHealth(float baseMaxHealth, bool refillCurrentHealth)
        {
            _strengthBaseMaxHealth = Mathf.Max(1f, baseMaxHealth);
            _hasStrengthBaseHealth = true;
            ApplyAllHealthBonuses(refillCurrentHealth);
        }

        private void ApplyAllHealthBonuses(bool refillCurrentHealth)
        {
            if (unitHealth == null) return;
            
            var baseMax = GetBaseHealthForScaling();
            var targetMaxHealth = baseMax
                                  * GetHealthMultiplier(UnitSkillType.Strength, strengthHealthBonusPerLevelPercent)
                                  * GetHealthMultiplier(UnitSkillType.Toughness, toughnessHealthBonusPerLevelPercent)
                                  * GetHealthMultiplier(UnitSkillType.Constitution, constitutionHealthBonusPerLevelPercent);
            
            unitHealth.SetMaxHealth(targetMaxHealth, refillCurrentHealth);
        }

        private float GetBaseHealthForScaling()
        {
            CacheStrengthBaseHealthIfNeeded();
            return _strengthBaseMaxHealth;
        }

        private float GetHealthMultiplier(UnitSkillType skillType, float bonusPercentPerLevel)
        {
            var t = SkillT(GetLevelRef(skillType));
            return 1f + t * (MaxSkillLevel - MinSkillLevel) * bonusPercentPerLevel * 0.01f;
        }

        private void CacheStrengthBaseHealthIfNeeded()
        {
            if (_hasStrengthBaseHealth || unitHealth == null) return;

            _strengthBaseMaxHealth = Mathf.Max(1f, unitHealth.MaxHealth);
            _hasStrengthBaseHealth = true;
        }
    }
}