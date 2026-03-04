using UnityEngine;
using Zombera.Characters;

namespace Zombera.Combat
{
    /// <summary>
    /// Centralized damage application and mitigation hook point.
    /// </summary>
    public static class DamageSystem
    {
        public static void ApplyDamage(IDamageable target, float amount, GameObject source = null)
        {
            if (target == null || target.IsDead || amount <= 0f)
            {
                return;
            }

            // TODO: Apply armor/resistance modifiers by damage type.
            // TODO: Emit combat events for UI, analytics, and AI response.
            target.TakeDamage(amount, source);
        }

        public static void ApplyDamage(UnitHealth target, float amount, GameObject source = null)
        {
            ApplyDamage((IDamageable)target, amount, source);
        }
    }
}