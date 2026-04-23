using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Inventory;

namespace Zombera.UI
{
    [AddComponentMenu("Zombera/UI/Inventory Panel Controller")]
    [DisallowMultipleComponent]
    public sealed class InventoryPanelController : MonoBehaviour
    {
        private enum InventoryFilter
        {
            All,
            Weapons,
            Ammo,
            Medical,
            Consumables,
            Materials,
            Other
        }

        [SerializeField] private InventoryEditTargetController editTargetController;
        [SerializeField] private TMP_InputField searchInputField;
        [SerializeField] private Button filterButton;
        [SerializeField] private TMP_Text filterLabel;
        [SerializeField] private Transform slotGrid;
        [SerializeField] private bool logBindingSummary;

        private sealed class SlotView
        {
            public RectTransform Root;
            public Image Frame;
            public Image Icon;
            public TMP_Text Initial;
            public TMP_Text Quantity;
            public InventorySlotInteraction Interaction;
        }

        private sealed class EquipmentSlotView
        {
            public EquipmentSlot Slot;
            public RectTransform Root;
            public Image Frame;
            public Image Icon;
            public TMP_Text ItemHint;
            public string DefaultHint;
            public Color BaseColor;
            public EquipmentDropTarget DropTarget;
        }

        private readonly List<SlotView> slotViews = new List<SlotView>(64);
        private readonly List<ItemStack> filteredStacks = new List<ItemStack>(64);
        private readonly Dictionary<EquipmentSlot, EquipmentSlotView> equipmentSlotViews = new Dictionary<EquipmentSlot, EquipmentSlotView>();

        private InventoryFilter currentFilter = InventoryFilter.All;
        private Unit currentUnit;
        private UnitInventory currentInventory;
        private EquipmentSystem currentEquipmentSystem;
        private EquipmentSystem subscribedEquipmentSystem;
        private Canvas rootCanvas;
        private RectTransform rootCanvasRect;

        private bool isDraggingItem;
        private int draggingSlotIndex = -1;
        private ItemStack draggingStack;
        private RectTransform dragGhostRoot;
        private Image dragGhostIcon;
        private TMP_Text dragGhostInitial;

        private RectTransform contextMenuRoot;
        private TMP_Text contextMenuTitle;
        private Button contextEquipButton;
        private Button contextDropButton;
        private int contextSlotIndex = -1;
        private float suppressDropUntilRealtime;

        private bool uiHooked;

        internal bool IsDraggingItem => isDraggingItem;

        private void Awake()
        {
            ResolveReferences();
            RebuildSlotViews();
            ResolveEquipmentSlotViews();
            EnsureContextMenu();
            UpdateFilterLabel();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RebuildSlotViews();
            ResolveEquipmentSlotViews();
            HookUi();
            HookEditTarget();

            if (editTargetController != null)
            {
                editTargetController.RefreshAndSelectDefault();
                HandleEditingUnitChanged(editTargetController.EditingUnit);
            }
            else
            {
                HandleEditingUnitChanged(null);
            }

            RefreshSlots();
        }

        private void OnDisable()
        {
            UnhookUi();
            UnhookEditTarget();
            UnhookInventory();
            UnhookEquipment();
            EndItemDrag();
            HideContextMenu();
            ResetEquipmentDropTargetVisuals();
        }

