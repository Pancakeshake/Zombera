using UnityEngine;

namespace Zombera.Characters
{
    public sealed partial class UnitStats
    {
        public void RecordWeightTrainingRep(float intensity = 1f)
        {
            var clampedIntensity = Mathf.Max(0.1f, intensity);
            AddStrengthExperience(GetStrengthWeightTrainingXpRate() * clampedIntensity);
        }

        public void RecordHeavyCarryWalkDistance(float distanceMeters, float carryRatio01)
        {
            if (distanceMeters <= 0f) return;
            var normalizedCarryRatio = Mathf.Clamp01(carryRatio01);
            if (normalizedCarryRatio < heavyCarryThreshold01) return;
            var overload = Mathf.InverseLerp(heavyCarryThreshold01, 1f, normalizedCarryRatio);
            var xpMultiplier = Mathf.Lerp(0.5f, 1.5f, overload);
            AddStrengthExperience(distanceMeters * GetStrengthHeavyCarryXpRate() * xpMultiplier);
        }

        public void RecordWeightedCombatHit(bool armed)
        {
            var xp = GetStrengthCombatHitXpRate();
            if (!armed) xp *= weightedUnarmedXpMultiplier;
            LogXpDebug(UnitSkillType.Strength, $"RecordWeightedCombatHit armed={armed} xp={xp:0.###}");
            AddStrengthExperience(xp);
        }

        public void RecordDamageTaken(float damageAmount)
        {
            if (damageAmount <= 0f) return;
            var toughnessRate = GetToughnessXpRate();
            var constitutionRate = GetConstitutionDamageXpRate();
            LogXpDebug(UnitSkillType.Toughness,
                $"RecordDamageTaken damage={damageAmount:0.###} toughnessRate={toughnessRate:0.###} constitutionRate={constitutionRate:0.###}");
            AddToughnessExperience(damageAmount * toughnessRate);
            AddConstitutionExperience(damageAmount * constitutionRate);
        }

        public void RecordMealConsumed(float quality = 1f)
        {
            AddConstitutionExperience(GetConstitutionMealXpRate() * Mathf.Max(0.1f, quality));
        }

        public void RecordVitaminConsumed()
        {
            AddConstitutionExperience(GetConstitutionVitaminXpRate());
        }

        public void RecordSprintDistance(float distanceMeters)
        {
            if (distanceMeters <= 0f) return;
            AddAgilityExperience(distanceMeters * GetAgilitySprintXpRate());
        }

        public void RecordExertionTime(float seconds)
        {
            if (seconds <= 0f) return;
            AddEnduranceExperience(seconds * GetEnduranceExertionXpRate());
        }

        public void RecordContainerSearched()
        {
            AddScavengingExperience(GetScavengingContainerXpRate());
        }

        public void RecordUndetectedTime(float seconds)
        {
            if (seconds <= 0f) return;
            AddStealthExperience(seconds * GetStealthUndetectedXpRate() * GetPostureStealthXpMultiplier());
        }

        public void RecordRangedHit()
        {
            var xpPerHit = GetShootingHitXpRate();
            LogXpDebug(UnitSkillType.Shooting, $"RecordRangedHit xp={xpPerHit:0.###}");
            AddShootingExperience(xpPerHit);
        }

        public void RecordMeleeHit()
        {
            var xpPerHit = GetMeleeHitXpRate();
            LogXpDebug(UnitSkillType.Melee, $"RecordMeleeHit xp={xpPerHit:0.###}");
            AddMeleeExperience(xpPerHit);
        }

        public void RecordHealApplied(float healAmount)
        {
            if (healAmount <= 0f) return;
            AddMedicalExperience(healAmount * GetMedicalHealXpRate());
        }

        public void RecordBuildPiecePlaced()
        {
            AddEngineeringExperience(GetEngineeringPieceXpRate());
        }

        public void RecordCraftingExperience(UnitSkillType skillType, float amount)
        {
            AddExperienceInternal(skillType, amount);
        }
    }
}