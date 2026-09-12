#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;

#endregion

namespace Zombera.UI
{
    public sealed partial class SquadPortraitStrip
    {
        private bool SelectFirstBoundUnitCore()
        {
            if (_slots == null || _slots.Length == 0) return false;

            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null || !slot.gameObject.activeSelf || slot.BoundUnit == null) continue;

                SelectSlot(i);
                return true;
            }

            SelectSlot(-1);
            return false;
        }

        private bool SelectPlayerOrFirstBoundUnitCore()
        {
            return TrySelectFirstRole(UnitRole.Player) || SelectFirstBoundUnit();
        }

        private bool TrySelectUnitCore(Unit unit)
        {
            return TrySelectUnit(unit, true);
        }


        private bool TrySelectUnitCore(Unit unit, bool allowTabSwitch)
        {
            if (unit == null || _slots == null || _slots.Length == 0) return false;

            if (!IsSquadTabPagingActive()) return TrySelectUnitInVisibleSlots(unit, true);

            var squadIndex = FindSquadIndexContainingUnit(unit);
            if (squadIndex < 0) return TrySelectUnitInVisibleSlots(unit, true);

            var maxTabIndex = Mathf.Max(0, GetConfiguredSquadTabCount() - 1);
            var desiredTabIndex = Mathf.Clamp(squadIndex, 0, maxTabIndex);

            if (allowTabSwitch && desiredTabIndex != _activeSquadTabIndex)
            {
                _activeSquadTabIndex = desiredTabIndex;
                BindActiveSquadToSlots();
                UpdateSquadTabVisuals();
            }

            return TrySelectUnitInVisibleSlots(unit, true);
        }

        private bool TrySelectVisibleSlotByIndexCore(int slotIndex)
        {
            if (_slots == null || _slots.Length == 0) return false;
            if (slotIndex < 0 || slotIndex >= _slots.Length) return false;

            var slot = _slots[slotIndex];
            if (slot == null || !slot.gameObject.activeSelf || slot.BoundUnit == null) return false;

            SelectSlot(slotIndex);
            return true;
        }


        private bool TrySelectUnitInVisibleSlots(Unit unit, bool invokeEvent)
        {
            if (unit == null || _slots == null || _slots.Length == 0) return false;

            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null || !slot.gameObject.activeSelf) continue;

                if (slot.BoundUnit != unit) continue;

                SelectSlot(i, invokeEvent);
                return true;
            }

            return false;
        }


        private void TickBars()
        {
            if (_slots == null) return;
            foreach (var slot in _slots)
            {
                if (!slot.gameObject.activeSelf || slot.BoundUnit == null) continue;
                var u = slot.BoundUnit;

                if (slot.hpFill != null && u.Health != null)
                {
                    var frac = u.Health.MaxHealth > 0f
                        ? Mathf.Clamp01(u.Health.CurrentHealth / u.Health.MaxHealth)
                        : 0f;
                    slot.hpFill.fillAmount = frac;
                    slot.hpFill.color = Color.Lerp(hpLow, hpFull, frac);
                }

                if (slot.staminaFill != null && u.Stats != null)
                    slot.staminaFill.fillAmount = u.Stats.StaminaRatio;
            }
        }


        private void HandlePortraitSlotClicked(int slotIndex)
        {
            ApplyPortraitSelectionModifiers(slotIndex);
            SelectSlot(slotIndex);
        }

        private void ApplyPortraitSelectionModifiers(int slotIndex)
        {
            if (_slots == null || slotIndex < 0 || slotIndex >= _slots.Length) return;

            var unit = _slots[slotIndex]?.BoundUnit;
            if (unit == null) return;

            var member = unit.GetComponent<SquadMember>();
            if (member == null) return;

            var squadManager = SquadManager.Instance;
            if (squadManager == null) return;

            var rosterIndex = ResolveRosterIndex(unit);

            if (IsShiftModifierPressed())
            {
                var anchorIndex = ResolvePortraitSelectionAnchorRosterIndex();
                if (anchorIndex < 0 && rosterIndex >= 0) anchorIndex = rosterIndex;

                if (anchorIndex >= 0 && rosterIndex >= 0)
                {
                    ApplyRosterRangeSelection(anchorIndex, rosterIndex);
                    _portraitSelectionAnchorRosterIndex = rosterIndex;
                }

                return;
            }

            if (IsCtrlModifierPressed())
            {
                squadManager.ToggleMemberInSelection(member);
                if (rosterIndex >= 0) _portraitSelectionAnchorRosterIndex = rosterIndex;
                return;
            }

            squadManager.SetSelectedMembers(new[] { member });
            if (rosterIndex >= 0) _portraitSelectionAnchorRosterIndex = rosterIndex;
        }

        private void ApplyRosterRangeSelection(int anchorRosterIndex, int targetRosterIndex)
        {
            if (_rosterUnits == null || _rosterUnits.Count == 0) return;

            var squadManager = SquadManager.Instance;
            if (squadManager == null) return;

            var start = Mathf.Min(anchorRosterIndex, targetRosterIndex);
            var end = Mathf.Max(anchorRosterIndex, targetRosterIndex);

            var members = new List<SquadMember>(end - start + 1);
            for (var i = start; i <= end; i++)
            {
                if (i < 0 || i >= _rosterUnits.Count) continue;

                var unit = _rosterUnits[i];
                if (unit == null) continue;

                var member = unit.GetComponent<SquadMember>();
                if (member != null) members.Add(member);
            }

            squadManager.SetSelectedMembers(members);
        }

        private int ResolvePortraitSelectionAnchorRosterIndex()
        {
            if (_portraitSelectionAnchorRosterIndex >= 0) return _portraitSelectionAnchorRosterIndex;

            var squadManager = SquadManager.Instance;
            if (squadManager != null && squadManager.HasSelection)
            {
                var firstSelected = squadManager.SelectedMembers[0];
                var rosterIndex = ResolveRosterIndex(firstSelected?.Unit);
                if (rosterIndex >= 0) return rosterIndex;
            }

            if (_selected >= 0 && _selected < _slots.Length)
            {
                var rosterIndex = ResolveRosterIndex(_slots[_selected]?.BoundUnit);
                if (rosterIndex >= 0) return rosterIndex;
            }

            return -1;
        }

        private int ResolveRosterIndex(Unit unit)
        {
            if (unit == null || _rosterUnits == null) return -1;

            for (var i = 0; i < _rosterUnits.Count; i++)
            {
                if (_rosterUnits[i] == unit) return i;
            }

            return -1;
        }

        private static bool IsShiftModifierPressed()
        {
            if (Keyboard.current == null) return false;

            return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }

        private static bool IsCtrlModifierPressed()
        {
            if (Keyboard.current == null) return false;

            return Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
        }

        private void SelectSlot(int idx, bool invokeEvent = true)
        {
            _selected = idx;
            var selected = idx >= 0 && idx < _slots.Length ? _slots[idx].BoundUnit : null;
            if (invokeEvent) OnPortraitClicked?.Invoke(selected);

            RefreshSelectionVisuals();
        }


        private bool TrySelectFirstRole(UnitRole role)
        {
            if (_rosterUnits == null || _rosterUnits.Count == 0) return false;

            var firstUnit = _rosterUnits.FirstOrDefault(unit => unit != null && unit.Role == role);
            return firstUnit != null && TrySelectUnit(firstUnit);
        }
    }
}
