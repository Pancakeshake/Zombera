#region

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed partial class ZomberaSquadManagementUI
    {
        private void Awake()
        {
            if (!buildOnAwake) return;

            BuildOrResolveUI();
            WireInteractions();
            PopulateInitialData();
            ShowTab(TabId.Squad);
            SetVisible(visibleOnStart && IsGameplayUiAllowed());
        }

        private void Update()
        {
            if (!useLiveGameData || liveRefreshInterval <= 0f || !IsVisible) return;

            _liveRefreshTimer += Time.unscaledDeltaTime;
            if (_liveRefreshTimer < liveRefreshInterval) return;

            _liveRefreshTimer = 0f;
            _ = TrySyncFromLiveData();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            TryReleaseVisibilityPause();
        }

        private void RebuildNowInternal()
        {
            forceRebuildOnAwake = true;
            BuildOrResolveUI();
            WireInteractions();
            PopulateInitialData();
            ShowTab(TabId.Squad);
            SetVisible(true);
        }

        private void SetVisibleInternal(bool visible)
        {
            var wasVisible = IsVisible;

            if (visible) RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();

            if (screenRoot == null)
            {
                if (!visible)
                {
                    if (wasVisible) TryReleaseVisibilityPause();

                    return;
                }

                BuildOrResolveUI();
                WireInteractions();
                PopulateInitialData();
                ShowTab(TabId.Squad);
            }

            if (screenRoot != null)
            {
                RepairMissingSlicedSprites();
                screenRoot.gameObject.SetActive(visible);
            }

            if (!wasVisible && visible)
                TryApplyVisibilityPause();
            else if (wasVisible && !visible) TryReleaseVisibilityPause();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;

            InvalidateLiveDataUnitCaches();

            if (IsGameplayUiAllowed()) return;

            SetVisible(false);
        }

        private void InvalidateLiveDataUnitCaches()
        {
            _cachedPlayerUnit = null;
            _playerUnitCacheValidUntil = -1000f;
            _cachedAllUnits.Clear();
            _allUnitsCacheValidUntil = -1000f;
            _cachedSquadManager = null;
            _squadManagerCacheValidUntil = -1000f;
        }

        private static bool IsGameplayUiAllowed()
        {
            if (GameManager.Instance != null)
            {
                var state = GameManager.Instance.CurrentState;
                return state == GameState.Playing || state == GameState.Paused;
            }

            var activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() &&
                   string.Equals(activeScene.name, "World", StringComparison.OrdinalIgnoreCase);
        }

        private void TryApplyVisibilityPause()
        {
            var timeSystem = ResolveTimeSystem();
            if (timeSystem == null)
            {
                _pausedByVisibility = false;
                return;
            }

            if (!timeSystem.IsPaused)
            {
                timeSystem.RequestPause();
                _pausedByVisibility = true;
                return;
            }

            _pausedByVisibility = false;
        }

        private void TryReleaseVisibilityPause()
        {
            if (!_pausedByVisibility) return;

            var timeSystem = ResolveTimeSystem();
            if (timeSystem != null) timeSystem.RequestResume();

            _pausedByVisibility = false;
        }

        private TimeSystem ResolveTimeSystem()
        {
            if (_resolvedTimeSystem == null) _resolvedTimeSystem = FindFirstObjectByType<TimeSystem>();

            return _resolvedTimeSystem;
        }

        private void PopulateInitialData()
        {
            if (useLiveGameData && TrySyncFromLiveData()) return;

            if (keepDemoFallbackWhenNoLiveData) SeedDemoData();
        }
    }
}
