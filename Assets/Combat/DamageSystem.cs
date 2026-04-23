using UnityEngine;
using Zombera.Characters;
using Zombera.Core;

namespace Zombera.Combat
{
    /// <summary>
    /// Centralized damage application and mitigation hook point.
    /// Supports per-damage-type armor/resistance via UnitHealth.GetDamageMultiplier.
    /// </summary>
    public static class DamageSystem
    {
        private static bool EnableLiveXpDebug = true;

        public static void ApplyDamage(IDamageable target, float amount, GameObject source = null)
        {
            ApplyDamage(target, amount, DamageType.Generic, source);
        }

        public static void ApplyDamage(IDamageable target, float amount, DamageType damageType, GameObject source = null)
        {
            if (target == null || target.IsDead || amount <= 0f)
            {
                LogXpDebug($"Skip ApplyDamage target={DescribeDamageable(target)} dead={(target != null && target.IsDead)} amount={amount:0.###} type={damageType} source={DescribeSource(source)}");
                return;
            }

            float mitigated = amount;

            // Apply resistance if the target is a UnitHealth with resistance data.
            if (target is UnitHealth unitHealth)
            {
                mitigated *= unitHealth.GetDamageMultiplier(damageType);
            }

            if (mitigated <= 0f)
            {
                LogXpDebug($"Skip ApplyDamage mitigated<=0 target={DescribeDamageable(target)} raw={amount:0.###} mitigated={mitigated:0.###} type={damageType} source={DescribeSource(source)}");
                return;
            }

            target.TakeDamage(mitigated, source);
            LogXpDebug($"ApplyDamage target={DescribeDamageable(target)} raw={amount:0.###} mitigated={mitigated:0.###} type={damageType} source={DescribeSource(source)}");
            AwardAttackerSkillProgression(source, damageType);
        }

        public static void ApplyDamage(UnitHealth target, float amount, GameObject source = null)
        {
            ApplyDamage((IDamageable)target, amount, DamageType.Generic, source);
        }

        public static void ApplyDamage(UnitHealth target, float amount, DamageType damageType, GameObject source = null)
        {
            ApplyDamage((IDamageable)target, amount, damageType, source);
        }

        private static void AwardAttackerSkillProgression(GameObject source, DamageType damageType)
        {
            if (source == null)
            {
                LogXpDebug($"Skip AwardAttackerSkillProgression source=null type={damageType}");
                return;
            }

            UnitStats sourceStats = ResolveSourceStats(source);
            if (sourceStats == null)
            {
                LogXpDebug($"Skip AwardAttackerSkillProgression sourceStats unresolved source={DescribeSource(source)} type={damageType}");
                return;
            }

            if (damageType == DamageType.Melee)
            {
                LogXpDebug($"Award melee XP via RecordMeleeHit source={DescribeSource(source)}");
                sourceStats.RecordMeleeHit();
                return;
            }

            LogXpDebug($"No melee XP award for damageType={damageType} source={DescribeSource(source)}");
        }

        private static UnitStats ResolveSourceStats(GameObject source)
        {
            Unit unit = source.GetComponent<Unit>();
            if (unit == null)
            {
                unit = source.GetComponentInParent<Unit>();
            }

            if (unit == null)
            {
                unit = source.GetComponentInChildren<Unit>();
            }

            if (unit != null && unit.Stats != null)
            {
                return unit.Stats;
            }

            WeaponSystem weaponSystem = source.GetComponent<WeaponSystem>();
            if (weaponSystem == null)
            {
                weaponSystem = source.GetComponentInParent<WeaponSystem>();
            }

            if (weaponSystem == null)
            {
                weaponSystem = source.GetComponentInChildren<WeaponSystem>();
            }

            if (weaponSystem != null)
            {
                if (weaponSystem.OwnerUnit != null && weaponSystem.OwnerUnit.Stats != null)
                {
                    return weaponSystem.OwnerUnit.Stats;
                }

                if (weaponSystem.OwnerStats != null)
                {
                    return weaponSystem.OwnerStats;
                }
            }

            UnitStats stats = source.GetComponent<UnitStats>();
            if (stats == null)
            {
                stats = source.GetComponentInParent<UnitStats>();
            }

            if (stats == null)
            {
                stats = source.GetComponentInChildren<UnitStats>();
            }

            return stats;
        }

        private static void LogXpDebug(string message)
        {
            if (!EnableLiveXpDebug)
            {
                return;
            }

            Debug.Log($"[XP DEBUG][DamageSystem] {message}");
        }

        private static string DescribeDamageable(IDamageable target)
        {
            if (target == null)
            {
                return "null";
            }

            if (target is Component component)
            {
                return component.gameObject.name;
            }

            return target.GetType().Name;
        }

        private static string DescribeSource(GameObject source)
        {
            return source != null ? source.name : "null";
        }
    }
}