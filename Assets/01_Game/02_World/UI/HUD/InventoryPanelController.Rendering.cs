using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Inventory;

namespace Zombera.UI
{
    public sealed partial class InventoryPanelController
    {
        private void RebuildSlotViews()
        {
            _slotViews.Clear();

            if (slotGrid == null) return;

            var owner = this;

            for (var i = 0; i < slotGrid.childCount; i++)
            {
                var slotTransform = slotGrid.GetChild(i);
                if (slotTransform == null) continue;

                var frame = slotTransform.GetComponent<Image>();
                if (frame == null) continue;

                var view = new SlotView
                {
                    Root = slotTransform as RectTransform,
                    Frame = frame,
                    Icon = EnsureSlotIcon(slotTransform),
                    Initial = EnsureSlotInitial(slotTransform),
                    Quantity = EnsureSlotQuantity(slotTransform)
                };

                EnsureSlotInteraction(slotTransform, owner, _slotViews.Count);
                _slotViews.Add(view);
            }
        }

        private static void EnsureSlotInteraction(Transform slotTransform, InventoryPanelController owner, int slotIndex)
        {
            var interaction = slotTransform.GetComponent<InventorySlotInteraction>();
            if (interaction == null) interaction = slotTransform.gameObject.AddComponent<InventorySlotInteraction>();

            interaction.Configure(owner, slotIndex);
        }

        private void ResolveEquipmentSlotViews()
        {
            if (_equipmentSlotViews.Count > 0 && _equipmentPanel != null && !_equipmentPanel.hasChanged)
                return;

            _equipmentSlotViews.Clear();

            _equipmentPanel ??= transform.Find("EquipmentPanel");
            if (_equipmentPanel == null) return;

            var candidateButtons = _equipmentPanel.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < candidateButtons.Length; i++)
            {
                var button = candidateButtons[i];
                if (!TryResolveEquipmentSlotButton(button, out var slot)) continue;

                _equipmentSlotViews[slot] = BuildEquipmentSlotView(button, slot);
            }

            _equipmentPanel.hasChanged = false;
        }

        private static bool TryResolveEquipmentSlotButton(Button button, out EquipmentSlot slot)
        {
            slot = default;
            if (button == null || button.transform == null) return false;
            if (!button.name.StartsWith("Slot_", StringComparison.Ordinal)) return false;

            var slotName = button.name["Slot_".Length..];
            return Enum.TryParse(slotName, true, out slot);
        }

        private EquipmentSlotView BuildEquipmentSlotView(Button button, EquipmentSlot slot)
        {
            var iconTransform = button.transform.Find("Icon");
            var hintTransform = button.transform.Find("ItemHint");

            var frame = button.GetComponent<Image>();
            var icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            var hint = hintTransform != null ? hintTransform.GetComponent<TMP_Text>() : null;

            var dropTarget = EnsureEquipmentDropTarget(button, slot, frame);
            EnsureEquipmentSlotInteraction(button, slot);

            return new EquipmentSlotView
            {
                Slot = slot,
                Icon = icon,
                ItemHint = hint,
                DefaultHint = hint != null ? hint.text : string.Empty,
                DropTarget = dropTarget
            };
        }

        private EquipmentDropTarget EnsureEquipmentDropTarget(Button button, EquipmentSlot slot, Image frame)
        {
            var dropTarget = button.GetComponent<EquipmentDropTarget>();
            if (dropTarget == null)
                dropTarget = button.gameObject.AddComponent<EquipmentDropTarget>();

            dropTarget.Configure(this, slot, frame);
            return dropTarget;
        }

        private void EnsureEquipmentSlotInteraction(Button button, EquipmentSlot slot)
        {
            var interaction = button.GetComponent<EquipmentSlotInteraction>();
            if (interaction == null)
                interaction = button.gameObject.AddComponent<EquipmentSlotInteraction>();

            interaction.Configure(this, slot);
        }

        private static Image EnsureSlotIcon(Transform slotTransform)
        {
            var iconTransform = slotTransform.Find("Icon");
            RectTransform iconRect;

            if (iconTransform == null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(slotTransform, false);
                iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(3f, 3f);
                iconRect.offsetMax = new Vector2(-3f, -3f);
            }
            else
            {
                iconRect = iconTransform.GetComponent<RectTransform>();
                if (iconRect == null) iconRect = iconTransform.gameObject.AddComponent<RectTransform>();
            }

            var icon = iconRect.GetComponent<Image>();
            if (icon == null) icon = iconRect.gameObject.AddComponent<Image>();

            icon.raycastTarget = false;
            icon.preserveAspect = true;
            return icon;
        }

