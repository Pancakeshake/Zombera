#region

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Zombera.UI
{
    /// <summary>
    ///     Controls bottom-center hotbar slots and visual state.
    /// </summary>
    public sealed class HotbarController : MonoBehaviour
    {
        [Header("Root")] [SerializeField] private RectTransform panelRoot;

        [SerializeField] private Image panelBackground;

        [Header("Slots")] [SerializeField] private List<HotbarSlotView> slots = new();

        [SerializeField] private Color selectedSlotTint = Color.white;
        [SerializeField] private Color defaultSlotTint = Color.gray;

        private HUDManager _hudManager;

        public bool IsInitialized { get; private set; }
        public int SelectedSlotIndex { get; private set; } = -1;

        public void Initialize(HUDManager manager)
        {
            if (IsInitialized) return;

            _hudManager = manager;

            if (panelRoot == null) panelRoot = transform as RectTransform;

            ClearSlots();
            IsInitialized = true;
            BindInventoryEvents();
        }

        private void BindInventoryEvents()
        {
            // HUDManager is the wiring hub. It calls SetSlotData when inventory changes.
            // This method is the explicit hook point for wiring up a live inventory feed.
            // Extend: pass an InventorySystem reference and subscribe to its OnSlotChanged event.
            _ = _hudManager;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetSlotData(int index, HotbarSlotViewData slotData)
        {
            if (!IsValidSlot(index)) return;

            var slot = slots[index];

            if (slot.iconImage != null)
            {
                slot.iconImage.sprite = slotData.icon;
                slot.iconImage.enabled = slotData.icon != null;
            }

            if (slot.quantityText != null)
                slot.quantityText.text = slotData.quantity > 0 ? slotData.quantity.ToString() : string.Empty;

            if (slot.keyLabelText != null) slot.keyLabelText.text = slotData.keyLabel;

            if (slot.cooldownOverlay != null)
            {
                slot.cooldownOverlay.fillAmount = Mathf.Clamp01(slotData.cooldown01);
                slot.cooldownOverlay.enabled = slotData.cooldown01 > 0f;
            }

            if (slot.slotFrameImage != null)
                slot.slotFrameImage.color = slotData.isActive ? selectedSlotTint : defaultSlotTint;
        }

        public void SetSelectedSlot(int index)
        {
            SelectedSlotIndex = index;

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];

                if (slot.slotFrameImage != null)
                    slot.slotFrameImage.color = i == SelectedSlotIndex ? selectedSlotTint : defaultSlotTint;
            }
        }

        public void ClearSlots()
        {
            for (var i = 0; i < slots.Count; i++) SetSlotData(i, default);

            SetSelectedSlot(-1);
        }

        private bool IsValidSlot(int index)
        {
            return index >= 0 && index < slots.Count;
        }
    }

    [Serializable]
    public struct HotbarSlotView
    {
        public Image slotFrameImage;
        public Image iconImage;
        public Image cooldownOverlay;
        public TextMeshProUGUI quantityText;
        public TextMeshProUGUI keyLabelText;
    }

    [Serializable]
    public struct HotbarSlotViewData
    {
        public Sprite icon;
        public int quantity;
        [Range(0f, 1f)] public float cooldown01;
        public bool isActive;
        public string keyLabel;
    }
}