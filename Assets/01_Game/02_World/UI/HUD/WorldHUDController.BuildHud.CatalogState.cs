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
        private void MarkBuildUiDirty(bool includeFilter)
        {
            if (includeFilter)
                _buildFilterDirty = true;

            _buildVisualsDirty = true;
            _buildPageIndicatorDirty = true;
        }


        private void UpdateBuildCatalogStateForCurrentFrame()
        {
            TryApplyPendingBuildSearchQuery();
            RefreshBuildCatalogSnapshotIfNeeded();
            EnsureBuildUiFresh();
        }


        private void TryApplyPendingBuildSearchQuery()
        {
            if (!BuildCatalogStateService.TryConsumePendingSearch(
                    Time.unscaledTime,
                    ref _hasPendingBuildSearchQuery,
                    _buildSearchApplyAt,
                    _pendingBuildSearchQuery,
                    _buildSearchQuery,
                    out var nextQuery))
                return;

            _buildSearchQuery = nextQuery;
            _explicitBuildItemBoxIndex = -2;
            _buildItemPageOffset = 0;
            MarkBuildUiDirty(includeFilter: true);
            BuildLog($"Search updated to '{_buildSearchQuery}'");
        }


        private void RefreshBuildCatalogSnapshotIfNeeded()
        {
            if (_easyBuildRadialMenuInputBridge == null)
                return;

            var now = Time.unscaledTime;
            if (!BuildCatalogStateService.ShouldPollCatalog(now, _nextBuildCatalogPollAt, _buildFilterDirty))
                return;

            _nextBuildCatalogPollAt = now + BuildCatalogPollIntervalSeconds;

            var itemCount = _easyBuildRadialMenuInputBridge.GetBuildItemCount();
            if (!BuildCatalogStateService.TryUpdateKnownItemCount(itemCount, ref _lastKnownBuildItemCount))
                return;

            _buildLabelCache.Clear();
            MarkBuildUiDirty(includeFilter: true);
        }


        private void EnsureBuildUiFresh()
        {
            if (_buildFilterDirty)
            {
                RebuildFilteredBuildSourceIndices();
                _buildFilterDirty = false;
                _buildVisualsDirty = true;
                _buildPageIndicatorDirty = true;
            }

            if (_buildVisualsDirty)
            {
                RefreshBottomBuildItemVisuals();
                _buildVisualsDirty = false;
            }

            if (_buildPageIndicatorDirty)
            {
                RefreshBuildPageIndicator();
                _buildPageIndicatorDirty = false;
            }
        }


        private string GetCachedBuildLabel(int sourceIndex)
        {
            return BuildCatalogStateService.GetCachedBuildLabel(
                sourceIndex,
                _buildLabelCache,
                _easyBuildRadialMenuInputBridge);
        }


        private void RebuildFilteredBuildSourceIndices()
        {
            BuildCatalogStateService.RebuildFilteredBuildSourceIndices(
                _easyBuildRadialMenuInputBridge,
                _buildSearchQuery,
                _buildLabelCache,
                _filteredBuildSourceIndices,
                ref _lastKnownBuildItemCount,
                ref _buildItemPageOffset,
                BuildHudItemsPerPage,
                BuildLog);
        }


        private void ClampBuildItemPageOffset()
        {
            _buildItemPageOffset = BuildCatalogStateService.ClampBuildItemPageOffset(
                _buildItemPageOffset,
                _filteredBuildSourceIndices.Count,
                BuildHudItemsPerPage);
        }


        private int ResolveBuildSourceItemIndex(int visibleItemIndex)
        {
            return BuildCatalogStateService.ResolveBuildSourceItemIndex(
                visibleItemIndex,
                _buildItemPageOffset,
                _filteredBuildSourceIndices,
                BuildHudItemsPerPage);
        }


        private int MapBuildSourceToVisibleIndex(int sourceIndex)
        {
            return BuildCatalogStateService.MapBuildSourceToVisibleIndex(
                sourceIndex,
                _buildItemPageOffset,
                _filteredBuildSourceIndices,
                BuildHudItemsPerPage);
        }


        private static class BuildCatalogStateService
        {
            internal static bool TryConsumePendingSearch(
                float now,
                ref bool hasPending,
                float applyAt,
                string pendingSearchQuery,
                string currentSearchQuery,
                out string nextSearchQuery)
            {
                nextSearchQuery = currentSearchQuery;
                if (!hasPending)
                    return false;

                if (now < applyAt)
                    return false;

                hasPending = false;
                nextSearchQuery = pendingSearchQuery ?? string.Empty;
                return !string.Equals(currentSearchQuery, nextSearchQuery, StringComparison.Ordinal);
            }


            internal static bool ShouldPollCatalog(float now, float nextPollAt, bool filterDirty)
            {
                return filterDirty || now >= nextPollAt;
            }


            internal static bool TryUpdateKnownItemCount(int itemCount, ref int lastKnownBuildItemCount)
            {
                if (itemCount == lastKnownBuildItemCount)
                    return false;

                lastKnownBuildItemCount = itemCount;
                return true;
            }


            internal static string GetCachedBuildLabel(
                int sourceIndex,
                IDictionary<int, string> labelCache,
                EasyBuildRadialMenuInputBridge bridge)
            {
                if (sourceIndex < 0) return string.Empty;

                if (labelCache != null && labelCache.TryGetValue(sourceIndex, out var cachedLabel))
                    return cachedLabel ?? string.Empty;

                var label = bridge?.GetHudItemLabel(sourceIndex) ?? string.Empty;
                if (labelCache != null) labelCache[sourceIndex] = label;
                return label;
            }


#pragma warning disable S107 // Parameter count reflects complete operation context — acceptable for internal service method
            internal static void RebuildFilteredBuildSourceIndices(
                EasyBuildRadialMenuInputBridge bridge,
                string searchQuery,
                IDictionary<int, string> labelCache,
                IList<int> filteredSourceIndices,
                ref int lastKnownBuildItemCount,
                ref int buildItemPageOffset,
                int itemsPerPage,
                Action<string, bool> buildLog)
            {
                filteredSourceIndices?.Clear();

                if (bridge == null || filteredSourceIndices == null) return;

                var itemCount = bridge.GetBuildItemCount();
                if (lastKnownBuildItemCount != itemCount)
                {
                    lastKnownBuildItemCount = itemCount;
                    labelCache?.Clear();
                }

                if (itemCount <= 0)
                {
                    buildLog?.Invoke("Build source list empty: itemCount=0", false);
                    return;
                }

                var filter = (searchQuery ?? string.Empty).Trim();
                var hasFilter = !string.IsNullOrWhiteSpace(filter);

                for (var sourceIndex = 0; sourceIndex < itemCount; sourceIndex++)
                {
                    if (!hasFilter)
                    {
                        filteredSourceIndices.Add(sourceIndex);
                        continue;
                    }

                    var label = GetCachedBuildLabel(sourceIndex, labelCache, bridge);
                    if (label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        filteredSourceIndices.Add(sourceIndex);
                }

                buildItemPageOffset = ClampBuildItemPageOffset(buildItemPageOffset, filteredSourceIndices.Count,
                    itemsPerPage);
                buildLog?.Invoke(
                    $"Build source rebuilt: total={itemCount}, filtered={filteredSourceIndices.Count}, filter='{filter}'",
                    true);
            }


            internal static int ClampBuildItemPageOffset(int currentOffset, int sourceCount, int itemsPerPage)
            {
                if (sourceCount <= 0) return 0;

                var maxOffset = Mathf.Max(0, sourceCount - Mathf.Max(1, itemsPerPage));
                return Mathf.Clamp(currentOffset, 0, maxOffset);
            }


            internal static int ResolveBuildSourceItemIndex(
                int visibleItemIndex,
                int pageOffset,
                IReadOnlyList<int> filteredSourceIndices,
                int itemsPerPage)
            {
                if (visibleItemIndex < 0 || visibleItemIndex >= Mathf.Max(1, itemsPerPage)) return -1;
                if (filteredSourceIndices == null || filteredSourceIndices.Count == 0) return -1;

                var listIndex = pageOffset + visibleItemIndex;
                if (listIndex < 0 || listIndex >= filteredSourceIndices.Count) return -1;

                return filteredSourceIndices[listIndex];
            }


            internal static int MapBuildSourceToVisibleIndex(
                int sourceIndex,
                int pageOffset,
                IReadOnlyList<int> filteredSourceIndices,
                int itemsPerPage)
            {
                if (sourceIndex < 0 || filteredSourceIndices == null) return -1;

                var visibleCount = Mathf.Min(Mathf.Max(1, itemsPerPage), filteredSourceIndices.Count - pageOffset);
                for (var visible = 0; visible < visibleCount; visible++)
                {
                    var listIndex = pageOffset + visible;
                    if (listIndex < 0 || listIndex >= filteredSourceIndices.Count) break;

                    if (filteredSourceIndices[listIndex] == sourceIndex)
                        return visible;
                }

                return -1;
            }
        }
    }
}