        private static TMP_Text EnsureSlotInitial(Transform slotTransform)
        {
            var initialTransform = slotTransform.Find("Initial");
            RectTransform initialRect;

            if (initialTransform == null)
            {
                var initialGo = new GameObject("Initial", typeof(RectTransform), typeof(TextMeshProUGUI));
                initialGo.transform.SetParent(slotTransform, false);
                initialRect = initialGo.GetComponent<RectTransform>();
                initialRect.anchorMin = Vector2.zero;
                initialRect.anchorMax = Vector2.one;
                initialRect.offsetMin = Vector2.zero;
                initialRect.offsetMax = Vector2.zero;
            }
            else
            {
                initialRect = initialTransform.GetComponent<RectTransform>();
                if (initialRect == null) initialRect = initialTransform.gameObject.AddComponent<RectTransform>();
            }

            var initial = initialRect.GetComponent<TMP_Text>();
            if (initial == null) initial = initialRect.gameObject.AddComponent<TextMeshProUGUI>();

            initial.font = TMP_Settings.defaultFontAsset;
            initial.fontSize = 18f;
            initial.fontStyle = FontStyles.Bold;
            initial.alignment = TextAlignmentOptions.Center;
            initial.color = new Color(0.84f, 0.80f, 0.70f, 0.9f);
            initial.raycastTarget = false;
            return initial;
        }

        private static TMP_Text EnsureSlotQuantity(Transform slotTransform)
        {
            var quantityTransform = slotTransform.Find("Quantity");
            RectTransform quantityRect;

            if (quantityTransform == null)
            {
                var quantityGo = new GameObject("Quantity", typeof(RectTransform), typeof(TextMeshProUGUI));
                quantityGo.transform.SetParent(slotTransform, false);
                quantityRect = quantityGo.GetComponent<RectTransform>();
                quantityRect.anchorMin = new Vector2(0.56f, 0f);
                quantityRect.anchorMax = new Vector2(1f, 0.35f);
                quantityRect.offsetMin = Vector2.zero;
                quantityRect.offsetMax = new Vector2(-2f, 2f);
            }
            else
            {
                quantityRect = quantityTransform.GetComponent<RectTransform>();
                if (quantityRect == null) quantityRect = quantityTransform.gameObject.AddComponent<RectTransform>();
            }

            var quantity = quantityRect.GetComponent<TMP_Text>();
            if (quantity == null) quantity = quantityRect.gameObject.AddComponent<TextMeshProUGUI>();

            quantity.font = TMP_Settings.defaultFontAsset;
            quantity.fontSize = 10f;
            quantity.fontStyle = FontStyles.Bold;
            quantity.alignment = TextAlignmentOptions.BottomRight;
            quantity.color = new Color(0.93f, 0.88f, 0.75f, 1f);
            quantity.raycastTarget = false;
            return quantity;
        }

        private void RefreshSlots()
        {
            EnsureRefreshViewsResolved();
            CollectFilteredStacks();
            RefreshEquippedItemLookup();
            SortFilteredStacksForDisplay();
            RenderFilteredStacks();
            HideContextMenuIfSelectionIsNoLongerVisible();
            RefreshEquipmentViews();
            LogRefreshSummary();
        }

        private void EnsureRefreshViewsResolved()
        {
            if (_slotViews.Count == 0) RebuildSlotViews();
            if (_equipmentSlotViews.Count == 0) ResolveEquipmentSlotViews();
        }

        private void CollectFilteredStacks()
        {
            _filteredStacks.Clear();
            if (_currentInventory == null) return;

            var query = searchInputField != null
                ? searchInputField.text
                : string.Empty;

            InventoryProjectionService.CollectFilteredStacks(
                _currentInventory.Items,
                query,
                _filteredStacks,
                MatchesFilter,
                MatchesSearch);
        }

        private void SortFilteredStacksForDisplay()
        {
            InventoryProjectionService.SortStacksForDisplay(_filteredStacks, _equippedItemsLookup, ResolveItemName);
        }

        private void RenderFilteredStacks()
        {
            for (var i = 0; i < _slotViews.Count; i++)
            {
                var view = _slotViews[i];
                if (i < _filteredStacks.Count)
                {
                    var isEquipped = InventoryProjectionService.IsEquipped(_filteredStacks[i].item, _equippedItemsLookup);
                    ApplyFilledSlot(view, _filteredStacks[i], isEquipped);
                    continue;
                }

                ApplyEmptySlot(view);
            }
        }

        private void HideContextMenuIfSelectionIsNoLongerVisible()
        {
            if (_contextMenuRoot == null || !_contextMenuRoot.gameObject.activeSelf) return;
            if (TryGetVisibleStackAtIndex(_contextSlotIndex, out _)) return;

            HideContextMenu();
        }

        private void LogRefreshSummary()
        {
            InventoryPanelDiagnostics.LogRefreshSummary(this, logBindingSummary, _currentUnit, _filteredStacks.Count,
                _slotViews.Count, _currentFilter.ToString());
        }

