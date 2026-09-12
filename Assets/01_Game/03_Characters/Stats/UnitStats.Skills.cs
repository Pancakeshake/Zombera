using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Characters
{
    public sealed partial class UnitStats
    {
        public int GetSkillLevel(UnitSkillType skillType)
        {
            return GetLevelRef(skillType);
        }

        public float GetCurrentExperience(UnitSkillType skillType)
        {
            return GetExperienceRef(skillType);
        }

        public float GetExperienceRequiredForNextLevel(UnitSkillType skillType)
        {
            return GetXpRequiredForLevel(skillType, GetLevelRef(skillType));
        }

        public float GetTotalExperienceEarned(UnitSkillType skillType)
        {
            var level = GetLevelRef(skillType);
            var currentXp = GetExperienceRef(skillType);
            var (baseReq, growthPerLevel) = GetProgressionConstants(skillType);

            var spentXp = 0f;
            for (var priorLevel = MinSkillLevel; priorLevel < level; priorLevel++)
                spentXp += EvaluateXpRequirement(skillType, priorLevel, baseReq, growthPerLevel);

            return Mathf.Max(0f, spentXp + currentXp);
        }

        public int GetSkillValue(UnitSkillType skillType)
        {
            var bonus = _equipmentBonuses.GetValueOrDefault(skillType);
            return Mathf.Clamp(GetLevelRef(skillType) + bonus, MinSkillLevel, MaxSkillLevel);
        }

        public void AddEquipmentBonus(UnitSkillType skill, int flat)
        {
            if (flat == 0) return;
            _equipmentBonuses.TryGetValue(skill, out var current);
            _equipmentBonuses[skill] = current + flat;
        }

        public void RemoveEquipmentBonus(UnitSkillType skill, int flat)
        {
            if (flat == 0 || !_equipmentBonuses.TryGetValue(skill, out var current)) return;
            var newVal = current - flat;
            if (newVal == 0)
                _equipmentBonuses.Remove(skill);
            else
                _equipmentBonuses[skill] = newVal;
        }

        public void SetSkill(UnitSkillType skillType, int value)
        {
            var clampedValue = Mathf.Clamp(value, MinSkillLevel, MaxSkillLevel);
            ref var level = ref GetLevelRef(skillType);
            
            if (level == clampedValue) return;

            level = clampedValue;
            if (IsHealthScalingSkill(skillType)) ApplyAllHealthBonuses(false);
        }

        public void SetSkillProgress(UnitSkillType skillType, int level, float currentExperience)
        {
            var clampedLevel = Mathf.Clamp(level, MinSkillLevel, MaxSkillLevel);
            var clampedXp = Mathf.Max(0f, currentExperience);

            if (clampedLevel >= MaxSkillLevel)
            {
                clampedXp = 0f;
            }
            else
            {
                var maxXpForLevel = GetXpRequiredForLevel(skillType, clampedLevel);
                clampedXp = maxXpForLevel > 0f ? Mathf.Clamp(clampedXp, 0f, maxXpForLevel - 0.0001f) : 0f;
            }

            ref var currentLevel = ref GetLevelRef(skillType);
            ref var currentXpField = ref GetExperienceRef(skillType);

            var changed = currentLevel != clampedLevel;
            currentLevel = clampedLevel;
            currentXpField = clampedXp;

            if (changed && IsHealthScalingSkill(skillType)) ApplyAllHealthBonuses(false);
        }

        public void ResetAllSkillsToLevelOne()
        {
            for (var i = 0; i <= (int)UnitSkillType.Stealth; i++)
            {
                var skill = (UnitSkillType)i;
                GetLevelRef(skill) = MinSkillLevel;
                GetExperienceRef(skill) = 0f;
            }
            ApplyAllHealthBonuses(false);
        }

        private void AddStrengthExperience(float amount) => AddExperienceInternal(UnitSkillType.Strength, amount);
        private void AddToughnessExperience(float amount) => AddExperienceInternal(UnitSkillType.Toughness, amount);
        private void AddConstitutionExperience(float amount) => AddExperienceInternal(UnitSkillType.Constitution, amount);
        private void AddShootingExperience(float amount) => AddExperienceInternal(UnitSkillType.Shooting, amount);
        private void AddMeleeExperience(float amount) => AddExperienceInternal(UnitSkillType.Melee, amount);
        private void AddAgilityExperience(float amount) => AddExperienceInternal(UnitSkillType.Agility, amount);
        private void AddEnduranceExperience(float amount) => AddExperienceInternal(UnitSkillType.Endurance, amount);
        private void AddScavengingExperience(float amount) => AddExperienceInternal(UnitSkillType.Scavenging, amount);
        private void AddStealthExperience(float amount) => AddExperienceInternal(UnitSkillType.Stealth, amount);
        private void AddMedicalExperience(float amount) => AddExperienceInternal(UnitSkillType.Medical, amount);
        private void AddEngineeringExperience(float amount) => AddExperienceInternal(UnitSkillType.Engineering, amount);

        private void AddExperienceInternal(UnitSkillType skillType, float amount)
        {
            if (amount <= 0f)
            {
                LogXpDebug(skillType, $"SKIP add XP amount={amount:0.###}");
                return;
            }

            ref var level = ref GetLevelRef(skillType);
            if (level >= MaxSkillLevel)
            {
                LogXpDebug(skillType, $"SKIP add XP at max level {level}");
                return;
            }

            var (baseReq, growth) = GetProgressionConstants(skillType);
            AddExperience(skillType, ref GetExperienceRef(skillType), ref level, amount,
                baseReq, growth,
                _ => { if (IsHealthScalingSkill(skillType)) ApplyAllHealthBonuses(false); });
        }

        private void AddExperience(UnitSkillType skillType, ref float xp, ref int level, float amount,
            float baseReq, float growthPerLevel, Action<int> onLevelUp)
        {
            var startingXp = xp;
            var startingLevel = level;
            xp += amount;
            var leveledUp = false;

            while (level < MaxSkillLevel)
            {
                var required = EvaluateXpRequirement(skillType, level, baseReq, growthPerLevel);
                if (xp < required) break;
                xp -= required;
                level++;
                leveledUp = true;
                onLevelUp?.Invoke(level);
            }

            if (level >= MaxSkillLevel)
            {
                level = MaxSkillLevel;
                xp = 0f;
            }

            LogXpDebug(skillType,
                $"Applied +{amount:0.###} XP | level {startingLevel}->{level} | currentXP {startingXp:0.###}->{xp:0.###} | leveled={leveledUp}");
            }

            public void SetStamina(float value)
            {
            baseMaxStamina = Mathf.Max(1f, value);
            stamina = baseMaxStamina;
            StaminaChanged?.Invoke(stamina, MaxStamina);
            }

            public void DrainStamina(float amount)
            {
            if (amount <= 0f) return;
            stamina = Mathf.Max(0f, stamina - amount);
            StaminaChanged?.Invoke(stamina, MaxStamina);
            }

            public void RegenStamina(float amount)
            {
            if (amount <= 0f) return;
            var max = MaxStamina;
            stamina = Mathf.Min(max, stamina + amount);
            StaminaChanged?.Invoke(stamina, max);
            }
            }
            }