#region

using System;
using System.Collections;
using UnityEngine;
using Zombera.Combat;
using Zombera.Core;
using Zombera.Inventory;

#endregion

namespace Zombera.Characters
{
    /// <summary>
    ///     Handles damage intake, healing, and death lifecycle for any unit.
    /// </summary>
    public sealed class UnitHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool initializeOnAwake = true;

        [Header("Armor & Resistance")]
        [Tooltip("Resistance per damage type, as a 0–1 damage-reduction fraction." +
                 " Index matches DamageType enum: 0=Generic 1=Melee 2=Ranged 3=Explosion 4=Fire.")]
        [SerializeField]
        private float[] damageResistances = new float[5];

        private readonly float[] runtimeDamageResistanceBonuses = new float[5];

        private bool _isInvulnerable;
        private GameObject _lastDamageSource;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }

        private UnitStats _cachedStats;
        private Unit _cachedUnit;
        private IDamageNotifiable _cachedDamageNotifiable;
        private UnitLootDropper _cachedLootDropper;

        private void Awake()
        {
            if (initializeOnAwake) ResetHealthToMax();

            // Ensure resistance array matches DamageType enum size.
            var resistanceCount = System.Enum.GetValues(typeof(DamageType)).Length;
            if (damageResistances == null || damageResistances.Length != resistanceCount)
            {
                var newResistances = new float[resistanceCount];
                if (damageResistances != null)
                {
                    System.Array.Copy(damageResistances, newResistances, Mathf.Min(damageResistances.Length, resistanceCount));
                }
                damageResistances = newResistances;
            }

            _cachedUnit = GetComponent<Unit>();
            _cachedStats = GetComponent<UnitStats>();
            if (_cachedStats == null && _cachedUnit != null) _cachedStats = _cachedUnit.Stats;

            _cachedDamageNotifiable = GetComponent<IDamageNotifiable>();
            _cachedLootDropper = GetComponent<UnitLootDropper>();
        }

        public bool IsDead { get; private set; }

        public void TakeDamage(float amount, GameObject source = null)
        {
            if (IsDead || _isInvulnerable || amount <= 0f) return;

            // Apply Toughness damage reduction if stats are present.
            var actualAmount = _cachedStats != null ? _cachedStats.ApplyToughnessDamageReduction(amount) : amount;
            if (actualAmount <= 0f) return;

            _lastDamageSource = source;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - actualAmount);
            Damaged?.Invoke(actualAmount);
            DamagedWithSource?.Invoke(actualAmount, source);

            // Award Toughness + Constitution XP for surviving damage.
            _cachedStats?.RecordDamageTaken(actualAmount);

            CoreEventBus.PublishGlobal(new UnitDamagedEvent
            {
                UnitId = _cachedUnit != null ? _cachedUnit.UnitId : gameObject.name,
                Role = _cachedUnit != null ? _cachedUnit.Role : UnitRole.Enemy,
                Amount = amount,
                CurrentHealth = CurrentHealth,
                MaxHealth = maxHealth,
                Position = transform.position,
                UnitObject = gameObject,
                DamageSource = source
            });

            if (CurrentHealth <= 0f) Kill();

            // Notify any AI controller on this unit so it can react to the damage source.
            _cachedDamageNotifiable?.OnDamagedBy(source);
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            Healed?.Invoke(amount);
        }

        public void Die()
        {
            if (IsDead) return;

            IsDead = true;
            CurrentHealth = 0f;
            Died?.Invoke();

            CoreEventBus.PublishGlobal(new UnitDeathEvent
            {
                UnitId = _cachedUnit != null ? _cachedUnit.UnitId : gameObject.name,
                Role = _cachedUnit != null ? _cachedUnit.Role : UnitRole.Enemy,
                Position = transform.position,
                UnitObject = gameObject,
                DamageSource = _lastDamageSource
            });

            // Notify any loot dropper on this unit.
            _cachedLootDropper?.OnUnitDied();
        }

        /// <summary>
        ///     Heals this unit, scaling the amount by the healer's Medical skill and awarding Medical XP to the healer.
        /// </summary>
        public void Heal(float amount, UnitStats healer)
        {
            if (IsDead || amount <= 0f) return;

            var scaledAmount = healer != null ? amount * healer.GetMedicalHealMultiplier() : amount;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + scaledAmount);
            Healed?.Invoke(scaledAmount);
            healer?.RecordHealApplied(scaledAmount);
        }

        public event Action<float> Damaged;

        /// <summary>Fired together with <see cref="Damaged"/>; includes the damage instigator when known.</summary>
        public event Action<float, GameObject> DamagedWithSource;

        public event Action Died;
        public event Action<float> Healed;

        /// <summary>
        ///     Returns the damage multiplier after resistance for a given damage type.
        ///     A resistance of 0 = full damage; 1 = immune.
        /// </summary>
        public float GetDamageMultiplier(DamageType damageType)
        {
            var idx = (int)damageType;
            if (idx < 0 || idx >= damageResistances.Length) return 1f;

            var baseResistance = damageResistances[idx];
            var runtimeResistance = idx < runtimeDamageResistanceBonuses.Length
                ? runtimeDamageResistanceBonuses[idx]
                : 0f;

            return 1f - Mathf.Clamp01(baseResistance + runtimeResistance);
        }

        public void AddDamageResistance(DamageType damageType, float amount)
        {
            if (Mathf.Abs(amount) < 0.0001f) return;

            var idx = (int)damageType;
            if (idx < 0 || idx >= runtimeDamageResistanceBonuses.Length) return;

            runtimeDamageResistanceBonuses[idx] += amount;
        }

        public void RemoveDamageResistance(DamageType damageType, float amount)
        {
            if (Mathf.Abs(amount) < 0.0001f) return;

            var idx = (int)damageType;
            if (idx < 0 || idx >= runtimeDamageResistanceBonuses.Length) return;

            runtimeDamageResistanceBonuses[idx] -= amount;
        }

        public void ResetHealthToMax()
        {
            CurrentHealth = maxHealth;
            IsDead = false;
        }

        /// <summary>Directly restores health to an exact value (e.g., from a save file). Clamps to [0, maxHealth].</summary>
        public void SetHealth(float value)
        {
            CurrentHealth = Mathf.Clamp(value, 0f, maxHealth);
            IsDead = CurrentHealth <= 0f;
        }

        public void SetMaxHealth(float value, bool refillCurrentHealth)
        {
            maxHealth = Mathf.Max(1f, value);

            if (refillCurrentHealth || CurrentHealth > maxHealth) CurrentHealth = maxHealth;

            if (CurrentHealth > 0f) IsDead = false;
        }

        // ReSharper disable once UnusedMember.Global
        public void ApplyDamage(float amount, GameObject source = null)
        {
            TakeDamage(amount, source);
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public void Kill()
        {
            Die();
        }

        // ReSharper disable once UnusedMember.Global
        public void Revive(float healthFraction = 0.5f)
        {
            if (!IsDead) return;

            IsDead = false;
            CurrentHealth = Mathf.Clamp01(healthFraction) * maxHealth;

            // Grant a brief invulnerability window so the unit
            // isn't instantly killed again by residual damage sources.
            StartCoroutine(InvulnerabilityWindow(1.5f));
        }

        private IEnumerator InvulnerabilityWindow(float seconds)
        {
            IsDead = true;
            yield return new WaitForSeconds(seconds);
            if (CurrentHealth > 0f) IsDead = false;
        }
    }
}