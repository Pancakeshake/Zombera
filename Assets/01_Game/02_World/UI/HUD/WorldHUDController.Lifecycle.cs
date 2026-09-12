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
using Zombera.UI.SquadManagement;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.UI
{
#pragma warning disable S101 // HUD is an intentional acronym in this project's naming convention
    public sealed partial class WorldHUDController
    {

        private void Awake()
        {
            playerDamagePopupFontSize = Mathf.Max(playerDamagePopupFontSize, 44f);
            enemyDamagePopupFontSize = Mathf.Max(enemyDamagePopupFontSize, 36f);

            // Self-destruct if this ended up in a non-World scene (e.g. built by accident
            // into the main menu scene). This prevents any scripts on child objects from
            // running and interfering with other UI such as the character creator.
            var sceneName = gameObject.scene.name;
            var inWorldScene = sceneName.IndexOf("World", StringComparison.OrdinalIgnoreCase) >= 0
                               || sceneName.IndexOf("3_Ui", StringComparison.OrdinalIgnoreCase) >= 0
                               || sceneName.IndexOf("Tester", StringComparison.OrdinalIgnoreCase) >= 0
                               || sceneName.IndexOf("Single_Tile_Stress", StringComparison.OrdinalIgnoreCase) >= 0
                               || sceneName.IndexOf("Test_AnimationBlendTrees",
                                   StringComparison.OrdinalIgnoreCase) >= 0
                               || sceneName.IndexOf("Animation_Testing", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!inWorldScene)
            {
                Debug.LogWarning(
                    $"[WorldHUDController] Found in scene '{sceneName}' — destroying to avoid menu interference. Run 'Tools/1. Quick Dev Tools/Build World HUD' from the World scene only.",
                    this);
                Destroy(gameObject);
                return;
            }

            _canvas = GetComponent<Canvas>();
            
            // Stay hidden until GameState.Playing — prevents rendering over main menu.
            // Skip this in UI testing/dev scenes to ensure immediate visibility.
            if (_canvas != null && !IsHudDevScene(sceneName)) _canvas.enabled = false;

            ResolveEasyBuildReferences();
        }


        private void Start()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (showEditorLayoutPreviews)
                    EnsureEditorLayoutPreviewVisibility();
                return;
            }
#endif

            ResolveEasyBuildReferences();

            EnsureInventoryPanelController();
            EnsureCraftingTabController();
            EnsureMapPanelController();
            EnsureMapDebugOverlay();
            _timeSystem = FindFirstObjectByType<TimeSystem>();
            SetAllPanels(false);
if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
            InitializeLayoutState();
            InitializeBottomSquadsDropTab();
            EnsureBuildSearchFieldVisual();
            EnsureBottomBuildItemsStrip();
            EnsureBuildControlsPopupVisual();
            ApplyBuildControlsPopupState(false);
            ResetBottomBarToSquadView();
            UpdateLayoutForTabState(false);
            MarkBuildUiDirty(includeFilter: true);

            if (pauseMenu != null) pauseMenu.Initialize();

            EnsureFormationsJobsFactionsPanels();
            EnsureHudDevPlaceholderData();

            // Sync visibility in case gameplay state was already set before this HUD subscribed.
            EnsureHudCanvasVisibleForCurrentContext();
        }

        private static bool IsHudDevScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return false;

            return sceneName.IndexOf("3_Ui", StringComparison.OrdinalIgnoreCase) >= 0
                   || sceneName.IndexOf("Tester", StringComparison.OrdinalIgnoreCase) >= 0
                   || sceneName.IndexOf("Single_Tile_Stress", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsHudDevScene()
        {
            return IsHudDevScene(gameObject.scene.name);
        }

        private void EnsureHudCanvasVisibleForCurrentContext()
        {
            if (_canvas == null) return;
            if (IsHudDevScene())
            {
                _canvas.enabled = true;
                return;
            }

            if (GameManager.Instance == null) return;

            var currentState = GameManager.Instance.CurrentState;
            if (currentState is GameState.Playing or GameState.Paused)
                _canvas.enabled = true;
        }


        private void Update()
        {
            if (!_damageEventsSubscribed) TrySubscribeDamageEvents();
            UpdateDamagePopups();

            if (Keyboard.current is { } kb)
            {
                if (TryHandleBuildHotkeys(kb)) return;
                if (TryHandleTabHotkey(kb)) return;
            }

            if (_showingBuildItems)
                TickBottomBuildModeState();
        }

        private bool TryHandleBuildHotkeys(Keyboard kb)
        {
            if (HandleBuildPageHotkeys(kb)) return true;
            if (HandleBottomBuildHotkeys(kb)) return true;
            if (HandleBottomSquadHotkeys(kb)) return true;
            return false;
        }

        private bool TryHandleTabHotkey(Keyboard kb)
        {
            if (kb.f1Key.wasPressedThisFrame) { ToggleTab(TabId.Squad); return true; }
            if (kb.f2Key.wasPressedThisFrame) { ToggleTab(TabId.Inventory); return true; }
            if (kb.f3Key.wasPressedThisFrame) { ToggleTab(TabId.Crafting); return true; }
            if (kb.f4Key.wasPressedThisFrame) { ToggleTab(TabId.Map); return true; }
            if (kb.f5Key.wasPressedThisFrame) { ToggleTab(TabId.Missions); return true; }
            if (kb.f6Key.wasPressedThisFrame) { ToggleTab(TabId.Formations); return true; }
            if (kb.f7Key.wasPressedThisFrame) { ToggleTab(TabId.Jobs); return true; }
            if (kb.f8Key.wasPressedThisFrame) { ToggleTab(TabId.Factions); return true; }
            if (kb.escapeKey.wasPressedThisFrame) { HandleEscapeKey(); return true; }
            return false;
        }

        private void HandleEscapeKey()
        {
            if (IsBuildModeActive() && TryDispatchBuildCommand(BuildHudCommandType.CancelBuild))
                return;

            if (ActiveTab != TabId.None)
            {
                CloseTab();
                return;
            }

            if (pauseMenu == null) return;

            if (pauseMenu.IsVisible)
                pauseMenu.Resume();
            else
            {
                pauseMenu.Show();
                GameManager.Instance?.SetGameState(GameState.Paused);
            }
        }


        private void OnEnable()
        {
            CoreEventBus.Instance?.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            CoreEventBus.Instance?.Subscribe<BuildModeChangedEvent>(OnBuildModeChanged);
            TrySubscribeDamageEvents();
#if UNITY_EDITOR
            TryRefreshEditorLayoutPreviewOnEnable();
#endif
        }


        private void OnDisable()
        {
            CoreEventBus.Instance?.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            CoreEventBus.Instance?.Unsubscribe<BuildModeChangedEvent>(OnBuildModeChanged);
            UnsubscribeDamageEvents();
            ReleaseMenuPauseIfOwned();
            ClearDamagePopups();
        }


        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (IsHudDevScene())
            {
                if (_canvas != null) _canvas.enabled = true;
                return;
            }

            var gameplay = evt.NewState is GameState.Playing or GameState.Paused;
            if (_canvas is { } canvas) canvas.enabled = gameplay;

            if (evt.NewState == GameState.Playing)
                ResolveEasyBuildReferences();

            if (gameplay) return;

            CloseTab();
            ClearDamagePopups();
        }


        private void EnsureInventoryPanelController()
        {
            if (inventoryPanel is not { } panel || panel.GetComponent<InventoryPanelController>() != null) return;

            panel.AddComponent<InventoryPanelController>();
        }


        private void EnsureCraftingTabController()
        {
            if (craftingPanel == null) return;

            _craftingTab = craftingPanel.GetComponentInChildren<CraftingTabController>(true);

            if (_craftingTab == null && craftingTabPrefab != null)
            {
                // Remove placeholder text if it exists
                var placeholderTexts = craftingPanel.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                foreach (var txt in placeholderTexts)
                {
                    if (txt.text.Contains("coming soon", StringComparison.OrdinalIgnoreCase))
                        Destroy(txt.gameObject);
                }

                var go = Instantiate(craftingTabPrefab, craftingPanel.transform);
                go.name = "CraftingTab";
                _craftingTab = go.GetComponent<CraftingTabController>();
            }
        }


        private void EnsureMapPanelController()
        {
            if (mapPanel == null && panelsRoot != null)
            {
                var panelRect = MakeRect("MapPanel", panelsRoot);
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                mapPanel = panelRect.gameObject;
            }

            if (mapPanel == null) return;

            var controller = mapPanel.GetComponent<WorldMapPanelController>();
            if (controller == null) controller = mapPanel.AddComponent<WorldMapPanelController>();
            controller.Initialize();
        }


        private void EnsureMapDebugOverlay()
        {
            if (GetComponent<MapDebugOverlay>() != null) return;
            gameObject.AddComponent<MapDebugOverlay>();
        }
    }
}
