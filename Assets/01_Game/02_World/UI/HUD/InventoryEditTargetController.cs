#region

using System;
using TMPro;
using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.UI
{
    [AddComponentMenu("Zombera/UI/Inventory Edit Target Controller")]
    [DisallowMultipleComponent]
    public sealed class InventoryEditTargetController : MonoBehaviour
    {
        [SerializeField] private SquadPortraitStrip headerPortraitStrip;
        [SerializeField] private SquadPortraitStrip modalPortraitStrip;
        [SerializeField] private TextMeshProUGUI editingUnitLabel;

        private bool _syncingSelection;

        public Unit EditingUnit { get; private set; }

        private void OnEnable()
        {
            Subscribe(headerPortraitStrip);
            Subscribe(modalPortraitStrip);
            RefreshAndSelectDefault();
        }

        private void OnDisable()
        {
            Unsubscribe(headerPortraitStrip);
            Unsubscribe(modalPortraitStrip);
        }

        public event Action<Unit> OnEditingUnitChanged;

        public void RefreshAndSelectDefault()
        {
            headerPortraitStrip?.RefreshBindings();
            modalPortraitStrip?.RefreshBindings();

            if (EditingUnit != null)
                if ((headerPortraitStrip != null && headerPortraitStrip.TrySelectUnit(EditingUnit)) ||
                    (modalPortraitStrip != null && modalPortraitStrip.TrySelectUnit(EditingUnit)))
                    return;

            if (headerPortraitStrip != null && headerPortraitStrip.SelectFirstBoundUnit()) return;

            if (modalPortraitStrip != null && modalPortraitStrip.SelectFirstBoundUnit()) return;

            SetEditingUnit(null);
        }

        private void Subscribe(SquadPortraitStrip strip)
        {
            if (strip != null) strip.OnPortraitClicked += HandlePortraitClicked;
        }

        private void Unsubscribe(SquadPortraitStrip strip)
        {
            if (strip != null) strip.OnPortraitClicked -= HandlePortraitClicked;
        }

        private void HandlePortraitClicked(Unit unit)
        {
            SetEditingUnit(unit);

            if (_syncingSelection || unit == null) return;

            _syncingSelection = true;
            try
            {
                if (headerPortraitStrip != null && headerPortraitStrip.SelectedUnit != unit)
                    headerPortraitStrip.TrySelectUnit(unit);

                if (modalPortraitStrip != null && modalPortraitStrip.SelectedUnit != unit)
                    modalPortraitStrip.TrySelectUnit(unit);
            }
            finally
            {
                _syncingSelection = false;
            }
        }

        private void SetEditingUnit(Unit unit)
        {
            if (EditingUnit == unit)
            {
                UpdateLabel(unit);
                return;
            }

            EditingUnit = unit;
            UpdateLabel(unit);
            OnEditingUnitChanged?.Invoke(unit);
        }

        private void UpdateLabel(Unit unit)
        {
            if (editingUnitLabel == null) return;

            editingUnitLabel.text = unit == null
                ? "Editing: None"
                : $"Editing: {unit.gameObject.name}";
        }
    }
}