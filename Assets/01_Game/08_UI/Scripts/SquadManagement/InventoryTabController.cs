#region

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed class InventoryTabController : MonoBehaviour
    {
        public enum InventorySlotState
        {
            Empty,
            Occupied,
            Equipped,
            Damaged
        }

        private readonly List<InventorySlotData> _slots = new();
        private readonly List<SlotView> _slotViews = new();
        private TMP_Text _capacityText;

        private TMP_Text _contextText;
        private TMP_FontAsset _fontAsset;
        private RectTransform _gridContent;

        private RectTransform _hostRoot;
        private Sprite _panelSprite;

        private int _selectedIndex = -1;
        private TMP_Text _selectedItemText;
        private Sprite _slotSprite;

        public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground, Sprite slotBackground)
        {
            if (host == null || font == null) return;

            _hostRoot = host;
            _fontAsset = font;
            _panelSprite = panelBackground;
            _slotSprite = slotBackground;

            ClearChildren(_hostRoot);

            var header = CreateRect("Header", _hostRoot);
            Stretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -6f), new Vector2(-6f, -72f));
            AddImage(header, new Color(0.20f, 0.20f, 0.18f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            var title = CreateText(header, "INVENTORY", 24f, new Color(0.95f, 0.91f, 0.78f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.45f, 1f), new Vector2(10f, 0f),
                Vector2.zero);

            _contextText = CreateText(header, "Operator: -", 14f, new Color(0.74f, 0.71f, 0.62f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            Stretch(_contextText.rectTransform, new Vector2(0.46f, 0f), new Vector2(0.78f, 1f), Vector2.zero,
                Vector2.zero);

            _capacityText = CreateText(header, "Capacity: 0/30", 14f, new Color(0.85f, 0.70f, 0.40f, 1f),
                FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            Stretch(_capacityText.rectTransform, new Vector2(0.78f, 0f), new Vector2(1f, 1f), Vector2.zero,
                new Vector2(-10f, 0f));

            var gridFrame = CreateRect("GridFrame", _hostRoot);
            Stretch(gridFrame, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 66f), new Vector2(-6f, -78f));
            AddImage(gridFrame, new Color(0.14f, 0.14f, 0.13f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            BuildGrid(gridFrame);

            var footer = CreateRect("Footer", _hostRoot);
            Stretch(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 6f), new Vector2(-6f, 62f));
            AddImage(footer, new Color(0.19f, 0.18f, 0.16f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            _selectedItemText = CreateText(footer, "Select a slot for item details.", 14f,
                new Color(0.81f, 0.77f, 0.67f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(_selectedItemText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 0f),
                new Vector2(-10f, 0f));
        }

        public void SetSlots(IReadOnlyList<InventorySlotData> source)
        {
            if (_hostRoot == null) return;

            _slots.Clear();

            const int minimumSlots = 30;
            var safeSource = source ?? Array.Empty<InventorySlotData>();
            var sourceCount = safeSource.Count;
            var visibleCount = Mathf.Max(minimumSlots, sourceCount);

            for (var i = 0; i < visibleCount; i++)
                _slots.Add(i < sourceCount
                    ? safeSource[i]
                    : new InventorySlotData(string.Empty, null, 0, InventorySlotState.Empty));

            RebuildSlots();
            _selectedIndex = -1;
            UpdateCapacityText();
            UpdateSelectedItemText();
        }

        public void SetContextSurvivor(string displayName)
        {
            if (_contextText == null) return;

            _contextText.text = "Operator: " + (string.IsNullOrWhiteSpace(displayName) ? "-" : displayName);
        }

        private void BuildGrid(RectTransform parent)
        {
            var scrollRoot = CreateRect("ScrollView", parent);
            Stretch(scrollRoot, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            AddImage(scrollRoot, new Color(0.11f, 0.11f, 0.10f, 1f), null);

            var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.08f;
            scrollRect.scrollSensitivity = 30f;

            var viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var viewportImage = AddImage(viewport, new Color(0f, 0f, 0f, 0.07f), null);
            viewportImage.maskable = true;

            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _gridContent = CreateRect("GridContent", viewport);
            _gridContent.anchorMin = new Vector2(0f, 1f);
            _gridContent.anchorMax = new Vector2(1f, 1f);
            _gridContent.pivot = new Vector2(0.5f, 1f);
            _gridContent.offsetMin = new Vector2(0f, 0f);
            _gridContent.offsetMax = new Vector2(0f, 0f);

            var grid = _gridContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(100f, 100f);
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(2, 2, 2, 2);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;

            var fitter = _gridContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = _gridContent;
        }

        private void RebuildSlots()
        {
            _slotViews.Clear();
            if (_gridContent == null) return;

            ClearChildren(_gridContent);

            for (var i = 0; i < _slots.Count; i++)
            {
                var captured = i;
                var view = BuildSlotView(_gridContent, _slots[i]);
                view.Button.onClick.AddListener(() => SelectSlot(captured));

                var relay = view.Root.gameObject.AddComponent<HoverRelay>();
                relay.HoverChanged += hovered =>
                {
                    view.IsHovered = hovered;
                    ApplySlotVisual(captured, view);
                };

                _slotViews.Add(view);
            }

            for (var i = 0; i < _slotViews.Count; i++) ApplySlotVisual(i, _slotViews[i]);
        }

        private SlotView BuildSlotView(RectTransform parent, InventorySlotData data)
        {
            var view = new SlotView { Root = CreateRect("Slot", parent) };
            view.Background = AddImage(view.Root, new Color(0.22f, 0.21f, 0.18f, 1f), _slotSprite);
            view.Background.type = Image.Type.Sliced;

            view.Button = view.Root.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = view.Background;
            view.Button.transition = Selectable.Transition.None;

            var iconRect = CreateRect("Icon", view.Root);
            Stretch(iconRect, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -22f));
            view.Icon = AddImage(iconRect, data.icon != null ? Color.white : new Color(0.26f, 0.29f, 0.26f, 1f),
                data.icon);
            view.Icon.raycastTarget = false;

            view.IconInitial = CreateText(iconRect, BuildInitial(data.itemName), 22f,
                new Color(0.84f, 0.79f, 0.67f, 0.84f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(view.IconInitial.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            view.IconInitial.gameObject.SetActive(data.icon == null && data.state != InventorySlotState.Empty);

            var qtyBadge = CreateRect("QuantityBadge", view.Root);
            Stretch(qtyBadge, new Vector2(0.56f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(-4f, 18f));
            var qtyBadgeImage = AddImage(qtyBadge, new Color(0.10f, 0.10f, 0.09f, 0.95f), _panelSprite);
            qtyBadgeImage.type = Image.Type.Sliced;
            qtyBadgeImage.raycastTarget = false;

            view.Quantity = CreateText(qtyBadge, data.quantity > 1 ? "x" + data.quantity : string.Empty, 11f,
                new Color(0.93f, 0.88f, 0.75f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(view.Quantity.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return view;
        }

        private void SelectSlot(int index)
        {
            if (_slots.Count == 0)
            {
                _selectedIndex = -1;
                UpdateSelectedItemText();
                return;
            }

            _selectedIndex = Mathf.Clamp(index, 0, _slots.Count - 1);
            for (var i = 0; i < _slotViews.Count; i++) ApplySlotVisual(i, _slotViews[i]);

            UpdateSelectedItemText();
        }

        private void UpdateCapacityText()
        {
            var used = 0;
            for (var i = 0; i < _slots.Count; i++)
                if (_slots[i].state != InventorySlotState.Empty)
                    used++;

            if (_capacityText != null) _capacityText.text = "Capacity: " + used + "/" + _slots.Count;
        }

        private void UpdateSelectedItemText()
        {
            if (_selectedItemText == null) return;

            if (_selectedIndex < 0 || _selectedIndex >= _slots.Count)
            {
                _selectedItemText.text = "Select a slot for item details.";
                return;
            }

            var slot = _slots[_selectedIndex];
            if (slot.state == InventorySlotState.Empty)
            {
                _selectedItemText.text = "Empty slot - assign equipment or consumables.";
                return;
            }

            var quantity = slot.quantity > 0 ? " | Qty: " + slot.quantity : string.Empty;
            _selectedItemText.text = slot.itemName + " | State: " + slot.state + quantity;
        }

        private void ApplySlotVisual(int index, SlotView view)
        {
            if (index < 0 || index >= _slots.Count) return;

            var selected = index == _selectedIndex;
            var data = _slots[index];

            ResolveSlotColors(data.state, out var baseColor, out var iconTint);
            var resolvedBackground = ResolveSlotBackgroundColor(data.state, baseColor, view.IsHovered, selected);

            ApplySlotBackground(view, resolvedBackground);
            ApplySlotIcon(view, data, iconTint);
            ApplySlotInitial(view, data);
            ApplySlotQuantity(view, data, selected);
        }

        private static void ResolveSlotColors(InventorySlotState state, out Color baseColor, out Color iconTint)
        {
            switch (state)
            {
                case InventorySlotState.Empty:
                    baseColor = new Color(0.15f, 0.15f, 0.14f, 0.98f);
                    iconTint = new Color(0.25f, 0.25f, 0.23f, 1f);
                    break;
                case InventorySlotState.Occupied:
                    baseColor = new Color(0.24f, 0.22f, 0.18f, 0.98f);
                    iconTint = Color.white;
                    break;
                case InventorySlotState.Equipped:
                    baseColor = new Color(0.28f, 0.32f, 0.20f, 0.98f);
                    iconTint = Color.white;
                    break;
                case InventorySlotState.Damaged:
                    baseColor = new Color(0.35f, 0.20f, 0.16f, 0.98f);
                    iconTint = new Color(1f, 0.89f, 0.85f, 1f);
                    break;
                default:
                    baseColor = new Color(0.24f, 0.22f, 0.18f, 0.98f);
                    iconTint = Color.white;
                    break;
            }
        }

        private static Color ResolveSlotBackgroundColor(InventorySlotState state, Color baseColor, bool isHovered,
            bool selected)
        {
            if (!isHovered)
                return selected
                    ? new Color(0.50f, 0.36f, 0.18f, 1f)
                    : baseColor;

            var hoverColor = state == InventorySlotState.Empty
                ? new Color(0.52f, 0.42f, 0.27f, 1f)
                : new Color(0.68f, 0.53f, 0.30f, 1f);
            var hoverBlend = state == InventorySlotState.Empty ? 0.24f : 0.34f;
            baseColor = Color.Lerp(baseColor, hoverColor, hoverBlend);

            return selected
                ? new Color(0.50f, 0.36f, 0.18f, 1f)
                : baseColor;
        }

        private static void ApplySlotBackground(SlotView view, Color color)
        {
            if (view.Background != null) view.Background.color = color;
        }

        private static void ApplySlotIcon(SlotView view, InventorySlotData data, Color iconTint)
        {
            if (view.Icon == null) return;

            view.Icon.color = data.icon != null ? iconTint : new Color(0.27f, 0.30f, 0.26f, 1f);
            view.Icon.sprite = data.icon;
        }

        private static void ApplySlotInitial(SlotView view, InventorySlotData data)
        {
            if (view.IconInitial == null) return;

            var showInitial = data.icon == null && data.state != InventorySlotState.Empty;
            view.IconInitial.gameObject.SetActive(showInitial);
            view.IconInitial.text = BuildInitial(data.itemName);
        }

        private static void ApplySlotQuantity(SlotView view, InventorySlotData data, bool selected)
        {
            if (view.Quantity == null) return;

            view.Quantity.text = data.quantity > 1 ? "x" + data.quantity : string.Empty;
            view.Quantity.color = selected
                ? new Color(1f, 0.95f, 0.80f, 1f)
                : new Color(0.88f, 0.84f, 0.72f, 1f);
        }

        private TMP_Text CreateText(
            RectTransform parent,
            string value,
            float size,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            var rect = CreateRect("Text", parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = _fontAsset;
            tmp.text = value;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Image AddImage(RectTransform rect, Color color, Sprite sprite)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            return image;
        }

        private static void Stretch(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static string BuildInitial(string itemName)
        {
            return string.IsNullOrWhiteSpace(itemName)
                ? "?"
                : itemName[..1].ToUpperInvariant();
        }

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null) return;

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        [Serializable]
        public struct InventorySlotData
        {
            public string itemName;
            public Sprite icon;
            public int quantity;
            public InventorySlotState state;

            public InventorySlotData(string itemName, Sprite icon, int quantity, InventorySlotState state)
            {
                this.itemName = itemName;
                this.icon = icon;
                this.quantity = Mathf.Max(0, quantity);
                this.state = state;
            }
        }

        private sealed class SlotView
        {
            public Image Background;
            public Button Button;
            public Image Icon;
            public TMP_Text IconInitial;
            public bool IsHovered;
            public TMP_Text Quantity;
            public RectTransform Root;
        }

        private sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action<bool> HoverChanged;

            public void OnPointerEnter(PointerEventData eventData)
            {
                HoverChanged?.Invoke(true);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                HoverChanged?.Invoke(false);
            }
        }
    }
}