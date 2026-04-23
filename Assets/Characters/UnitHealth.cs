using System;
using UnityEngine;
using Zombera.AI;
using Zombera.Combat;
using Zombera.Core;
using Zombera.Inventory;

namespace Zombera.Characters
{
    /// <summary>
    /// Handles damage intake, healing, and death lifecycle for any unit.
    /// </summary>
    public sealed class UnitHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool initializeOnAwake = true;

        [Header("Armor & Resistance")]
        [Tooltip("Resistance per damage type, as a 0–1 damage-reduction fraction." +
                 " Index matches DamageType enum: 0=Generic 1=Melee 2=Ranged 3=Explosion 4=Fire.")]
        [SerializeField] private float[] damageResistances = new float[5];

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }

        public event Action<float> Damaged;
        public event Action Died;
        public event Action<float> Healed;

        private GameObject lastDamageSource;

        /// <summary>Returns the damage multiplier after resistance for a given damage type.
        /// A resistance of 0 = full damage; 1 = immune.</summary>
        public float GetDamageMultiplier(DamageType damageType)
        {
            int idx = (int)damageType;
            if (idx < 0 || idx >= damageResistances.Length) return 1f;
            return 1f - Mathf.Clamp01(damageResistances[idx]);
        }

        private void Awake()
        {
            if (initializeOnAwake)
            {
                ResetHealthToMax();
            }
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

            if (refillCurrentHealth || CurrentHealth > maxHealth)
            {
                CurrentHealth = maxHealth;
            }

            if (CurrentHealth > 0f)
            {
                IsDead = false;
            }
        }

        public void TakeDamage(float amount, GameObject source = null)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            // Apply Toughness damage reduction if stats are present.
            UnitStats stats = GetComponent<UnitStats>();
            if (stats == null)
            {
                Unit unitRef = GetComponent<Unit>();
                if (unitRef != null)
                {
                    stats = unitRef.Stats;
                }
            }
            float actualAmount = stats != null ? stats.ApplyToughnessDamageReduction(amount) : amount;
            if (actualAmount <= 0f) return;

            lastDamageSource = source;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - actualAmount);
            Damaged?.Invoke(actualAmount);

            // Award Toughness + Constitution XP for surviving damage.
            stats?.RecordDamageTaken(actualAmount);

            Unit unit = GetComponent<Unit>();
            EventSystem.PublishGlobal(new UnitDamagedEvent
            {
                UnitId = unit != null ? unit.UnitId : gameObject.name,
                Role = unit != null ? unit.Role : UnitRole.Enemy,
                Amount = amount,
                CurrentHealth = CurrentHealth,
                MaxHealth = maxHealth,
                Position = transform.position,
                UnitObject = gameObject,
                DamageSource = source
            });

            if (CurrentHealth <= 0f)
            {
                Kill();
            }

            // Notify any AI controller on this unit so it can react to the damage source.
            GetComponent<ZombieAI>()?.OnDamagedBy(source);
        }

        public void ApplyDamage(float amount, GameObject source = null)
        {
            TakeDamage(amount, source);
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            Healed?.Invoke(amount);
        }

        /// <summary>
        /// Heals this unit, scaling the amount by the healer's Medical skill and awarding Medical XP to the healer.
        /// </summary>
        public void Heal(float amount, UnitStats healer)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            float scaledAmount = healer != null ? amount * healer.GetMedicalHealMultiplier() : amount;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + scaledAmount);
            Healed?.Invoke(scaledAmount);
            healer?.RecordHealApplied(scaledAmount);
        }

        public void Die()
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;
            CurrentHealth = 0f;
            Died?.Invoke();

            Unit unit = GetComponent<Unit>();

            EventSystem.PublishGlobal(new UnitDeathEvent
            {
                UnitId = unit != null ? unit.UnitId : gameObject.name,
                Role = unit != null ? unit.Role : UnitRole.Enemy,
                Position = transform.position,
                UnitObject = gameObject,
                DamageSource = lastDamageSource
            });

            // Notify any loot dropper on this unit.
            GetComponent<UnitLootDropper>()?.OnUnitDied();
        }

        public void Kill()
        {
            Die();
        }

        public void Revive(float healthFraction = 0.5f)
        {
            if (!IsDead)
            {
                return;
            }

            IsDead = false;
            CurrentHealth = Mathf.Clamp01(healthFraction) * maxHealth;

            // Grant a brief invulnerability window so the unit
            // isn't instantly killed again by residual damage sources.
            StartCoroutine(InvulnerabilityWindow(1.5f));
        }

        private System.Collections.IEnumerator InvulnerabilityWindow(float seconds)
        {
            IsDead = true;
            yield return new WaitForSeconds(seconds);
            if (CurrentHealth > 0f)
            {
                IsDead = false;
            }
        }
    }
}