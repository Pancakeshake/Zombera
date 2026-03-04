using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Inventory
{
    /// <summary>
    /// Handles item equipment and slot assignment.
    /// </summary>
    public sealed class EquipmentSystem : MonoBehaviour
    {
        [SerializeField] private List<EquipmentSlotBinding> equippedItems = new List<EquipmentSlotBinding>();

        public IReadOnlyList<EquipmentSlotBinding> EquippedItems => equippedItems;

        public bool Equip(EquipmentSlot slot, ItemDefinition itemDefinition)
        {
            if (itemDefinition == null)
            {
                return false;
            }

            for (int i = 0; i < equippedItems.Count; i++)
            {
                if (equippedItems[i].slot != slot)
                {
                    continue;
                }

                EquipmentSlotBinding binding = equippedItems[i];
                binding.item = itemDefinition;
                equippedItems[i] = binding;
                return true;
            }

            equippedItems.Add(new EquipmentSlotBinding(slot, itemDefinition));
            return true;
        }

        public bool Unequip(EquipmentSlot slot)
        {
            for (int i = 0; i < equippedItems.Count; i++)
            {
                if (equippedItems[i].slot == slot)
                {
                    equippedItems.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public ItemDefinition GetEquippedItem(EquipmentSlot slot)
        {
            for (int i = 0; i < equippedItems.Count; i++)
            {
                if (equippedItems[i].slot == slot)
                {
                    return equippedItems[i].item;
                }
            }

            return null;
        }

        // TODO: Validate compatibility rules per slot and character archetype.
        // TODO: Apply equipped stat modifiers and visual attachments.
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
        PrimaryWeapon,
        SecondaryWeapon,
        Head,
        Body,
        Utility
    }
}