        private void RefreshEquippedItemLookup()
        {
            if (_currentEquipmentSystem == null)
            {
                _equippedItemsLookup.Clear();
                return;
            }

            InventoryProjectionService.RefreshEquippedItemLookup(_currentEquipmentSystem.EquippedItems,
                _equippedItemsLookup);
        }

        private bool TryGetVisibleStackAtIndex(int slotIndex, out ItemStack stack)
        {
            if (slotIndex >= 0 && slotIndex < _filteredStacks.Count)
            {
                stack = _filteredStacks[slotIndex];
                return stack.item != null && stack.quantity > 0;
            }

            stack = default;
            return false;
        }

        private bool MatchesFilter(ItemDefinition item)
        {
            return _currentFilter switch
            {
                InventoryFilter.All => true,
                InventoryFilter.Weapons => item.itemType == ItemType.Weapon,
                InventoryFilter.Ammo => item.itemType == ItemType.Ammo,
                InventoryFilter.Medical => item.itemType == ItemType.Medical,
                InventoryFilter.Consumables => item.itemType is ItemType.Food or ItemType.Vitamin,
                InventoryFilter.Materials => item.itemType == ItemType.Material,
                InventoryFilter.Other => item.itemType == ItemType.Generic,
                _ => false
            };
        }

        private static bool MatchesSearch(ItemDefinition item, string query)
        {
            if (item == null) return false;

            var trimmedQuery = query != null ? query.Trim() : string.Empty;
            if (trimmedQuery.Length == 0) return true;

            return ContainsIgnoreCase(item.displayName, trimmedQuery)
                   || ContainsIgnoreCase(item.itemId, trimmedQuery)
                   || ContainsIgnoreCase(item.name, trimmedQuery);
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value)
                   && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int CompareStacksForDisplay(ItemStack a, ItemStack b)
        {
            return InventoryProjectionService.CompareStacksForDisplay(a, b, _equippedItemsLookup, ResolveItemName);
        }

        private bool IsItemEquipped(ItemDefinition item)
        {
            if (item == null || _currentEquipmentSystem == null) return false;

            return InventoryProjectionService.IsEquipped(item, _currentEquipmentSystem.EquippedItems);
        }

        private static void ApplyFilledSlot(SlotView view, ItemStack stack, bool isEquipped)
        {
            if (view == null) return;

            if (view.Frame != null)
                view.Frame.color = isEquipped
                    ? new Color(0.14f, 0.24f, 0.18f, 1f)
                    : new Color(0.19f, 0.19f, 0.16f, 1f);

            var item = stack.item;
            var itemName = ResolveItemName(item);
            var iconSprite = item != null ? item.inventoryIcon : null;

            if (view.Icon != null)
            {
                view.Icon.sprite = iconSprite;
                view.Icon.color = Color.white;
                view.Icon.enabled = iconSprite != null;
            }

            if (view.Initial != null)
            {
                var showInitial = iconSprite == null;
                view.Initial.gameObject.SetActive(showInitial);
                view.Initial.text = showInitial ? BuildInitial(itemName) : string.Empty;
            }

            if (view.Quantity == null) return;

            view.Quantity.color = isEquipped
                ? new Color(0.73f, 0.96f, 0.79f, 1f)
                : new Color(0.93f, 0.88f, 0.75f, 1f);

            var quantityText = string.Empty;
            if (isEquipped)
                quantityText = stack.quantity > 1 ? $"E x{stack.quantity}" : "E";
            else if (stack.quantity > 1)
                quantityText = $"x{stack.quantity}";

            view.Quantity.text = quantityText;
        }

        private static void ApplyEmptySlot(SlotView view)
        {
            if (view == null) return;

            if (view.Frame != null) view.Frame.color = new Color(0.11f, 0.11f, 0.14f, 1f);

            if (view.Icon != null)
            {
                view.Icon.sprite = null;
                view.Icon.enabled = false;
            }

            if (view.Initial != null)
            {
                view.Initial.text = string.Empty;
                view.Initial.gameObject.SetActive(false);
            }

            if (view.Quantity != null) view.Quantity.text = string.Empty;
        }

        private static string ResolveItemName(ItemDefinition item)
        {
            if (item == null) return "Unknown";

            if (!string.IsNullOrWhiteSpace(item.displayName)) return item.displayName.Trim();

            return !string.IsNullOrWhiteSpace(item.itemId) ? item.itemId.Trim() : item.name;
        }

        private static string BuildInitial(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "?";

            foreach (var character in value)
            {
                if (char.IsLetterOrDigit(character)) return char.ToUpperInvariant(character).ToString();
            }

            return value[..1].ToUpperInvariant();
        }
    }
}
