#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UMA.CharacterSystem;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Data;

#endregion

// ReSharper disable UseNullPropagation

namespace Zombera.Inventory
{
    /// <summary>
    ///     Handles item equipment and slot assignment.
    /// </summary>
    [ExecuteAlways]
    public sealed partial class EquipmentSystem : MonoBehaviour
    {
        [SerializeField] private List<EquipmentSlotBinding> equippedItems = new();
        [SerializeField] private UnitStats unitStats;
        [SerializeField] private UnitHealth unitHealth;
        [SerializeField] private WeaponSystem weaponSystem;
        [SerializeField] private bool syncEquipmentAppearanceFromEquipment = true;

        [Header("Editor Quick Slots")]
        [SerializeField] private ItemDefinition headItem;
        [SerializeField] private ItemDefinition chestItem;
        [SerializeField] private ItemDefinition backItem;
        [SerializeField] private ItemDefinition legsItem;
        [SerializeField] private ItemDefinition feetItem;
        [SerializeField] private ItemDefinition leftHandItem;
        [SerializeField] private ItemDefinition rightHandItem;

        private readonly Dictionary<EquipmentSlot, GameObject> _equippedVisualInstances = new();

        private DynamicCharacterAvatar _cachedAvatar;
        private Animator _cachedAnimator;
        private bool _isRebuilding;

#if UNITY_EDITOR
        private bool _editorDelayedRebuildQueued;
#endif

        public IReadOnlyList<EquipmentSlotBinding> EquippedItems => equippedItems;

        private void Awake()
        {
            if (unitStats == null) unitStats = GetComponent<UnitStats>();
            if (unitHealth == null) unitHealth = GetComponent<UnitHealth>();
            if (weaponSystem == null) weaponSystem = GetComponent<WeaponSystem>();

            _cachedAvatar = GetComponentInChildren<DynamicCharacterAvatar>();
            _cachedAnimator = GetComponentInChildren<Animator>();

            RebuildEquippedVisuals();
            RefreshWeaponFromEquipment();
        }

        private void Start()
        {
            SyncEquipmentAppearanceFromEquipment();
        }
    }

    [Serializable]
    public struct EquipmentSlotBinding
    {
        public EquipmentSlot slot;
        public ItemDefinition item;

        public EquipmentSlotBinding(EquipmentSlot equipmentSlot, ItemDefinition itemDefinition)
        {
            slot = equipmentSlot;
            item = itemDefinition;
        }
    }

    public enum EquipmentSlot
    {
        // Legacy values retained for backward compatibility.
        PrimaryWeapon = 0,
        SecondaryWeapon = 1,
        Head = 2,
        Body = 3,
        Utility = 4,

        // Canonical silhouette layout names.
        LeftHand = PrimaryWeapon,
        RightHand = SecondaryWeapon,
        Chest = Body,
        Belt = Utility,
        Face = 5,
        Back = 6,
        Legs = 7,
        Feet = 8
    }
}
