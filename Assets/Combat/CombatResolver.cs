using UnityEngine;

namespace Zombera.Combat
{
    /// <summary>
    /// Pure combat math helper with no scene dependencies.
    /// </summary>
    public static class CombatResolver
    {
        public const float DefaultMinHitChance01 = 0.10f;
        public const float DefaultMaxHitChance01 = 0.95f;

        public static CombatResult ResolveAttack(
            int accuracy,
            int evasion,
            float baseDamage,
            float hitBias01 = 0f,
            float criticalChance01 = 0.05f,
            float criticalMultiplier = 1.5f)
        {
            return EvaluateAttack(
                accuracy,
                evasion,
                baseDamage,
                Random.value,
                Random.value,
                hitBias01,
                criticalChance01,
                criticalMultiplier,
                DefaultMinHitChance01,
                DefaultMaxHitChance01);
        }

        public static CombatResult EvaluateAttack(
            int accuracy,
            int evasion,
            float baseDamage,
            float hitRoll01,
            float criticalRoll01,
            float hitBias01 = 0f,
            float criticalChance01 = 0.05f,
            float criticalMultiplier = 1.5f,
            float minHitChance01 = DefaultMinHitChance01,
            float maxHitChance01 = DefaultMaxHitChance01)
        {
            float clampedDamage = Mathf.Max(0f, baseDamage);
            float chance = 0.50f + (accuracy - evasion) * 0.01f + hitBias01;
            chance = Mathf.Clamp(chance, Mathf.Clamp01(minHitChance01), Mathf.Clamp01(maxHitChance01));

            float safeHitRoll = Mathf.Clamp01(hitRoll01);
            float safeCriticalRoll = Mathf.Clamp01(criticalRoll01);

            bool didHit = safeHitRoll <= chance;
            bool isCritical = didHit && safeCriticalRoll <= Mathf.Clamp01(criticalChance01);

            float damage = 0f;
            if (didHit)
            {
                float multiplier = isCritical ? Mathf.Max(1f, criticalMultiplier) : 1f;
                damage = clampedDamage * multiplier;
            }

            return new CombatResult(didHit, isCritical, damage, chance, safeHitRoll, safeCriticalRoll);
        }
    }
}