        private void ResolveReferences()
        {
            if (editTargetController == null)
            {
                editTargetController = GetComponent<InventoryEditTargetController>();
            }

            if (searchInputField == null)
            {
                Transform searchTransform = transform.Find("InventoryGrid/InventoryControls/SearchBar");
                if (searchTransform != null)
                {
                    searchInputField = searchTransform.GetComponent<TMP_InputField>();
                }
            }

            if (filterButton == null)
            {
                Transform filterTransform = transform.Find("InventoryGrid/InventoryControls/FilterButton");
                if (filterTransform != null)
                {
                    filterButton = filterTransform.GetComponent<Button>();
                }
            }

            if (filterLabel == null)
            {
                Transform labelTransform = transform.Find("InventoryGrid/InventoryControls/FilterButton/Label");
                if (labelTransform != null)
                {
                    filterLabel = labelTransform.GetComponent<TMP_Text>();
                }

                if (filterLabel == null && filterButton != null)
                {
                    filterLabel = filterButton.GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (slotGrid == null)
            {
                Transform slotGridTransform = transform.Find("InventoryGrid/Viewport/SlotGrid");
                if (slotGridTransform != null)
                {
                    slotGrid = slotGridTransform;
                }
            }

            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>();
            }

            if (rootCanvasRect == null && rootCanvas != null)
            {
                rootCanvasRect = rootCanvas.transform as RectTransform;
            }
        }

        private void HookUi()
        {
            if (uiHooked)
            {
                return;
            }

            if (searchInputField != null)
            {
                searchInputField.onValueChanged.AddListener(HandleSearchInputChanged);
            }

            if (filterButton != null)
            {
                filterButton.onClick.AddListener(HandleFilterButtonClicked);
            }

            uiHooked = true;
        }

        private void UnhookUi()
        {
            if (!uiHooked)
            {
                return;
            }

            if (searchInputField != null)
            {
                searchInputField.onValueChanged.RemoveListener(HandleSearchInputChanged);
            }

            if (filterButton != null)
            {
                filterButton.onClick.RemoveListener(HandleFilterButtonClicked);
            }

            uiHooked = false;
        }

        private void HookEditTarget()
        {
            if (editTargetController != null)
            {
                editTargetController.OnEditingUnitChanged += HandleEditingUnitChanged;
            }
        }

        private void UnhookEditTarget()
        {
            if (editTargetController != null)
            {
                editTargetController.OnEditingUnitChanged -= HandleEditingUnitChanged;
            }
        }

        private void HandleEditingUnitChanged(Unit unit)
        {
            currentUnit = unit;
            currentEquipmentSystem = FindEquipmentSystemOnUnit(unit);
            HookEquipment();
            HideContextMenu();

            UnitInventory resolvedInventory = null;
            if (unit != null)
            {
                resolvedInventory = unit.Inventory != null
                    ? unit.Inventory
                    : unit.GetComponent<UnitInventory>();
            }

            if (resolvedInventory == currentInventory)
            {
                RefreshSlots();
                RefreshEquipmentViews();
                return;
            }

            UnhookInventory();
            currentInventory = resolvedInventory;

            if (currentInventory != null)
            {
                currentInventory.OnInventoryChanged += HandleInventoryChanged;
            }

            RefreshSlots();
            RefreshEquipmentViews();
        }

        private void HandleInventoryChanged()
        {
            RefreshSlots();
        }

        private void HandleEquipmentChanged()
        {
            RefreshSlots();
            RefreshEquipmentViews();
        }

        private void UnhookInventory()
        {
            if (currentInventory != null)
            {
                currentInventory.OnInventoryChanged -= HandleInventoryChanged;
                currentInventory = null;
            }
        }

        private void HookEquipment()
        {
            if (currentEquipmentSystem == subscribedEquipmentSystem)
            {
                return;
            }

            UnhookEquipment();

            if (currentEquipmentSystem != null)
            {
                currentEquipmentSystem.OnEquipmentChanged += HandleEquipmentChanged;
                subscribedEquipmentSystem = currentEquipmentSystem;
            }
        }

        private void UnhookEquipment()
        {
            if (subscribedEquipmentSystem != null)
            {
                subscribedEquipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;
                subscribedEquipmentSystem = null;
            }
        }

        private void HandleSearchInputChanged(string _)
        {
            RefreshSlots();
        }

        private void HandleFilterButtonClicked()
        {
            int nextFilter = ((int)currentFilter + 1) % Enum.GetValues(typeof(InventoryFilter)).Length;
            currentFilter = (InventoryFilter)nextFilter;
            UpdateFilterLabel();
            RefreshSlots();
        }

        private void UpdateFilterLabel()
        {
            if (filterLabel == null)
            {
                return;
            }

            filterLabel.text = currentFilter switch
            {
                InventoryFilter.All => "FILTER: ALL",
                InventoryFilter.Weapons => "FILTER: WEAPONS",
                InventoryFilter.Ammo => "FILTER: AMMO",
                InventoryFilter.Medical => "FILTER: MEDICAL",
                InventoryFilter.Consumables => "FILTER: CONSUMABLES",
                InventoryFilter.Materials => "FILTER: MATERIALS",
                _ => "FILTER: OTHER",
            };
        }

        private void RebuildSlotViews()
        {
            slotViews.Clear();

            if (slotGrid == null)
            {
                return;
            }

            for (int i = 0; i < slotGrid.childCount; i++)
            {
                Transform slotTransform = slotGrid.GetChild(i);
                if (slotTransform == null)
                {
                    continue;
                }

                Image frame = slotTransform.GetComponent<Image>();
                if (frame == null)
                {
                    continue;
                }

                SlotView view = new SlotView
                {
                    Root = slotTransform as RectTransform,
                    Frame = frame,
                    Icon = EnsureSlotIcon(slotTransform),
                    Initial = EnsureSlotInitial(slotTransform),
                    Quantity = EnsureSlotQuantity(slotTransform),
                    Interaction = EnsureSlotInteraction(slotTransform, slotViews.Count),
                };

                slotViews.Add(view);
            }
        }

        private static InventorySlotInteraction EnsureSlotInteraction(Transform slotTransform, int slotIndex)
        {
            InventorySlotInteraction interaction = slotTransform.GetComponent<InventorySlotInteraction>();
            if (interaction == null)
            {
                interaction = slotTransform.gameObject.AddComponent<InventorySlotInteraction>();
            }

            InventoryPanelController owner = slotTransform.GetComponentInParent<InventoryPanelController>();
            interaction.Configure(owner, slotIndex);
            return interaction;
        }

        private void ResolveEquipmentSlotViews()
        {
            equipmentSlotViews.Clear();

            Transform equipmentPanel = transform.Find("EquipmentPanel");
            if (equipmentPanel == null)
            {
                return;
            }

            Button[] candidateButtons = equipmentPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < candidateButtons.Length; i++)
            {
                Button button = candidateButtons[i];
                if (button == null || button.transform == null)
                {
                    continue;
                }

                if (!button.name.StartsWith("Slot_", StringComparison.Ordinal))
                {
                    continue;
                }

                string slotName = button.name.Substring("Slot_".Length);
                if (!Enum.TryParse(slotName, true, out EquipmentSlot slot))
                {
                    continue;
                }

                Transform iconTransform = button.transform.Find("Icon");
                Transform hintTransform = button.transform.Find("ItemHint");

                Image frame = button.GetComponent<Image>();
                Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
                TMP_Text hint = hintTransform != null ? hintTransform.GetComponent<TMP_Text>() : null;

                EquipmentDropTarget dropTarget = button.GetComponent<EquipmentDropTarget>();
                if (dropTarget == null)
                {
                    dropTarget = button.gameObject.AddComponent<EquipmentDropTarget>();
                }

                dropTarget.Configure(this, slot, frame);

                EquipmentSlotView view = new EquipmentSlotView
                {
                    Slot = slot,
                    Root = button.transform as RectTransform,
                    Frame = frame,
                    Icon = icon,
                    ItemHint = hint,
                    DefaultHint = hint != null ? hint.text : string.Empty,
                    BaseColor = frame != null ? frame.color : Color.white,
                    DropTarget = dropTarget,
                };

                equipmentSlotViews[slot] = view;
            }
        }

