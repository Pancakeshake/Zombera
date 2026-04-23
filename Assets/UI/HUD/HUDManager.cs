using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Zombera.Characters;
using Zombera.UI.SquadManagement;

namespace Zombera.UI
{
    /// <summary>
    /// Initializes and coordinates all HUD panel controllers.
    /// </summary>
    public sealed class HUDManager : MonoBehaviour
    {
        [Header("HUD Root")]
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private RectTransform hudRoot;

        [Header("Panel Prefabs")]
        [SerializeField] private GameObject squadPanelPrefab;
        [SerializeField] private GameObject commandPanelPrefab;
        [SerializeField] private GameObject minimapPrefab;
        [SerializeField] private GameObject playerStatusPrefab;
        [SerializeField] private GameObject hotbarPrefab;
        [SerializeField] private GameObject alertPanelPrefab;

        [Header("Panel Instances")]
        [SerializeField] private SquadPanelController squadPanel;
        [SerializeField] private CommandPanelController commandPanel;
        [SerializeField] private MinimapController minimap;
        [SerializeField] private PlayerStatusController playerStatus;
        [SerializeField] private HotbarController hotbar;
        [SerializeField] private AlertController alertPanel;

        [Header("Squad Management Overlay")]
        [SerializeField] private bool enableSquadManagementOverlay = true;
        [SerializeField] private bool squadManagementStartsOpen;
        [SerializeField] private KeyCode toggleSquadManagementKey = KeyCode.Tab;
        [SerializeField] private KeyCode closeSquadManagementKey = KeyCode.Escape;
        [SerializeField] private KeyCode openSquadTabKey = KeyCode.F1;
        [SerializeField] private KeyCode openInventoryTabKey = KeyCode.F2;
        [SerializeField] private KeyCode openCraftingTabKey = KeyCode.F3;
        [SerializeField] private KeyCode openMapTabKey = KeyCode.F4;
        [SerializeField] private KeyCode openMissionsTabKey = KeyCode.F5;
        [SerializeField] private KeyCode quickOpenInventoryKey = KeyCode.I;
        [SerializeField] private ZomberaSquadManagementUI squadManagementUI;

        private bool isSquadManagementVisible;

        private enum MenuHotkeyTab
        {
            Squad,
            Inventory,
            Crafting,
            Map,
            Missions
        }

        public bool IsInitialized { get; private set; }

        public SquadPanelController SquadPanel => squadPanel;
        public CommandPanelController CommandPanel => commandPanel;
        public MinimapController Minimap => minimap;
        public PlayerStatusController PlayerStatus => playerStatus;
        public HotbarController Hotbar => hotbar;
        public AlertController AlertPanel => alertPanel;

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
            if (IsInitialized)
            {
                return;
            }

            if (hudRoot == null)
            {
                if (hudCanvas != null)
                {
                    hudRoot = hudCanvas.transform as RectTransform;
                }
                else
                {
                    hudRoot = transform as RectTransform;
                }
            }

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
        }

