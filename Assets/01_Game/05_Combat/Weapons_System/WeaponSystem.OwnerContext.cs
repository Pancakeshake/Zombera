#region

using UnityEngine;
using Zombera.Characters;
using Zombera.Inventory;
using Zombera.Systems;

#endregion

namespace Zombera.Combat
{
    public sealed partial class WeaponSystem
    {
        private readonly struct OwnerContextSnapshot
        {
            public readonly Unit OwnerUnit;
            public readonly UnitStats OwnerStats;
            public readonly UnitInventory OwnerInventory;

            public OwnerContextSnapshot(Unit ownerUnit, UnitStats ownerStats, UnitInventory ownerInventory)
            {
                OwnerUnit = ownerUnit;
                OwnerStats = ownerStats;
                OwnerInventory = ownerInventory;
            }
        }

        private void EnsureOwnerContextResolved()
        {
            ResolveOwnerReferences();
        }

        private OwnerContextSnapshot CaptureOwnerContextSnapshot()
        {
            EnsureOwnerContextResolved();
            return new OwnerContextSnapshot(OwnerUnit, OwnerStats, _ownerInventory);
        }

        private float ResolveRangedScaledDamage(float baseDamage)
        {
            EnsureOwnerContextResolved();
            return OwnerStats == null
                ? Mathf.Max(0f, baseDamage)
                : OwnerStats.ApplyShootingDamageScaling(baseDamage);
        }

        private float ResolveMeleeScaledDamage(float baseDamage)
        {
            EnsureOwnerContextResolved();
            return OwnerStats == null
                ? Mathf.Max(0f, baseDamage)
                : OwnerStats.ApplyMeleeDamageScaling(baseDamage);
        }

        private bool ShouldAwardWeightedStrengthXp()
        {
            var context = CaptureOwnerContextSnapshot();
            return context.OwnerStats != null
                   && context.OwnerInventory != null
                   && context.OwnerStats.IsHeavyCarry(context.OwnerInventory.CarryRatio);
        }

        private void TryAwardWeightedCombatStrengthXp(bool armedAttack)
        {
            var context = CaptureOwnerContextSnapshot();
            if (context.OwnerStats == null || !ShouldAwardWeightedStrengthXp()) return;

            context.OwnerStats.RecordWeightedCombatHit(armedAttack);
        }

        private GameObject ResolveDamageSourceObject()
        {
            var context = CaptureOwnerContextSnapshot();
            return context.OwnerUnit != null ? context.OwnerUnit.gameObject : gameObject;
        }

        private bool IsFacingTarget(Vector3 toTarget)
        {
            if (toTarget.sqrMagnitude <= 0.0001f) return true;

            var forward = transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            var direction = toTarget.normalized;
            var requiredDot = Mathf.Cos(Mathf.Clamp(closeRangeFacingAngleDegrees, 0f, 180f) * Mathf.Deg2Rad);
            return Vector3.Dot(forward, direction) >= requiredDot;
        }

        private float ResolveMeleeRange()
        {
            var configuredRange = equippedWeapon?.effectiveRange ?? 0f;
            return configuredRange <= 0f ? meleeRange : Mathf.Min(meleeRange, configuredRange);
        }

        private void ResolveOwnerReferences()
        {
            if (OwnerUnit == null)
            {
                OwnerUnit = GetComponent<Unit>();
                if (OwnerUnit == null) OwnerUnit = GetComponentInParent<Unit>();
                }

            if (OwnerStats == null)
            {
                OwnerStats = OwnerUnit != null ? OwnerUnit.Stats : null;
                if (OwnerStats == null)
                {
                    OwnerStats = GetComponent<UnitStats>();
                    if (OwnerStats == null) OwnerStats = GetComponentInParent<UnitStats>();
                }
            }

            if (_ownerInventory == null)
            {
                _ownerInventory = OwnerUnit != null ? OwnerUnit.Inventory : null;
                if (_ownerInventory == null)
                {
                    _ownerInventory = GetComponent<UnitInventory>();
                    if (_ownerInventory == null) _ownerInventory = GetComponentInParent<UnitInventory>();
                    }
                    }
                    }

        /// <summary>Effective ranged range scaled by the owner's Shooting skill.</summary>
        private float GetEffectiveRangedRange()
        {
            var baseRange = equippedWeapon != null ? equippedWeapon.effectiveRange : 20f;
            return OwnerStats == null
                ? baseRange
                : baseRange * OwnerStats.GetShootingEffectiveRangeMultiplier();
        }

        public static bool IsRangedCategory(WeaponCategory category)
        {
            return category is WeaponCategory.Pistol
                or WeaponCategory.Rifle
                or WeaponCategory.Shotgun
                or WeaponCategory.Bow;
        }
    }
}
