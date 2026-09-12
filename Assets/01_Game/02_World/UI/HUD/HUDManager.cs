#region

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Zombera.UI.SquadManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#endregion

namespace Zombera.UI
{
    // ReSharper disable InvertIf
    /// <summary>
    ///     Initializes and coordinates all HUD panel controllers.
    /// </summary>
    [SuppressMessage(
        "Major Code Smell",
        "S101",
        Justification = "HUDManager is a long-established serialized type name used by scenes and prefabs.")]
    public sealed class HUDManager : MonoBehaviour
    {
        [Header("HUD Root")] [SerializeField] private Canvas hudCanvas;

        [SerializeField] private RectTransform hudRoot;

        [Header("Panel Prefabs")] [SerializeField]
        private GameObject squadPanelPrefab;

        [SerializeField] private GameObject commandPanelPrefab;
        [SerializeField] private GameObject minimapPrefab;
        [SerializeField] private GameObject playerStatusPrefab;
        [SerializeField] private GameObject hotbarPrefab;
        [SerializeField] private GameObject alertPanelPrefab;

        [Header("Panel Instances")] [SerializeField]
        private SquadPanelController squadPanel;

        [SerializeField] private CommandPanelController commandPanel;
        [SerializeField] private MinimapController minimap;
        [SerializeField] private PlayerStatusController playerStatus;
        [SerializeField] private HotbarController hotbar;
        [SerializeField] private AlertController alertPanel;

        [Header("Squad Management Overlay")] [SerializeField]
        private bool enableSquadManagementOverlay = true;

        [SerializeField] private bool squadManagementStartsOpen;
        [SerializeField] private KeyCode toggleSquadManagementKey = KeyCode.Tab;
        [SerializeField] private KeyCode closeSquadManagementKey = KeyCode.Escape;
        [SerializeField] private KeyCode openSquadTabKey = KeyCode.F1;
        [SerializeField] private KeyCode openInventoryTabKey = KeyCode.F2;
        [SerializeField] private KeyCode openCraftingTabKey = KeyCode.F3;
        [SerializeField] private KeyCode openMapTabKey = KeyCode.F4;
        [SerializeField] private KeyCode openMissionsTabKey = KeyCode.F5;
        [SerializeField] private KeyCode openFormationsTabKey = KeyCode.F6;
        [SerializeField] private KeyCode openJobsTabKey = KeyCode.F7;
        [SerializeField] private KeyCode openFactionsTabKey = KeyCode.F8;
        [SerializeField] private KeyCode quickOpenInventoryKey = KeyCode.I;
        [SerializeField] private ZomberaSquadManagementUI squadManagementUI;

        private bool _isSquadManagementVisible;

    #if ENABLE_INPUT_SYSTEM
        private static readonly IReadOnlyDictionary<KeyCode, Key> InputSystemKeyMap = new Dictionary<KeyCode, Key>
        {
            [KeyCode.Alpha0] = Key.Digit0,
            [KeyCode.Alpha1] = Key.Digit1,
            [KeyCode.Alpha2] = Key.Digit2,
            [KeyCode.Alpha3] = Key.Digit3,
            [KeyCode.Alpha4] = Key.Digit4,
            [KeyCode.Alpha5] = Key.Digit5,
            [KeyCode.Alpha6] = Key.Digit6,
            [KeyCode.Alpha7] = Key.Digit7,
            [KeyCode.Alpha8] = Key.Digit8,
            [KeyCode.Alpha9] = Key.Digit9,
            [KeyCode.Return] = Key.Enter,
            [KeyCode.KeypadEnter] = Key.Enter,
            [KeyCode.LeftControl] = Key.LeftCtrl,
            [KeyCode.RightControl] = Key.RightCtrl,
            [KeyCode.LeftShift] = Key.LeftShift,
            [KeyCode.RightShift] = Key.RightShift,
            [KeyCode.LeftAlt] = Key.LeftAlt,
            [KeyCode.RightAlt] = Key.RightAlt,
            [KeyCode.LeftCommand] = Key.LeftMeta,
            [KeyCode.RightCommand] = Key.RightMeta,
            [KeyCode.BackQuote] = Key.Backquote,
            [KeyCode.None] = Key.None,
            [KeyCode.Backspace] = Key.Backspace,
            [KeyCode.Delete] = Key.Delete
        };
    #endif

        public bool IsInitialized { get; private set; }

        public MinimapController Minimap => minimap;
        public PlayerStatusController PlayerStatus => playerStatus;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            HandleSquadManagementInput();
        }

