#region

using UnityEngine;
using UnityEngine.EventSystems;

#endregion

namespace Zombera.UI
{
    [DisallowMultipleComponent]
    internal sealed class SquadPortraitRosterDragInteraction : MonoBehaviour, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IDropHandler
    {
        private SquadPortraitStrip _strip;
        private int _slotIndex;

        public void Configure(SquadPortraitStrip strip, int slotIndex)
        {
            _strip = strip;
            _slotIndex = slotIndex;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _strip?.BeginPortraitDrag(_slotIndex, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _strip?.UpdatePortraitDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _strip?.EndPortraitDrag(eventData);
        }

        public void OnDrop(PointerEventData eventData)
        {
            _ = eventData;
            _strip?.HandlePortraitDropOnSlot(_slotIndex);
        }
    }
}
