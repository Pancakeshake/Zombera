#region

using System;
using UnityEngine;

#endregion

namespace Zombera.Characters
{
    public enum AttributeXpRateKey
    {
        StrengthWeightTrainingRep,
        StrengthHeavyCarryPerMeter,
        StrengthWeightedCombatHit,
        ToughnessDamagePerPoint,
        ConstitutionDamagePerPoint,
        ConstitutionMeal,
        ConstitutionVitamin,
        ShootingHit,
        MeleeHit,
        AgilitySprintPerMeter,
        EnduranceExertionPerSecond,
        MedicalHealPerPoint,
        EngineeringPiecePlaced,
        ScavengingContainer,
        StealthUndetectedPerSecond
    }

    [CreateAssetMenu(menuName = "Zombera/Characters/Attribute Configuration", fileName = "AttributeConfiguration")]
    public sealed class AttributeConfiguration : ScriptableObject
    {
        [Serializable]
        public struct SkillProgressionTuning
        {
            public UnitSkillType skillType;
            [Min(1f)] public float baseXpRequirement;
            [Min(0f)] public float growthPerLevel;
            [Tooltip("Optional multiplier curve applied across skill progression (X = normalized level, Y = multiplier).")]
            public AnimationCurve growthMultiplierCurve;
        }

        [Serializable]
        public struct ActivityRateTuning
        {
            public AttributeXpRateKey key;
            [Min(0f)] public float rate;
        }

        [Header("Skill Progression")]
        [SerializeField] private SkillProgressionTuning[] skillProgression =
        {
            new() { skillType = UnitSkillType.Strength, baseXpRequirement = 20f, growthPerLevel = 4f },
            new() { skillType = UnitSkillType.Shooting, baseXpRequirement = 20f, growthPerLevel = 4f },
            new() { skillType = UnitSkillType.Melee, baseXpRequirement = 20f, growthPerLevel = 4f },
            new() { skillType = UnitSkillType.Medical, baseXpRequirement = 25f, growthPerLevel = 5f },
            new() { skillType = UnitSkillType.Engineering, baseXpRequirement = 25f, growthPerLevel = 5f },
            new() { skillType = UnitSkillType.Toughness, baseXpRequirement = 25f, growthPerLevel = 5f },
            new() { skillType = UnitSkillType.Constitution, baseXpRequirement = 30f, growthPerLevel = 6f },
            new() { skillType = UnitSkillType.Agility, baseXpRequirement = 22f, growthPerLevel = 4f },
            new() { skillType = UnitSkillType.Endurance, baseXpRequirement = 22f, growthPerLevel = 4f },
            new() { skillType = UnitSkillType.Scavenging, baseXpRequirement = 18f, growthPerLevel = 3f },
            new() { skillType = UnitSkillType.Stealth, baseXpRequirement = 20f, growthPerLevel = 4f }
        };

        [Header("XP Activity Rates")]
        [SerializeField] private ActivityRateTuning[] activityRates =
        {
            new() { key = AttributeXpRateKey.StrengthWeightTrainingRep, rate = 8f },
            new() { key = AttributeXpRateKey.StrengthHeavyCarryPerMeter, rate = 0.2f },
            new() { key = AttributeXpRateKey.StrengthWeightedCombatHit, rate = 2.5f },
            new() { key = AttributeXpRateKey.ToughnessDamagePerPoint, rate = 0.05f },
            new() { key = AttributeXpRateKey.ConstitutionDamagePerPoint, rate = 0.02f },
            new() { key = AttributeXpRateKey.ConstitutionMeal, rate = 10f },
            new() { key = AttributeXpRateKey.ConstitutionVitamin, rate = 5f },
            new() { key = AttributeXpRateKey.ShootingHit, rate = 3f },
            new() { key = AttributeXpRateKey.MeleeHit, rate = 3f },
            new() { key = AttributeXpRateKey.AgilitySprintPerMeter, rate = 0.1f },
            new() { key = AttributeXpRateKey.EnduranceExertionPerSecond, rate = 0.05f },
            new() { key = AttributeXpRateKey.MedicalHealPerPoint, rate = 0.08f },
            new() { key = AttributeXpRateKey.EngineeringPiecePlaced, rate = 12f },
            new() { key = AttributeXpRateKey.ScavengingContainer, rate = 8f },
            new() { key = AttributeXpRateKey.StealthUndetectedPerSecond, rate = 0.04f }
        };

        public bool TryGetProgression(UnitSkillType skillType, out SkillProgressionTuning tuning)
        {
            if (skillProgression != null)
            {
                for (var i = 0; i < skillProgression.Length; i++)
                {
                    if (skillProgression[i].skillType != skillType) continue;
                    tuning = skillProgression[i];
                    return true;
                }
            }

            tuning = default;
            return false;
        }

        public float EvaluateXpRequirement(
            UnitSkillType skillType,
            int level,
            int minLevel,
            int maxLevel,
            float fallbackBaseRequirement,
            float fallbackGrowthPerLevel)
        {
            var clampedLevel = Mathf.Clamp(level, minLevel, maxLevel);
            if (clampedLevel >= maxLevel) return 0f;

            var baseReq = Mathf.Max(1f, fallbackBaseRequirement);
            var growth = Mathf.Max(0f, fallbackGrowthPerLevel);
            AnimationCurve curve = null;

            if (TryGetProgression(skillType, out var tuning))
            {
                if (tuning.baseXpRequirement > 0f) baseReq = tuning.baseXpRequirement;
                growth = Mathf.Max(0f, tuning.growthPerLevel);
                curve = tuning.growthMultiplierCurve;
            }

            var baseRequiredXp = baseReq + (clampedLevel - minLevel) * growth;
            if (curve == null || curve.length == 0) return Mathf.Max(0f, baseRequiredXp);

            var normalizedLevel = maxLevel > minLevel
                ? (clampedLevel - minLevel) / (float)(maxLevel - minLevel)
                : 0f;
            var multiplier = Mathf.Max(0f, curve.Evaluate(Mathf.Clamp01(normalizedLevel)));
            return Mathf.Max(0f, baseRequiredXp * multiplier);
        }

        public float GetXpRate(AttributeXpRateKey key, float fallbackRate)
        {
            var fallback = Mathf.Max(0f, fallbackRate);
            if (activityRates == null) return fallback;

            for (var i = 0; i < activityRates.Length; i++)
            {
                if (activityRates[i].key != key) continue;
                return Mathf.Max(0f, activityRates[i].rate);
            }

            return fallback;
        }
    }
}
