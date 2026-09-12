using System;
using UnityEngine;

namespace Zombera.Characters
{
    public sealed partial class UnitStats
    {
        private ref int GetLevelRef(UnitSkillType skillType)
        {
            switch (skillType)
            {
                case UnitSkillType.Strength: return ref strength;
                case UnitSkillType.Shooting: return ref shooting;
                case UnitSkillType.Melee: return ref melee;
                case UnitSkillType.Medical: return ref medical;
                case UnitSkillType.Engineering: return ref engineering;
                case UnitSkillType.Toughness: return ref toughness;
                case UnitSkillType.Constitution: return ref constitution;
                case UnitSkillType.Agility: return ref agility;
                case UnitSkillType.Endurance: return ref endurance;
                case UnitSkillType.Scavenging: return ref scavenging;
                case UnitSkillType.Stealth: return ref stealth;
                default: throw new ArgumentOutOfRangeException(nameof(skillType));
            }
        }

        private ref float GetExperienceRef(UnitSkillType skillType)
        {
            switch (skillType)
            {
                case UnitSkillType.Strength: return ref strengthExperience;
                case UnitSkillType.Shooting: return ref shootingExperience;
                case UnitSkillType.Melee: return ref meleeExperience;
                case UnitSkillType.Medical: return ref medicalExperience;
                case UnitSkillType.Engineering: return ref engineeringExperience;
                case UnitSkillType.Toughness: return ref toughnessExperience;
                case UnitSkillType.Constitution: return ref constitutionExperience;
                case UnitSkillType.Agility: return ref agilityExperience;
                case UnitSkillType.Endurance: return ref enduranceExperience;
                case UnitSkillType.Scavenging: return ref scavengingExperience;
                case UnitSkillType.Stealth: return ref stealthExperience;
                default: throw new ArgumentOutOfRangeException(nameof(skillType));
            }
        }

        private (float baseReq, float growth) GetProgressionConstants(UnitSkillType skillType)
        {
            var fallback = ResolveFallbackProgressionConstants(skillType);
            if (attributeConfiguration == null) return fallback;

            if (!attributeConfiguration.TryGetProgression(skillType, out var tuning)) return fallback;

            var baseReq = tuning.baseXpRequirement > 0f ? tuning.baseXpRequirement : fallback.baseReq;
            var growth = Mathf.Max(0f, tuning.growthPerLevel);
            return (baseReq, growth);
        }

        private (float baseReq, float growth) ResolveFallbackProgressionConstants(UnitSkillType skillType)
        {
            switch (skillType)
            {
                case UnitSkillType.Strength: return (strengthXpBaseRequirement, strengthXpRequirementGrowthPerLevel);
                case UnitSkillType.Shooting: return (shootingXpBaseRequirement, shootingXpRequirementGrowthPerLevel);
                case UnitSkillType.Melee: return (meleeXpBaseRequirement, meleeXpRequirementGrowthPerLevel);
                case UnitSkillType.Medical: return (medicalXpBaseRequirement, medicalXpRequirementGrowthPerLevel);
                case UnitSkillType.Engineering: return (engineeringXpBaseRequirement, engineeringXpRequirementGrowthPerLevel);
                case UnitSkillType.Toughness: return (toughnessXpBaseRequirement, toughnessXpRequirementGrowthPerLevel);
                case UnitSkillType.Constitution: return (constitutionXpBaseRequirement, constitutionXpRequirementGrowthPerLevel);
                case UnitSkillType.Agility: return (agilityXpBaseRequirement, agilityXpRequirementGrowthPerLevel);
                case UnitSkillType.Endurance: return (enduranceXpBaseRequirement, enduranceXpRequirementGrowthPerLevel);
                case UnitSkillType.Scavenging: return (scavengingXpBaseRequirement, scavengingXpRequirementGrowthPerLevel);
                case UnitSkillType.Stealth: return (stealthXpBaseRequirement, stealthXpRequirementGrowthPerLevel);
                default: throw new ArgumentOutOfRangeException(nameof(skillType));
            }
        }

        private static bool IsHealthScalingSkill(UnitSkillType skillType)
        {
            return skillType is UnitSkillType.Strength or UnitSkillType.Toughness or UnitSkillType.Constitution;
        }

        private static float SkillT(int skillValue)
        {
            return (Mathf.Clamp(skillValue, MinSkillLevel, MaxSkillLevel) - MinSkillLevel) /
                   (float)(MaxSkillLevel - MinSkillLevel);
        }

        private bool TryGetProgressionParameters(
            UnitSkillType skillType,
            out int level,
            out float currentXp,
            out float baseReq,
            out float growthPerLevel)
        {
            try
            {
                level = GetLevelRef(skillType);
                currentXp = GetExperienceRef(skillType);
                var (b, g) = GetProgressionConstants(skillType);
                baseReq = b;
                growthPerLevel = g;
                return true;
            }
            catch
            {
                level = MinSkillLevel;
                currentXp = 0f;
                baseReq = 0f;
                growthPerLevel = 0f;
                return false;
            }
        }

        // ── Activity Rate Selection (Phase 4) ────────────────────────────────

        private float ResolveXpActivityRate(AttributeXpRateKey key, float serializedRate, float defaultRate)
        {
            var fallback = serializedRate > 0f ? serializedRate : defaultRate;
            return attributeConfiguration != null
                ? attributeConfiguration.GetXpRate(key, fallback)
                : fallback;
        }

        private float GetStrengthWeightTrainingXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.StrengthWeightTrainingRep,
            strengthXpPerWeightTrainingRep,
            DefaultStrengthXpPerWeightTrainingRep);

        private float GetStrengthHeavyCarryXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.StrengthHeavyCarryPerMeter,
            strengthXpPerMeterHeavyCarry,
            DefaultStrengthXpPerMeterHeavyCarry);

        private float GetStrengthCombatHitXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.StrengthWeightedCombatHit,
            strengthXpPerWeightedCombatHit,
            DefaultStrengthXpPerWeightedCombatHit);

        private float GetToughnessXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.ToughnessDamagePerPoint,
            toughnessXpPerDamagePoint,
            DefaultToughnessXpPerDamagePoint);

        private float GetConstitutionDamageXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.ConstitutionDamagePerPoint,
            constitutionXpPerDamagePoint,
            DefaultConstitutionXpPerDamagePoint);

        private float GetConstitutionMealXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.ConstitutionMeal,
            constitutionXpPerMeal,
            DefaultConstitutionXpPerMeal);

        private float GetConstitutionVitaminXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.ConstitutionVitamin,
            constitutionXpPerVitamin,
            DefaultConstitutionXpPerVitamin);

        private float GetShootingHitXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.ShootingHit,
            shootingXpPerRangedHit,
            DefaultShootingXpPerRangedHit);

        private float GetMeleeHitXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.MeleeHit,
            meleeXpPerHit,
            DefaultMeleeXpPerHit);

        private float GetAgilitySprintXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.AgilitySprintPerMeter,
            agilityXpPerMeterSprinted,
            DefaultAgilityXpPerMeterSprinted);

        private float GetEnduranceExertionXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.EnduranceExertionPerSecond,
            enduranceXpPerSecondExertion,
            DefaultEnduranceXpPerSecondExertion);

        private float GetMedicalHealXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.MedicalHealPerPoint,
            medicalXpPerHealPoint,
            DefaultMedicalXpPerHealPoint);

        private float GetEngineeringPieceXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.EngineeringPiecePlaced,
            engineeringXpPerPiecePlaced,
            DefaultEngineeringXpPerPiecePlaced);

        private float GetScavengingContainerXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.ScavengingContainer,
            scavengingXpPerContainer,
            DefaultScavengingXpPerContainer);

        private float GetStealthUndetectedXpRate() => ResolveXpActivityRate(
            AttributeXpRateKey.StealthUndetectedPerSecond,
            stealthXpPerSecondUndetected,
            DefaultStealthXpPerSecondUndetected);

        // ── XP Debug & Validation (Phase 6) ──────────────────────────────────

        /// <summary>Logs XP changes if debug conditions are met.</summary>
        private void LogXpDebug(UnitSkillType skillType, string message)
        {
            if (!ShouldLogXpDebug(skillType)) return;

            var unit = GetComponent<Unit>();
            var unitName = unit != null ? unit.gameObject.name : gameObject.name;
            var role = unit != null ? unit.Role.ToString() : "UnknownRole";
            Debug.Log($"[XP DEBUG][UnitStats][{unitName}][{role}][{skillType}] {message}", this);
        }

        /// <summary>
        /// Validates if XP logging should occur based on:
        /// 1. Global debug toggle (enableLiveXpDebug)
        /// 2. Skill type filter (liveXpDebugCombatSkillsOnly)
        /// 3. Entity filter (logOnlyPlayerXp)
        /// </summary>
        private bool ShouldLogXpDebug(UnitSkillType skillType)
        {
            if (!enableLiveXpDebug) return false;

            // Guard: Filter out non-combat skills if the combat-only toggle is active.
            if (liveXpDebugCombatSkillsOnly && !IsCombatSkill(skillType)) return false;

            // Guard: If not restricting to player, any entity that passed above checks logs.
            if (!logOnlyPlayerXp) return true;

            // Guard: Verify entity role is Player.
            var unit = GetComponent<Unit>();
            return unit != null && unit.Role == UnitRole.Player;
        }

        private static bool IsCombatSkill(UnitSkillType skillType)
        {
            return skillType switch
            {
                UnitSkillType.Strength => true,
                UnitSkillType.Shooting => true,
                UnitSkillType.Melee => true,
                UnitSkillType.Toughness => true,
                UnitSkillType.Constitution => true,
                _ => false
            };
        }

        private float GetXpRequiredForLevel(UnitSkillType skillType, int level)
        {
            var clampedLevel = Mathf.Clamp(level, MinSkillLevel, MaxSkillLevel);

            if (clampedLevel >= MaxSkillLevel) return 0f;

            var (baseReq, growthPerLevel) = GetProgressionConstants(skillType);
            return EvaluateXpRequirement(skillType, clampedLevel, baseReq, growthPerLevel);
        }

        private float EvaluateXpRequirement(UnitSkillType skillType, int level, float baseReq, float growthPerLevel)
        {
            var clampedLevel = Mathf.Clamp(level, MinSkillLevel, MaxSkillLevel);
            if (clampedLevel >= MaxSkillLevel) return 0f;

            if (attributeConfiguration != null)
                return attributeConfiguration.EvaluateXpRequirement(
                    skillType,
                    clampedLevel,
                    MinSkillLevel,
                    MaxSkillLevel,
                    baseReq,
                    growthPerLevel);

            return Mathf.Max(0f, baseReq + (clampedLevel - MinSkillLevel) * growthPerLevel);
        }

        private void ClampSkillsToBounds()
        {
            for (var i = 0; i <= (int)UnitSkillType.Stealth; i++)
            {
                var skill = (UnitSkillType)i;
                ref var val = ref GetLevelRef(skill);
                val = Mathf.Clamp(val, MinSkillLevel, MaxSkillLevel);
            }
        }

        private void AutoResolveReferences()
        {
            if (unitHealth == null) unitHealth = GetComponent<UnitHealth>();
        }
        }
        }