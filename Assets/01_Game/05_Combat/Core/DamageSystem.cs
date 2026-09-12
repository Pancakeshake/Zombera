#region

using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.Combat
{
    /// <summary>
    ///     Centralized damage application and mitigation hook point.
    ///     Supports per-damage-type armor/resistance via UnitHealth.GetDamageMultiplier.
    /// </summary>
    public static class DamageSystem
    {
        public static void ApplyDamage(IDamageable target, float amount, GameObject source = null)
        {
            ApplyDamage(target, amount, DamageType.Generic, source);
        }

        public static void ApplyDamage(IDamageable target, float amount, DamageType damageType,
            GameObject source = null)
        {
            if (target == null || target.IsDead || amount <= 0f)
            {
                LogXpDebug(
                    $"Skip ApplyDamage target={DescribeDamageable(target)} dead={target is { IsDead: true }} amount={amount:0.###} type={damageType} source={DescribeSource(source)}");
                return;
            }

            var sourceStats = ResolveSourceStats(source);
            ApplyDamageInternal(target, amount, damageType, sourceStats, source);
        }

        public static void ApplyDamage(IDamageable target, float amount, UnitStats sourceStats, DamageType damageType = DamageType.Generic, GameObject sourceObject = null)
        {
            if (target == null || target.IsDead || amount <= 0f) return;
            ApplyDamageInternal(target, amount, damageType, sourceStats, sourceObject);
        }

        private static void ApplyDamageInternal(IDamageable target, float amount, DamageType damageType, UnitStats sourceStats, GameObject sourceObject)
        {
            var mitigated = amount;

            // Apply resistance if the target is a UnitHealth with resistance data.
            if (target is UnitHealth unitHealth) mitigated *= unitHealth.GetDamageMultiplier(damageType);

            if (mitigated <= 0f)
            {
                LogXpDebug(
                    $"Skip ApplyDamage mitigated<=0 target={DescribeDamageable(target)} raw={amount:0.###} mitigated={mitigated:0.###} type={damageType} source={DescribeSource(sourceObject)}");
                return;
            }

            target.TakeDamage(mitigated, sourceObject);
            LogXpDebug(
                $"ApplyDamage target={DescribeDamageable(target)} raw={amount:0.###} mitigated={mitigated:0.###} type={damageType} source={DescribeSource(sourceObject)}");
            
            if (sourceStats != null)
                AwardAttackerSkillProgressionInternal(sourceStats, damageType);
        }

        // ReSharper disable once UnusedMember.Global
        public static void ApplyDamage(UnitHealth target, float amount, GameObject source = null)
        {
            ApplyDamage((IDamageable)target, amount, DamageType.Generic, source);
        }

        public static void ApplyDamage(UnitHealth target, float amount, DamageType damageType, GameObject source = null)
        {
            ApplyDamage((IDamageable)target, amount, damageType, source);
        }

        private static void AwardAttackerSkillProgressionInternal(UnitStats sourceStats, DamageType damageType)
        {
            if (damageType == DamageType.Melee)
            {
                sourceStats.RecordMeleeHit();
            }
            else if (damageType == DamageType.Ranged)
            {
                sourceStats.RecordRangedHit();
            }
        }

        private static UnitStats ResolveSourceStats(GameObject source)
        {
            var unit = source.GetComponent<Unit>();
            if (unit == null) unit = source.GetComponentInParent<Unit>();

            if (unit == null) unit = source.GetComponentInChildren<Unit>();

            if (unit != null && unit.Stats != null) return unit.Stats;

            var weaponSystem = source.GetComponent<WeaponSystem>();
            if (weaponSystem == null) weaponSystem = source.GetComponentInParent<WeaponSystem>();

            if (weaponSystem == null) weaponSystem = source.GetComponentInChildren<WeaponSystem>();

            if (weaponSystem != null)
            {
                if (weaponSystem.OwnerUnit != null && weaponSystem.OwnerUnit.Stats != null)
                    return weaponSystem.OwnerUnit.Stats;

                if (weaponSystem.OwnerStats != null) return weaponSystem.OwnerStats;
            }

            var stats = source.GetComponent<UnitStats>();
            if (stats == null) stats = source.GetComponentInParent<UnitStats>();

            if (stats == null) stats = source.GetComponentInChildren<UnitStats>();

            return stats;
        }

        [System.Diagnostics.Conditional("DAMAGE_XP_DEBUG")]
        private static void LogXpDebug(string message)
        {
            Debug.Log($"[XP DEBUG][DamageSystem] {message}");
        }

        private static string DescribeDamageable(IDamageable target)
        {
            return target switch
            {
                null => "null",
                Component component => component.gameObject.name,
                _ => target.GetType().Name
            };
        }

        private static string DescribeSource(GameObject source)
        {
            return source != null ? source.name : "null";
        }
    }
}