using UnityEngine;
using UnityEngine.EventSystems;
using Zombera.Characters;

namespace Zombera.UI
{
    [AddComponentMenu("Zombera/UI/Squad Portrait Drop Target")]
    public sealed class SquadPortraitDropTarget : MonoBehaviour, IDropHandler
    {
        private SquadPortraitSlot _slot;
        private InventoryPanelController _inventoryController;

        private void Awake()
        {
            _slot = GetComponent<SquadPortraitSlot>();
            _inventoryController = GetComponentInParent<InventoryPanelController>();
            
            // If not found in parent, try finding it in the scene (as it might be in a different canvas branch)
            if (_inventoryController == null)
            {
                _inventoryController = FindFirstObjectByType<InventoryPanelController>();
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (_inventoryController == null || !_inventoryController.IsDraggingItem) return;
            if (_slot == null || _slot.BoundUnit == null) return;

            if (_inventoryController.TransferDraggingItemToTarget(_slot.BoundUnit))
            {
                Debug.Log($"[SquadPortraitDropTarget] Transferred item to {_slot.BoundUnit.name}.");
            }
        }
}
}