        private static Image EnsureSlotIcon(Transform slotTransform)
        {
            Transform iconTransform = slotTransform.Find("Icon");
            RectTransform iconRect;

            if (iconTransform == null)
            {
                GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(slotTransform, false);
                iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(3f, 3f);
                iconRect.offsetMax = new Vector2(-3f, -3f);
            }
            else
            {
                iconRect = iconTransform as RectTransform;
            }

            Image icon = iconRect.GetComponent<Image>();
            if (icon == null)
            {
                icon = iconRect.gameObject.AddComponent<Image>();
            }

            icon.raycastTarget = false;
            icon.preserveAspect = true;
            return icon;
        }

        private static TMP_Text EnsureSlotInitial(Transform slotTransform)
        {
            Transform initialTransform = slotTransform.Find("Initial");
            RectTransform initialRect;

            if (initialTransform == null)
            {
                GameObject initialGo = new GameObject("Initial", typeof(RectTransform), typeof(TextMeshProUGUI));
                initialGo.transform.SetParent(slotTransform, false);
                initialRect = initialGo.GetComponent<RectTransform>();
                initialRect.anchorMin = Vector2.zero;
                initialRect.anchorMax = Vector2.one;
                initialRect.offsetMin = Vector2.zero;
                initialRect.offsetMax = Vector2.zero;
            }
            else
            {
                initialRect = initialTransform as RectTransform;
            }

            TMP_Text initial = initialRect.GetComponent<TMP_Text>();
            if (initial == null)
            {
                initial = initialRect.gameObject.AddComponent<TextMeshProUGUI>();
            }

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
            Transform quantityTransform = slotTransform.Find("Quantity");
            RectTransform quantityRect;

            if (quantityTransform == null)
            {
                GameObject quantityGo = new GameObject("Quantity", typeof(RectTransform), typeof(TextMeshProUGUI));
                quantityGo.transform.SetParent(slotTransform, false);
                quantityRect = quantityGo.GetComponent<RectTransform>();
                quantityRect.anchorMin = new Vector2(0.56f, 0f);
                quantityRect.anchorMax = new Vector2(1f, 0.35f);
                quantityRect.offsetMin = Vector2.zero;
                quantityRect.offsetMax = new Vector2(-2f, 2f);
            }
            else
            {
                quantityRect = quantityTransform as RectTransform;
            }

            TMP_Text quantity = quantityRect.GetComponent<TMP_Text>();
            if (quantity == null)
            {
                quantity = quantityRect.gameObject.AddComponent<TextMeshProUGUI>();
            }

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
            if (slotViews.Count == 0)
            {
                RebuildSlotViews();
            }

            if (equipmentSlotViews.Count == 0)
            {
                ResolveEquipmentSlotViews();
            }

            filteredStacks.Clear();

            if (currentInventory != null)
            {
                string query = searchInputField != null
                    ? searchInputField.text
                    : string.Empty;

                for (int i = 0; i < currentInventory.Items.Count; i++)
                {
                    ItemStack stack = currentInventory.Items[i];
                    if (stack.item == null || stack.quantity <= 0)
                    {
                        continue;
                    }

                    if (!MatchesFilter(stack.item) || !MatchesSearch(stack.item, query))
                    {
                        continue;
                    }

                    filteredStacks.Add(stack);
                }
            }

            if (filteredStacks.Count > 1)
            {
                filteredStacks.Sort(CompareStacksForDisplay);
            }

            for (int i = 0; i < slotViews.Count; i++)
            {
                SlotView view = slotViews[i];
                if (i < filteredStacks.Count)
                {
                    bool isEquipped = IsItemEquipped(filteredStacks[i].item);
                    ApplyFilledSlot(view, filteredStacks[i], isEquipped);
                }
                else
                {
                    ApplyEmptySlot(view);
                }
            }

            if (contextMenuRoot != null && contextMenuRoot.gameObject.activeSelf)
            {
                if (!TryGetVisibleStackAtIndex(contextSlotIndex, out _))
                {
                    HideContextMenu();
                }
            }

            RefreshEquipmentViews();

            if (logBindingSummary)
            {
                string unitName = currentUnit != null ? currentUnit.gameObject.name : "None";
                Debug.Log($"[InventoryPanelController] Unit={unitName}, VisibleItems={filteredStacks.Count}, Slots={slotViews.Count}, Filter={currentFilter}.", this);
            }
        }