        private void BindGameplayEvents()
        {
            Zombera.Core.EventSystem.Instance?.Subscribe<Zombera.Core.SquadRosterChangedEvent>(OnSquadRosterChanged);
            Zombera.Core.EventSystem.Instance?.Subscribe<Zombera.Core.GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnSquadRosterChanged(Zombera.Core.SquadRosterChangedEvent evt)
        {
            // Roster changed — let any open squad panel refresh on the next frame.
            // Full row-population is handled by SquadPanelController.SetMembers().
            _ = evt;
        }

        private void OnGameStateChanged(Zombera.Core.GameStateChangedEvent evt)
        {
            // Automatically hide or show the HUD based on game state.
            bool playing = evt.NewState == Zombera.Core.GameState.Playing;
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
            if (hudCanvas != null)
            {
                hudCanvas.enabled = visible;
            }

            squadPanel?.SetVisible(visible);
            commandPanel?.SetVisible(visible);
            minimap?.SetVisible(visible);
            playerStatus?.SetVisible(visible);
            hotbar?.SetVisible(visible);
            alertPanel?.SetVisible(visible);

            if (squadManagementUI != null)
            {
                squadManagementUI.SetVisible(visible && isSquadManagementVisible);
            }
        }

        public void ShowAlert(AlertViewData alertData)
        {
            alertPanel?.ShowAlert(alertData);
        }

        public void ClearAlert()
        {
            alertPanel?.ClearAlert();
        }

        private T EnsurePanelInstance<T>(GameObject prefab, T existing, string fallbackName) where T : MonoBehaviour
        {
            if (existing != null)
            {
                return existing;
            }

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

            T panelController = panelObject.GetComponent<T>();

            if (panelController == null)
            {
                panelController = panelObject.AddComponent<T>();
            }

            return panelController;
        }

        private void InitializeSquadManagementOverlay()
        {
            if (!enableSquadManagementOverlay)
            {
                isSquadManagementVisible = false;
                return;
            }

            if (squadManagementUI == null)
            {
                squadManagementUI = GetComponentInChildren<ZomberaSquadManagementUI>(true);
            }

            if (squadManagementUI == null)
            {
                squadManagementUI = FindFirstObjectByType<ZomberaSquadManagementUI>();
            }

            if (squadManagementUI == null)
            {
                GameObject overlayRoot = new GameObject("SquadManagementUI", typeof(RectTransform));
                overlayRoot.transform.SetParent(hudRoot != null ? hudRoot : transform, false);
                squadManagementUI = overlayRoot.AddComponent<ZomberaSquadManagementUI>();
            }

            isSquadManagementVisible = squadManagementStartsOpen;
            squadManagementUI.SetVisible(isSquadManagementVisible);
        }

        private void HandleSquadManagementInput()
        {
            if (!Application.isPlaying || !enableSquadManagementOverlay || squadManagementUI == null)
            {
                return;
            }

            if (HandleSquadManagementTabHotkeys())
            {
                return;
            }

            if (WasKeyPressedThisFrame(toggleSquadManagementKey))
            {
                isSquadManagementVisible = !isSquadManagementVisible;
                squadManagementUI.SetVisible(isSquadManagementVisible);
                return;
            }

            if (isSquadManagementVisible && WasKeyPressedThisFrame(closeSquadManagementKey))
            {
                isSquadManagementVisible = false;
                squadManagementUI.SetVisible(false);
            }
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

            return false;
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
                default:
                    squadManagementUI.OpenSquadTab();
                    break;
            }

            isSquadManagementVisible = true;
        }

        private static bool WasKeyPressedThisFrame(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && TryMapKeyCodeToInputSystemKey(keyCode, out Key mappedKey))
            {
                var keyControl = keyboard[mappedKey];
                return keyControl != null && keyControl.wasPressedThisFrame;
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
            switch (keyCode)
            {
                case KeyCode.Alpha0: key = Key.Digit0; return true;
                case KeyCode.Alpha1: key = Key.Digit1; return true;
                case KeyCode.Alpha2: key = Key.Digit2; return true;
                case KeyCode.Alpha3: key = Key.Digit3; return true;
                case KeyCode.Alpha4: key = Key.Digit4; return true;
                case KeyCode.Alpha5: key = Key.Digit5; return true;
                case KeyCode.Alpha6: key = Key.Digit6; return true;
                case KeyCode.Alpha7: key = Key.Digit7; return true;
                case KeyCode.Alpha8: key = Key.Digit8; return true;
                case KeyCode.Alpha9: key = Key.Digit9; return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    key = Key.Enter;
                    return true;
                case KeyCode.LeftControl:
                    key = Key.LeftCtrl;
                    return true;
                case KeyCode.RightControl:
                    key = Key.RightCtrl;
                    return true;
                case KeyCode.LeftShift:
                    key = Key.LeftShift;
                    return true;
                case KeyCode.RightShift:
                    key = Key.RightShift;
                    return true;
                case KeyCode.LeftAlt:
                    key = Key.LeftAlt;
                    return true;
                case KeyCode.RightAlt:
                    key = Key.RightAlt;
                    return true;
                case KeyCode.LeftCommand:
                    key = Key.LeftMeta;
                    return true;
                case KeyCode.RightCommand:
                    key = Key.RightMeta;
                    return true;
                case KeyCode.BackQuote:
                    key = Key.Backquote;
                    return true;
                default:
                    if (Enum.TryParse(keyCode.ToString(), true, out key))
                    {
                        return true;
                    }

                    key = Key.None;
                    return false;
            }
        }
#endif
    }
}