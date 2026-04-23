using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        [Serializable]
        public struct InventorySlotData
        {
            public string ItemName;
            public Sprite Icon;
            public int Quantity;
            public InventorySlotState State;

            public InventorySlotData(string itemName, Sprite icon, int quantity, InventorySlotState state)
            {
                ItemName = itemName;
                Icon = icon;
                Quantity = Mathf.Max(0, quantity);
                State = state;
            }
        }

        private sealed class SlotView
        {
            public RectTransform Root;
            public Button Button;
            public Image Background;
            public Image Icon;
            public TMP_Text IconInitial;
            public TMP_Text Quantity;
            public bool IsHovered;
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

        private readonly List<InventorySlotData> slots = new List<InventorySlotData>();
        private readonly List<SlotView> slotViews = new List<SlotView>();

        private RectTransform hostRoot;
        private RectTransform gridContent;
        private TMP_FontAsset fontAsset;
        private Sprite panelSprite;
        private Sprite slotSprite;

        private TMP_Text contextText;
        private TMP_Text capacityText;
        private TMP_Text selectedItemText;

        private int selectedIndex = -1;

        public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground, Sprite slotBackground)
        {
            hostRoot = host;
            fontAsset = font;
            panelSprite = panelBackground;
            slotSprite = slotBackground;

            ClearChildren(hostRoot);

            RectTransform header = CreateRect("Header", hostRoot);
            Stretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -6f), new Vector2(-6f, -72f));
            AddImage(header, new Color(0.20f, 0.20f, 0.18f, 0.98f), panelSprite).type = Image.Type.Sliced;

            TMP_Text title = CreateText(header, "INVENTORY", 24f, new Color(0.95f, 0.91f, 0.78f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.45f, 1f), new Vector2(10f, 0f), Vector2.zero);

            contextText = CreateText(header, "Operator: -", 14f, new Color(0.74f, 0.71f, 0.62f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(contextText.rectTransform, new Vector2(0.46f, 0f), new Vector2(0.78f, 1f), Vector2.zero, Vector2.zero);

            capacityText = CreateText(header, "Capacity: 0/30", 14f, new Color(0.85f, 0.70f, 0.40f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            Stretch(capacityText.rectTransform, new Vector2(0.78f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-10f, 0f));

            RectTransform gridFrame = CreateRect("GridFrame", hostRoot);
            Stretch(gridFrame, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 66f), new Vector2(-6f, -78f));
            AddImage(gridFrame, new Color(0.14f, 0.14f, 0.13f, 0.98f), panelSprite).type = Image.Type.Sliced;

            BuildGrid(gridFrame);

            RectTransform footer = CreateRect("Footer", hostRoot);
            Stretch(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 6f), new Vector2(-6f, 62f));
            AddImage(footer, new Color(0.19f, 0.18f, 0.16f, 0.98f), panelSprite).type = Image.Type.Sliced;

            selectedItemText = CreateText(footer, "Select a slot for item details.", 14f, new Color(0.81f, 0.77f, 0.67f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(selectedItemText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-10f, 0f));
        }

        public void SetSlots(IReadOnlyList<InventorySlotData> source)
        {
            slots.Clear();

            int minimumSlots = 30;
            int sourceCount = source != null ? source.Count : 0;
            int visibleCount = Mathf.Max(minimumSlots, sourceCount);

            for (int i = 0; i < visibleCount; i++)
            {
                if (source != null && i < source.Count)
                {
                    slots.Add(source[i]);
                }
                else
                {
                    slots.Add(new InventorySlotData(string.Empty, null, 0, InventorySlotState.Empty));
                }
            }

            RebuildSlots();
            selectedIndex = -1;
            UpdateCapacityText();
            UpdateSelectedItemText();
        }

        public void SetContextSurvivor(string displayName)
        {
            if (contextText != null)
            {
                contextText.text = "Operator: " + (string.IsNullOrWhiteSpace(displayName) ? "-" : displayName);
            }
        }

        private void BuildGrid(RectTransform parent)
        {
            RectTransform scrollRoot = CreateRect("ScrollView", parent);
            Stretch(scrollRoot, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            AddImage(scrollRoot, new Color(0.11f, 0.11f, 0.10f, 1f), null);

            ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.08f;
            scrollRect.scrollSensitivity = 30f;

            RectTransform viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Image viewportImage = AddImage(viewport, new Color(0f, 0f, 0f, 0.07f), null);
            viewportImage.maskable = true;

            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            gridContent = CreateRect("GridContent", viewport);
            gridContent.anchorMin = new Vector2(0f, 1f);
            gridContent.anchorMax = new Vector2(1f, 1f);
            gridContent.pivot = new Vector2(0.5f, 1f);
            gridContent.offsetMin = new Vector2(0f, 0f);
            gridContent.offsetMax = new Vector2(0f, 0f);

            GridLayoutGroup grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(100f, 100f);
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(2, 2, 2, 2);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;

            ContentSizeFitter fitter = gridContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = gridContent;
        }

        private void RebuildSlots()
        {
            slotViews.Clear();
            if (gridContent == null)
            {
                return;
            }

            ClearChildren(gridContent);

            for (int i = 0; i < slots.Count; i++)
            {
                int captured = i;
                SlotView view = BuildSlotView(gridContent, slots[i]);
                view.Button.onClick.AddListener(() => SelectSlot(captured));

                HoverRelay relay = view.Root.gameObject.AddComponent<HoverRelay>();
                relay.HoverChanged += hovered =>
                {
                    view.IsHovered = hovered;
                    ApplySlotVisual(captured, view);
                };

                slotViews.Add(view);
            }

            for (int i = 0; i < slotViews.Count; i++)
            {
                ApplySlotVisual(i, slotViews[i]);
            }
        }

        private SlotView BuildSlotView(RectTransform parent, InventorySlotData data)
        {
            SlotView view = new SlotView();
            view.Root = CreateRect("Slot", parent);
            view.Background = AddImage(view.Root, new Color(0.22f, 0.21f, 0.18f, 1f), slotSprite);
            view.Background.type = Image.Type.Sliced;

            view.Button = view.Root.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = view.Background;
            view.Button.transition = Selectable.Transition.None;

            RectTransform iconRect = CreateRect("Icon", view.Root);
            Stretch(iconRect, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -22f));
            view.Icon = AddImage(iconRect, data.Icon != null ? Color.white : new Color(0.26f, 0.29f, 0.26f, 1f), data.Icon);
            view.Icon.raycastTarget = false;

            view.IconInitial = CreateText(iconRect, BuildInitial(data.ItemName), 22f, new Color(0.84f, 0.79f, 0.67f, 0.84f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(view.IconInitial.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            view.IconInitial.gameObject.SetActive(data.Icon == null && data.State != InventorySlotState.Empty);

            RectTransform qtyBadge = CreateRect("QuantityBadge", view.Root);
            Stretch(qtyBadge, new Vector2(0.56f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(-4f, 18f));
            Image qtyBadgeImage = AddImage(qtyBadge, new Color(0.10f, 0.10f, 0.09f, 0.95f), panelSprite);
            qtyBadgeImage.type = Image.Type.Sliced;
            qtyBadgeImage.raycastTarget = false;

            view.Quantity = CreateText(qtyBadge, data.Quantity > 1 ? "x" + data.Quantity : string.Empty, 11f, new Color(0.93f, 0.88f, 0.75f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(view.Quantity.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return view;
        }

        private void SelectSlot(int index)
        {
            if (slots.Count == 0)
            {
                selectedIndex = -1;
                UpdateSelectedItemText();
                return;
            }

            selectedIndex = Mathf.Clamp(index, 0, slots.Count - 1);
            for (int i = 0; i < slotViews.Count; i++)
            {
                ApplySlotVisual(i, slotViews[i]);
            }

            UpdateSelectedItemText();
        }

        private void UpdateCapacityText()
        {
            int used = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].State != InventorySlotState.Empty)
                {
                    used++;
                }
            }

            if (capacityText != null)
            {
                capacityText.text = "Capacity: " + used + "/" + slots.Count;
            }
        }

        private void UpdateSelectedItemText()
        {
            if (selectedItemText == null)
            {
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= slots.Count)
            {
                selectedItemText.text = "Select a slot for item details.";
                return;
            }

            InventorySlotData slot = slots[selectedIndex];
            if (slot.State == InventorySlotState.Empty)
            {
                selectedItemText.text = "Empty slot - assign equipment or consumables.";
                return;
            }

            string quantity = slot.Quantity > 0 ? " | Qty: " + slot.Quantity : string.Empty;
            selectedItemText.text = slot.ItemName + " | State: " + slot.State + quantity;
        }

        private void ApplySlotVisual(int index, SlotView view)
        {
            if (index < 0 || index >= slots.Count)
            {
                return;
            }

            bool selected = index == selectedIndex;
            InventorySlotData data = slots[index];

            Color baseColor;
            Color iconTint;
            switch (data.State)
            {
                case InventorySlotState.Empty:
                    baseColor = new Color(0.15f, 0.15f, 0.14f, 0.98f);
                    iconTint = new Color(0.25f, 0.25f, 0.23f, 1f);
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

            if (view.IsHovered)
            {
                Color hoverColor = data.State == InventorySlotState.Empty
                    ? new Color(0.52f, 0.42f, 0.27f, 1f)
                    : new Color(0.68f, 0.53f, 0.30f, 1f);
                float hoverBlend = data.State == InventorySlotState.Empty ? 0.24f : 0.34f;
                baseColor = Color.Lerp(baseColor, hoverColor, hoverBlend);
            }

            if (selected)
            {
                baseColor = new Color(0.50f, 0.36f, 0.18f, 1f);
            }

            if (view.Background != null)
            {
                view.Background.color = baseColor;
            }

            if (view.Icon != null)
            {
                view.Icon.color = data.Icon != null ? iconTint : new Color(0.27f, 0.30f, 0.26f, 1f);
                view.Icon.sprite = data.Icon;
            }

            if (view.IconInitial != null)
            {
                bool showInitial = data.Icon == null && data.State != InventorySlotState.Empty;
                view.IconInitial.gameObject.SetActive(showInitial);
                view.IconInitial.text = BuildInitial(data.ItemName);
            }

            if (view.Quantity != null)
            {
                view.Quantity.text = data.Quantity > 1 ? "x" + data.Quantity : string.Empty;
                view.Quantity.color = selected
                    ? new Color(1f, 0.95f, 0.80f, 1f)
                    : new Color(0.88f, 0.84f, 0.72f, 1f);
            }
        }

        private TMP_Text CreateText(
            RectTransform parent,
            string value,
            float size,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect("Text", parent);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = fontAsset;
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
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Image AddImage(RectTransform rect, Color color, Sprite sprite)
        {
            Image image = rect.gameObject.AddComponent<Image>();
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
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return "?";
            }

            return itemName.Substring(0, 1).ToUpperInvariant();
        }

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
