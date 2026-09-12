using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zombera.Inventory;

namespace Zombera.UI
{
    public sealed partial class InventoryPanelController
    {
        internal void HandleSlotBeginDrag(int slotIndex, PointerEventData eventData)
        {
            if (!TryGetVisibleStackAtIndex(slotIndex, out var stack))
            {
                EndItemDrag();
                return;
            }

            _draggingStack = stack;
            IsDraggingItem = true;
            HideContextMenu();

            EnsureDragGhost();
            ApplyDragGhostVisual(stack);
            UpdateDragGhostPosition(eventData);
        }

        internal void HandleSlotDrag(PointerEventData eventData)
        {
            if (!IsDraggingItem) return;

            UpdateDragGhostPosition(eventData);
        }

        internal void HandleSlotEndDrag(PointerEventData eventData)
        {
            _ = eventData;
            EndItemDrag();
        }

        internal void HandleItemDroppedOnEquipmentSlot(EquipmentSlot slot)
        {
            if (!IsDraggingItem || _draggingStack.item == null) return;

            _ = TryEquipItemToSlot(_draggingStack.item, slot);
        }

        private void EnsureDragGhost()
        {
            if (_dragGhostRoot != null) return;

            var parent = _rootCanvasRect != null ? _rootCanvasRect : transform;
            InventoryPanelVisualFactory.BuildDragGhost(parent, out _dragGhostRoot, out _dragGhostIcon,
                out _dragGhostInitial);

            _dragGhostRoot.gameObject.SetActive(false);
        }

        private void ApplyDragGhostVisual(ItemStack stack)
        {
            if (_dragGhostRoot == null) return;

            var item = stack.item;
            var iconSprite = item != null ? item.inventoryIcon : null;

            if (_dragGhostIcon != null)
            {
                _dragGhostIcon.sprite = iconSprite;
                _dragGhostIcon.color = Color.white;
                _dragGhostIcon.enabled = iconSprite != null;
            }

            if (_dragGhostInitial != null)
            {
                var showInitial = iconSprite == null;
                _dragGhostInitial.gameObject.SetActive(showInitial);
                _dragGhostInitial.text = showInitial ? BuildInitial(ResolveItemName(item)) : string.Empty;
            }

            _dragGhostRoot.gameObject.SetActive(true);
            _dragGhostRoot.SetAsLastSibling();
        }

        private void UpdateDragGhostPosition(PointerEventData eventData)
        {
            if (_dragGhostRoot == null || eventData == null) return;

            if (_rootCanvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootCanvasRect,
                    eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                _dragGhostRoot.anchoredPosition = localPoint + new Vector2(18f, -12f);
                return;
            }

            _dragGhostRoot.position = eventData.position + new Vector2(18f, -12f);
        }

        private void EndItemDrag()
        {
            IsDraggingItem = false;
            _draggingStack = default;

            if (_dragGhostRoot != null)
                _dragGhostRoot.gameObject.SetActive(false);

            ResetEquipmentDropTargetVisuals();
        }

        private void ResetEquipmentDropTargetVisuals()
        {
            foreach (var pair in _equipmentSlotViews)
                pair.Value?.DropTarget?.ResetVisual();
        }
    }
}