        private void RefreshEquipmentViews()
        {
            EquipmentSystem equipmentSystem = ResolveEquipmentSystem();

            foreach (KeyValuePair<EquipmentSlot, EquipmentSlotView> pair in equipmentSlotViews)
            {
                EquipmentSlotView view = pair.Value;
                if (view == null)
                {
                    continue;
                }

                ItemDefinition equippedItem = equipmentSystem != null
                    ? equipmentSystem.GetEquippedItem(view.Slot)
                    : null;

                if (view.Icon != null)
                {
                    Sprite iconSprite = equippedItem != null ? equippedItem.inventoryIcon : null;
                    view.Icon.sprite = iconSprite;
                    view.Icon.color = Color.white;
                    view.Icon.enabled = iconSprite != null;
                }

                if (view.ItemHint != null)
                {
                    view.ItemHint.text = equippedItem != null
                        ? ResolveItemName(equippedItem)
                        : view.DefaultHint;
                }

                view.DropTarget?.ResetVisual();
            }
        }

        private EquipmentSystem ResolveEquipmentSystem()
        {
            if (currentUnit == null)
            {
                currentEquipmentSystem = null;
                UnhookEquipment();
                return null;
            }

            if (currentEquipmentSystem == null)
            {
                currentEquipmentSystem = FindEquipmentSystemOnUnit(currentUnit);
                HookEquipment();
            }

            if (currentEquipmentSystem == null)
            {
                WeaponSystem weaponSystem = FindWeaponSystemOnUnit(currentUnit);
                if (weaponSystem == null)
                {
                    weaponSystem = currentUnit.gameObject.AddComponent<WeaponSystem>();
                }

                currentEquipmentSystem = currentUnit.gameObject.AddComponent<EquipmentSystem>();
                HookEquipment();
                Debug.Log($"[InventoryPanelController] Auto-added missing EquipmentSystem to '{currentUnit.name}' for context-menu equip.", currentUnit);
            }

            return currentEquipmentSystem;
        }

        private static EquipmentSystem FindEquipmentSystemOnUnit(Unit unit)
        {
            if (unit == null)
            {
                return null;
            }

            EquipmentSystem equipmentSystem = unit.GetComponent<EquipmentSystem>();
            if (equipmentSystem != null)
            {
                return equipmentSystem;
            }

            equipmentSystem = unit.GetComponentInChildren<EquipmentSystem>(true);
            if (equipmentSystem != null)
            {
                return equipmentSystem;
            }

            return unit.GetComponentInParent<EquipmentSystem>();
        }

        private static WeaponSystem FindWeaponSystemOnUnit(Unit unit)
        {
            if (unit == null)
            {
                return null;
            }

            WeaponSystem weaponSystem = unit.GetComponent<WeaponSystem>();
            if (weaponSystem != null)
            {
                return weaponSystem;
            }

            weaponSystem = unit.GetComponentInChildren<WeaponSystem>(true);
            if (weaponSystem != null)
            {
                return weaponSystem;
            }

            return unit.GetComponentInParent<WeaponSystem>();
        }

        private bool TryGetVisibleStackAtIndex(int slotIndex, out ItemStack stack)
        {
            if (slotIndex >= 0 && slotIndex < filteredStacks.Count)
            {
                stack = filteredStacks[slotIndex];
                return stack.item != null && stack.quantity > 0;
            }

            stack = default;
            return false;
        }

