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
#pragma warning disable S101 // HUD is an intentional acronym in this project's naming convention
    public sealed partial class WorldHUDController
    {
        private void OnBuildModeChanged(BuildModeChangedEvent evt)
        {
            if (evt.IsActive == _showingBuildItems) return;

            HandleBuildModeVisibilityTransition(evt.IsActive);
        }


        private void TickBottomBuildModeState()
        {
            RefreshBuildDependenciesForTick();

            if (_showingBuildItems)
            {
                if (_pausedByMenuTab)
                    ReleaseMenuPauseIfOwned();

                UpdateBuildCatalogStateForCurrentFrame();
                RefreshBottomBuildItemHighlights();
            }
        }


        private void HandleBuildModeVisibilityTransition(bool showBuildItems)
        {
            BuildLog($"Build mode visibility changed: {_showingBuildItems} -> {showBuildItems}");
            _showingBuildItems = showBuildItems;

            if (_showingBuildItems)
                PrepareBuildCatalogStateForModeEnter();
            else
                ResetBuildCatalogStateForModeExit();

            MarkBuildUiDirty(includeFilter: true);
            ApplyBottomSquadsExpandedVisualState();
            UpdateLayoutForTabState(ActiveTab != TabId.None);
        }


        private void PrepareBuildCatalogStateForModeEnter()
        {
            RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy.IfMissing);
            _nextBuildCatalogPollAt = 0f;
            _lastKnownBuildItemCount = -1;
        }


        private void ResetBuildCatalogStateForModeExit()
        {
            _explicitBuildItemBoxIndex = -2;
            _activeBuildCategoryIndex = -1;
            _filteredBuildSourceIndices.Clear();
            _buildLabelCache.Clear();
            _buildItemPageOffset = 0;
        }


        private void HideBuildPageIndicatorVisual()
        {
            if (_buildPageIndicatorRoot != null && _buildPageIndicatorRoot.gameObject.activeSelf)
                _buildPageIndicatorRoot.gameObject.SetActive(false);
        }


        private void SyncBuildCategoryFromTabs()
        {
            RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy.IfMissing);
            if (_easyBuildRadialMenuInputBridge == null)
            {
                BuildLog("Skipping category sync: EasyBuild bridge missing", true);
                return;
            }

            if (portraitStrip == null)
            {
                BuildLog("Skipping category sync: portrait strip missing", true);
                return;
            }

            var categoryCount = _easyBuildRadialMenuInputBridge.GetBuildCategoryCount();
            if (categoryCount <= 0)
            {
                BuildLog("Skipping category sync: no categories available");
                return;
            }

            var targetCategory = Mathf.Clamp(portraitStrip.ActiveSquadTabIndex, 0, categoryCount - 1);
            if (targetCategory == _activeBuildCategoryIndex) return;

            if (_easyBuildRadialMenuInputBridge.TrySetActiveBuildCategory(targetCategory))
            {
                BuildLog($"Category synced to {targetCategory} (count={categoryCount})");
                _activeBuildCategoryIndex = targetCategory;
                _explicitBuildItemBoxIndex = -2;
                _buildItemPageOffset = 0;
                _buildLabelCache.Clear();
                _lastKnownBuildItemCount = -1;
                MarkBuildUiDirty(includeFilter: true);
            }
            else
            {
                BuildLog($"Category sync failed for target={targetCategory} (count={categoryCount})");
            }
        }


        private void BuildLog(string message, bool verbose = false)
        {
            if (!enableBuildDebugLogs) return;
            if (verbose && !verboseBuildDebugLogs) return;

            Debug.Log($"[WorldHUD][Build] {message}", this);
        }
    }
}
