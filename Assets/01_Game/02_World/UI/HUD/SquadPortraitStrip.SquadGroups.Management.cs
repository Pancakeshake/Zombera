using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.UI
{
    public sealed partial class SquadPortraitStrip
    {
        private const int MaxSquadGroupCount = 10;
        private const int DefaultSquadGroupCount = 4;

        [Serializable]
        private sealed class SquadGroupDefinition
        {
            public string DisplayName = "Squad 1";
            public readonly List<string> MemberUnitIds = new(10);
        }

        private readonly List<SquadGroupDefinition> _squadGroups = new(MaxSquadGroupCount);
        private readonly Dictionary<string, Unit> _unitsByKey = new(32);
        private readonly Dictionary<Button, SquadTabInteraction> _squadTabInteractionByButton = new(12);
        private readonly Dictionary<Button, SquadTabDropInteraction> _squadTabDropByButton = new(12);

        private Button _addSquadTabButton;
        private RectTransform _squadTabRenameRoot;
        private TMP_InputField _squadTabRenameInput;
        private int _renamingSquadTabIndex = -1;

        private RectTransform _portraitDragGhostRoot;
        private Image _portraitDragGhostImage;
        private RectTransform _portraitDragGhostCanvas;
        private SquadPortraitDragPayload? _activePortraitDrag;

        internal readonly struct SquadPortraitDragPayload
        {
            public readonly string UnitId;
            public readonly int SourceSquadIndex;
            public readonly int SourceSlotIndex;

            public SquadPortraitDragPayload(string unitId, int sourceSquadIndex, int sourceSlotIndex)
            {
                UnitId = unitId;
                SourceSquadIndex = sourceSquadIndex;
                SourceSlotIndex = sourceSlotIndex;
            }
        }

        internal bool IsPortraitDragActive => _activePortraitDrag.HasValue;

        internal bool TryGetActivePortraitDrag(out SquadPortraitDragPayload payload)
        {
            if (!_activePortraitDrag.HasValue)
            {
                payload = default;
                return false;
            }

            payload = _activePortraitDrag.Value;
            return true;
        }

        private void EnsureSquadGroupDefinitions()
        {
            if (_squadGroups.Count > 0) return;

            var initialCount = Mathf.Clamp(squadTabCount, 1, MaxSquadGroupCount);
            for (var i = 0; i < initialCount; i++)
                _squadGroups.Add(CreateDefaultSquadGroup(i));

            squadTabCount = _squadGroups.Count;
        }

        private static SquadGroupDefinition CreateDefaultSquadGroup(int index)
        {
            return new SquadGroupDefinition { DisplayName = $"Squad {index + 1}" };
        }

        private static string GetDefaultSquadGroupName(int index)
        {
            return $"Squad {index + 1}";
        }

        private int GetConfiguredSquadTabCount()
        {
            EnsureSquadGroupDefinitions();
            return Mathf.Clamp(_squadGroups.Count, 1, MaxSquadGroupCount);
        }

        private void SyncSquadGroupsWithRoster()
        {
            EnsureSquadGroupDefinitions();
            RebuildUnitLookup();

            if (_preserveManualSquadLayout)
            {
                EnforceExclusiveSquadMembership();
                TrimSquadsToCapacity();
                AssignUnassignedRosterUnits();
            }
            else
            {
                RebuildSequentialSquadMembership();
            }

            EnsurePlayerOccupiesSquadOneSlotZero();
        }

        private void RebuildSequentialSquadMembership()
        {
            var pageSize = GetSquadTabPageSize();
            for (var squadIndex = 0; squadIndex < _squadGroups.Count; squadIndex++)
                _squadGroups[squadIndex].MemberUnitIds.Clear();

            var orderedKeys = BuildSequentialRosterKeys();
            var squadIndexCursor = 0;
            foreach (var unitKey in orderedKeys)
            {
                while (squadIndexCursor < _squadGroups.Count
                       && _squadGroups[squadIndexCursor].MemberUnitIds.Count >= pageSize)
                {
                    squadIndexCursor++;
                }

                if (squadIndexCursor >= _squadGroups.Count) break;

                _squadGroups[squadIndexCursor].MemberUnitIds.Add(unitKey);
            }
        }

        private List<string> BuildSequentialRosterKeys()
        {
            var keys = new List<string>(_rosterUnits.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            var player = ResolvePlayerUnitForRoster();
            if (player != null)
            {
                var playerKey = GetUnitKey(player);
                if (!string.IsNullOrWhiteSpace(playerKey) && seen.Add(playerKey))
                    keys.Add(playerKey);
            }

            foreach (var unit in _rosterUnits)
            {
                if (unit == null || !unit.IsAlive) continue;

                var unitKey = GetUnitKey(unit);
                if (string.IsNullOrWhiteSpace(unitKey) || !seen.Add(unitKey)) continue;

                keys.Add(unitKey);
            }

            return keys;
        }

        private Unit ResolvePlayerUnitForRoster()
        {
            foreach (var unit in _rosterUnits)
            {
                if (unit != null && unit.IsAlive && unit.Role == UnitRole.Player)
                    return unit;
            }

            if (UnitManager.HasInstance)
            {
                var player = UnitManager.Instance.FindFirstUnitByRole(UnitRole.Player);
                if (player != null && player.IsAlive)
                    return player;
            }

            return null;
        }

        private void EnsurePlayerOccupiesSquadOneSlotZero()
        {
            if (_squadGroups.Count == 0) return;

            var player = ResolvePlayerUnitForRoster();
            if (player == null) return;

            var playerKey = GetUnitKey(player);
            if (string.IsNullOrWhiteSpace(playerKey)) return;

            for (var squadIndex = 1; squadIndex < _squadGroups.Count; squadIndex++)
                _squadGroups[squadIndex].MemberUnitIds.Remove(playerKey);

            var squadOne = _squadGroups[0];
            squadOne.MemberUnitIds.Remove(playerKey);
            squadOne.MemberUnitIds.Insert(0, playerKey);

            var pageSize = GetSquadTabPageSize();
            while (squadOne.MemberUnitIds.Count > pageSize)
                squadOne.MemberUnitIds.RemoveAt(squadOne.MemberUnitIds.Count - 1);
        }

        private void TrimSquadsToCapacity()
        {
            var pageSize = GetSquadTabPageSize();
            var overflow = new List<string>(8);

            for (var squadIndex = 0; squadIndex < _squadGroups.Count; squadIndex++)
            {
                var group = _squadGroups[squadIndex];
                while (group.MemberUnitIds.Count > pageSize)
                {
                    var index = group.MemberUnitIds.Count - 1;
                    overflow.Add(group.MemberUnitIds[index]);
                    group.MemberUnitIds.RemoveAt(index);
                }
            }

            if (overflow.Count == 0) return;

            for (var i = overflow.Count - 1; i >= 0; i--)
                TryAssignUnitToFirstAvailableSquad(overflow[i]);
        }

        private void EnforceExclusiveSquadMembership()
        {
            var claimed = new HashSet<string>(StringComparer.Ordinal);

            for (var squadIndex = 0; squadIndex < _squadGroups.Count; squadIndex++)
            {
                var group = _squadGroups[squadIndex];
                for (var i = group.MemberUnitIds.Count - 1; i >= 0; i--)
                {
                    var unitId = group.MemberUnitIds[i];
                    if (string.IsNullOrWhiteSpace(unitId)
                        || !_unitsByKey.ContainsKey(unitId)
                        || !claimed.Add(unitId))
                    {
                        group.MemberUnitIds.RemoveAt(i);
                    }
                }
            }
        }

        private void AssignUnassignedRosterUnits()
        {
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            for (var squadIndex = 0; squadIndex < _squadGroups.Count; squadIndex++)
            {
                foreach (var unitId in _squadGroups[squadIndex].MemberUnitIds)
                    claimed.Add(unitId);
            }

            foreach (var unit in _rosterUnits)
            {
                if (unit == null || !unit.IsAlive) continue;

                var unitKey = GetUnitKey(unit);
                if (string.IsNullOrWhiteSpace(unitKey) || claimed.Contains(unitKey)) continue;

                if (!TryAssignUnitToFirstAvailableSquad(unitKey)) break;

                claimed.Add(unitKey);
            }
        }

        private void RebuildUnitLookup()
        {
            _unitsByKey.Clear();
            foreach (var unit in _rosterUnits)
            {
                if (unit == null || !unit.IsAlive) continue;
                _unitsByKey[GetUnitKey(unit)] = unit;
            }
        }

        private bool TryAssignUnitToFirstAvailableSquad(string unitKey)
        {
            if (string.IsNullOrWhiteSpace(unitKey) || !_unitsByKey.ContainsKey(unitKey)) return false;

            var pageSize = GetSquadTabPageSize();
            for (var squadIndex = 0; squadIndex < _squadGroups.Count; squadIndex++)
            {
                var group = _squadGroups[squadIndex];
                if (group.MemberUnitIds.Contains(unitKey)) return true;
                if (group.MemberUnitIds.Count >= pageSize) continue;

                group.MemberUnitIds.Add(unitKey);
                return true;
            }

            return false;
        }

        private void BindActiveSquadToSlots()
        {
            if (_slots == null || _slots.Length == 0) return;

            EnsureSquadGroupDefinitions();
            _activeSquadTabIndex = Mathf.Clamp(_activeSquadTabIndex, 0, Mathf.Max(0, _squadGroups.Count - 1));

            var group = _squadGroups[_activeSquadTabIndex];
            var pageSize = GetSquadTabPageSize();

            for (var slotIndex = 0; slotIndex < _slots.Length; slotIndex++)
            {
                var slot = _slots[slotIndex];
                if (slot == null) continue;

                Unit boundUnit = null;
                var showSlot = slotIndex < pageSize;
                if (showSlot && slotIndex < group.MemberUnitIds.Count)
                    TryResolveUnit(group.MemberUnitIds[slotIndex], out boundUnit);

                if (showSlot && boundUnit != null)
                {
                    slot.BoundUnit = boundUnit;
                    slot.gameObject.SetActive(true);
                    if (slot.nameLabel != null)
                        slot.nameLabel.text = boundUnit.gameObject.name;

                    if (!TryApplySelectionPortrait(slot, boundUnit))
                        ApplyPortraitFromUnitHead(slot, boundUnit);

                    var captured = slotIndex;
                    slot.slotButton?.onClick.RemoveAllListeners();
                    slot.slotButton?.onClick.AddListener(() => HandlePortraitSlotClicked(captured));
                    ConfigurePortraitSlotPointerSurface(slot, hasBoundUnit: true);
                }
                else
                {
                    ClearEmptyPortraitSlotVisual(slot, showSlot);
                }

                EnsurePortraitDragInteraction(slot, slotIndex);
            }

            ValidateSelectionAfterBind();
        }

        private void ClearEmptyPortraitSlotVisual(SquadPortraitSlot slot, bool showSlot)
        {
            if (slot == null) return;

            slot.BoundUnit = null;
            slot.gameObject.SetActive(showSlot);

            if (slot.portraitImage != null)
            {
                slot.portraitImage.sprite = null;
                slot.portraitImage.color = new Color(0.20f, 0.20f, 0.25f, 1f);
            }

            if (slot.nameLabel != null)
                slot.nameLabel.text = string.Empty;

            if (slot.selectOverlay != null)
                slot.selectOverlay.gameObject.SetActive(false);

            if (slot.hpFill != null)
                slot.hpFill.fillAmount = 0f;

            if (slot.staminaFill != null)
                slot.staminaFill.fillAmount = 0f;

            if (slot.slotButton != null)
                slot.slotButton.onClick.RemoveAllListeners();

            ConfigurePortraitSlotPointerSurface(slot, hasBoundUnit: false);
        }

        private static void ConfigurePortraitSlotPointerSurface(SquadPortraitSlot slot, bool hasBoundUnit)
        {
            if (slot == null) return;

            var rootGraphic = slot.slotButton != null
                ? slot.slotButton.targetGraphic
                : slot.GetComponent<Graphic>();
            if (rootGraphic == null)
                rootGraphic = slot.GetComponent<Graphic>();

            if (rootGraphic != null)
                rootGraphic.raycastTarget = true;

            var graphics = slot.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null || graphic == rootGraphic) continue;
                graphic.raycastTarget = false;
            }

            if (slot.slotButton != null)
                slot.slotButton.interactable = hasBoundUnit;
        }

        private void ValidateSelectionAfterBind()
        {
            if (_slots == null || _selected < 0) return;

            if (_selected >= _slots.Length || _slots[_selected] == null || _slots[_selected].BoundUnit == null)
                SelectSlot(-1, false);
            else
                RefreshSelectionVisuals();
        }

        private bool TryResolveUnit(string unitId, out Unit unit)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                unit = null;
                return false;
            }

            return _unitsByKey.TryGetValue(unitId, out unit) && unit != null && unit.IsAlive;
        }

        private static string GetUnitKey(Unit unit)
        {
            if (unit == null) return string.Empty;

            return string.IsNullOrWhiteSpace(unit.UnitId)
                ? unit.GetInstanceID().ToString()
                : unit.UnitId;
        }

        private int FindSquadIndexContainingUnit(Unit unit)
        {
            if (unit == null) return -1;

            var unitKey = GetUnitKey(unit);
            for (var squadIndex = 0; squadIndex < _squadGroups.Count; squadIndex++)
            {
                if (_squadGroups[squadIndex].MemberUnitIds.Contains(unitKey))
                    return squadIndex;
            }

            return -1;
        }
    }
}
