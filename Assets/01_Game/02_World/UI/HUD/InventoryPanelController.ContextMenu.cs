using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zombera.UI
{
    public sealed partial class InventoryPanelController
    {
        internal void HandleSlotPointerClick(int slotIndex, PointerEventData eventData)
        {
            if (eventData == null) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                ShowContextMenu(slotIndex, eventData);
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            {
                HandleSlotDoubleClick(slotIndex);
                return;
            }

            HideContextMenu();
        }

        private void HandleSlotDoubleClick(int slotIndex)
        {
            if (!TryGetVisibleStackAtIndex(slotIndex, out var stack) || stack.item == null)
            {
                HideContextMenu();
                return;
            }

            _suppressDropUntilRealtime = Time.unscaledTime + 0.2f;

            var targetSlot = ResolveDefaultEquipSlot(stack.item);
            TryEquipItemToSlot(stack.item, targetSlot);
            HideContextMenu();
        }

        private void ShowContextMenu(int slotIndex, PointerEventData eventData)
        {
            if (!TryGetVisibleStackAtIndex(slotIndex, out var stack))
            {
                HideContextMenu();
                return;
            }

            EnsureContextMenu();
            if (_contextMenuRoot == null) return;

            _contextSlotIndex = slotIndex;

            if (_contextMenuTitle != null)
                _contextMenuTitle.text = ResolveItemName(stack.item);

            if (_contextEquipButton != null)
            {
                _contextEquipButton.interactable = stack.item != null;
                if (_contextEquipButtonLabel != null)
                {
                    var isEquipped = IsItemEquipped(stack.item);
                    _contextEquipButtonLabel.text = isEquipped ? "Unequip" : "Equip";
                }
            }

            if (_contextDropButton != null)
                _contextDropButton.interactable = stack.item != null && stack.quantity > 0;

            PositionContextMenu(slotIndex, eventData);
            _contextMenuRoot.gameObject.SetActive(true);
            _contextMenuRoot.SetAsLastSibling();
        }

        private void PositionContextMenu(int slotIndex, PointerEventData eventData)
        {
            if (_contextMenuRoot == null) return;

            var panelRect = transform as RectTransform;
            if (panelRect == null) return;

            var uiCamera = eventData?.pressEventCamera;
            var screenPoint = eventData?.position ?? Vector2.zero;

            if (slotIndex >= 0 && slotIndex < _slotViews.Count && _slotViews[slotIndex].Root != null)
            {
                var slotRect = _slotViews[slotIndex].Root;
                var worldRightEdge = slotRect.TransformPoint(new Vector3(slotRect.rect.xMax, 0f, 0f));
                screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldRightEdge);
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, screenPoint, uiCamera,
                    out var localPoint)) return;

            var desired = localPoint + new Vector2(_contextMenuRoot.rect.width * 0.55f + 16f, 0f);
            _contextMenuRoot.anchoredPosition = ClampToRect(panelRect.rect, desired, _contextMenuRoot.rect.size);
        }

        private static Vector2 ClampToRect(Rect container, Vector2 desiredCenter, Vector2 size)
        {
            var halfWidth = size.x * 0.5f;
            var halfHeight = size.y * 0.5f;

            var x = Mathf.Clamp(desiredCenter.x, container.xMin + halfWidth, container.xMax - halfWidth);
            var y = Mathf.Clamp(desiredCenter.y, container.yMin + halfHeight, container.yMax - halfHeight);
            return new Vector2(x, y);
        }

        private void HideContextMenu()
        {
            _contextSlotIndex = -1;

            if (_contextMenuRoot != null)
                _contextMenuRoot.gameObject.SetActive(false);
        }

        private void EnsureContextMenu()
        {
            if (_contextMenuRoot != null) return;

            InventoryPanelVisualFactory.BuildContextMenu(
                this,
                transform,
                out _contextMenuRoot,
                out _contextMenuTitle,
                out _contextEquipButton,
                out _contextEquipButtonLabel,
                out _contextDropButton);

            ConfigureContextActionButton(_contextEquipButton, false);
            ConfigureContextActionButton(_contextDropButton, true);

            _contextMenuRoot.gameObject.SetActive(false);
        }

        private void ConfigureContextActionButton(Button button, bool isDropAction)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();

            var actionButton = button.gameObject.GetComponent<InventoryContextActionButton>();
            if (actionButton == null)
                actionButton = button.gameObject.AddComponent<InventoryContextActionButton>();

            actionButton.Configure(this, isDropAction);
        }

        internal void HandleContextEquipClicked()
        {
            if (!TryGetVisibleStackAtIndex(_contextSlotIndex, out var stack) || stack.item == null)
            {
                HideContextMenu();
                return;
            }

            _suppressDropUntilRealtime = Time.unscaledTime + 0.2f;

            if (IsItemEquipped(stack.item))
                TryUnequipItem(stack.item);
            else
                TryEquipItemToSlot(stack.item, ResolveDefaultEquipSlot(stack.item));

            HideContextMenu();
        }

        internal void HandleContextDropClicked()
        {
            if (Time.unscaledTime < _suppressDropUntilRealtime) return;

            if (!TryGetVisibleStackAtIndex(_contextSlotIndex, out var stack) || stack.item == null ||
                stack.quantity <= 0)
            {
                HideContextMenu();
                return;
            }

            _ = TryDropItemStack(stack.item, stack.quantity);
            HideContextMenu();
        }
    }
}
