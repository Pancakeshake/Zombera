#region

using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Data;
using Zombera.Systems;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.Combat
{
    public sealed partial class WeaponSystem
    {
        private readonly struct AttackExecutionContext
        {
            public readonly IDamageable Target;
            public readonly WeaponData Weapon;

            public AttackExecutionContext(IDamageable target, WeaponData weapon)
            {
                Target = target;
                Weapon = weapon;
            }

            public bool HasWeapon => Weapon != null;
        }

        private interface IAttackHandler
        {
            bool Execute(WeaponSystem owner, in AttackExecutionContext context);
        }

        private sealed class RangedAttackHandler : IAttackHandler
        {
            public bool Execute(WeaponSystem owner, in AttackExecutionContext context)
            {
                return owner.HandleRangedAttack(context);
            }
        }

        private sealed class MeleeAttackHandler : IAttackHandler
        {
            public bool Execute(WeaponSystem owner, in AttackExecutionContext context)
            {
                return owner.HandleMeleeAttack(context);
            }
        }

        private sealed class ThrowableAttackHandler : IAttackHandler
        {
            public bool Execute(WeaponSystem owner, in AttackExecutionContext context)
            {
                return owner.HandleThrowableAttack(context);
            }
        }

        private sealed class UnarmedAttackHandler : IAttackHandler
        {
            public bool Execute(WeaponSystem owner, in AttackExecutionContext context)
            {
                return owner.HandleUnarmedAttack(context);
            }
        }

        private static readonly IAttackHandler RangedAttackHandlerInstance = new RangedAttackHandler();
        private static readonly IAttackHandler MeleeAttackHandlerInstance = new MeleeAttackHandler();
        private static readonly IAttackHandler ThrowableAttackHandlerInstance = new ThrowableAttackHandler();
        private static readonly IAttackHandler UnarmedAttackHandlerInstance = new UnarmedAttackHandler();

        public bool TryAttackTarget(IDamageable target)
        {
            EnsureOwnerContextResolved();

            if (target == null || target.IsDead) return false;

            var context = new AttackExecutionContext(target, equippedWeapon);
            var handler = !context.HasWeapon
                ? UnarmedAttackHandlerInstance
                : ResolveAttackHandler(context.Weapon.weaponCategory);
            return handler.Execute(this, context);
        }

        private static IAttackHandler ResolveAttackHandler(WeaponCategory category)
        {
            return category switch
            {
                WeaponCategory.Pistol => RangedAttackHandlerInstance,
                WeaponCategory.Rifle => RangedAttackHandlerInstance,
                WeaponCategory.Shotgun => RangedAttackHandlerInstance,
                WeaponCategory.Bow => RangedAttackHandlerInstance,
                WeaponCategory.Melee => MeleeAttackHandlerInstance,
                WeaponCategory.Throwable => ThrowableAttackHandlerInstance,
                _ => UnarmedAttackHandlerInstance
            };
        }

        public bool TryAttackTarget(UnitHealth target)
        {
            return TryAttackTarget((IDamageable)target);
        }

        public void Reload()
        {
            if (equippedWeapon != null && equippedWeapon.magazineSize > 0)
                CurrentAmmo = equippedWeapon.magazineSize;
        }

        private bool HandleRangedAttack(AttackExecutionContext context)
        {
            return FireProjectileAt(context.Target);
        }

        private bool HandleMeleeAttack(AttackExecutionContext context)
        {
            var meleeHit = TryApplyCloseRangeHit(context.Target, context.Weapon.baseDamage, ResolveMeleeRange(), true);
            if (meleeHit)
            {
                CoreEventBus.PublishGlobal(new NoiseEvent
                {
                    Position = transform.position,
                    Radius = meleeRange * 3f,
                    NoiseType = NoiseType.Generic,
                    Source = gameObject
                });
            }

            return meleeHit;
        }

        private bool HandleThrowableAttack(AttackExecutionContext context)
        {
            ThrowAt(context.Target);
            return true;
        }

        private bool HandleUnarmedAttack(AttackExecutionContext context)
        {
            // Unarmed fallback: bare-hands melee at close range.
            return TryApplyCloseRangeHit(context.Target, 10f, unarmedRange, false);
        }

        private bool FireProjectileAt(IDamageable target)
        {
            var weaponData = equippedWeapon;
            if (weaponData == null) return false;

            var isBowShot = weaponData.weaponCategory == WeaponCategory.Bow;
            var bowCanDamageTarget = true;
            var bowProjectileSettings = ResolveBowProjectileSettings();

            // Ranged weapons consume one round per shot; block if empty.
            if (weaponData.magazineSize > 0)
            {
                if (CurrentAmmo <= 0) return false;
                CurrentAmmo--;
            }

            var scaledDamage = ResolveRangedScaledDamage(weaponData.baseDamage);
            var sourceObject = ResolveDamageSourceObject();

            // Emit a noise event so nearby zombies investigate the shot.
            var noiseRadius = ResolveShotNoiseRadius(isBowShot);
            CoreEventBus.PublishGlobal(new NoiseEvent
            {
                Position = transform.position,
                Radius = noiseRadius,
                NoiseType = isBowShot ? NoiseType.Generic : NoiseType.Gunshot,
                Source = sourceObject
            });

            // Range-accuracy check:
            // - Bow: 0..maxRange always allowed with linear distance falloff in hit chance.
            // - Other ranged: retain effective-range over-penalty behavior.
            if (target is Component rangeCheckComponent)
            {
                var dist = Vector3.Distance(transform.position, rangeCheckComponent.transform.position);

                if (isBowShot)
                {
                    bowCanDamageTarget = CanBowDamageTargetAtDistance(dist);
                }
                else
                {
                    var effectiveRange = GetEffectiveRangedRange();
                    if (dist > effectiveRange)
                    {
                        var overRangeFraction = (dist - effectiveRange) / Mathf.Max(1f, effectiveRange);
                        var hitChancePenalty = Mathf.Clamp01(overRangeFraction);
                        var skillBonus = OwnerStats != null ? OwnerStats.GetShootingHitChanceBonus() : 0f;
                        var hitChance = Mathf.Clamp01(1f - hitChancePenalty + skillBonus);
                        if (Random.value > hitChance) return false; // legacy non-bow miss behavior
                    }
                }
            }

            if (isBowShot) TryPlayBowReleaseAudio();

            if (target is not Component targetComponent)
            {
                DamageSystem.ApplyDamage(target, scaledDamage, sourceObject);
                TryAwardWeightedCombatStrengthXp(true);
                OwnerStats?.RecordRangedHit();
                return true;
            }

            ResolveProjectileSpawnPose(out var projectileSpawnPosition, out var projectileSpawnRotation);

            Projectile projectile = null;
            if (isBowShot)
            {
                projectile = CreateBowProjectileInstance(projectileSpawnPosition, projectileSpawnRotation);
            }
            else if (projectilePrefab != null)
            {
                projectile = ProjectilePoolRegistry.SpawnFromProjectilePrefab(
                    projectilePrefab,
                    projectileSpawnPosition,
                    projectileSpawnRotation);
            }

            if (projectile != null)
            {
                if (isBowShot) ConfigureBowProjectileAudio(projectile);

                if (isBowShot)
                {
                    if (bowCanDamageTarget)
                        projectile.InitializeBallistic(
                            targetComponent.transform,
                            scaledDamage,
                            sourceObject,
                            target,
                            OwnerStats,
                            ShouldAwardWeightedStrengthXp(),
                            true,
                            bowProjectileSettings.ArcHeight,
                            bowProjectileSettings.GravityScale,
                            bowProjectileSettings.StickToTargets,
                            bowProjectileSettings.EmbedInEnvironment,
                            bowProjectileSettings.EmbeddedLifetimeSeconds);
                    else
                        projectile.InitializeBallistic(
                            null,
                            0f,
                            sourceObject,
                            null,
                            OwnerStats,
                            false,
                            true,
                            bowProjectileSettings.ArcHeight,
                            bowProjectileSettings.GravityScale,
                            false,
                            bowProjectileSettings.EmbedInEnvironment,
                            bowProjectileSettings.EmbeddedLifetimeSeconds);
                }
                else
                {
                    projectile.Initialize(
                        targetComponent.transform,
                        scaledDamage,
                        sourceObject,
                        target,
                        OwnerStats,
                        ShouldAwardWeightedStrengthXp(),
                        true);
                }

                if (!isBowShot || bowCanDamageTarget) OwnerStats?.RecordRangedHit();

                return true;
            }

            if (isBowShot && !bowCanDamageTarget) return true;

            DamageSystem.ApplyDamage(target, scaledDamage, sourceObject);
            TryAwardWeightedCombatStrengthXp(true);
            OwnerStats?.RecordRangedHit();

            return true;
        }

        private void ThrowAt(IDamageable target)
        {
            if (target == null) return;

            var sourceObject = ResolveDamageSourceObject();

            if (projectilePrefab == null || muzzlePoint == null)
            {
                // No throwable prefab configured: fall back to instant-hit.
                DamageSystem.ApplyDamage(target, equippedWeapon?.baseDamage ?? 5f, sourceObject);
                return;
            }

            if (target is not Component targetComponent)
            {
                DamageSystem.ApplyDamage(target, equippedWeapon?.baseDamage ?? 5f, sourceObject);
                return;
            }

            // Spawn the throwable and initialize with an arc trajectory.
            var thrown = ProjectilePoolRegistry.SpawnFromProjectilePrefab(
                projectilePrefab,
                muzzlePoint.position,
                Quaternion.identity);
            thrown.InitializeArc(targetComponent.transform, equippedWeapon?.baseDamage ?? 5f, sourceObject, target,
                OwnerStats);
        }

        private bool TryApplyCloseRangeHit(IDamageable target, float damage, float maxRange, bool armedAttack)
        {
            var scaledDamage = ResolveMeleeScaledDamage(damage);
            var sourceObject = ResolveDamageSourceObject();

            if (target is not Component targetComponent)
            {
                DamageSystem.ApplyDamage(target, scaledDamage, DamageType.Melee, sourceObject);
                TryAwardWeightedCombatStrengthXp(armedAttack);
                return true;
            }

            var toTarget = targetComponent.transform.position - transform.position;
            toTarget.y = 0f;

            var effectiveRange = Mathf.Max(0.1f, maxRange);
            if (toTarget.sqrMagnitude > effectiveRange * effectiveRange) return true;

            if (requireFacingForCloseRangeHits && !IsFacingTarget(toTarget)) return true;

            DamageSystem.ApplyDamage(target, scaledDamage, DamageType.Melee, sourceObject);
            TryAwardWeightedCombatStrengthXp(armedAttack);
            TryApplyKnockback(target);
            return true;
        }

        private void TryApplyKnockback(IDamageable target)
        {
            if (OwnerStats == null || target is not Component targetComp) return;

            var knockbackChance = OwnerStats.GetMeleeKnockbackChance() + OwnerStats.GetStrengthKnockbackChanceBonus();
            if (Random.value > knockbackChance) return;

            var dir = targetComp.transform.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f) return;

            if (targetComp.TryGetComponent(out UnitController controller))
            {
                controller.ApplyKnockback(dir.normalized * knockbackForce);
            }
            else
            {
                var rb = targetComp.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                    rb.AddForce(dir.normalized * knockbackForce, ForceMode.Impulse);
            }
        }
    }
}
