using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI
{
    /// <summary>
    /// Controls bottom-center hotbar slots and visual state.
    /// </summary>
    public sealed class HotbarController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image panelBackground;

        [Header("Slots")]
        [SerializeField] private List<HotbarSlotView> slots = new List<HotbarSlotView>();
        [SerializeField] private Color selectedSlotTint = Color.white;
        [SerializeField] private Color defaultSlotTint = Color.gray;

        public bool IsInitialized { get; private set; }
        public int SelectedSlotIndex { get; private set; } = -1;

        private HUDManager hudManager;

        public void Initialize(HUDManager manager)
        {
            if (IsInitialized)
            {
                return;
            }

            hudManager = manager;

            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            ClearSlots();
            IsInitialized = true;

            // TODO: Bind inventory/equipment feed to hotbar slot updates.
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetSlotData(int index, HotbarSlotViewData slotData)
        {
            if (!IsValidSlot(index))
            {
                return;
            }

            HotbarSlotView slot = slots[index];

            if (slot.iconImage != null)
            {
                slot.iconImage.sprite = slotData.icon;
                slot.iconImage.enabled = slotData.icon != null;
            }

            if (slot.quantityText != null)
            {
                slot.quantityText.text = slotData.quantity > 0 ? slotData.quantity.ToString() : string.Empty;
            }

            if (slot.keyLabelText != null)
            {
                slot.keyLabelText.text = slotData.keyLabel;
            }

            if (slot.cooldownOverlay != null)
            {
                slot.cooldownOverlay.fillAmount = Mathf.Clamp01(slotData.cooldown01);
                slot.cooldownOverlay.enabled = slotData.cooldown01 > 0f;
            }

            if (slot.slotFrameImage != null)
            {
                slot.slotFrameImage.color = slotData.isActive ? selectedSlotTint : defaultSlotTint;
            }
        }

        public void SetSelectedSlot(int index)
        {
            SelectedSlotIndex = index;

            for (int i = 0; i < slots.Count; i++)
            {
                HotbarSlotView slot = slots[i];

                if (slot.slotFrameImage != null)
                {
                    slot.slotFrameImage.color = i == SelectedSlotIndex ? selectedSlotTint : defaultSlotTint;
                }
            }
        }

        public void ClearSlots()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                SetSlotData(i, default);
            }

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