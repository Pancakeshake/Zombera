using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zombera.Characters;

namespace Zombera.UI
{
    public sealed partial class SquadPortraitStrip
    {
        internal void BeginPortraitDrag(int slotIndex, PointerEventData eventData)
        {
            if (!IsSquadTabPagingActive() || _slots == null || slotIndex < 0 || slotIndex >= _slots.Length)
                return;

            var slot = _slots[slotIndex];
            if (slot == null || slot.BoundUnit == null) return;

            var unitKey = GetUnitKey(slot.BoundUnit);
            if (string.IsNullOrWhiteSpace(unitKey)) return;

            _activePortraitDrag = new SquadPortraitDragPayload(unitKey, _activeSquadTabIndex, slotIndex);
            EnsurePortraitDragGhost();
            ApplyPortraitDragGhostVisual(slot);
            UpdatePortraitDragGhostPosition(eventData);
        }

        internal void UpdatePortraitDrag(PointerEventData eventData)
        {
            if (!_activePortraitDrag.HasValue) return;
            UpdatePortraitDragGhostPosition(eventData);
        }

        internal void EndPortraitDrag(PointerEventData eventData)
        {
            _ = eventData;
            _activePortraitDrag = null;

            if (_portraitDragGhostRoot != null)
                _portraitDragGhostRoot.gameObject.SetActive(false);
        }

        internal void HandlePortraitDropOnSlot(int slotIndex)
        {
            if (!_activePortraitDrag.HasValue || _slots == null) return;
            if (slotIndex < 0 || slotIndex >= GetSquadTabPageSize()) return;

            var payload = _activePortraitDrag.Value;
            if (!TryMovePortrait(payload, _activeSquadTabIndex, slotIndex))
                return;

            BindActiveSquadToSlots();
            RefreshSelectionVisuals();
            EndPortraitDrag(null);
        }

        internal void HandlePortraitDropOnSquadTab(int squadIndex)
        {
            if (!_activePortraitDrag.HasValue) return;

            var payload = _activePortraitDrag.Value;
            var pageSize = GetSquadTabPageSize();
            var targetSquad = Mathf.Clamp(squadIndex, 0, Mathf.Max(0, _squadGroups.Count - 1));
            var targetGroup = _squadGroups[targetSquad];
            var targetSlot = Mathf.Clamp(targetGroup.MemberUnitIds.Count, 0, pageSize - 1);

            if (!TryMovePortrait(payload, targetSquad, targetSlot))
                return;

            if (targetSquad != _activeSquadTabIndex)
                SetActiveSquadTab(targetSquad);
            else
                BindActiveSquadToSlots();

            RefreshSelectionVisuals();
            EndPortraitDrag(null);
        }

        private bool TryMovePortrait(SquadPortraitDragPayload payload, int targetSquadIndex, int targetSlotIndex)
        {
            EnsureSquadGroupDefinitions();

            var pageSize = GetSquadTabPageSize();
            targetSquadIndex = Mathf.Clamp(targetSquadIndex, 0, Mathf.Max(0, _squadGroups.Count - 1));
            targetSlotIndex = Mathf.Clamp(targetSlotIndex, 0, pageSize - 1);

            if (payload.SourceSquadIndex < 0 || payload.SourceSquadIndex >= _squadGroups.Count)
                return false;

            var sourceGroup = _squadGroups[payload.SourceSquadIndex];
            var targetGroup = _squadGroups[targetSquadIndex];

            var sourceListIndex = sourceGroup.MemberUnitIds.IndexOf(payload.UnitId);
            if (sourceListIndex < 0) return false;

            if (targetSquadIndex == payload.SourceSquadIndex)
            {
                if (!TryReorderWithinSquad(sourceGroup, sourceListIndex, targetSlotIndex))
                    return false;

                _preserveManualSquadLayout = true;
                return true;
            }

            if (targetGroup.MemberUnitIds.Count >= pageSize)
                return false;

            RemoveUnitFromAllSquads(payload.UnitId);
            var insertIndex = Mathf.Clamp(targetSlotIndex, 0, targetGroup.MemberUnitIds.Count);
            targetGroup.MemberUnitIds.Insert(insertIndex, payload.UnitId);
            _preserveManualSquadLayout = true;
            return true;
        }

