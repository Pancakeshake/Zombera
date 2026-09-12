#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Data;
using Zombera.Inventory;
using Zombera.Systems;

#endregion

// ReSharper disable InvertIf

namespace Zombera.Combat
{
    /// <summary>
    ///     Handles weapon equip/use flow for firearms, bows, melee, and throwables.
    /// </summary>
    public sealed partial class WeaponSystem : MonoBehaviour
    {
        private static readonly string[] FallbackMuzzleSearchNames =
        {
            "Muzzle",
            "Socket_RightHand",
            "RightHand",
            "Hand_R",
            "mixamorig:RightHand"
        };

        private static readonly int HasWeaponAnimatorParameterHash = Animator.StringToHash("HasWeapon");
        private static readonly int EquipAnimatorParameterHash = Animator.StringToHash("Equip");

        [SerializeField] private WeaponData equippedWeapon;
        [SerializeField] private WeaponData secondaryWeapon;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Projectile projectilePrefab;

        [Header("Visual Testing Slots")]
        public ItemDefinition slotHandR;
        public ItemDefinition slotHandL;
        public ItemDefinition slotBack;
        public ItemDefinition slotHipR;
        public ItemDefinition slotHipL;
        public ItemDefinition slotChest;

        private readonly Dictionary<WeaponSlot, GameObject> _visualInstances = new();

        [Header("Close-Range Hit Validation")]
        [SerializeField]
        private bool requireFacingForCloseRangeHits = true;

        [SerializeField] [Range(0f, 180f)] private float closeRangeFacingAngleDegrees = 65f;
        [SerializeField] [Min(0.1f)] private float unarmedRange = 2.25f;
        [SerializeField] [Min(0.1f)] private float meleeRange = 1.5f;

        [Header("Knockback")]
        [SerializeField]
        [Min(0f)]
        private float knockbackForce = 6f;

        [Header("Bow")]
        [SerializeField] private BowWeaponData fallbackBowWeaponData;

        private UnitInventory _ownerInventory;
        private Material _runtimeBowProjectileMaterial;
        private BowSubsystem _bowSubsystem;

        public WeaponData EquippedWeapon => equippedWeapon;

        // ReSharper disable once UnusedMember.Global
        public int CurrentAmmo { get; private set; }

        // ReSharper disable once UnusedMember.Global
        public int MagazineSize => equippedWeapon != null ? equippedWeapon.magazineSize : 0;

        public bool NeedsReload => equippedWeapon != null && equippedWeapon.magazineSize > 0 && CurrentAmmo <= 0;

        // ReSharper disable once UnusedMember.Global
        public bool IsRangedWeaponEquipped => equippedWeapon != null && IsRangedCategory(equippedWeapon.weaponCategory);

        public bool IsBowEquipped => equippedWeapon != null && equippedWeapon.weaponCategory == WeaponCategory.Bow;
        public Unit OwnerUnit { get; private set; }
        public UnitStats OwnerStats { get; private set; }

        public void RestoreAmmo(int ammo)
        {
            CurrentAmmo = Mathf.Clamp(ammo, 0, MagazineSize);
        }

        private BowWeaponData GetActiveBowWeaponData()
        {
            if (equippedWeapon != null && equippedWeapon.bowWeaponData != null)
                return equippedWeapon.bowWeaponData;

            return fallbackBowWeaponData;
        }

        private void Awake()
        {
            EnsureOwnerContextResolved();
            EnsureMuzzlePoint();
            RebuildVisuals();
        }

        private void OnDestroy()
        {
            ClearVisuals();
        }
    }

    public enum WeaponCategory
    {
        Pistol,
        Rifle,
        Shotgun,
        Melee,
        Throwable,
        Bow
    }
}
