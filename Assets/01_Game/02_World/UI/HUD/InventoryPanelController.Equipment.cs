using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Inventory;

namespace Zombera.UI
{
    public sealed partial class InventoryPanelController
    {
        private void RefreshEquipmentViews()
        {
            var equipmentSystem = ResolveEquipmentSystem();

            foreach (var pair in _equipmentSlotViews)
                UpdateEquipmentSlotView(pair.Value, equipmentSystem);
        }

        private static void UpdateEquipmentSlotView(EquipmentSlotView view, EquipmentSystem equipmentSystem)
        {
            if (view == null) return;

            var equippedItem = equipmentSystem != null
                ? equipmentSystem.GetEquippedItem(view.Slot)
                : null;

            if (view.Icon != null)
            {
                var iconSprite = equippedItem != null ? equippedItem.inventoryIcon : null;
                view.Icon.sprite = iconSprite;
                view.Icon.color = Color.white;
                view.Icon.enabled = iconSprite != null;
            }

            if (view.ItemHint != null)
                view.ItemHint.text = equippedItem != null
                    ? ResolveItemName(equippedItem)
                    : view.DefaultHint;

            view.DropTarget?.ResetVisual();
        }

        public EquipmentSystem GetCurrentEquipmentSystem()
        {
            return ResolveEquipmentSystem();
        }

        private EquipmentSystem ResolveEquipmentSystem()
        {
            if (_currentUnit == null)
            {
                _currentEquipmentSystem = null;
                UnhookEquipment();
                return null;
            }

            if (_currentEquipmentSystem == null)
                _currentEquipmentSystem = ResolveOrProvisionEquipmentSystem(_currentUnit);

            HookEquipment();

            return _currentEquipmentSystem;
        }

        private static EquipmentSystem ResolveOrProvisionEquipmentSystem(Unit unit)
        {
            var existing = FindEquipmentSystemOnUnit(unit);
            if (existing != null) return existing;

            return AutoProvisionEquipmentSystem(unit);
        }

        private static EquipmentSystem AutoProvisionEquipmentSystem(Unit unit)
        {
            EnsureWeaponSystemProvisioning(unit);

            var equipmentSystem = unit.gameObject.AddComponent<EquipmentSystem>();
            InventoryPanelDiagnostics.LogAutoProvisionedEquipment(unit, equipmentSystem);
            return equipmentSystem;
        }

        private static void EnsureWeaponSystemProvisioning(Unit unit)
        {
            var weaponSystem = FindWeaponSystemOnUnit(unit);
            if (weaponSystem == null)
                _ = unit.gameObject.AddComponent<WeaponSystem>();
        }

        private static EquipmentSystem FindEquipmentSystemOnUnit(Unit unit)
        {
            if (unit == null) return null;

            return unit.GetComponent<EquipmentSystem>()
                   ?? unit.GetComponentInChildren<EquipmentSystem>(true)
                   ?? unit.GetComponentInParent<EquipmentSystem>();
        }

        private static WeaponSystem FindWeaponSystemOnUnit(Unit unit)
        {
            if (unit == null) return null;

            return unit.GetComponent<WeaponSystem>()
                   ?? unit.GetComponentInChildren<WeaponSystem>(true)
                   ?? unit.GetComponentInParent<WeaponSystem>();
        }

        private bool TryEquipItemToSlot(ItemDefinition item, EquipmentSlot slot)
        {
            if (item == null) return false;

            var equipmentSystem = ResolveEquipmentSystem();
            if (equipmentSystem == null)
            {
                InventoryPanelDiagnostics.LogEquipMissingSystem(this);
                return false;
            }

            var equipped = InventoryPanelActionLayer.TryEquip(equipmentSystem, slot, item);
            if (equipped) RefreshEquipmentViews();

            return equipped;
        }

        private void TryUnequipItem(ItemDefinition item)
        {
            if (item == null) return;

            var equipmentSystem = ResolveEquipmentSystem();
            if (equipmentSystem == null) return;

            if (InventoryPanelActionLayer.TryUnequip(equipmentSystem, item))
                RefreshEquipmentViews();
        }

        private static EquipmentSlot ResolveDefaultEquipSlot(ItemDefinition item)
        {
            if (item == null) return EquipmentSlot.Belt;

            if (item.enforceSpecificEquipSlot) return item.forcedEquipSlot;

            if (item.equippedArmorData != null) return item.equippedArmorData.equipSlot;

            if (item.itemType == ItemType.Weapon || item.equippedWeaponData != null) return EquipmentSlot.RightHand;

            return EquipmentSlot.Belt;
        }
    }
}
