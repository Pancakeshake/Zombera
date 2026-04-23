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

        [Header("Handling")]
        [Tooltip("World-space recoil impulse applied to the camera/character per shot.")]
        public float recoilForce = 0.3f;
        [Tooltip("Maximum bullet spread half-angle in degrees at effective range.")]
        [Range(0f, 45f)] public float spreadAngle = 2f;
        [Tooltip("Seconds from reload start to chamber-ready.")]
        [Min(0f)] public float reloadTimeSeconds = 1.5f;
        [Tooltip("Animator layer/parameter prefix used to select the matching weapon animation set.")]
        public string animationProfileId;
    }
}