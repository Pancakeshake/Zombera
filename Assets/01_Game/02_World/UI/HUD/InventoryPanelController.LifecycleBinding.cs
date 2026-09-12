using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;

namespace Zombera.UI
{
    public sealed partial class InventoryPanelController
    {
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
            ResolveEditTargetReference();
            ResolveSearchInputReference();
            ResolveFilterButtonReference();
            ResolveFilterLabelReference();
            ResolveSlotGridReference();
            ResolveRootCanvasReferences();
        }

        private void ResolveEditTargetReference()
        {
            if (editTargetController == null)
                editTargetController = GetComponent<InventoryEditTargetController>();
        }

        private void ResolveSearchInputReference()
        {
            if (searchInputField != null) return;

            var searchTransform = transform.Find("InventoryGrid/InventoryControls/SearchBar");
            if (searchTransform != null)
                searchInputField = searchTransform.GetComponent<TMP_InputField>();
        }

        private void ResolveFilterButtonReference()
        {
            if (filterButton != null) return;

            var filterTransform = transform.Find("InventoryGrid/InventoryControls/FilterButton");
            if (filterTransform != null)
                filterButton = filterTransform.GetComponent<Button>();
        }

        private void ResolveFilterLabelReference()
        {
            if (filterLabel != null) return;

            var labelTransform = transform.Find("InventoryGrid/InventoryControls/FilterButton/Label");
            if (labelTransform != null)
                filterLabel = labelTransform.GetComponent<TMP_Text>();

            if (filterLabel == null && filterButton != null)
                filterLabel = filterButton.GetComponentInChildren<TMP_Text>(true);
        }

        private void ResolveSlotGridReference()
        {
            if (slotGrid != null) return;

            var slotGridTransform = transform.Find("InventoryGrid/Viewport/SlotGrid");
            if (slotGridTransform != null)
                slotGrid = slotGridTransform;
        }

        private void ResolveRootCanvasReferences()
        {
            if (_rootCanvas == null)
                _rootCanvas = GetComponentInParent<Canvas>();

            if (_rootCanvasRect == null && _rootCanvas != null)
                _rootCanvasRect = _rootCanvas.transform as RectTransform;
        }

        private void HookUi()
        {
            if (_uiHooked) return;

            if (searchInputField != null) searchInputField.onValueChanged.AddListener(HandleSearchInputChanged);

            if (filterButton != null) filterButton.onClick.AddListener(HandleFilterButtonClicked);

            _uiHooked = true;
        }

        private void UnhookUi()
        {
            if (!_uiHooked) return;

            if (searchInputField != null) searchInputField.onValueChanged.RemoveListener(HandleSearchInputChanged);

            if (filterButton != null) filterButton.onClick.RemoveListener(HandleFilterButtonClicked);

            _uiHooked = false;
        }

        private void HookEditTarget()
        {
            if (editTargetController != null) editTargetController.OnEditingUnitChanged += HandleEditingUnitChanged;
        }

        private void UnhookEditTarget()
        {
            if (editTargetController != null) editTargetController.OnEditingUnitChanged -= HandleEditingUnitChanged;
        }

        private void HandleEditingUnitChanged(Unit unit)
        {
            RebindCurrentUnit(unit);
            HideContextMenu();

            var resolvedInventory = ResolveInventoryForUnit(unit);

            if (resolvedInventory == _currentInventory)
            {
                RefreshSlots();
                RefreshEquipmentViews();
                return;
            }

            RebindCurrentInventory(resolvedInventory);

            RefreshSlots();
            RefreshEquipmentViews();
        }

        private void RebindCurrentUnit(Unit unit)
        {
            _currentUnit = unit;
            _currentEquipmentSystem = FindEquipmentSystemOnUnit(unit);
            HookEquipment();
        }

        private static UnitInventory ResolveInventoryForUnit(Unit unit)
        {
            if (unit == null) return null;

            return unit.Inventory ?? unit.GetComponent<UnitInventory>();
        }

        private void RebindCurrentInventory(UnitInventory resolvedInventory)
        {
            UnhookInventory();
            _currentInventory = resolvedInventory;

            if (_currentInventory != null)
                _currentInventory.OnInventoryChanged += HandleInventoryChanged;
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
            if (_currentInventory == null) return;

            _currentInventory.OnInventoryChanged -= HandleInventoryChanged;
            _currentInventory = null;
        }

        private void HookEquipment()
        {
            if (_currentEquipmentSystem == _subscribedEquipmentSystem) return;

            UnhookEquipment();

            if (_currentEquipmentSystem == null) return;

            _currentEquipmentSystem.OnEquipmentChanged += HandleEquipmentChanged;
            _subscribedEquipmentSystem = _currentEquipmentSystem;
        }

        private void UnhookEquipment()
        {
            if (_subscribedEquipmentSystem == null) return;

            _subscribedEquipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;
            _subscribedEquipmentSystem = null;
        }

        private void HandleSearchInputChanged(string _)
        {
            RefreshSlots();
        }

        private void HandleFilterButtonClicked()
        {
            var nextFilter = ((int)_currentFilter + 1) % Enum.GetValues(typeof(InventoryFilter)).Length;
            _currentFilter = (InventoryFilter)nextFilter;
            UpdateFilterLabel();
            RefreshSlots();
        }

        private void UpdateFilterLabel()
        {
            if (filterLabel == null) return;

            filterLabel.text = _currentFilter switch
            {
                InventoryFilter.All => "FILTER: ALL",
                InventoryFilter.Weapons => "FILTER: WEAPONS",
                InventoryFilter.Ammo => "FILTER: AMMO",
                InventoryFilter.Medical => "FILTER: MEDICAL",
                InventoryFilter.Consumables => "FILTER: CONSUMABLES",
                InventoryFilter.Materials => "FILTER: MATERIALS",
                InventoryFilter.Other => "FILTER: OTHER",
                _ => "FILTER: UNKNOWN"
            };
        }
    }
}