        private void RemoveUnitFromAllSquads(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId)) return;

            for (var squadIndex = 0; squadIndex < _squadGroups.Count; squadIndex++)
                _squadGroups[squadIndex].MemberUnitIds.Remove(unitId);
        }

        private static bool TryReorderWithinSquad(SquadGroupDefinition group, int sourceListIndex, int targetSlotIndex)
        {
            if (group == null || sourceListIndex < 0 || sourceListIndex >= group.MemberUnitIds.Count)
                return false;

            var unitId = group.MemberUnitIds[sourceListIndex];
            group.MemberUnitIds.RemoveAt(sourceListIndex);

            var insertIndex = Mathf.Clamp(targetSlotIndex, 0, group.MemberUnitIds.Count);
            if (sourceListIndex < insertIndex) insertIndex--;

            insertIndex = Mathf.Clamp(insertIndex, 0, group.MemberUnitIds.Count);
            group.MemberUnitIds.Insert(insertIndex, unitId);
            return true;
        }

        private void EnsurePortraitDragGhost()
        {
            if (_portraitDragGhostRoot != null) return;

            var canvas = GetComponentInParent<Canvas>();
            _portraitDragGhostCanvas = canvas != null ? canvas.transform as RectTransform : transform as RectTransform;

            var ghostGo = new GameObject("PortraitDragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _portraitDragGhostRoot = ghostGo.GetComponent<RectTransform>();
            _portraitDragGhostRoot.SetParent(_portraitDragGhostCanvas, false);
            _portraitDragGhostRoot.sizeDelta = new Vector2(72f, 72f);

            var canvasGroup = ghostGo.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0.82f;
            canvasGroup.blocksRaycasts = false;

            _portraitDragGhostImage = ghostGo.GetComponent<Image>();
            _portraitDragGhostImage.raycastTarget = false;
            _portraitDragGhostRoot.gameObject.SetActive(false);
        }

        private void ApplyPortraitDragGhostVisual(SquadPortraitSlot slot)
        {
            if (_portraitDragGhostRoot == null || slot == null) return;

            if (slot.portraitImage != null && slot.portraitImage.sprite != null)
            {
                _portraitDragGhostImage.sprite = slot.portraitImage.sprite;
                _portraitDragGhostImage.color = slot.portraitImage.color;
                _portraitDragGhostImage.preserveAspect = true;
            }
            else
            {
                _portraitDragGhostImage.sprite = null;
                _portraitDragGhostImage.color = new Color(0.2f, 0.55f, 0.38f, 0.75f);
            }

            _portraitDragGhostRoot.gameObject.SetActive(true);
            _portraitDragGhostRoot.SetAsLastSibling();
        }

        private void UpdatePortraitDragGhostPosition(PointerEventData eventData)
        {
            if (_portraitDragGhostRoot == null || eventData == null) return;

            if (_portraitDragGhostCanvas != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _portraitDragGhostCanvas, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                _portraitDragGhostRoot.anchoredPosition = localPoint + new Vector2(18f, -12f);
                return;
            }

            _portraitDragGhostRoot.position = eventData.position + new Vector2(18f, -12f);
        }

        private void EnsurePortraitDragInteraction(SquadPortraitSlot slot, int slotIndex)
        {
            if (slot == null || !IsBottomPortraitStrip()) return;

            if (!slot.TryGetComponent(out SquadPortraitRosterDragInteraction interaction))
                interaction = slot.gameObject.AddComponent<SquadPortraitRosterDragInteraction>();

            interaction.Configure(this, slotIndex);
        }

        internal bool TryAddSquadGroup()
        {
            EnsureSquadGroupDefinitions();
            if (_squadGroups.Count >= MaxSquadGroupCount) return false;

            var newIndex = _squadGroups.Count;
            _squadGroups.Add(CreateDefaultSquadGroup(newIndex));
            squadTabCount = _squadGroups.Count;

            EnsureSquadTabButtonCapacity(_squadGroups.Count);
            LayoutSquadTabRow();
            ConfigureSquadTabButtons();
            UpdateAddSquadTabButtonState();
            SetActiveSquadTab(newIndex);
            return true;
        }

        internal bool TryDeleteSquadGroup(int tabIndex)
        {
            EnsureSquadGroupDefinitions();
            if (_squadGroups.Count <= 1) return false;
            if (tabIndex < 0 || tabIndex >= _squadGroups.Count) return false;

            var displacedMembers = new List<string>(_squadGroups[tabIndex].MemberUnitIds);
            _squadGroups.RemoveAt(tabIndex);
            squadTabCount = _squadGroups.Count;

            foreach (var unitId in displacedMembers)
                TryAssignUnitToFirstAvailableSquad(unitId);

            if (_activeSquadTabIndex > tabIndex)
                _activeSquadTabIndex--;
            else if (_activeSquadTabIndex >= _squadGroups.Count)
                _activeSquadTabIndex = Mathf.Max(0, _squadGroups.Count - 1);

            _preserveManualSquadLayout = true;
            HideSquadTabContextMenu();
            EnsureDynamicSquadTabUi();
            ConfigureSquadTabButtons();
            UpdateAddSquadTabButtonState();
            BindActiveSquadToSlots();
            UpdateSquadTabVisuals();
            return true;
        }

        internal void BeginRenameSquadTab(int tabIndex)
        {
            HideSquadTabContextMenu();
            EnsureSquadGroupDefinitions();
            if (tabIndex < 0 || tabIndex >= _squadGroups.Count) return;

            EnsureSquadTabRenameInput();
            if (_squadTabRenameInput == null) return;

            var button = GetSquadTabButton(tabIndex);
            if (button == null) return;

            _renamingSquadTabIndex = tabIndex;
            var buttonRect = button.transform as RectTransform;
            if (buttonRect == null) return;

            _squadTabRenameRoot.SetParent(buttonRect, false);
            _squadTabRenameRoot.anchorMin = Vector2.zero;
            _squadTabRenameRoot.anchorMax = Vector2.one;
            _squadTabRenameRoot.offsetMin = Vector2.zero;
            _squadTabRenameRoot.offsetMax = Vector2.zero;
            _squadTabRenameRoot.gameObject.SetActive(true);

            _squadTabRenameInput.text = _squadGroups[tabIndex].DisplayName;
            _squadTabRenameInput.Select();
            _squadTabRenameInput.ActivateInputField();
        }

        private void CommitSquadTabRename(string newName)
        {
            if (_renamingSquadTabIndex < 0 || _renamingSquadTabIndex >= _squadGroups.Count)
            {
                CancelSquadTabRename();
                return;
            }

            var trimmed = string.IsNullOrWhiteSpace(newName)
                ? GetDefaultSquadGroupName(_renamingSquadTabIndex)
                : newName.Trim();

            _squadGroups[_renamingSquadTabIndex].DisplayName = trimmed;
            CancelSquadTabRename();
            UpdateSquadTabLabels();
        }

        private void CancelSquadTabRename()
        {
            _renamingSquadTabIndex = -1;
            if (_squadTabRenameRoot != null)
                _squadTabRenameRoot.gameObject.SetActive(false);
        }

        private Button GetSquadTabButton(int tabIndex)
        {
            if (squadTabButtons == null || tabIndex < 0 || tabIndex >= squadTabButtons.Length)
                return null;

            return squadTabButtons[tabIndex];
        }
    }
}
