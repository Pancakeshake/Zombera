#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Zombera.UI.SquadManagement;

#endregion

namespace Zombera.UI
{
    public sealed partial class SquadPortraitStrip
    {

        private void Start()
        {
            EnsureBottomStripSlotCapacityAndLayout();
            _slots = GetComponentsInChildren<SquadPortraitSlot>(true);
            EnsureRosterHeaderTabsNearLabel();
            EnsureRuntimeSquadTabButtons();
            ConfigureSquadTabButtons();
            EnsureQuickFormationButtons();
            RefreshBindings();
        }


        private void Update()
        {
            _ticker += Time.unscaledDeltaTime;
            if (_ticker < 0.1f) return;
            _ticker = 0f;
            TickBars();

            _portraitRetryTicker += Time.unscaledDeltaTime;
            var retryInterval = Mathf.Max(0.1f, missingPortraitRetryIntervalSeconds);
            if (_portraitRetryTicker < retryInterval) return;

            _portraitRetryTicker = 0f;
            RetryMissingPortraits();
        }


        private void OnEnable()
        {
            var studio = PortraitStudioManager.Instance;
            if (studio != null)
            {
                studio.PortraitRendered -= HandlePortraitStudioRendered;
                studio.PortraitRendered += HandlePortraitStudioRendered;
            }

            EnsureBottomStripSlotCapacityAndLayout();
            EnsureRosterHeaderTabsNearLabel();

            _slots = GetComponentsInChildren<SquadPortraitSlot>(true);
            if (_slots is not { Length: > 0 }) return;

            if (SquadManager.Instance != null)
                SquadManager.Instance.SelectionChanged += OnSquadManagerSelectionChanged;

            EnsureRuntimeSquadTabButtons();
            ConfigureSquadTabButtons();
            EnsureQuickFormationButtons();
            RefreshBindings();
        }


        private void OnDisable()
        {
            if (SquadManager.Instance != null)
                SquadManager.Instance.SelectionChanged -= OnSquadManagerSelectionChanged;
        }


        private void OnSquadManagerSelectionChanged(IReadOnlyList<SquadMember> selectedMembers)
        {
            if (selectedMembers != null && selectedMembers.Count > 0)
            {
                var rosterIndex = ResolveRosterIndex(selectedMembers[0]?.Unit);
                if (rosterIndex >= 0) _portraitSelectionAnchorRosterIndex = rosterIndex;
            }

            RefreshSelectionVisuals();
            UpdateSquadTabVisuals();
        }


        private void RefreshSelectionVisuals()
        {
            if (_slots == null) return;

            var squadManager = SquadManager.Instance;
            if (squadManager == null) return;

            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null || slot.selectOverlay == null) continue;

                var unit = slot.BoundUnit;
                var isSelected = false;

                if (unit != null)
                {
                    var member = unit.GetComponent<SquadMember>();
                    if (member != null)
                        isSelected = squadManager.SelectedMembers.Contains(member);
                }

                slot.selectOverlay.color = selectTint;
                slot.selectOverlay.gameObject.SetActive(isSelected);
            }
        }


        private void OnDestroy()
        {
            var studio = PortraitStudioManager.Instance;
            if (studio != null)
                studio.PortraitRendered -= HandlePortraitStudioRendered;

            ReleaseCapturedPortraits();
            ClearHeadshotCache();
            _portraitReadbackBlockedUnitIds.Clear();
            _readablePixelCacheByTextureId.Clear();
        }

        private void HandlePortraitStudioRendered(PortraitStudioRenderResult result)
        {
            if (result == null || !result.Success || result.CachedSprite == null || _slots == null) return;

            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                var unit = slot != null ? slot.BoundUnit : null;
                if (unit == null || slot.portraitImage == null) continue;

                var unitKey = string.IsNullOrWhiteSpace(unit.UnitId)
                    ? unit.GetInstanceID().ToString()
                    : unit.UnitId;
                if (!string.Equals(unitKey, result.UnitKey, StringComparison.Ordinal)) continue;

                slot.portraitImage.sprite = result.CachedSprite;
                slot.portraitImage.color = Color.white;
                slot.portraitImage.type = Image.Type.Simple;
                slot.portraitImage.preserveAspect = true;
            }
        }

        private void RefreshBindingsCore()
        {
            if (_slots == null || _slots.Length == 0) return;

            var previousSelection = SelectedUnit;

            ReleaseCapturedPortraits();
            RebuildRosterUnits();
            SyncSquadGroupsWithRoster();

            EnsureRuntimeSquadTabButtons();
            ConfigureSquadTabButtons();
            RefreshQuickFormationVisuals();

            BindRosterForActiveTab(previousSelection);
            QueueRosterPortraitStudioRequests();
            UpdateSquadTabVisuals();

            if (TryRestorePreviousSelection(previousSelection))
            {
                RefreshSelectionVisuals();
                return;
            }

            // Preserve manually selected squad page on periodic refreshes.
            // Do not force-select the player here, because TrySelectUnit(player)
            // can switch pages back to tab 1 and override user page selection.
            if (IsSquadTabPagingActive())
            {
                SelectFirstBoundUnit();
                RefreshSelectionVisuals();
                return;
            }

            SelectPlayerOrFirstBoundUnit();
            RefreshSelectionVisuals();
        }


        private void RebuildRosterUnits()
        {
            _rosterUnits.Clear();
            var seenUnitKeys = new HashSet<string>(StringComparer.Ordinal);
            var all = ResolveRosterUnitsForBinding();

            foreach (var u in all)
            {
                if (u == null) continue;
                if (!u.IsAlive) continue;
                if (u.Role is not (UnitRole.Player or UnitRole.SquadMember or UnitRole.Survivor)) continue;

                var unitKey = GetUnitKey(u);
                if (string.IsNullOrWhiteSpace(unitKey) || !seenUnitKeys.Add(unitKey)) continue;

                _rosterUnits.Add(u);
            }

            _rosterUnits.Sort(CompareUnitsForRoster);
            PruneHeadshotCacheToRoster();
        }


        private List<Unit> ResolveRosterUnitsForBinding()
        {
            _unitManagerQueryBuffer.Clear();

            var manager = UnitManager.Instance;
            if (manager != null)
                return manager.GetAllActiveUnits(_unitManagerQueryBuffer);

            var discovered = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            _unitManagerQueryBuffer.AddRange(discovered);
            return _unitManagerQueryBuffer;
        }


        private void BindRosterForActiveTab(Unit previousSelection)
        {
            if (!IsSquadTabPagingActive())
            {
                _activeSquadTabIndex = 0;
                BindRosterSliceToSlots(0, _slots.Length);
                return;
            }

            var maxTabIndex = Mathf.Max(0, GetConfiguredSquadTabCount() - 1);
            _activeSquadTabIndex = ResolveActiveSquadTabIndex(previousSelection, maxTabIndex);
            BindActiveSquadToSlots();
        }


        private int ResolveActiveSquadTabIndex(Unit previousSelection, int maxTabIndex)
        {
            if (previousSelection != null && previousSelection.IsAlive)
            {
                var squadIndex = FindSquadIndexContainingUnit(previousSelection);
                if (squadIndex >= 0)
                    return Mathf.Clamp(squadIndex, 0, maxTabIndex);
            }

            return Mathf.Clamp(_activeSquadTabIndex, 0, maxTabIndex);
        }


        private bool TryRestorePreviousSelection(Unit previousSelection)
        {
            return previousSelection != null
                   && previousSelection.IsAlive
                   && TrySelectUnit(previousSelection);
        }
    }
}
