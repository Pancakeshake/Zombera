#region

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Inventory;

#endregion

namespace Zombera.UI
{
    [AddComponentMenu("Zombera/UI/Inventory Panel Controller")]
    [DisallowMultipleComponent]
    public sealed partial class InventoryPanelController : MonoBehaviour
    {
        [SerializeField] private InventoryEditTargetController editTargetController;
        [SerializeField] private TMP_InputField searchInputField;
        [SerializeField] private Button filterButton;
        [SerializeField] private TMP_Text filterLabel;
        [SerializeField] private Transform slotGrid;
        [SerializeField] private bool logBindingSummary;

        private readonly Dictionary<EquipmentSlot, EquipmentSlotView> _equipmentSlotViews = new();
        private readonly HashSet<ItemDefinition> _equippedItemsLookup = new();
        private readonly List<ItemStack> _filteredStacks = new(64);
        private readonly List<SlotView> _slotViews = new(64);

        private Button _contextDropButton;
        private Button _contextEquipButton;
        private RectTransform _contextMenuRoot;
        private TMP_Text _contextMenuTitle;
        private int _contextSlotIndex = -1;
        private TMP_Text _contextEquipButtonLabel;

        private EquipmentSystem _currentEquipmentSystem;
        private InventoryFilter _currentFilter = InventoryFilter.All;
        private UnitInventory _currentInventory;
        private Unit _currentUnit;

        private Image _dragGhostIcon;
        private TMP_Text _dragGhostInitial;
        private RectTransform _dragGhostRoot;
        private ItemStack _draggingStack;

        private Canvas _rootCanvas;
        private RectTransform _rootCanvasRect;
        private EquipmentSystem _subscribedEquipmentSystem;
        private float _suppressDropUntilRealtime;
        private Transform _equipmentPanel;

        private bool _uiHooked;

        internal bool IsDraggingItem { get; private set; }

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

        private sealed class SlotView
        {
            public Image Frame;
            public Image Icon;
            public TMP_Text Initial;
            public TMP_Text Quantity;
            public RectTransform Root;
        }

        private sealed class EquipmentSlotView
        {
            public string DefaultHint;
            public EquipmentDropTarget DropTarget;
            public Image Icon;
            public TMP_Text ItemHint;
            public EquipmentSlot Slot;
        }
    }
}
