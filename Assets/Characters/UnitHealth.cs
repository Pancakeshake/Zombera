using System;
using UnityEngine;
using Zombera.Core;

namespace Zombera.Characters
{
    /// <summary>
    /// Handles damage intake, healing, and death lifecycle for any unit.
    /// </summary>
    public sealed class UnitHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool initializeOnAwake = true;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }

        public event Action<float> Damaged;
        public event Action Died;
        public event Action<float> Healed;

        private GameObject lastDamageSource;

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

        public void TakeDamage(float amount, GameObject source = null)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            lastDamageSource = source;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Damaged?.Invoke(amount);

            if (CurrentHealth <= 0f)
            {
                Kill();
            }

            // TODO: Capture damage source and type for combat logs/AI reactions.
            _ = source;
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

            // TODO: Support status effects that modify healing effectiveness.
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

            // TODO: Trigger death animation/ragdoll and loot/drop rules.
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

            // TODO: Apply revival penalties and temporary invulnerability windows.
        }
    }
}