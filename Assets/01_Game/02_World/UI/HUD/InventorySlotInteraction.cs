using UnityEngine;
using UnityEngine.EventSystems;

namespace Zombera.UI
{
    public sealed class InventorySlotInteraction : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler,
        IEndDragHandler
    {
        private InventoryPanelController _owner;
        private int _slotIndex;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _owner?.HandleSlotBeginDrag(_slotIndex, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _owner?.HandleSlotDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _owner?.HandleSlotEndDrag(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _owner?.HandleSlotPointerClick(_slotIndex, eventData);
        }

        public void Configure(InventoryPanelController controller, int index)
        {
            _owner = controller;
            _slotIndex = index;
        }
    }
}
