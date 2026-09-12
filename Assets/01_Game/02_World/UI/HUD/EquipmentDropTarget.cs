using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zombera.Inventory;

namespace Zombera.UI
{
    public sealed class EquipmentDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private readonly Color _hoverColor = new(0.16f, 0.34f, 0.28f, 1f);
        private Color _baseColor;
        private Image _frame;
        private InventoryPanelController _owner;
        private EquipmentSlot _slot;

        public void OnDrop(PointerEventData eventData)
        {
            _owner?.HandleItemDroppedOnEquipmentSlot(_slot);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_frame == null || _owner == null || !_owner.IsDraggingItem) return;

            _frame.color = _hoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ResetVisual();
        }

        public void Configure(InventoryPanelController controller, EquipmentSlot equipmentSlot, Image targetFrame)
        {
            _owner = controller;
            _slot = equipmentSlot;
            _frame = targetFrame;
            _baseColor = _frame != null ? _frame.color : Color.white;
        }

        public void ResetVisual()
        {
            if (_frame != null) _frame.color = _baseColor;
        }
    }
}
