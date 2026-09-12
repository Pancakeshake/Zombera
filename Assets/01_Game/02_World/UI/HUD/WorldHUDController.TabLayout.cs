#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.UI
{
    public sealed partial class WorldHUDController
    {

        private void ApplyTab(TabId tab)
        {
            ActiveTab = tab;

            if (squadPanel) squadPanel.SetActive(tab == TabId.Squad);
            if (inventoryPanel) inventoryPanel.SetActive(tab == TabId.Inventory);
            if (craftingPanel) craftingPanel.SetActive(tab == TabId.Crafting);
            if (mapPanel) mapPanel.SetActive(tab == TabId.Map);
            if (missionsPanel) missionsPanel.SetActive(tab == TabId.Missions);
            if (formationsPanel) formationsPanel.SetActive(tab == TabId.Formations);
            if (jobsPanel) jobsPanel.SetActive(tab == TabId.Jobs);
            if (factionsPanel) factionsPanel.SetActive(tab == TabId.Factions);

            if (tab == TabId.Crafting && _craftingTab != null)
            {
                var unit = portraitStrip != null ? portraitStrip.SelectedUnit : null;
                _craftingTab.SetContext(unit, unit != null ? unit.Inventory : null);
                _craftingTab.SetContextSurvivor(unit != null ? unit.name : null);
            }

            RefreshFormationsJobsTab(tab);
            RefreshFactionsTab(tab);

            var panelOpen = tab != TabId.None;
if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(panelOpen);
                var c = dimOverlay.color;
                c.a = panelOpen ? dimAlpha : 0f;
                dimOverlay.color = c;
            }

            UpdateLayoutForTabState(panelOpen);
            ApplyMenuPauseState(panelOpen);

            topBar?.SetActiveTabHighlight(tab);
            OnTabChanged?.Invoke(tab);
        }


        private void ApplyMenuPauseState(bool panelOpen)
        {
            if (!pauseGameWhenMenuOpen)
            {
                if (!panelOpen) _pausedByMenuTab = false;

                return;
            }

            // Build placement must stay interactive; never let HUD tab pause suspend it.
            if (IsBuildModeActive())
            {
                if (_pausedByMenuTab)
                    ReleaseMenuPauseIfOwned();

                return;
            }

            var timeSystem = ResolveTimeSystem();
            if (timeSystem == null)
            {
                if (!panelOpen) _pausedByMenuTab = false;

                return;
            }

            if (panelOpen)
            {
                if (_pausedByMenuTab || timeSystem.IsPaused) return;

                timeSystem.RequestPause();
                _pausedByMenuTab = true;
                return;
            }

            ReleaseMenuPauseIfOwned();
        }


        private void ReleaseMenuPauseIfOwned()
        {
            if (!_pausedByMenuTab) return;

            _pausedByMenuTab = false;

            if (!pauseGameWhenMenuOpen) return;

            var timeSystem = ResolveTimeSystem();
            if (timeSystem == null || !timeSystem.IsPaused) return;

            if (GameManager.Instance is { CurrentState: var state }
                && state is not (GameState.Playing or GameState.Paused))
                return;

            timeSystem.RequestResume();
        }


        private TimeSystem ResolveTimeSystem()
        {
            if (_timeSystem == null) _timeSystem = FindFirstObjectByType<TimeSystem>();

            return _timeSystem;
        }


        private void InitializeLayoutState()
        {
            if (panelsRoot == null && squadPanel != null) panelsRoot = squadPanel.transform.parent as RectTransform;

            if (bottomBarRoot == null && portraitStrip != null)
                bottomBarRoot = portraitStrip.transform.parent as RectTransform;

            EnsureBottomDayTimeLayout();
            EnsureBottomSquadTabRowLayout(_bottomBarExpanded);

            if (panelsRoot == null) return;

            _panelsBottomInsetWhenBottomBarVisible = panelsRoot.offsetMin.y;
            _layoutInitialized = true;
        }


        private void EnsureBottomDayTimeLayout()
        {
            if (bottomBarRoot == null) return;

            _bottomDayTimeRoot ??= bottomBarRoot.Find("DayTimeBottomRight") as RectTransform;
            if (_bottomDayTimeRoot == null) return;

            var tabsRoot = bottomBarRoot.Find("SquadPageTabs") as RectTransform;
            var rowInsetFromTop = tabsRoot != null
                ? Mathf.Abs(tabsRoot.anchoredPosition.y)
                : 6f;
            var rowHeight = tabsRoot != null && tabsRoot.sizeDelta.y > 0f
                ? tabsRoot.sizeDelta.y
                : Mathf.Max(bottomSquadsTabHeight, 24f);

            _bottomDayTimeRoot.anchorMin = new Vector2(1f, 1f);
            _bottomDayTimeRoot.anchorMax = new Vector2(1f, 1f);
            _bottomDayTimeRoot.pivot = new Vector2(1f, 1f);
            _bottomDayTimeRoot.anchoredPosition = new Vector2(-12f, -rowInsetFromTop);
            _bottomDayTimeRoot.sizeDelta = new Vector2(240f, rowHeight);

            var label = _bottomDayTimeRoot.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.alignment = TextAlignmentOptions.MidlineRight;
        }


        private void EnsureBottomSquadTabRowLayout(bool bottomBarExpanded)
        {
            if (bottomBarRoot == null) return;

            var bottomBarImage = bottomBarRoot.GetComponent<Image>();
            if (bottomBarImage != null)
                bottomBarImage.raycastTarget = false;

            var tabsRoot = bottomBarRoot.Find("SquadPageTabs") as RectTransform;
            if (tabsRoot == null) return;

            var rowHeight = Mathf.Max(bottomSquadsTabHeight, 24f);
            const float horizontalInset = 8f;
            const float verticalInset = 6f;

            if (bottomBarExpanded)
            {
                tabsRoot.anchorMin = new Vector2(0f, 1f);
                tabsRoot.anchorMax = new Vector2(0f, 1f);
                tabsRoot.pivot = new Vector2(0f, 1f);
                tabsRoot.anchoredPosition = new Vector2(horizontalInset, -verticalInset);
            }
            else
            {
                tabsRoot.anchorMin = new Vector2(0f, 0f);
                tabsRoot.anchorMax = new Vector2(0f, 0f);
                tabsRoot.pivot = new Vector2(0f, 0f);
                tabsRoot.anchoredPosition = new Vector2(horizontalInset, verticalInset);
            }

            tabsRoot.sizeDelta = new Vector2(tabsRoot.sizeDelta.x, rowHeight);

            var tabsLayout = tabsRoot.GetComponent<HorizontalLayoutGroup>();
            if (tabsLayout != null)
            {
                tabsLayout.childAlignment = TextAnchor.MiddleLeft;
                tabsLayout.childControlHeight = true;
                tabsLayout.childForceExpandHeight = true;
            }

            portraitStrip?.EnsureBottomHudTabHitAreas();

            if (_bottomSquadsTabRoot != null)
                EnsureBottomHudButtonHitArea(_bottomSquadsTabRoot);
        }


        private void UpdateLayoutForTabState(bool panelOpen)
        {
            if (!_layoutInitialized) InitializeLayoutState();

            var showBottomBar = !panelOpen;

            if (bottomBarRoot != null)
            {
                bottomBarRoot.gameObject.SetActive(showBottomBar);
                if (showBottomBar)
                {
                    var size = bottomBarRoot.sizeDelta;
                    var targetHeight = _bottomBarExpanded
                        ? _bottomBarExpandedHeight
                        : Mathf.Max(bottomBarCollapsedHeight, bottomSquadsTabHeight + 6f);

                    if (!Mathf.Approximately(size.y, targetHeight))
                        bottomBarRoot.sizeDelta = new Vector2(size.x, targetHeight);
                }
            }

            ApplyBottomContentMode(showBottomBar);
            EnsureBottomDayTimeLayout();
            EnsureBottomSquadTabRowLayout(_bottomBarExpanded);
            if (_bottomSquadsTabRoot != null) _bottomSquadsTabRoot.gameObject.SetActive(!panelOpen);

            var showBuildExtras = _showingBuildItems && !panelOpen && _bottomBarExpanded;
            ApplyBuildSearchFieldState(showBuildExtras);
            ApplyBuildCommandStripState(showBuildExtras);
            ApplyBuildControlsPopupState(_showingBuildItems && !panelOpen);
            ApplyLegacyTopTabButtonsState(!showBuildExtras);

            var showQuickFormations = showBottomBar && _bottomBarExpanded && !showBuildExtras;
            portraitStrip?.SetQuickFormationStripVisible(showQuickFormations);

            if (panelsRoot == null) return;

            var collapsedInset = Mathf.Max(bottomBarCollapsedHeight, bottomSquadsTabHeight + 6f);
            var targetInset = 0f;
            if (showBottomBar) targetInset = _bottomBarExpanded ? _panelsBottomInsetWhenBottomBarVisible : collapsedInset;

            var min = panelsRoot.offsetMin;
            min.y = targetInset;
            panelsRoot.offsetMin = min;
        }


        private void ApplyLegacyTopTabButtonsState(bool visible)
        {
            if (_bottomSquadsTabRoot == null) return;

            var tabsRoot = _bottomSquadsTabRoot.parent as RectTransform;
            if (tabsRoot == null) return;

            for (var i = 0; i < tabsRoot.childCount; i++)
            {
                var child = tabsRoot.GetChild(i) as RectTransform;
                if (child == null) continue;

                if (child == _bottomSquadsTabRoot || child == _buildSearchFieldRoot || child == _buildPageIndicatorRoot
                    || child == _buildCommandStripRoot
                    || string.Equals(child.name, "QuickFormationStrip", StringComparison.Ordinal)
                    || string.Equals(child.name, "AddSquadTab", StringComparison.Ordinal)
                    || string.Equals(child.name, "SquadTabRenameInput", StringComparison.Ordinal))
                    continue;

                if (!child.name.StartsWith("SquadTab_", StringComparison.Ordinal))
                    continue;

                if (child.gameObject.activeSelf != visible)
                    child.gameObject.SetActive(visible);
            }
        }


        private void ApplyBottomContentMode(bool showBottomBar)
        {
            var showExpandedBottomContent = showBottomBar && _bottomBarExpanded;
            var showPortraits = showExpandedBottomContent && !_showingBuildItems;
            var showBuildItems = showExpandedBottomContent && _showingBuildItems;

            if (portraitStrip != null) portraitStrip.gameObject.SetActive(showPortraits);

            if (_bottomBuildItemsRoot == null && showBuildItems)
                EnsureBottomBuildItemsStrip();

            if (_bottomBuildItemsRoot != null)
                _bottomBuildItemsRoot.gameObject.SetActive(showBuildItems);

            if (showBuildItems)
                RefreshBottomBuildItemHighlights();
        }


        private void SetAllPanels(bool active)
        {
            if (squadPanel) squadPanel.SetActive(active);
            if (inventoryPanel) inventoryPanel.SetActive(active);
            if (craftingPanel) craftingPanel.SetActive(active);
            if (mapPanel) mapPanel.SetActive(active);
            if (missionsPanel) missionsPanel.SetActive(active);
            if (formationsPanel) formationsPanel.SetActive(active);
            if (jobsPanel) jobsPanel.SetActive(active);
        }
    }
}
