using UnityEngine;

namespace Zombera.Characters
{
    public sealed partial class UnitStats
    {
        public bool IsHeavyCarry(float carryRatio01)
        {
            return Mathf.Clamp01(carryRatio01) >= heavyCarryThreshold01;
        }

        public float ApplyStrengthDamageScaling(float baseDamage)
        {
            return Mathf.Max(0f, baseDamage) * GetStrengthDamageMultiplier();
        }

        public float ApplyShootingDamageScaling(float baseDamage)
        {
            var t = SkillT(shooting);
            return Mathf.Max(0f, baseDamage) * Mathf.Lerp(1f, shootingDamageMultiplierAtLevel100, t);
        }

        public float ApplyMeleeDamageScaling(float baseDamage)
        {
            var t = SkillT(melee);
            return Mathf.Max(0f, baseDamage) * Mathf.Lerp(1f, meleeDamageMultiplierAtLevel100, t);
        }

        public float GetMeleeAttackSpeedMultiplier()
        {
            return 1f + Mathf.Lerp(0f, meleeAttackSpeedBonusAtLevel100, SkillT(melee));
        }

        public float GetMeleeKnockbackChance()
        {
            return Mathf.Lerp(0f, meleeKnockbackChanceAtLevel100, SkillT(melee));
        }

        public float GetStrengthKnockbackChanceBonus()
        {
            return Mathf.Lerp(0f, strengthKnockbackChanceBonusAtLevel100, SkillT(strength));
        }

        public float GetEngineeringBuildSpeedMultiplier()
        {
            return 1f + Mathf.Lerp(0f, engineeringBuildSpeedBonusAtLevel100, SkillT(engineering));
        }

        public float GetToughnessDamageReduction()
        {
            return Mathf.Lerp(0f, toughnessDamageReductionAtLevel100, SkillT(toughness));
        }

        public float ApplyToughnessDamageReduction(float incomingDamage)
        {
            var reduction = GetToughnessDamageReduction();
            return Mathf.Max(0f, incomingDamage * (1f - reduction));
        }

        public float GetAgilityMoveSpeedMultiplier()
        {
            return 1f + Mathf.Lerp(0f, agilityMoveSpeedBonusAtLevel100, SkillT(agility));
        }

        public float GetAgilityDodgeChance()
        {
            return Mathf.Lerp(0f, agilityDodgeBonusAtLevel100, SkillT(agility));
        }

        public float GetEnduranceStaminaMultiplier()
        {
            return 1f + Mathf.Lerp(0f, enduranceStaminaBonusAtLevel100, SkillT(endurance));
        }

        public float GetEnduranceRegenMultiplier()
        {
            return 1f + Mathf.Lerp(0f, enduranceRegenBonusAtLevel100, SkillT(endurance));
        }

        public float GetMedicalHealMultiplier()
        {
            return 1f + Mathf.Lerp(0f, medicalHealBonusAtLevel100, SkillT(medical));
        }

        public float GetScavengingLootMultiplier()
        {
            return 1f + Mathf.Lerp(0f, scavengingLootBonusAtLevel100, SkillT(scavenging));
        }

        public float GetEncumbranceSpeedMultiplier(float carryRatio01)
        {
            var ratio = Mathf.Clamp01(carryRatio01);
            if (ratio < heavyCarryThreshold01) return 1f;
            var overload = Mathf.InverseLerp(heavyCarryThreshold01, 1f, ratio);
            return 1f - Mathf.Lerp(0f, encumbranceSpeedPenaltyMax, overload);
        }

        public float GetStealthDetectionRadiusMultiplier()
        {
            return 1f - Mathf.Lerp(0f, stealthDetectionRadiusReductionAtLevel100, SkillT(stealth));
        }

        public float GetPostureDetectionMultiplier()
        {
            return CurrentPosture switch
            {
                PostureState.Crouching => crouchDetectionMultiplier,
                PostureState.Crawling => crawlDetectionMultiplier,
                _ => 1f
            };
        }

        private float GetPostureSpeedMultiplier()
        {
            var t = SkillT(stealth);
            return CurrentPosture switch
            {
                PostureState.Crouching => Mathf.Lerp(crouchSpeedAtLevel1, crouchSpeedAtLevel100, t),
                PostureState.Crawling => Mathf.Lerp(crawlSpeedAtLevel1, crawlSpeedAtLevel100, t),
                _ => 1f
            };
        }

        public float SetPostureState(PostureState state)
        {
            CurrentPosture = state;
            return GetPostureSpeedMultiplier();
        }

        private float GetPostureStealthXpMultiplier()
        {
            return CurrentPosture switch
            {
                PostureState.Crouching => crouchStealthXpMultiplier,
                PostureState.Crawling => crawlStealthXpMultiplier,
                _ => 1f
            };
        }

        public float GetShootingEffectiveRangeMultiplier()
        {
            return 1f + Mathf.Lerp(0f, shootingRangeBonusAtLevel100, SkillT(shooting));
        }

        public float GetShootingHitChanceBonus()
        {
            return Mathf.Lerp(0f, shootingHitChanceBonusAtLevel100, SkillT(shooting));
        }

        public float GetStrengthDamageMultiplier()
        {
            return Mathf.Lerp(1f, strengthDamageMultiplierAtLevel100, SkillT(strength));
        }
    }
}