        internal void HandleSlotPointerClick(int slotIndex, PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                ShowContextMenu(slotIndex, eventData);
                return;
            }

            HideContextMenu();
        }

        internal void HandleSlotBeginDrag(int slotIndex, PointerEventData eventData)
        {
            if (!TryGetVisibleStackAtIndex(slotIndex, out ItemStack stack))
            {
                EndItemDrag();
                return;
            }

            draggingSlotIndex = slotIndex;
            draggingStack = stack;
            isDraggingItem = true;
            HideContextMenu();

            EnsureDragGhost();
            ApplyDragGhostVisual(stack);
            UpdateDragGhostPosition(eventData);
        }

        internal void HandleSlotDrag(PointerEventData eventData)
        {
            if (!isDraggingItem)
            {
                return;
            }

            UpdateDragGhostPosition(eventData);
        }

        internal void HandleSlotEndDrag(PointerEventData eventData)
        {
            _ = eventData;
            EndItemDrag();
        }

        internal void HandleItemDroppedOnEquipmentSlot(EquipmentSlot slot)
        {
            if (!isDraggingItem || draggingStack.item == null)
            {
                return;
            }

            if (TryEquipItemToSlot(draggingStack.item, slot))
            {
                RefreshEquipmentViews();
            }
        }

        private void ShowContextMenu(int slotIndex, PointerEventData eventData)
        {
            if (!TryGetVisibleStackAtIndex(slotIndex, out ItemStack stack))
            {
                HideContextMenu();
                return;
            }

            EnsureContextMenu();
            if (contextMenuRoot == null)
            {
                return;
            }

            contextSlotIndex = slotIndex;

            if (contextMenuTitle != null)
            {
                contextMenuTitle.text = ResolveItemName(stack.item);
            }

            if (contextEquipButton != null)
            {
                contextEquipButton.interactable = stack.item != null;
            }

            if (contextDropButton != null)
            {
                contextDropButton.interactable = stack.item != null && stack.quantity > 0;
            }

            PositionContextMenu(slotIndex, eventData);
            contextMenuRoot.gameObject.SetActive(true);
            contextMenuRoot.SetAsLastSibling();
        }

