using UnityEngine;
using UnityEngine.EventSystems;
using Zombera.Inventory;

namespace Zombera.UI
{
    [AddComponentMenu("Zombera/UI/Equipment Slot Interaction")]
    public sealed class EquipmentSlotInteraction : MonoBehaviour, IPointerClickHandler
    {
        private EquipmentSlot _slot;
        private InventoryPanelController _owner;

        public void Configure(InventoryPanelController owner, EquipmentSlot slot)
        {
            _owner = owner;
            _slot = slot;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right) return;

            var es = FindEquipmentSystem();
            if (es != null)
            {
                es.Unequip(_slot);
            }
        }

        private EquipmentSystem FindEquipmentSystem()
        {
            // EquipmentSystem is usually on the unit being edited.
            // InventoryPanelController manages the current editing unit.
            // I'll add a way to get the current EquipmentSystem from the controller.
            return _owner != null ? _owner.GetCurrentEquipmentSystem() : null;
        }
    }
}
