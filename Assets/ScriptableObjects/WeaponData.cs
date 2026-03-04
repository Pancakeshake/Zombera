using UnityEngine;
using Zombera.Combat;

namespace Zombera.Data
{
    /// <summary>
    /// Weapon tuning values consumed by WeaponSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Weapon Data", fileName = "WeaponData")]
    public sealed class WeaponData : ScriptableObject
    {
        public string weaponId;
        public string displayName;
        public WeaponCategory weaponCategory = WeaponCategory.Pistol;
        public float baseDamage = 10f;
        public float effectiveRange = 20f;
        public float fireRate = 3f;
        public int magazineSize = 10;

        // TODO: Add recoil, spread, reload time, and animation profile references.
    }
}