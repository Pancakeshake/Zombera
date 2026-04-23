using System;
using UnityEngine;
using UnityEngine.Events;

namespace Zombera.BuildingSystem
{
    /// <summary>
    /// Minimal destructible health for base structures.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StructureHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool initializeOnAwake = true;
        [SerializeField] private bool destroyGameObjectOnDeath = true;
        [SerializeField] private UnityEvent onDestroyed;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDestroyed { get; private set; }

        public event Action<float, GameObject> Damaged;
        public event Action Destroyed;

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
            IsDestroyed = false;
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
                IsDestroyed = false;
            }
        }

        public void SetDestroyGameObjectOnDeath(bool shouldDestroy)
        {
            destroyGameObjectOnDeath = shouldDestroy;
        }

        public void TakeDamage(float amount, GameObject source = null)
        {
            if (IsDestroyed || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Damaged?.Invoke(amount, source);

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (IsDestroyed || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        }

        public void Die()
        {
            if (IsDestroyed)
            {
                return;
            }

            IsDestroyed = true;
            CurrentHealth = 0f;
            Destroyed?.Invoke();
            onDestroyed?.Invoke();

            if (destroyGameObjectOnDeath)
            {
                Destroy(gameObject);
            }
        }
    }
}
