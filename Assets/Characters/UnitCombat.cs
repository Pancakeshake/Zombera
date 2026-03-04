using System.Collections.Generic;
using UnityEngine;
using Zombera.Combat;

namespace Zombera.Characters
{
    /// <summary>
    /// Coordinates targeting and attack execution for unit combat behavior.
    /// </summary>
    public sealed class UnitCombat : MonoBehaviour, IAttackable
    {
        [SerializeField] private WeaponSystem weaponSystem;
        [SerializeField] private TargetingSystem targetingSystem;

        private readonly List<IDamageable> damageableTargetBuffer = new List<IDamageable>();

        public IDamageable MarkedTarget { get; private set; }

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

        public bool Attack(IReadOnlyList<IDamageable> visibleTargets)
        {
            IDamageable target = ChooseTarget(visibleTargets);

            if (target == null)
            {
                return false;
            }

            return weaponSystem != null && weaponSystem.TryAttackTarget(target);
        }

        public bool ExecuteAttack(IReadOnlyList<UnitHealth> visibleTargets)
        {
            IReadOnlyList<IDamageable> damageables = BuildDamageableBuffer(visibleTargets);
            return Attack(damageables);
        }

        public IDamageable ChooseTarget(IReadOnlyList<IDamageable> visibleTargets)
        {
            if (targetingSystem == null)
            {
                return null;
            }

            return targetingSystem.ResolveHybridTarget(MarkedTarget, visibleTargets, transform.position);
        }

        public UnitHealth SelectTarget(IReadOnlyList<UnitHealth> visibleTargets)
        {
            IReadOnlyList<IDamageable> damageables = BuildDamageableBuffer(visibleTargets);
            return ChooseTarget(damageables) as UnitHealth;
        }

        public void Reload()
        {
            weaponSystem?.Reload();
        }

        private IReadOnlyList<IDamageable> BuildDamageableBuffer(IReadOnlyList<UnitHealth> visibleTargets)
        {
            damageableTargetBuffer.Clear();

            if (visibleTargets == null)
            {
                return damageableTargetBuffer;
            }

            for (int i = 0; i < visibleTargets.Count; i++)
            {
                UnitHealth health = visibleTargets[i];

                if (health != null)
                {
                    damageableTargetBuffer.Add(health);
                }
            }

            return damageableTargetBuffer;
        }

        // TODO: Add attack cooldown windows and animation sync points.
        // TODO: Add friendly-fire rules and line-of-sight validation.
    }
}