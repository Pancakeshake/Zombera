using UnityEngine;
using Zombera.Characters;
using Zombera.Data;

namespace Zombera.Combat
{
    /// <summary>
    /// Handles weapon equip/use flow for firearms, melee, and throwables.
    /// </summary>
    public sealed class WeaponSystem : MonoBehaviour
    {
        [SerializeField] private WeaponData equippedWeapon;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Projectile projectilePrefab;

        public WeaponData EquippedWeapon => equippedWeapon;

        public void EquipWeapon(WeaponData weaponData)
        {
            equippedWeapon = weaponData;

            // TODO: Apply weapon animation profile and stat modifiers.
        }

        public bool TryAttackTarget(IDamageable target)
        {
            if (target == null || target.IsDead || equippedWeapon == null)
            {
                return false;
            }

            switch (equippedWeapon.weaponCategory)
            {
                case WeaponCategory.Pistol:
                case WeaponCategory.Rifle:
                case WeaponCategory.Shotgun:
                    FireProjectileAt(target);
                    break;
                case WeaponCategory.Melee:
                    DamageSystem.ApplyDamage(target, equippedWeapon.baseDamage, gameObject);
                    break;
                case WeaponCategory.Throwable:
                    ThrowAt(target);
                    break;
            }

            // TODO: Consume ammo, enforce fire rate, and handle spread/recoil.
            return true;
        }

        public bool TryAttackTarget(UnitHealth target)
        {
            return TryAttackTarget((IDamageable)target);
        }

        public void Reload()
        {
            // TODO: Implement reload timings and ammo source validation.
        }

        private void FireProjectileAt(IDamageable target)
        {
            if (projectilePrefab == null || muzzlePoint == null || !(target is Component targetComponent))
            {
                DamageSystem.ApplyDamage(target, equippedWeapon.baseDamage, gameObject);
                return;
            }

            Projectile projectile = Instantiate(projectilePrefab, muzzlePoint.position, muzzlePoint.rotation);
            projectile.Initialize(targetComponent.transform, equippedWeapon.baseDamage, gameObject, target);
        }

        private void ThrowAt(IDamageable target)
        {
            // TODO: Spawn and arc throwable toward target.
            _ = target;
        }
    }

    public enum WeaponCategory
    {
        Pistol,
        Rifle,
        Shotgun,
        Melee,
        Throwable
    }
}