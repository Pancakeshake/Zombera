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

#endregion

namespace Zombera.UI
{
    public sealed partial class SquadPortraitStrip
    {

        private void SetActiveSquadTabCore(int tabIndex)
        {
            if (!IsSquadTabPagingActive()) return;

            HideSquadTabContextMenu();

            var clampedTabIndex = Mathf.Clamp(tabIndex, 0, Mathf.Max(0, GetConfiguredSquadTabCount() - 1));
            if (clampedTabIndex == _activeSquadTabIndex) return;

            var previouslySelected = SelectedUnit;
            _activeSquadTabIndex = clampedTabIndex;
            BindActiveSquadToSlots();
            UpdateSquadTabVisuals();

            if (previouslySelected != null && TrySelectUnitInVisibleSlots(previouslySelected, false)) return;

            if (SelectFirstBoundUnitCore()) return;

            SelectSlot(-1, false);
        }


        private void BindRosterSliceToSlots(int startIndex, int visibleCapacity)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                var rosterIndex = startIndex + i;
                var showSlot = i < visibleCapacity && rosterIndex >= 0 && rosterIndex < _rosterUnits.Count;

                if (showSlot)
                {
                    var boundUnit = _rosterUnits[rosterIndex];

                    slot.BoundUnit = boundUnit;
                    slot.gameObject.SetActive(true);
                    if (slot.nameLabel != null)
                        slot.nameLabel.text = boundUnit.gameObject.name;

                    if (!TryApplySelectionPortrait(slot, boundUnit))
                        ApplyPortraitFromUnitHead(slot, boundUnit);

                    var captured = i;
                    slot.slotButton?.onClick.RemoveAllListeners();
                    slot.slotButton?.onClick.AddListener(() => HandlePortraitSlotClicked(captured));
                    ConfigurePortraitSlotPointerSurface(slot, hasBoundUnit: true);
                    EnsurePortraitDragInteraction(slot, i);
                }
                else
                {
                    slot.BoundUnit = null;
                    slot.gameObject.SetActive(false);
                    ConfigurePortraitSlotPointerSurface(slot, hasBoundUnit: false);
                }
            }
        }


        private bool IsSquadTabPagingActive()
        {
            if (!enableSquadTabs || _slots == null || _slots.Length == 0) return false;

            return !autoEnableTabsForBottomStrip
                   || string.Equals(gameObject.name, "PortraitStrip", StringComparison.OrdinalIgnoreCase);
        }


        private bool IsBottomPortraitStrip()
        {
            return string.Equals(gameObject.name, "PortraitStrip", StringComparison.OrdinalIgnoreCase);
        }


        private void EnsureBottomStripSlotCapacityAndLayout()
        {
            if (!IsBottomPortraitStrip()) return;

            slotsPerSquadTab = Mathf.Max(10, slotsPerSquadTab);

            var discoveredSlots = GetComponentsInChildren<SquadPortraitSlot>(true);
            if (discoveredSlots == null || discoveredSlots.Length == 0) return;

            var requiredSlotCount = Mathf.Max(1, slotsPerSquadTab);
            var slots = new List<SquadPortraitSlot>(Mathf.Max(requiredSlotCount, discoveredSlots.Length));
            slots.AddRange(discoveredSlots);

            var slotParent = slots[0].transform.parent;
            var templateSlot = slots[0].gameObject;

            for (var i = slots.Count; i < requiredSlotCount; i++)
            {
                var clone = Instantiate(templateSlot, slotParent, false);
                clone.name = $"MemberPort_Auto_{i + 1}";
                clone.SetActive(true);

                var cloneSlot = clone.GetComponent<SquadPortraitSlot>();
                if (cloneSlot != null)
                {
                    cloneSlot.BoundUnit = null;
                    if (cloneSlot.selectOverlay != null) cloneSlot.selectOverlay.gameObject.SetActive(false);
                    slots.Add(cloneSlot);
                }
            }

            var stripLayout = GetComponent<HorizontalLayoutGroup>();
            if (stripLayout != null)
            {
                stripLayout.childControlWidth = true;
                stripLayout.childControlHeight = true;
                stripLayout.childForceExpandWidth = false;
                stripLayout.childForceExpandHeight = false;
                stripLayout.spacing = Mathf.Max(4f, stripLayout.spacing);
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                var layoutElement = slot.GetComponent<LayoutElement>();
                if (layoutElement == null) layoutElement = slot.gameObject.AddComponent<LayoutElement>();

                layoutElement.minWidth = portraitSlotWidth;
                layoutElement.preferredWidth = portraitSlotWidth;
                layoutElement.flexibleWidth = 0f;
                layoutElement.minHeight = portraitSlotHeight;
                layoutElement.preferredHeight = portraitSlotHeight;
            }

            _slots = slots.ToArray();
        }


        private int GetSquadTabPageSize()
        {
            if (_slots == null || _slots.Length == 0) return 1;

            return Mathf.Clamp(slotsPerSquadTab, 1, _slots.Length);
        }


        private void EnsureRuntimeSquadTabButtons()
        {
            if (!IsSquadTabPagingActive()) return;

            EnsureSquadGroupDefinitions();

            if (!HasAnyConfiguredTabButton())
            {
                var tabsRoot = ResolveSquadPageTabsRoot();
                if (tabsRoot == null)
                {
                    var stripParent = transform.parent as RectTransform;
                    if (stripParent == null) return;

                    var root = new GameObject("SquadPageTabs_Runtime", typeof(RectTransform),
                        typeof(HorizontalLayoutGroup));
                    tabsRoot = root.GetComponent<RectTransform>();
                    tabsRoot.SetParent(stripParent, false);
                    tabsRoot.anchorMin = new Vector2(0f, 1f);
                    tabsRoot.anchorMax = new Vector2(0f, 1f);
                    tabsRoot.pivot = new Vector2(0f, 1f);
                    tabsRoot.anchoredPosition = new Vector2(8f, -6f);
                    tabsRoot.sizeDelta = new Vector2(640f, 38f);

                    var layout = root.GetComponent<HorizontalLayoutGroup>();
                    layout.spacing = 8f;
                    layout.childAlignment = TextAnchor.MiddleLeft;
                    layout.childControlWidth = true;
                    layout.childControlHeight = true;
                    layout.childForceExpandWidth = false;
                    layout.childForceExpandHeight = false;
                }

                EnsureSquadTabButtonCapacity(GetConfiguredSquadTabCount());
            }

            EnsureDynamicSquadTabUi();
        }


        private void EnsureRosterHeaderTabsNearLabel()
        {
            var stripParent = transform.parent;
            if (stripParent == null) return;

            var rosterHeader = stripParent.Find("RosterHeader") as RectTransform;
            if (rosterHeader == null) return;

            var changed = false;

            var rosterLabelTransform = rosterHeader.Find("RosterLabel");
            if (rosterLabelTransform != null)
            {
                var rosterLabelLayout = rosterLabelTransform.GetComponent<LayoutElement>();
                if (rosterLabelLayout != null && !Mathf.Approximately(rosterLabelLayout.flexibleWidth, 0f))
                {
                    rosterLabelLayout.flexibleWidth = 0f;
                    changed = true;
                }
            }

            var tabsTransform = rosterHeader.Find("SquadPageTabs");
            if (tabsTransform != null)
            {
                var tabsLayout = tabsTransform.GetComponent<LayoutElement>();
                if (tabsLayout != null && !Mathf.Approximately(tabsLayout.flexibleWidth, 0f))
                {
                    tabsLayout.flexibleWidth = 0f;
                    changed = true;
                }
            }

            if (changed) LayoutRebuilder.ForceRebuildLayoutImmediate(rosterHeader);
        }


        private bool HasAnyConfiguredTabButton()
        {
            return squadTabButtons != null && Array.Exists(squadTabButtons, button => button != null);
        }


        private void ConfigureSquadTabButtons()
        {
            if (squadTabButtons == null || squadTabButtons.Length == 0) return;

            EnsureSquadGroupDefinitions();
            EnsureDynamicSquadTabUi();
            EnsureSquadTabInteractions();

            var tabsActive = IsSquadTabPagingActive();
            var configuredTabCount = GetConfiguredSquadTabCount();

            for (var i = 0; i < squadTabButtons.Length; i++)
            {
                var button = squadTabButtons[i];
                if (button == null) continue;

                var shouldBeVisible = tabsActive && i < configuredTabCount;
                if (button.gameObject.activeSelf != shouldBeVisible) button.gameObject.SetActive(shouldBeVisible);
            }

            _activeSquadTabIndex = Mathf.Clamp(_activeSquadTabIndex, 0, Mathf.Max(0, configuredTabCount - 1));
            UpdateSquadTabLabels();
            UpdateSquadTabVisuals();
            LayoutSquadTabRow();
        }


        private void UpdateSquadTabVisuals()
        {
            if (squadTabButtons == null || squadTabButtons.Length == 0) return;

            RebuildSquadTabSelectionHighlights();

            var tabsActive = IsSquadTabPagingActive();
            var configuredTabCount = GetConfiguredSquadTabCount();

            for (var i = 0; i < squadTabButtons.Length; i++)
            {
                var button = squadTabButtons[i];
                if (button == null) continue;

                var shouldBeVisible = tabsActive && i < configuredTabCount;
                if (button.gameObject.activeSelf != shouldBeVisible) button.gameObject.SetActive(shouldBeVisible);

                var hasSelectedMembers = _squadTabIndicesWithSelection.Contains(i);
                var isViewingTab = i == _activeSquadTabIndex;
                var useActiveStyle = hasSelectedMembers
                                     || (_squadTabIndicesWithSelection.Count == 0 && isViewingTab);

                var buttonImage = button.targetGraphic as Image;
                if (buttonImage == null) buttonImage = button.GetComponent<Image>();

                if (buttonImage != null)
                    buttonImage.color = useActiveStyle ? squadTabActiveColor : squadTabInactiveColor;

                var label = GetOrCacheTabButtonLabel(button);
                if (label != null)
                {
                    label.color = useActiveStyle
                        ? squadTabActiveLabelColor
                        : squadTabInactiveLabelColor;
                }
            }
        }


        private void RebuildSquadTabSelectionHighlights()
        {
            _squadTabIndicesWithSelection.Clear();
            if (!IsSquadTabPagingActive()) return;

            EnsureSquadGroupDefinitions();

            var squadManager = SquadManager.Instance;
            if (squadManager == null) return;

            var selectedMembers = squadManager.SelectedMembers;
            for (var i = 0; i < selectedMembers.Count; i++)
            {
                var member = selectedMembers[i];
                var unit = member?.Unit;
                if (unit == null) continue;

                var tabIndex = FindSquadIndexContainingUnit(unit);
                if (tabIndex >= 0)
                    _squadTabIndicesWithSelection.Add(tabIndex);
            }
        }


        private TextMeshProUGUI GetOrCacheTabButtonLabel(Button button)
        {
            if (button == null) return null;

            if (_squadTabLabelByButton.TryGetValue(button, out var cachedLabel) && cachedLabel != null)
                return cachedLabel;

            var resolvedLabel = button.GetComponentInChildren<TextMeshProUGUI>(true);
            _squadTabLabelByButton[button] = resolvedLabel;
            return resolvedLabel;
        }


        private static int CompareUnitsForRoster(Unit a, Unit b)
        {
            var roleCompare = GetRolePriority(a.Role).CompareTo(GetRolePriority(b.Role));
            return roleCompare != 0
                ? roleCompare
                : string.Compare(a.gameObject.name, b.gameObject.name, StringComparison.OrdinalIgnoreCase);
        }


        private static int GetRolePriority(UnitRole role)
        {
            return role switch
            {
                UnitRole.Player => 0,
                UnitRole.SquadMember => 1,
                UnitRole.Survivor => 2,
                _ => 3
            };
        }
    }
}
