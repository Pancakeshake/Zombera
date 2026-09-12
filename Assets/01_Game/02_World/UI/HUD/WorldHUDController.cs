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
    [AddComponentMenu("Zombera/UI/World HUD Controller")]
    [DisallowMultipleComponent]
    public sealed partial class WorldHUDController : MonoBehaviour
    {
        public enum TabId
        {
            None = -1,
            Squad = 0,
            Inventory = 1,
            Crafting = 2,
            Map = 3,
            Missions = 4,
            Formations = 5,
            Jobs = 6,
            Factions = 7
        }

        private const float PopupCameraFullScanIntervalSeconds = 1.5f;

        // ── Serialized ────────────────────────────────────────────────────────

        [Header("Controllers")] [SerializeField]
        private TopBarController topBar;

        [SerializeField] private SquadPortraitStrip portraitStrip;

        [Header("Pause Menu")]
        [SerializeField] private Menus.PauseMenuController pauseMenu;

        [Header("Panels")] [SerializeField] private GameObject squadPanel;

        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private GameObject craftingPanel;
        [SerializeField] private GameObject craftingTabPrefab;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject missionsPanel;
        [SerializeField] private GameObject formationsPanel;
        [SerializeField] private GameObject jobsPanel;
        [SerializeField] private GameObject factionsPanel;

        [Header("Layout")] [SerializeField] private RectTransform panelsRoot;

        [SerializeField] private RectTransform bottomBarRoot;

        [Header("Bottom Squads Tab")] [SerializeField]
        private bool enableBottomSquadsDropTab = true;

        [SerializeField] private bool startWithBottomBarExpanded = true;
        [SerializeField] [Min(24f)] private float bottomSquadsTabHeight = 38f;
        [SerializeField] [Min(72f)] private float bottomSquadsTabButtonWidth = 148f;
        [SerializeField] [Min(24f)] private float bottomBarCollapsedHeight = 42f;
        [SerializeField] private string bottomSquadsTabLabel = "Squads";
        [SerializeField] private string bottomBuildsTabLabel = "Builds";
        [SerializeField] private Color bottomSquadsTabExpandedColor = new(0.16f, 0.43f, 0.31f, 0.92f);
        [SerializeField] private Color bottomSquadsTabCollapsedColor = new(0.08f, 0.09f, 0.11f, 0.92f);
        [SerializeField] private Color bottomSquadsTabTextColor = new(0.93f, 0.92f, 0.88f, 0.98f);
        [SerializeField] [Min(140f)] private float buildSearchFieldWidth = 180f;
        [SerializeField] private string buildSearchPlaceholder = "Search builds...";

        [Header("Overlay")] [SerializeField] private Image dimOverlay;

        [SerializeField] [Range(0f, 1f)] private float dimAlpha = 0.40f;

        [Header("Time Control")] [SerializeField]
        private bool pauseGameWhenMenuOpen = true;

        [Header("Build Controls Popup")] [SerializeField]
        private bool showBuildControlsPopup = true;

        [SerializeField] private Vector2 buildControlsPopupSize = new(372f, 340f);
        [SerializeField] private Vector2 buildControlsPopupOffset = new(-14f, -86f);
        [SerializeField] private Color buildControlsPopupBackgroundColor = new(0.08f, 0.10f, 0.13f, 0.92f);
        [SerializeField] private Color buildControlsPopupBorderColor = new(0.23f, 0.35f, 0.43f, 0.95f);
        [SerializeField] private Color buildControlsPopupTextColor = new(0.92f, 0.94f, 0.96f, 0.98f);
        [SerializeField] [Min(10f)] private float buildControlsPopupFontSize = 16f;
        [SerializeField] private bool showBuildHeightButtons = true;
        [SerializeField] [Min(0.05f)] private float buildHeightStepMeters = 0.25f;
        [SerializeField] [TextArea(3, 8)]
        private string buildControlsPopupText =
            "Build Controls\nLMB Place\nR / Mouse Wheel Rotate\n1-9 Select Item\nEsc Cancel";

        [Header("Damage Popup")] [SerializeField]
        private bool showDamagePopups = true;

        [SerializeField] private bool showPlayerDamagePopups = true;
        [SerializeField] private bool showEnemyDamagePopups = true;
        [SerializeField] [Min(0.1f)] private float playerDamagePopupHeight = 2.1f;
        [SerializeField] [Min(0.1f)] private float playerDamagePopupLifetime = 0.95f;
        [SerializeField] [Min(0f)] private float playerDamagePopupRisePixels = 52f;
        [SerializeField] private Vector2 playerDamagePopupJitter = new(16f, 8f);
        [SerializeField] [Min(8f)] private float playerDamagePopupFontSize = 24f;
        [SerializeField] private Color playerDamagePopupColor = new(0.96f, 0.20f, 0.20f, 1f);
        [SerializeField] [Min(0.1f)] private float enemyDamagePopupHeight = 2.2f;
        [SerializeField] [Min(0.1f)] private float enemyDamagePopupLifetime = 0.8f;
        [SerializeField] [Min(0f)] private float enemyDamagePopupRisePixels = 54f;
        [SerializeField] private Vector2 enemyDamagePopupJitter = new(12f, 6f);
        [SerializeField] [Min(8f)] private float enemyDamagePopupFontSize = 20f;
        [SerializeField] private Color enemyDamagePopupColor = new(1f, 0.66f, 0.22f, 1f);

        [Header("Easy Build")]
        [Tooltip(
            "Optional. Assign the same Easy Build bridge used by the Player (radial catalog → HUD tiles). Leave empty to auto-resolve from PlayerInputController / scene.")]
        [SerializeField]
        private EasyBuildRadialMenuInputBridge easyBuildRadialMenuBridge;

        [Tooltip("Optional. Cursor/orbital placement binder on the Player; used for selection highlight sync.")]
        [SerializeField]
        private EasyBuildCursorPlacementBinder easyBuildCursorPlacementBinder;

        [Tooltip(
            "Optional. Drag the spawned Player root here so the HUD can find Easy Build components before global searches (recommended).")]
        [SerializeField]
        private Transform easyBuildOwnerTransform;

        [Header("Build Debug")]
        [SerializeField] private bool enableBuildDebugLogs = true;
        [SerializeField] private bool verboseBuildDebugLogs;

        [Header("Editor Scene Layout")]
        [SerializeField]
        [Tooltip("Shows runtime-generated HUD strips/panels in the Scene view for layout positioning.")]
        private bool showEditorLayoutPreviews = true;

        [SerializeField] private bool editorPreviewShowCraftingPanel = true;
        [SerializeField] private bool editorPreviewShowBuildItemsStrip;
        [SerializeField] private bool editorPreviewExpandBottomBar = true;

        private readonly List<DamagePopupView> _damagePopups = new(16);

        // ── State ─────────────────────────────────────────────────────────────

        private Canvas _canvas;
        private bool _bottomBarExpanded = true;
        private float _bottomBarExpandedHeight;
        private Image _bottomSquadsTabBackground;
        private RectTransform _bottomSquadsTabRoot;
        private RectTransform _bottomDayTimeRoot;
        private TextMeshProUGUI _bottomSquadsTabText;
        private RectTransform _buildSearchFieldRoot;
        private TMP_InputField _buildSearchInput;
        private string _buildSearchQuery = string.Empty;
        private RectTransform _buildPageIndicatorRoot;
        private TextMeshProUGUI _buildPageIndicatorText;
        private RectTransform _buildCommandStripRoot;
        private readonly List<BuildCommandButtonView> _buildCommandButtons = new(6);

        /// <summary>World HUD bottom strip shows 9 item tiles per page (digits 1–9); catalog entries beyond that paginate.</summary>
        private const int BuildHudItemsPerPage = 9;
        private const float BuildSearchApplyDelaySeconds = 0.10f;
        private const float BuildCatalogPollIntervalSeconds = 0.40f;

        private int _buildItemPageOffset;
        private RectTransform _bottomBuildItemsRoot;
        private readonly List<BuildItemBoxView> _bottomBuildItemBoxes = new(12);
        private readonly List<int> _filteredBuildSourceIndices = new(128);
        private readonly Dictionary<int, string> _buildLabelCache = new(256);
        private readonly Dictionary<int, Sprite> _buildIconSpriteCache = new(16);
        private bool _buildFilterDirty = true;
        private bool _buildVisualsDirty = true;
        private bool _buildPageIndicatorDirty = true;
        private int _lastKnownBuildItemCount = -1;
        private float _nextBuildCatalogPollAt;
        private string _pendingBuildSearchQuery = string.Empty;
        private bool _hasPendingBuildSearchQuery;
        private float _buildSearchApplyAt;
        private CraftingTabController _craftingTab;
        private FormationsTabController _formationsTab;
        private JobsTabController _jobsTab;
        private FactionsTabController _factionsTab;
        private EasyBuildCursorPlacementBinder _easyBuildCursorPlacementBinder;
private EasyBuildRadialMenuInputBridge _easyBuildRadialMenuInputBridge;
        private BuildPlacementController _legacyBuildPlacementController;
        private float _nextBuildModeResolveAt;
        private int _activeBuildItemBoxIndex = -1;
        private int _explicitBuildItemBoxIndex = -2;
        private int _activeBuildCategoryIndex = -1;
        private bool _showingBuildItems;
        private bool _damageEventsSubscribed;
        private RectTransform _damagePopupRoot;
        private RectTransform _buildControlsPopupRoot;
        private TextMeshProUGUI _buildControlsPopupLabel;
        private RectTransform _buildHeightButtonsRow;
        private Button _buildHeightDownButton;
        private Button _buildHeightUpButton;
        private TextMeshProUGUI _buildHeightValueLabel;
        private bool _layoutInitialized;
        private float _nextPopupCameraFullScanTime = -1000f;
        private float _panelsBottomInsetWhenBottomBarVisible;
        private bool _pausedByMenuTab;
        private Camera _popupCamera;
        private TimeSystem _timeSystem;

        private static readonly string[] BuildCommandLabels =
        {
            "<", ">", "Place", "Delete", "Edit", "Replace"
        };

        public TabId ActiveTab { get; private set; } = TabId.None;
        public bool IsBuildPlacementModeActive => IsBuildModeActive();

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired whenever the active tab changes (including closing to None).</summary>
        public event Action<TabId> OnTabChanged;

        // ── Public API ────────────────────────────────────────────────────────

        public void OpenTab(TabId tab)
        {
            if (ActiveTab == tab) return;
            PrepareTab(tab);
            ApplyTab(tab);
        }

        public void ToggleTab(TabId tab)
        {
            PrepareTab(tab);
            ApplyTab(ActiveTab == tab ? TabId.None : tab);
        }

        private void PrepareTab(TabId tab)
        {
            if (tab is TabId.Formations or TabId.Jobs or TabId.Factions)
                EnsureFormationsJobsFactionsPanels();

            if (IsHudDevScene() && tab is TabId.Formations or TabId.Jobs or TabId.Factions)
                EnsureHudDevPlaceholderData();

            if (IsHudDevScene() && _canvas != null)
                _canvas.enabled = true;
        }

        public void CloseTab()
        {
            ApplyTab(TabId.None);
        }
    }
}