        private void PositionContextMenu(int slotIndex, PointerEventData eventData)
        {
            if (contextMenuRoot == null)
            {
                return;
            }

            RectTransform panelRect = transform as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            Camera uiCamera = eventData != null ? eventData.pressEventCamera : null;
            Vector2 screenPoint = eventData != null ? eventData.position : Vector2.zero;

            if (slotIndex >= 0 && slotIndex < slotViews.Count && slotViews[slotIndex].Root != null)
            {
                RectTransform slotRect = slotViews[slotIndex].Root;
                Vector3 worldRightEdge = slotRect.TransformPoint(new Vector3(slotRect.rect.xMax, 0f, 0f));
                screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldRightEdge);
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, screenPoint, uiCamera, out Vector2 localPoint))
            {
                return;
            }

            Vector2 desired = localPoint + new Vector2(contextMenuRoot.rect.width * 0.55f + 16f, 0f);
            contextMenuRoot.anchoredPosition = ClampToRect(panelRect.rect, desired, contextMenuRoot.rect.size);
        }

        private static Vector2 ClampToRect(Rect container, Vector2 desiredCenter, Vector2 size)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;

            float x = Mathf.Clamp(desiredCenter.x, container.xMin + halfWidth, container.xMax - halfWidth);
            float y = Mathf.Clamp(desiredCenter.y, container.yMin + halfHeight, container.yMax - halfHeight);
            return new Vector2(x, y);
        }

        private void HideContextMenu()
        {
            contextSlotIndex = -1;

            if (contextMenuRoot != null)
            {
                contextMenuRoot.gameObject.SetActive(false);
            }
        }

        private void EnsureContextMenu()
        {
            if (contextMenuRoot != null)
            {
                return;
            }

            GameObject root = new GameObject("ItemContextMenu", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(transform, false);

            contextMenuRoot = root.GetComponent<RectTransform>();
            contextMenuRoot.anchorMin = new Vector2(0.5f, 0.5f);
            contextMenuRoot.anchorMax = new Vector2(0.5f, 0.5f);
            contextMenuRoot.pivot = new Vector2(0.5f, 0.5f);
            contextMenuRoot.sizeDelta = new Vector2(196f, 118f);

            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.26f, 0.30f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            contextMenuTitle = CreateContextLabel(contextMenuRoot, "Title", new Vector2(0f, 0.66f), new Vector2(1f, 1f), 14f, FontStyles.Bold);
            contextMenuTitle.alignment = TextAlignmentOptions.Center;
            contextMenuTitle.color = new Color(0.90f, 0.86f, 0.74f, 1f);
            contextMenuTitle.text = "Item";

            contextEquipButton = CreateContextButton(contextMenuRoot, "EquipButton", "Equip", new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.60f));
            contextDropButton = CreateContextButton(contextMenuRoot, "DropButton", "Drop", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.30f));

            if (contextEquipButton != null)
            {
                contextEquipButton.onClick.RemoveAllListeners();
                InventoryContextActionButton actionButton = contextEquipButton.gameObject.GetComponent<InventoryContextActionButton>();
                if (actionButton == null)
                {
                    actionButton = contextEquipButton.gameObject.AddComponent<InventoryContextActionButton>();
                }

                actionButton.Configure(this, isDropAction: false);
            }

            if (contextDropButton != null)
            {
                contextDropButton.onClick.RemoveAllListeners();
                InventoryContextActionButton actionButton = contextDropButton.gameObject.GetComponent<InventoryContextActionButton>();
                if (actionButton == null)
                {
                    actionButton = contextDropButton.gameObject.AddComponent<InventoryContextActionButton>();
                }

                actionButton.Configure(this, isDropAction: true);
            }

            contextMenuRoot.gameObject.SetActive(false);
        }

        private static TMP_Text CreateContextLabel(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, float fontSize, FontStyles fontStyle)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);

            TMP_Text label = go.GetComponent<TMP_Text>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateContextButton(Transform parent, string objectName, string text, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);

            RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = anchorMin;
            buttonRect.anchorMax = anchorMax;
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            Image buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = new Color(0.16f, 0.19f, 0.23f, 1f);

            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.94f, 0.88f, 1f);
            colors.pressedColor = new Color(0.72f, 0.84f, 0.77f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.52f, 0.52f, 0.52f, 0.75f);
            button.colors = colors;

            TMP_Text label = CreateContextLabel(buttonGo.transform, "Label", Vector2.zero, Vector2.one, 13f, FontStyles.Bold);
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.90f, 0.90f, 0.90f, 1f);

            return button;
        }

        internal void HandleContextEquipClicked()
        {
            if (!TryGetVisibleStackAtIndex(contextSlotIndex, out ItemStack stack) || stack.item == null)
            {
                HideContextMenu();
                return;
            }

            suppressDropUntilRealtime = Time.unscaledTime + 0.2f;

            EquipmentSlot targetSlot = ResolveDefaultEquipSlot(stack.item);
            TryEquipItemToSlot(stack.item, targetSlot);
            HideContextMenu();
        }

        internal void HandleContextDropClicked()
        {
            if (Time.unscaledTime < suppressDropUntilRealtime)
            {
                return;
            }

            if (!TryGetVisibleStackAtIndex(contextSlotIndex, out ItemStack stack) || stack.item == null || stack.quantity <= 0)
            {
                HideContextMenu();
                return;
            }

            TryDropItemStack(stack.item, stack.quantity);
            HideContextMenu();
        }

        private bool TryEquipItemToSlot(ItemDefinition item, EquipmentSlot slot)
        {
            if (item == null)
            {
                return false;
            }

            EquipmentSystem equipmentSystem = ResolveEquipmentSystem();
            if (equipmentSystem == null)
            {
                Debug.LogWarning("[InventoryPanelController] Equip requested but selected unit has no EquipmentSystem.", this);
                return false;
            }

            bool equipped = equipmentSystem.Equip(slot, item);
            if (equipped)
            {
                RefreshEquipmentViews();
            }

            return equipped;
        }

        private static EquipmentSlot ResolveDefaultEquipSlot(ItemDefinition item)
        {
            if (item == null)
            {
                return EquipmentSlot.Belt;
            }

            if (item.itemType == ItemType.Weapon || item.equippedWeaponData != null)
            {
                return EquipmentSlot.RightHand;
            }

            return EquipmentSlot.Belt;
        }

        private bool TryDropItemStack(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0 || currentInventory == null)
            {
                return false;
            }

            if (!currentInventory.RemoveItem(item, quantity))
            {
                return false;
            }

            SpawnDroppedPickup(item, quantity);
            return true;
        }

        private void SpawnDroppedPickup(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0)
            {
                return;
            }

            Vector3 origin = currentUnit != null ? currentUnit.transform.position : Vector3.zero;
            Vector3 forward = currentUnit != null ? currentUnit.transform.forward : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            Vector3 spawnPosition = origin + forward.normalized * 1.25f + Vector3.up * 0.2f;

            GameObject pickupRoot = new GameObject($"Dropped_{item.name}");
            pickupRoot.transform.position = spawnPosition;

            SphereCollider trigger = pickupRoot.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.45f;

            Rigidbody rb = pickupRoot.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            ItemPickup pickup = pickupRoot.AddComponent<ItemPickup>();
            pickup.Initialize(item, quantity);
        }

        private void EnsureDragGhost()
        {
            if (dragGhostRoot != null)
            {
                return;
            }

            Transform parent = rootCanvasRect != null ? rootCanvasRect : transform;

            GameObject root = new GameObject("InventoryDragGhost", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);

            dragGhostRoot = root.GetComponent<RectTransform>();
            dragGhostRoot.anchorMin = new Vector2(0.5f, 0.5f);
            dragGhostRoot.anchorMax = new Vector2(0.5f, 0.5f);
            dragGhostRoot.pivot = new Vector2(0.5f, 0.5f);
            dragGhostRoot.sizeDelta = new Vector2(78f, 78f);

            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.10f, 0.78f);
            bg.raycastTarget = false;

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(root.transform, false);
            RectTransform iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(4f, 4f);
            iconRect.offsetMax = new Vector2(-4f, -4f);
            dragGhostIcon = iconGo.GetComponent<Image>();
            dragGhostIcon.preserveAspect = true;
            dragGhostIcon.raycastTarget = false;

            GameObject initialGo = new GameObject("Initial", typeof(RectTransform), typeof(TextMeshProUGUI));
            initialGo.transform.SetParent(root.transform, false);
            RectTransform initialRect = initialGo.GetComponent<RectTransform>();
            initialRect.anchorMin = Vector2.zero;
            initialRect.anchorMax = Vector2.one;
            initialRect.offsetMin = Vector2.zero;
            initialRect.offsetMax = Vector2.zero;
            dragGhostInitial = initialGo.GetComponent<TMP_Text>();
            dragGhostInitial.font = TMP_Settings.defaultFontAsset;
            dragGhostInitial.fontSize = 24f;
            dragGhostInitial.fontStyle = FontStyles.Bold;
            dragGhostInitial.alignment = TextAlignmentOptions.Center;
            dragGhostInitial.color = new Color(0.93f, 0.88f, 0.76f, 1f);
            dragGhostInitial.raycastTarget = false;

            dragGhostRoot.gameObject.SetActive(false);
        }

        private void ApplyDragGhostVisual(ItemStack stack)
        {
            if (dragGhostRoot == null)
            {
                return;
            }

            ItemDefinition item = stack.item;
            Sprite iconSprite = item != null ? item.inventoryIcon : null;

            if (dragGhostIcon != null)
            {
                dragGhostIcon.sprite = iconSprite;
                dragGhostIcon.color = Color.white;
                dragGhostIcon.enabled = iconSprite != null;
            }

            if (dragGhostInitial != null)
            {
                bool showInitial = iconSprite == null;
                dragGhostInitial.gameObject.SetActive(showInitial);
                dragGhostInitial.text = showInitial ? BuildInitial(ResolveItemName(item)) : string.Empty;
            }

            dragGhostRoot.gameObject.SetActive(true);
            dragGhostRoot.SetAsLastSibling();
        }

        private void UpdateDragGhostPosition(PointerEventData eventData)
        {
            if (dragGhostRoot == null || eventData == null)
            {
                return;
            }

            if (rootCanvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(rootCanvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                dragGhostRoot.anchoredPosition = localPoint + new Vector2(18f, -12f);
                return;
            }

            dragGhostRoot.position = eventData.position + new Vector2(18f, -12f);
        }

        private void EndItemDrag()
        {
            isDraggingItem = false;
            draggingSlotIndex = -1;
            draggingStack = default;

            if (dragGhostRoot != null)
            {
                dragGhostRoot.gameObject.SetActive(false);
            }

            ResetEquipmentDropTargetVisuals();
        }

        private void ResetEquipmentDropTargetVisuals()
        {
            foreach (KeyValuePair<EquipmentSlot, EquipmentSlotView> pair in equipmentSlotViews)
            {
                pair.Value?.DropTarget?.ResetVisual();
            }
        }

        private bool MatchesFilter(ItemDefinition item)
        {
            return currentFilter switch
            {
                InventoryFilter.All => true,
                InventoryFilter.Weapons => item.itemType == ItemType.Weapon,
                InventoryFilter.Ammo => item.itemType == ItemType.Ammo,
                InventoryFilter.Medical => item.itemType == ItemType.Medical,
                InventoryFilter.Consumables => item.itemType == ItemType.Food || item.itemType == ItemType.Vitamin,
                InventoryFilter.Materials => item.itemType == ItemType.Material,
                _ => item.itemType == ItemType.Generic,
            };
        }

        private static bool MatchesSearch(ItemDefinition item, string query)
        {
            if (item == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            string trimmedQuery = query.Trim();
            if (trimmedQuery.Length == 0)
            {
                return true;
            }

            StringComparison comparison = StringComparison.OrdinalIgnoreCase;

            if (!string.IsNullOrWhiteSpace(item.displayName) && item.displayName.IndexOf(trimmedQuery, comparison) >= 0)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(item.itemId) && item.itemId.IndexOf(trimmedQuery, comparison) >= 0)
            {
                return true;
            }

            return item.name.IndexOf(trimmedQuery, comparison) >= 0;
        }

        private int CompareStacksForDisplay(ItemStack a, ItemStack b)
        {
            bool aEquipped = IsItemEquipped(a.item);
            bool bEquipped = IsItemEquipped(b.item);

            if (aEquipped != bEquipped)
            {
                return aEquipped ? -1 : 1;
            }

            string aName = ResolveItemName(a.item);
            string bName = ResolveItemName(b.item);
            return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsItemEquipped(ItemDefinition item)
        {
            if (item == null || currentEquipmentSystem == null)
            {
                return false;
            }

            IReadOnlyList<EquipmentSlotBinding> equipped = currentEquipmentSystem.EquippedItems;
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].item == item)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyFilledSlot(SlotView view, ItemStack stack, bool isEquipped)
        {
            if (view == null)
            {
                return;
            }

            if (view.Frame != null)
            {
                view.Frame.color = isEquipped
                    ? new Color(0.14f, 0.24f, 0.18f, 1f)
                    : new Color(0.19f, 0.19f, 0.16f, 1f);
            }

            ItemDefinition item = stack.item;
            string itemName = ResolveItemName(item);
            Sprite iconSprite = item != null ? item.inventoryIcon : null;

            if (view.Icon != null)
            {
                view.Icon.sprite = iconSprite;
                view.Icon.color = Color.white;
                view.Icon.enabled = iconSprite != null;
            }

            if (view.Initial != null)
            {
                bool showInitial = iconSprite == null;
                view.Initial.gameObject.SetActive(showInitial);
                view.Initial.text = showInitial ? BuildInitial(itemName) : string.Empty;
            }

            if (view.Quantity != null)
            {
                view.Quantity.color = isEquipped
                    ? new Color(0.73f, 0.96f, 0.79f, 1f)
                    : new Color(0.93f, 0.88f, 0.75f, 1f);

                if (isEquipped)
                {
                    view.Quantity.text = stack.quantity > 1
                        ? "E x" + stack.quantity
                        : "E";
                }
                else
                {
                    view.Quantity.text = stack.quantity > 1 ? "x" + stack.quantity : string.Empty;
                }
            }
        }

        private static void ApplyEmptySlot(SlotView view)
        {
            if (view == null)
            {
                return;
            }

            if (view.Frame != null)
            {
                view.Frame.color = new Color(0.11f, 0.11f, 0.14f, 1f);
            }

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

            if (view.Quantity != null)
            {
                view.Quantity.text = string.Empty;
            }
        }

        private static string ResolveItemName(ItemDefinition item)
        {
            if (item == null)
            {
                return "Unknown";
            }

            if (!string.IsNullOrWhiteSpace(item.displayName))
            {
                return item.displayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(item.itemId))
            {
                return item.itemId.Trim();
            }

            return item.name;
        }

        private static string BuildInitial(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "?";
            }

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    return char.ToUpperInvariant(character).ToString();
                }
            }

            return value.Substring(0, 1).ToUpperInvariant();
        }
    }

    public sealed class InventorySlotInteraction : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private InventoryPanelController owner;
        private int slotIndex;

        public void Configure(InventoryPanelController controller, int index)
        {
            owner = controller;
            slotIndex = index;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            owner?.HandleSlotPointerClick(slotIndex, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            owner?.HandleSlotBeginDrag(slotIndex, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            owner?.HandleSlotDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            owner?.HandleSlotEndDrag(eventData);
        }
    }

    public sealed class InventoryContextActionButton : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        private InventoryPanelController owner;
        private bool isDropAction;

        public void Configure(InventoryPanelController controller, bool isDropAction)
        {
            owner = controller;
            this.isDropAction = isDropAction;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            TriggerAction();
            eventData?.Use();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            TriggerAction();
        }

        private void TriggerAction()
        {
            if (owner == null)
            {
                return;
            }

            if (isDropAction)
            {
                owner.HandleContextDropClicked();
            }
            else
            {
                owner.HandleContextEquipClicked();
            }
        }
    }

    public sealed class EquipmentDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private InventoryPanelController owner;
        private EquipmentSlot slot;
        private Image frame;
        private Color baseColor;
        private readonly Color hoverColor = new Color(0.16f, 0.34f, 0.28f, 1f);

        public void Configure(InventoryPanelController controller, EquipmentSlot equipmentSlot, Image targetFrame)
        {
            owner = controller;
            slot = equipmentSlot;
            frame = targetFrame;
            baseColor = frame != null ? frame.color : Color.white;
        }

        public void OnDrop(PointerEventData eventData)
        {
            owner?.HandleItemDroppedOnEquipmentSlot(slot);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (frame == null || owner == null || !owner.IsDraggingItem)
            {
                return;
            }

            frame.color = hoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ResetVisual();
        }

        public void ResetVisual()
        {
            if (frame != null)
            {
                frame.color = baseColor;
            }
        }
    }
}
