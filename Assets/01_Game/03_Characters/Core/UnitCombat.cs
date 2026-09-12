#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Combat;
using Zombera.Factions;

#endregion

namespace Zombera.Characters
{
    /// <summary>
    ///     Coordinates targeting and attack execution for unit combat behavior.
    /// </summary>
    public sealed class UnitCombat : MonoBehaviour, IAttackable
    {
        [SerializeField] private WeaponSystem weaponSystem;
        [SerializeField] private TargetingSystem targetingSystem;
        [SerializeField] [Min(0f)] private float unarmedFallbackDamage = 10f;
        [SerializeField] [Min(0.1f)] private float unarmedFallbackRange = 1.65f;
        [SerializeField] private bool requireFacingForUnarmedFallback = true;
        [SerializeField] [Range(0f, 180f)] private float unarmedFallbackFacingAngleDegrees = 65f;
        [SerializeField] private UnitStats unitStats;
        [SerializeField] private UnitInventory unitInventory;
        [SerializeField] private bool enableFriendlyFireGuard = true;

        [Header("Attack Timing")]
        [Tooltip("Minimum seconds between attacks. Prevents continuous attack spam.")]
        [SerializeField]
        [Min(0f)]
        private float attackCooldownSeconds = 0.5f;

        private readonly List<IDamageable> _damageableTargetBuffer = new();
        private float _lastAttackTime = float.MinValue;

        private Unit _selfUnit;

        public IDamageable MarkedTarget { get; private set; }

        /// <summary>Effective attack cooldown reduced by the owner's Melee attack-speed bonus.</summary>
        public float EffectiveAttackCooldown
        {
            get
            {
                var multiplier = unitStats != null ? unitStats.GetMeleeAttackSpeedMultiplier() : 1f;
                return Mathf.Max(0.1f, attackCooldownSeconds / Mathf.Max(0.01f, multiplier));
            }
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public bool IsAttackOnCooldown => Time.time - _lastAttackTime < EffectiveAttackCooldown;

        private void Awake()
        {
            if (weaponSystem == null) weaponSystem = GetComponent<WeaponSystem>();

            if (targetingSystem == null) targetingSystem = GetComponent<TargetingSystem>();

            if (unitStats == null) unitStats = GetComponent<UnitStats>();

            if (unitInventory == null) unitInventory = GetComponent<UnitInventory>();

            _selfUnit = GetComponent<Unit>();
        }

        public bool Attack(IReadOnlyList<IDamageable> visibleTargets)
        {
            if (IsAttackOnCooldown) return false;

            var target = ChooseTarget(visibleTargets);

            if (target == null) return false;

            // Friendly-fire guard: skip targets on the same faction.
            if (enableFriendlyFireGuard && _selfUnit != null && target is Component targetComp)
            {
                var targetUnit = targetComp.GetComponent<Unit>();
                if (targetUnit != null && !FactionManager.AreUnitsHostile(_selfUnit, targetUnit))
                    return false;
            }

            if (weaponSystem != null)
            {
                var hit = weaponSystem.TryAttackTarget(target);
                if (hit) _lastAttackTime = Time.time;
                return hit;
            }

            var fallbackDamage = Mathf.Max(0f, unarmedFallbackDamage);
            if (fallbackDamage <= 0f) return false;

            if (!CanApplyUnarmedFallbackHit(target))
                // Attack input was accepted, but no hit lands until target is in believable range/facing.
                return true;

            var scaledDamage = unitStats != null
                ? unitStats.ApplyStrengthDamageScaling(fallbackDamage)
                : fallbackDamage;

            var sourceObject = _selfUnit != null ? _selfUnit.gameObject : gameObject;
            DamageSystem.ApplyDamage(target, scaledDamage, DamageType.Melee, sourceObject);
            _lastAttackTime = Time.time;

            if (unitStats != null && unitInventory != null && unitStats.IsHeavyCarry(unitInventory.CarryRatio))
                unitStats.RecordWeightedCombatHit(false);

            return true;
        }

        public IDamageable ChooseTarget(IReadOnlyList<IDamageable> visibleTargets)
        {
            if (targetingSystem != null)
                return targetingSystem.ResolveHybridTarget(MarkedTarget, visibleTargets, transform.position);

            // Fallback: return the marked target if valid, otherwise the nearest candidate.
            if (MarkedTarget is { IsDead: false }) return MarkedTarget;

            if (visibleTargets == null || visibleTargets.Count == 0) return null;

            IDamageable nearest = null;
            var nearestDistSqr = float.MaxValue;

            foreach (var candidate in visibleTargets)
            {
                if (candidate == null || candidate.IsDead || candidate is not Component c) continue;
                var dSqr = (c.transform.position - transform.position).sqrMagnitude;
                if (dSqr >= nearestDistSqr) continue;

                nearestDistSqr = dSqr;
                nearest = candidate;
            }

            return nearest;
        }

        public void Reload()
        {
            weaponSystem?.Reload();
        }

        public bool IsOnCooldown()
        {
            return IsAttackOnCooldown;
        }

        public void SetAttackCooldownSeconds(float cooldownSeconds)
        {
            attackCooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public void SetMarkedTarget(IDamageable target)
        {
            MarkedTarget = target;
        }

        public void SetMarkedTarget(UnitHealth target)
        {
            SetMarkedTarget((IDamageable)target);
        }

        public void ClearMarkedTarget()
        {
            MarkedTarget = null;
        }

        public bool ExecuteAttack(IReadOnlyList<UnitHealth> visibleTargets)
        {
            var damageables = BuildDamageableBuffer(visibleTargets);
            return Attack(damageables);
        }

        public UnitHealth SelectTarget(IReadOnlyList<UnitHealth> visibleTargets)
        {
            var damageables = BuildDamageableBuffer(visibleTargets);
            return ChooseTarget(damageables) as UnitHealth;
        }

        private IReadOnlyList<IDamageable> BuildDamageableBuffer(IReadOnlyList<UnitHealth> visibleTargets)
        {
            _damageableTargetBuffer.Clear();

            if (visibleTargets == null) return _damageableTargetBuffer;

            foreach (var health in visibleTargets)
                if (health != null)
                    _damageableTargetBuffer.Add(health);

            return _damageableTargetBuffer;
        }

        private bool CanApplyUnarmedFallbackHit(IDamageable target)
        {
            if (target is not Component targetComponent) return true;

            var toTarget = targetComponent.transform.position - transform.position;
            toTarget.y = 0f;

            var effectiveRange = Mathf.Max(0.1f, unarmedFallbackRange);
            if (toTarget.sqrMagnitude > effectiveRange * effectiveRange) return false;

            if (!requireFacingForUnarmedFallback) return true;

            if (toTarget.sqrMagnitude <= 0.0001f) return true;

            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            var requiredDot = Mathf.Cos(Mathf.Clamp(unarmedFallbackFacingAngleDegrees, 0f, 180f) * Mathf.Deg2Rad);
            return Vector3.Dot(forward, toTarget.normalized) >= requiredDot;
        }

        /// <summary>
        ///     Fires the named animator trigger on this unit's Animator component.
        ///     Call from attack execution paths to synchronise combat events with animation.
        /// </summary>
        public void NotifyAnimationSyncPoint(string triggerName)
        {
            var animator = GetComponentInChildren<Animator>();

            if (animator != null && !string.IsNullOrEmpty(triggerName)) animator.SetTrigger(triggerName);
        }
    }
}