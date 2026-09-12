#region

using System;
using System.Linq;
using Zombera.Data;

#endregion

namespace Zombera.Inventory
{
    public sealed partial class EquipmentSystem
    {
        private readonly struct EquipmentMutation
        {
            public readonly ItemDefinition RemovedItem;
            public readonly ItemDefinition AddedItem;
            public readonly EquipmentSlot Slot;
            public readonly bool RemoveStats;
            public readonly bool AddStats;
            public readonly bool RemoveExistingVisual;
            public readonly bool RefreshWeaponAndAppearance;
            public readonly bool PublishChangedEvent;

            public EquipmentMutation(
                ItemDefinition removedItem,
                ItemDefinition addedItem,
                EquipmentSlot slot,
                bool removeStats,
                bool addStats,
                bool removeExistingVisual,
                bool refreshWeaponAndAppearance,
                bool publishChangedEvent)
            {
                RemovedItem = removedItem;
                AddedItem = addedItem;
                Slot = slot;
                RemoveStats = removeStats;
                AddStats = addStats;
                RemoveExistingVisual = removeExistingVisual;
                RefreshWeaponAndAppearance = refreshWeaponAndAppearance;
                PublishChangedEvent = publishChangedEvent;
            }
        }

        public event Action OnEquipmentChanged;

        public bool Equip(EquipmentSlot slot, ItemDefinition itemDefinition)
        {
            if (itemDefinition == null) return false;

            var resolvedSlot = ResolveRequestedSlot(slot, itemDefinition);

            // Keep one equipped binding per item; move between slots when needed.
            RemoveDuplicateItemBindings(itemDefinition, resolvedSlot);

            for (var i = 0; i < equippedItems.Count; i++)
            {
                if (equippedItems[i].slot != resolvedSlot) continue;

                var replacingDifferentItem = equippedItems[i].item != itemDefinition;
                var replacedItem = equippedItems[i].item;

                var binding = equippedItems[i];
                binding.item = itemDefinition;
                equippedItems[i] = binding;

                ApplyEquipmentMutation(new EquipmentMutation(
                    replacedItem,
                    itemDefinition,
                    resolvedSlot,
                    replacingDifferentItem,
                    replacingDifferentItem,
                    true,
                    true,
                    true));

                return true;
            }

            equippedItems.Add(new EquipmentSlotBinding(resolvedSlot, itemDefinition));

            ApplyEquipmentMutation(new EquipmentMutation(
                null,
                itemDefinition,
                resolvedSlot,
                false,
                true,
                false,
                true,
                true));

            return true;
        }

        public void ClearAll()
        {
            for (var i = equippedItems.Count - 1; i >= 0; i--)
            {
                Unequip(equippedItems[i].slot);
            }
        }

        // ReSharper disable once UnusedMember.Global
        public bool Unequip(EquipmentSlot slot)
        {
            for (var i = 0; i < equippedItems.Count; i++)
            {
                if (equippedItems[i].slot != slot) continue;

                var removedItem = equippedItems[i].item;
                equippedItems.RemoveAt(i);

                ApplyEquipmentMutation(new EquipmentMutation(
                    removedItem,
                    null,
                    slot,
                    true,
                    false,
                    true,
                    true,
                    true));

                return true;
            }

            return false;
        }

        private void ApplyEquipmentMutation(EquipmentMutation mutation)
        {
            // Keep side effects in a stable order: stats -> visuals -> weapon sync -> UMA sync -> event.
            if (mutation.RemoveStats && mutation.RemovedItem != null)
                RemoveStatBonuses(mutation.RemovedItem);

            if (mutation.AddStats && mutation.AddedItem != null)
                ApplyStatBonuses(mutation.AddedItem);

            if (mutation.RemoveExistingVisual)
                RemoveVisualAttachment(mutation.Slot);

            if (mutation.AddedItem != null)
                ApplyVisualAttachment(mutation.Slot, mutation.AddedItem);

            if (mutation.RefreshWeaponAndAppearance)
            {
                RefreshWeaponFromEquipment();
                SyncEquipmentAppearanceFromEquipment();
            }

            if (mutation.PublishChangedEvent)
                OnEquipmentChanged?.Invoke();
        }

        private static EquipmentSlot ResolveRequestedSlot(EquipmentSlot requestedSlot, ItemDefinition item)
        {
            return item is { enforceSpecificEquipSlot: true } ? item.forcedEquipSlot : requestedSlot;
        }

        private void RemoveDuplicateItemBindings(ItemDefinition item, EquipmentSlot keepSlot)
        {
            for (var i = equippedItems.Count - 1; i >= 0; i--)
            {
                var binding = equippedItems[i];
                if (binding.item != item || binding.slot == keepSlot) continue;

                equippedItems.RemoveAt(i);

                ApplyEquipmentMutation(new EquipmentMutation(
                    binding.item,
                    null,
                    binding.slot,
                    true,
                    false,
                    true,
                    false,
                    false));
            }
        }

        public ItemDefinition GetEquippedItem(EquipmentSlot slot)
        {
            for (var i = 0; i < equippedItems.Count; i++)
                if (equippedItems[i].slot == slot)
                    return equippedItems[i].item;

            return null;
        }

        private void RefreshWeaponFromEquipment()
        {
            var desiredWeapon = ResolveEquippedWeaponData();
            if (weaponSystem?.EquippedWeapon == desiredWeapon) return;

            weaponSystem?.EquipWeapon(desiredWeapon);
        }

        private WeaponData ResolveEquippedWeaponData()
        {
            var equippedRightHandItem = GetEquippedItem(EquipmentSlot.RightHand);
            if (equippedRightHandItem != null && equippedRightHandItem.equippedWeaponData != null)
                return equippedRightHandItem.equippedWeaponData;

            var equippedLeftHandItem = GetEquippedItem(EquipmentSlot.LeftHand);
            if (equippedLeftHandItem != null && equippedLeftHandItem.equippedWeaponData != null)
                return equippedLeftHandItem.equippedWeaponData;

            return equippedItems
                .Select(binding => binding.item?.equippedWeaponData)
                .FirstOrDefault(weaponData => weaponData != null);
        }
    }
}