        public void Initialize()
        {
            if (IsInitialized) return;

            if (hudRoot == null)
                hudRoot = hudCanvas != null ? hudCanvas.transform as RectTransform : transform as RectTransform;

            squadPanel = EnsurePanelInstance(squadPanelPrefab, squadPanel, "SquadPanel");
            commandPanel = EnsurePanelInstance(commandPanelPrefab, commandPanel, "CommandPanel");
            minimap = EnsurePanelInstance(minimapPrefab, minimap, "Minimap");
            playerStatus = EnsurePanelInstance(playerStatusPrefab, playerStatus, "PlayerStatus");
            hotbar = EnsurePanelInstance(hotbarPrefab, hotbar, "Hotbar");
            alertPanel = EnsurePanelInstance(alertPanelPrefab, alertPanel, "AlertPanel");

            squadPanel?.Initialize(this);
            commandPanel?.Initialize(this);
            minimap?.Initialize(this);
            playerStatus?.Initialize(this);
            hotbar?.Initialize(this);
            alertPanel?.Initialize(this);

            InitializeSquadManagementOverlay();

            IsInitialized = true;
            BindGameplayEvents();

            // Check initial visibility in case we spawned after the state change event
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                SetVisible(true);
            }
            }

        private void BindGameplayEvents()
        {
            CoreEventBus.Instance?.Subscribe<SquadRosterChangedEvent>(OnSquadRosterChanged);
            CoreEventBus.Instance?.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private static void OnSquadRosterChanged(SquadRosterChangedEvent evt)
        {
            var manager = FindFirstObjectByType<HUDManager>();
            if (manager == null || manager.squadPanel == null) return;

            manager.RefreshSquadPanel();
        }

        public void RefreshSquadPanel()
        {
            if (squadPanel == null) return;

            var squadManager = SquadManager.Instance != null
                ? SquadManager.Instance
                : FindFirstObjectByType<SquadManager>();
            if (squadManager == null) return;

            var members = squadManager.SquadMembers;
            var viewData = new List<SquadMemberViewData>(members.Count);

            foreach (var member in members)
            {
                if (member == null) continue;

                var isSelected = squadManager.SelectedMembers.Contains(member);
                var health = member.UnitHealth;

                viewData.Add(new SquadMemberViewData
                {
                    unitId = member.MemberId,
                    displayName = member.gameObject.name,
                    health01 = health != null && health.MaxHealth > 0 ? health.CurrentHealth / health.MaxHealth : 0f,
                    isSelected = isSelected,
                    isDowned = health != null && health.IsDead // Adjust based on project's 'downed' definition
                });
            }

            squadPanel.SetMembers(viewData);
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            // Automatically hide or show the HUD based on game state.
            var playing = evt.NewState == GameState.Playing;
            SetVisible(playing);
        }

        /// <summary>Wires the player status panel to a live unit's health/stamina/morale events.</summary>
        public void BindPlayerUnit(Unit unit)
        {
            if (!IsInitialized) Initialize();
            playerStatus?.BindUnit(unit);
        }

        public void SetVisible(bool visible)
        {
            if (hudCanvas != null) hudCanvas.enabled = visible;

            squadPanel?.SetVisible(visible);
            commandPanel?.SetVisible(visible);
            minimap?.SetVisible(visible);
            playerStatus?.SetVisible(visible);
            hotbar?.SetVisible(visible);
            alertPanel?.SetVisible(visible);

            if (squadManagementUI != null) squadManagementUI.SetVisible(visible && _isSquadManagementVisible);
        }

        public void ClearAlert()
        {
            alertPanel?.ClearAlert();
        }

        private T EnsurePanelInstance<T>(GameObject prefab, T existing, string fallbackName) where T : MonoBehaviour
        {
            if (existing != null) return existing;

            GameObject panelObject;

            if (prefab != null)
            {
                panelObject = Instantiate(prefab, hudRoot);
            }
            else
            {
                panelObject = new GameObject(fallbackName, typeof(RectTransform));
                panelObject.transform.SetParent(hudRoot, false);
            }

            var panelController = panelObject.GetComponent<T>();

            if (panelController == null) panelController = panelObject.AddComponent<T>();

            return panelController;
        }

        private void InitializeSquadManagementOverlay()
        {
            if (!enableSquadManagementOverlay)
            {
                _isSquadManagementVisible = false;
                return;
            }

            if (squadManagementUI == null) squadManagementUI = GetComponentInChildren<ZomberaSquadManagementUI>(true);

            if (squadManagementUI == null) squadManagementUI = FindFirstObjectByType<ZomberaSquadManagementUI>();

            if (squadManagementUI == null)
            {
                var overlayRoot = new GameObject("SquadManagementUI", typeof(RectTransform));
                overlayRoot.transform.SetParent(hudRoot != null ? hudRoot : transform, false);
                squadManagementUI = overlayRoot.AddComponent<ZomberaSquadManagementUI>();
            }

            _isSquadManagementVisible = squadManagementStartsOpen;
            squadManagementUI.SetVisible(_isSquadManagementVisible);
        }

        private void HandleSquadManagementInput()
        {
            if (!Application.isPlaying || !enableSquadManagementOverlay || squadManagementUI == null) return;

            if (HandleSquadManagementTabHotkeys()) return;

            if (WasKeyPressedThisFrame(toggleSquadManagementKey))
            {
                _isSquadManagementVisible = !_isSquadManagementVisible;
                squadManagementUI.SetVisible(_isSquadManagementVisible);
                return;
            }

            if (!_isSquadManagementVisible || !WasKeyPressedThisFrame(closeSquadManagementKey)) return;

            _isSquadManagementVisible = false;
            squadManagementUI.SetVisible(false);
        }

        private bool HandleSquadManagementTabHotkeys()
        {
            if (WasKeyPressedThisFrame(openSquadTabKey))
            {
                OpenSquadManagementTab(MenuHotkeyTab.Squad);
                return true;
            }

            if (WasKeyPressedThisFrame(openInventoryTabKey) || WasKeyPressedThisFrame(quickOpenInventoryKey))
            {
                OpenSquadManagementTab(MenuHotkeyTab.Inventory);
                return true;
            }

            if (WasKeyPressedThisFrame(openCraftingTabKey))
            {
                OpenSquadManagementTab(MenuHotkeyTab.Crafting);
                return true;
            }

            if (WasKeyPressedThisFrame(openMapTabKey))
            {
                OpenSquadManagementTab(MenuHotkeyTab.Map);
                return true;
            }

            if (WasKeyPressedThisFrame(openMissionsTabKey))
            {
                OpenSquadManagementTab(MenuHotkeyTab.Missions);
                return true;
            }

            if (WasKeyPressedThisFrame(openFormationsTabKey))
            {
                OpenSquadManagementTab(MenuHotkeyTab.Formations);
                return true;
            }

            if (!WasKeyPressedThisFrame(openJobsTabKey))
            {
                if (!WasKeyPressedThisFrame(openFactionsTabKey)) return false;
                OpenSquadManagementTab(MenuHotkeyTab.Factions);
                return true;
            }

            OpenSquadManagementTab(MenuHotkeyTab.Jobs);
            return true;
        }

        private void OpenSquadManagementTab(MenuHotkeyTab tab)
        {
            switch (tab)
            {
                case MenuHotkeyTab.Squad:
                    squadManagementUI.OpenSquadTab();
                    break;
                case MenuHotkeyTab.Inventory:
                    squadManagementUI.OpenInventoryTab();
                    break;
                case MenuHotkeyTab.Crafting:
                    squadManagementUI.OpenCraftingTab();
                    break;
                case MenuHotkeyTab.Map:
                    squadManagementUI.OpenMapTab();
                    break;
                case MenuHotkeyTab.Missions:
                    squadManagementUI.OpenMissionsTab();
                    break;
                case MenuHotkeyTab.Formations:
                    squadManagementUI.OpenFormationsTab();
                    break;
                case MenuHotkeyTab.Jobs:
                    squadManagementUI.OpenJobsTab();
                    break;
                case MenuHotkeyTab.Factions:
                    squadManagementUI.OpenFactionsTab();
                    break;
                default:
                    squadManagementUI.OpenSquadTab();
                    break;
            }

            _isSquadManagementVisible = true;
        }

        private static bool WasKeyPressedThisFrame(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && TryMapKeyCodeToInputSystemKey(keyCode, out var mappedKey))
            {
                var keyControl = keyboard[mappedKey];
                return keyControl is { wasPressedThisFrame: true };
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryMapKeyCodeToInputSystemKey(KeyCode keyCode, out Key key)
        {
            if (InputSystemKeyMap.TryGetValue(keyCode, out key)) return true;

            if (Enum.TryParse(keyCode.ToString(), true, out key)) return true;

            key = Key.None;
            return false;
        }
#endif

        private enum MenuHotkeyTab
        {
            Squad,
            Inventory,
            Crafting,
            Map,
            Missions,
            Formations,
            Jobs,
            Factions
        }
    }
}