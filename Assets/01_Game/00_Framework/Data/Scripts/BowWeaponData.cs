#region

using UnityEngine;

#endregion

namespace Zombera.Data
{
    /// <summary>
    ///     Bow-specific tuning consumed by WeaponSystem bow projectiles, hit-chance, and audio.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Bow Weapon Data", fileName = "BowWeaponData")]
    public sealed class BowWeaponData : ScriptableObject
    {
        [Header("Projectile")]
        [Min(0f)] public float projectileArcHeight = 1.2f;
        [Min(0.1f)] public float projectileGravityScale = 1.35f;
        [Min(0.1f)] public float projectileEmbeddedLifetimeSeconds = 18f;
        public bool projectilesStickToTargets = true;
        public bool projectilesEmbedInEnvironment = true;

        [Header("Range & Accuracy")]
        [Min(1f)] public float maximumRangeMeters = 100f;
        [Range(0f, 1f)] public float hitChanceAtMaxRange = 0.35f;

        [Header("Visuals")]
        public GameObject arrowVisualPrefab;
        public bool preferArrowVisualPrefab = true;
        public bool enableRuntimeProjectileFallback = true;
        [Min(0.01f)] public float runtimeProjectileThickness = 0.025f;
        [Min(0.05f)] public float runtimeProjectileLength = 0.35f;
        public Color runtimeProjectileColor = new(0.58f, 0.42f, 0.24f, 1f);

        [Header("Audio")]
        public AudioClip releaseArrowClip;
        [Range(0f, 1f)] public float releaseArrowVolume = 0.9f;
        public AudioClip hitBodyClip;
        [Range(0f, 1f)] public float hitBodyVolume = 0.9f;
    }
}
