using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Zombera.UI.SquadManagement;

namespace Zombera.UI
{
    /// <summary>
    /// Builds and drives the in-game bottom HUD entirely at runtime.
    /// No prefabs required — add this component to any scene GameObject.
    /// Call Bind(player) after the player Unit is ready.
    /// </summary>
    public sealed class WorldHUD : MonoBehaviour
    {
        [Header("Runtime Build")]
        [Tooltip("If a baked WorldHUDCanvas exists, rebuild it on play so style updates apply automatically.")]
        [SerializeField] private bool rebuildBakedCanvasOnPlay = true;

        [Header("Squad Management Overlay")]
        [SerializeField] private bool enableSquadManagementOverlay = true;
        [SerializeField] private bool squadManagementStartsOpen;
        [SerializeField] private bool hideCombatHudWhileSquadManagementOpen = true;
        [SerializeField] private KeyCode toggleSquadManagementKey = KeyCode.Tab;
        [SerializeField] private KeyCode closeSquadManagementKey = KeyCode.Escape;
        [SerializeField] private KeyCode openSquadTabKey = KeyCode.F1;
        [SerializeField] private KeyCode openInventoryTabKey = KeyCode.F2;
        [SerializeField] private KeyCode openCraftingTabKey = KeyCode.F3;
        [SerializeField] private KeyCode openMapTabKey = KeyCode.F4;
        [SerializeField] private KeyCode openMissionsTabKey = KeyCode.F5;
        [SerializeField] private KeyCode quickOpenInventoryKey = KeyCode.I;
        [SerializeField] private KeyCode pauseToggleKey = KeyCode.Space;
        [SerializeField] private ZomberaSquadManagementUI squadManagementUI;

        [Header("Damage Popup")]
        [SerializeField, Min(0.1f)] private float playerDamagePopupHeight = 2.1f;
        [SerializeField, Min(0.1f)] private float playerDamagePopupLifetime = 0.9f;
        [SerializeField, Min(0f)] private float playerDamagePopupRisePixels = 46f;
        [SerializeField] private Vector2 playerDamagePopupJitter = new Vector2(18f, 8f);
        [SerializeField, Min(8f)] private float playerDamagePopupFontSize = 24f;

        [Header("Enemy Damage Popup")]
        [SerializeField] private bool showZombieDamagePopups = true;
        [SerializeField] private bool includeEnemyDamagePopups = false;
        [SerializeField, Min(0.1f)] private float zombieDamagePopupHeight = 2.2f;
        [SerializeField, Min(0.1f)] private float zombieDamagePopupLifetime = 0.75f;
        [SerializeField, Min(0f)] private float zombieDamagePopupRisePixels = 54f;
        [SerializeField] private Vector2 zombieDamagePopupJitter = new Vector2(12f, 6f);
        [SerializeField, Min(8f)] private float zombieDamagePopupFontSize = 20f;
        [SerializeField] private Color zombieDamagePopupColor = new Color(1f, 0.66f, 0.22f, 1f);

        // ── Scene layout bindings ─────────────────────────────────────────────
        // Assign these in the Inspector to drive the HUD from editor-built scene
        // objects instead of the procedural BuildHUD() path. Leave all null to
        // fall back to the existing code-driven build.
        [Header("Scene Layout — Combat HUD (optional)")]
        [Tooltip("The root GameObject that contains the bottom combat bar. Toggled inactive when squad management opens.")]
        [SerializeField] private GameObject combatHudRootOverride;
        [Tooltip("Full-screen RectTransform used as the parent for world-space damage popup labels.")]
        [SerializeField] private RectTransform damagePopupRootOverride;

        [Header("Scene Layout — Squad Entries (optional)")]
        [Tooltip("One entry per squad card. Assign portrait, health fill, name, and rank label from your scene prefab.")]
        [SerializeField] private List<SceneSquadEntryBinding> squadEntryBindings = new List<SceneSquadEntryBinding>();

        [Header("Scene Layout — Time Controls (optional)")]
        [SerializeField] private RectTransform timeControlsRootOverride;
        [SerializeField] private Button pauseButtonOverride;
        [SerializeField] private Button speed1xButtonOverride;
        [SerializeField] private Button speed2xButtonOverride;
        [SerializeField] private Button speed4xButtonOverride;

        // Palette: worn steel + rust + muted warning accents.
        static readonly Color C_BarBackdrop   = new Color(0.06f, 0.06f, 0.05f, 0.92f);
        static readonly Color C_BarCore       = new Color(0.14f, 0.13f, 0.12f, 0.96f);
        static readonly Color C_BarGlow       = new Color(0.23f, 0.09f, 0.07f, 0.18f);
        static readonly Color C_Divider       = new Color(0.66f, 0.47f, 0.24f, 0.46f);
        static readonly Color C_Panel         = new Color(0.17f, 0.15f, 0.13f, 0.92f);
        static readonly Color C_PanelSoft     = new Color(0.16f, 0.14f, 0.13f, 0.78f);
        static readonly Color C_Text          = new Color(0.87f, 0.84f, 0.77f, 1f);
        static readonly Color C_TextDim       = new Color(0.56f, 0.52f, 0.45f, 1f);
        static readonly Color C_TextSignal    = new Color(0.82f, 0.67f, 0.40f, 1f);
        static readonly Color C_HpTrack       = new Color(0.18f, 0.15f, 0.13f, 1f);
        static readonly Color C_HpHealthy     = new Color(0.39f, 0.50f, 0.32f, 1f);
        static readonly Color C_HpInjured     = new Color(0.75f, 0.49f, 0.24f, 1f);
        static readonly Color C_HpCritical    = new Color(0.56f, 0.22f, 0.20f, 1f);
        static readonly Color C_Bolt          = new Color(0.34f, 0.29f, 0.24f, 0.95f);
        static readonly Color C_BoltCore      = new Color(0.24f, 0.20f, 0.17f, 0.95f);
        static readonly Color C_TimePanel     = new Color(0.10f, 0.10f, 0.09f, 0.88f);
        static readonly Color C_TimeButton    = new Color(0.18f, 0.16f, 0.14f, 1f);
        static readonly Color C_TimeButtonOn  = new Color(0.54f, 0.36f, 0.17f, 1f);
        static readonly Color C_DamagePopup   = new Color(0.96f, 0.20f, 0.20f, 1f);

        private const int MaxHudSquadEntries = 3;
        private const float HudDataRefreshInterval = 0.35f;

        [System.Serializable]
        public sealed class SceneSquadEntryBinding
        {
            [Tooltip("Root GameObject of this squad card — will be shown/hidden based on squad size.")]
            public GameObject root;
            [Tooltip("Portrait Image component inside this card.")]
            public Image portrait;
            [Tooltip("TextMeshPro label showing the character initial when no portrait sprite is available.")]
            public TextMeshProUGUI portraitInitial;
            [Tooltip("TextMeshPro label showing the unit's name.")]
            public TextMeshProUGUI nameText;
            [Tooltip("TextMeshPro label showing the unit's level/rank.")]
            public TextMeshProUGUI rankText;
            [Tooltip("Image used as the health fill bar (Image.Type = Filled, Horizontal).")]
            public Image healthFill;
            [Tooltip("Optional Button — click opens Squad Management.")]
            public Button button;
        }

        private sealed class SquadEntryView
        {
            public RectTransform Root;
            public Image Portrait;
            public TextMeshProUGUI PortraitInitial;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Power;
            public TextMeshProUGUI Rank;
            public Image HealthFill;
        }

        private sealed class LiveHudUnitContext
        {
            public Unit Unit;
            public string DisplayName;
            public int PowerRating;
            public int Rank;
            public float Health01;
            public Sprite Portrait;
        }

        private sealed class DamagePopupView
        {
            public RectTransform Root;
            public TextMeshProUGUI Label;
            public Transform FollowTarget;
            public Vector3 WorldAnchor;
            public Vector2 Jitter;
            public float Age;
            public float Height;
            public float Lifetime;
            public float RisePixels;
            public Color BaseColor;
        }

        // ── live refs ────────────────────────────────────────────────────────
        private UnitHealth _health;
        private Slider     _hpSlider;
        private Image      _hpFillImage;
        private TextMeshProUGUI _hpText;
        private TextMeshProUGUI _playerNameText;
        private TextMeshProUGUI _conditionText;
        private GameObject _combatHudRoot;
        private RectTransform _damagePopupRoot;
        private RectTransform _timeControlsRoot;
        private Image _pauseButtonImage;
        private Image _speed1xButtonImage;
        private Image _speed2xButtonImage;
        private Image _speed4xButtonImage;
        private TimeSystem _timeSystem;
        private Camera _popupCamera;
        private bool _damageEventsSubscribed;
        private float _lastNonPausedSpeed = 1f;
        private bool _isSquadManagementOpen;
        private float _hudDataRefreshTimer;
        private readonly List<SquadEntryView> _squadEntryViews = new List<SquadEntryView>(MaxHudSquadEntries);
        private readonly List<LiveHudUnitContext> _liveHudUnits = new List<LiveHudUnitContext>(MaxHudSquadEntries);
        private readonly List<DamagePopupView> _damagePopupViews = new List<DamagePopupView>(8);

        private enum MenuHotkeyTab
        {
            Squad,
            Inventory,
            Crafting,
            Map,
            Missions
        }

        // ── lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            playerDamagePopupFontSize = Mathf.Max(playerDamagePopupFontSize, 44f);
            zombieDamagePopupFontSize = Mathf.Max(zombieDamagePopupFontSize, 36f);

            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();

            // If the user has assigned scene layout bindings in the Inspector,
            // use those directly and skip all procedural building.
            if (HasSceneLayoutBindings())
            {
                UseSceneLayoutBindings();
            }
            else
            {
                // If the HUD was pre-built into the scene (via HUDBaker), the canvas
                // child already exists — just resolve the live refs and skip building.
                Transform existing = transform.Find("WorldHUDCanvas");
                if (existing != null)
                {
                    bool shouldRebuild = Application.isPlaying &&
                        (rebuildBakedCanvasOnPlay || RequiresHudCanvasRebuild(existing));

                    if (shouldRebuild)
                    {
                        Destroy(existing.gameObject);
                        BuildHUD();
                    }
                    else
                    {
                        NormalizeHudCanvas(existing);
                        ResolveRefs(existing);
                    }
                }
                else
                {
                    BuildHUD();
                }
            }

            ResolveTimeSystem();
            RefreshTimeControlButtons();
            InitializeSquadManagementOverlay();
            TrySubscribeDamageEvents();
        }

        private void OnEnable()
        {
            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();

            Transform existing = transform.Find("WorldHUDCanvas");
            if (existing != null)
            {
                NormalizeHudCanvas(existing);
                ResolveRefs(existing);
            }
            else if (Application.isPlaying)
            {
                BuildHUD();
            }

            RefreshTimeControlButtons();
        }

        private void Update()
        {
            HandleTimeControlHotkeys();
            HandleSquadManagementInput();
            RefreshBottomBarData(false);
            RefreshTimeControlButtons();
            UpdateDamagePopups();

            if (!_damageEventsSubscribed)
            {
                TrySubscribeDamageEvents();
            }
        }

        private void OnDestroy()
        {
            UnbindHealth();
            ClearDamagePopups();
            UnsubscribeDamageEvents();
        }

        // ── scene layout binding ─────────────────────────────────────────────

        private bool HasSceneLayoutBindings()
        {
            return combatHudRootOverride != null
                || damagePopupRootOverride != null
                || timeControlsRootOverride != null
                || (squadEntryBindings != null && squadEntryBindings.Count > 0);
        }

        private void UseSceneLayoutBindings()
        {
            // Combat HUD root (toggled when squad management opens/closes)
            _combatHudRoot = combatHudRootOverride;

            // Damage popup root — full-screen transparent rect
            _damagePopupRoot = damagePopupRootOverride;

            // Time control buttons — wire callbacks then grab the image for visual state
            _timeControlsRoot = timeControlsRootOverride;

            if (pauseButtonOverride != null)
            {
                pauseButtonOverride.onClick.RemoveAllListeners();
                pauseButtonOverride.onClick.AddListener(OnPauseButtonClicked);
                _pauseButtonImage = pauseButtonOverride.targetGraphic as Image;
            }
            if (speed1xButtonOverride != null)
            {
                speed1xButtonOverride.onClick.RemoveAllListeners();
                speed1xButtonOverride.onClick.AddListener(() => SetGameSpeed(1f));
                _speed1xButtonImage = speed1xButtonOverride.targetGraphic as Image;
            }
            if (speed2xButtonOverride != null)
            {
                speed2xButtonOverride.onClick.RemoveAllListeners();
                speed2xButtonOverride.onClick.AddListener(() => SetGameSpeed(2f));
                _speed2xButtonImage = speed2xButtonOverride.targetGraphic as Image;
            }
            if (speed4xButtonOverride != null)
            {
                speed4xButtonOverride.onClick.RemoveAllListeners();
                speed4xButtonOverride.onClick.AddListener(() => SetGameSpeed(4f));
                _speed4xButtonImage = speed4xButtonOverride.targetGraphic as Image;
            }

            // Squad entry views — map the serializable bindings to internal SquadEntryView list
            _squadEntryViews.Clear();
            if (squadEntryBindings != null)
            {
                for (int i = 0; i < squadEntryBindings.Count && i < MaxHudSquadEntries; i++)
                {
                    SceneSquadEntryBinding binding = squadEntryBindings[i];
                    if (binding == null) continue;

                    SquadEntryView view = new SquadEntryView
                    {
                        Root            = binding.root != null ? binding.root.GetComponent<RectTransform>() ?? binding.root.transform as RectTransform : null,
                        Portrait        = binding.portrait,
                        PortraitInitial = binding.portraitInitial,
                        Name            = binding.nameText,
                        Rank            = binding.rankText,
                        HealthFill      = binding.healthFill
                    };

                    // Wire squad card button callback
                    if (binding.button != null)
                    {
                        binding.button.onClick.RemoveAllListeners();
                        binding.button.onClick.AddListener(() => OpenSquadManagementTab(MenuHotkeyTab.Squad));
                    }

                    _squadEntryViews.Add(view);
                }
            }

            // Set the player name / HP fill from the first squad entry (index 0 = player)
            if (_squadEntryViews.Count > 0)
            {
                _playerNameText = _squadEntryViews[0].Name;
                _hpFillImage    = _squadEntryViews[0].HealthFill;
            }
        }

        // ── public API ───────────────────────────────────────────────────────

        /// <summary>Wires the HUD to a live player Unit.</summary>
        public void Bind(Unit player)
        {
            if (player == null) return;

            UnbindHealth();

            if (_playerNameText != null)
                _playerNameText.text = player.gameObject.name;

            _health = player.Health;
            if (_health != null)
            {
                _health.Damaged += OnDamaged;
                _health.Healed  += OnHealed;
                _health.Died    += RefreshHP;
                RefreshHP();
            }

            RefreshBottomBarData(true);
        }

        public void SetAmmo(int current, int reserve)
        {
            // Removed from HUD by design.
        }

        public void AddKill()
        {
            // Removed from HUD by design.
        }

        public void SelectSlot(int index)
        {
            // Removed from HUD by design.
        }

        // ── private helpers ──────────────────────────────────────────────────

        private void UnbindHealth()
        {
            if (_health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Healed  -= OnHealed;
            _health.Died    -= RefreshHP;
            _health = null;
            ClearDamagePopups();
        }

        private void OnDamaged(float amount)
        {
            RefreshHP();
            ShowDamagePopup(amount);
        }
        private void OnHealed(float _)  => RefreshHP();

        private void TrySubscribeDamageEvents()
        {
            if (_damageEventsSubscribed || EventSystem.Instance == null)
            {
                return;
            }

            EventSystem.Instance.Subscribe<UnitDamagedEvent>(OnUnitDamaged);
            _damageEventsSubscribed = true;
        }

        private void UnsubscribeDamageEvents()
        {
            if (!_damageEventsSubscribed)
            {
                return;
            }

            EventSystem.Instance?.Unsubscribe<UnitDamagedEvent>(OnUnitDamaged);
            _damageEventsSubscribed = false;
        }

        private void OnUnitDamaged(UnitDamagedEvent gameEvent)
        {
            if (!Application.isPlaying || gameEvent.Amount <= 0f || gameEvent.UnitObject == null)
            {
                return;
            }

            bool isPlayer = gameEvent.Role == UnitRole.Player;
            bool isZombie = gameEvent.Role == UnitRole.Zombie;
            bool isEnemy = gameEvent.Role == UnitRole.Enemy || gameEvent.Role == UnitRole.Bandit;

            if (isPlayer)
            {
                // Avoid duplicate popups when we are already bound to this same player health.
                if (_health != null && gameEvent.UnitObject == _health.gameObject)
                {
                    return;
                }

                ShowDamagePopupForTarget(
                    gameEvent.Amount,
                    gameEvent.UnitObject.transform,
                    playerDamagePopupHeight,
                    playerDamagePopupLifetime,
                    playerDamagePopupRisePixels,
                    playerDamagePopupJitter,
                    playerDamagePopupFontSize,
                    C_DamagePopup,
                    includeHpSuffix: true);
                return;
            }

            if (!showZombieDamagePopups && !includeEnemyDamagePopups)
            {
                return;
            }

            if (!isZombie && !isEnemy)
            {
                return;
            }

            ShowDamagePopupForTarget(
                gameEvent.Amount,
                gameEvent.UnitObject.transform,
                zombieDamagePopupHeight,
                zombieDamagePopupLifetime,
                zombieDamagePopupRisePixels,
                zombieDamagePopupJitter,
                zombieDamagePopupFontSize,
                zombieDamagePopupColor,
                includeHpSuffix: false);
        }

        private void RefreshHP()
        {
            if (_health == null) return;

            float frac = _health.MaxHealth > 0f
                ? _health.CurrentHealth / _health.MaxHealth : 0f;

            if (_hpSlider != null) _hpSlider.value = frac;

            if (_hpFillImage != null)
            {
                _hpFillImage.color = frac > 0.65f
                    ? C_HpHealthy
                    : frac > 0.35f ? C_HpInjured : C_HpCritical;
            }

            if (_hpText   != null)
                _hpText.text = $"{Mathf.CeilToInt(_health.CurrentHealth)}"
                             + $" <color=#7a6e5e>/ {Mathf.CeilToInt(_health.MaxHealth)}</color>";

            if (_conditionText != null)
                _conditionText.text = frac > 0.6f
                    ? "<color=#c4b68e>STABLE</color>"
                    : frac > 0.3f
                        ? "<color=#bc7b42>INJURED</color>"
                        : "<color=#8e3d36>CRITICAL</color>";

            RefreshBottomBarData(true);
        }

        // ── HUD construction ─────────────────────────────────────────────────

        /// <summary>
        /// Builds the HUD hierarchy under this GameObject.
        /// Called automatically in Awake when no baked hierarchy exists.
        /// Also callable from the HUDBaker editor tool in edit mode.
        /// </summary>
        [ContextMenu("Rebuild HUD Now")]
        public void BuildHUD()
        {
            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();

            Transform existing = transform.Find("WorldHUDCanvas");
            if (existing != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(existing.gameObject);
                }
                else
                {
                    DestroyImmediate(existing.gameObject);
                }
            }

            // ── Canvas ───────────────────────────────────────────────────────
            GameObject cvGo = new GameObject("WorldHUDCanvas");
            cvGo.transform.SetParent(transform, false);

            Canvas cv = cvGo.AddComponent<Canvas>();
            cv.renderMode  = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = ZomberaCanvasLayer.Hud;

            RectTransform canvasRect = cvGo.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.pivot = new Vector2(0.5f, 0.5f);
                canvasRect.anchoredPosition = Vector2.zero;
                canvasRect.sizeDelta = Vector2.zero;
                canvasRect.localScale = Vector3.one;
            }

            CanvasScaler cs = cvGo.AddComponent<CanvasScaler>();
            cs.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight  = 0.8f; // height-driven so bar stays proportional to screen height

            cvGo.AddComponent<GraphicRaycaster>();

            // ── Bottom bar frame ─────────────────────────────────────────────
            RectTransform frame = MakeRect("BottomOverlay", cvGo.transform);
            SetAnchors(frame, 0, 0, 1, 0);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta        = new Vector2(0, 150);
            AddImage(frame, C_BarBackdrop);
            _combatHudRoot = frame.gameObject;

            RectTransform frameGrime = MakeRect("FrameGrime", frame);
            SetAnchors(frameGrime, 0, 0, 1, 1);
            frameGrime.anchoredPosition = Vector2.zero;
            frameGrime.sizeDelta        = new Vector2(-20, -10);
            AddImage(frameGrime, new Color(0.20f, 0.11f, 0.08f, 0.10f));

            RectTransform glow = MakeRect("BottomGlow", frame);
            SetAnchors(glow, 0.5f, 1f, 0.5f, 1f);
            glow.pivot            = new Vector2(0.5f, 1f);
            glow.anchoredPosition = new Vector2(0, 8);
            glow.sizeDelta        = new Vector2(1400, 160);
            AddImage(glow, C_BarGlow);

            // Full-width strip, anchored to bottom edge.
            RectTransform bar = MakeRect("BottomBar", frame);
            SetAnchors(bar, 0, 0, 1, 0);
            bar.anchoredPosition = new Vector2(0, 6);
            bar.sizeDelta        = new Vector2(-28, 122);
            AddImage(bar, C_BarCore);

            RectTransform barWear = MakeRect("BarWear", bar);
            SetAnchors(barWear, 0, 0, 1, 1);
            barWear.anchoredPosition = Vector2.zero;
            barWear.sizeDelta        = new Vector2(-24, -18);
            AddImage(barWear, new Color(0.29f, 0.16f, 0.11f, 0.10f));

            // top divider line
            RectTransform divider = MakeRect("Divider", bar);
            SetAnchors(divider, 0, 1, 1, 1);
            divider.anchoredPosition = Vector2.zero;
            divider.sizeDelta        = new Vector2(0, 2);
            AddImage(divider, C_Divider);

            RectTransform lowerLip = MakeRect("LowerLip", bar);
            SetAnchors(lowerLip, 0, 0, 1, 0);
            lowerLip.anchoredPosition = Vector2.zero;
            lowerLip.sizeDelta        = new Vector2(0, 4);
            AddImage(lowerLip, new Color(0.04f, 0.03f, 0.03f, 0.82f));

            RectTransform seamLeft = MakeRect("SeamLeft", bar);
            SetAnchors(seamLeft, 0.305f, 0, 0.305f, 1);
            seamLeft.anchoredPosition = Vector2.zero;
            seamLeft.sizeDelta        = new Vector2(2, -10);
            AddImage(seamLeft, new Color(0.07f, 0.06f, 0.05f, 0.65f));

            RectTransform seamRight = MakeRect("SeamRight", bar);
            SetAnchors(seamRight, 0.816f, 0, 0.816f, 1);
            seamRight.anchoredPosition = Vector2.zero;
            seamRight.sizeDelta        = new Vector2(2, -10);
            AddImage(seamRight, new Color(0.07f, 0.06f, 0.05f, 0.65f));

            AddPanelBolt(bar, "BoltTopLeft", 0f, 1f, new Vector2(14f, -11f));
            AddPanelBolt(bar, "BoltBottomLeft", 0f, 0f, new Vector2(14f, 11f));
            AddPanelBolt(bar, "BoltTopRight", 1f, 1f, new Vector2(-14f, -11f));
            AddPanelBolt(bar, "BoltBottomRight", 1f, 0f, new Vector2(-14f, 11f));
            AddPanelBolt(bar, "BoltTopCenter", 0.5f, 1f, new Vector2(0f, -11f));
            AddPanelBolt(bar, "BoltBottomCenter", 0.5f, 0f, new Vector2(0f, 11f));

            // ── Main section — squad strip + menu tiles ─────────────────────
            BuildBottomCommandLayout(bar);
            BuildTimeControls(cvGo.transform);
            BuildDamagePopupRoot(cvGo.transform);
            RefreshBottomBarData(true);
            RefreshTimeControlButtons();
        }

        private static bool RequiresHudCanvasRebuild(Transform canvasTransform)
        {
            if (canvasTransform == null)
            {
                return true;
            }

            RectTransform canvasRect = canvasTransform as RectTransform;
            if (canvasRect != null && canvasRect.localScale.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            Canvas canvas = canvasTransform.GetComponent<Canvas>();
            if (canvas == null)
            {
                return true;
            }

            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                return true;
            }

            if (canvasTransform.GetComponent<GraphicRaycaster>() == null)
            {
                return true;
            }

            return canvasTransform.Find("BottomOverlay") == null;
        }

        private static void NormalizeHudCanvas(Transform canvasTransform)
        {
            if (canvasTransform == null)
            {
                return;
            }

            RectTransform canvasRect = canvasTransform as RectTransform;
            if (canvasRect != null)
            {
                canvasRect.localScale = Vector3.one;
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.pivot = new Vector2(0.5f, 0.5f);
                canvasRect.anchoredPosition = Vector2.zero;
                canvasRect.sizeDelta = Vector2.zero;
            }

            Canvas canvas = canvasTransform.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = ZomberaCanvasLayer.Hud;
                canvas.enabled = true;
            }

            CanvasScaler scaler = canvasTransform.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.8f;
            }

            GraphicRaycaster raycaster = canvasTransform.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = true;
            }
        }

        // ─── Bottom command layout ───────────────────────────────────────────
        private void BuildBottomCommandLayout(RectTransform bar)
        {
            _squadEntryViews.Clear();
            _playerNameText = null;
            _conditionText = null;
            _hpSlider = null;
            _hpText = null;
            _hpFillImage = null;

            RectTransform strip = MakeRect("CommandStrip", bar);
            SetAnchors(strip, 0f, 0f, 1f, 1f);
            strip.anchoredPosition = Vector2.zero;
            strip.sizeDelta = new Vector2(-18f, -12f);

            RectTransform squadRegion = MakeRect("SquadRegion", strip);
            SetAnchors(squadRegion, 0f, 0f, 0.53f, 1f);
            squadRegion.anchoredPosition = Vector2.zero;
            squadRegion.sizeDelta = new Vector2(-6f, 0f);
            AddImage(squadRegion, C_PanelSoft);

            RectTransform actionRegion = MakeRect("ActionRegion", strip);
            SetAnchors(actionRegion, 0.53f, 0f, 1f, 1f);
            actionRegion.anchoredPosition = Vector2.zero;
            actionRegion.sizeDelta = new Vector2(0f, 0f);
            AddImage(actionRegion, C_PanelSoft);

            BuildSquadStrip(squadRegion);
            BuildActionStrip(actionRegion);
        }

        private void BuildSquadStrip(RectTransform squadRegion)
        {
            RectTransform squadRow = MakeRect("SquadRow", squadRegion);
            SetAnchors(squadRow, 0f, 0f, 1f, 1f);
            squadRow.anchoredPosition = Vector2.zero;
            squadRow.sizeDelta = new Vector2(-4f, -4f);

            HorizontalLayoutGroup layout = squadRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            RectTransform markerTile = MakeRect("RosterMarkerTile", squadRow);
            LayoutElement markerLayout = markerTile.gameObject.AddComponent<LayoutElement>();
            markerLayout.preferredWidth = 56f;
            markerLayout.minWidth = 56f;
            markerLayout.flexibleWidth = 0f;
            AddImage(markerTile, new Color(0.11f, 0.10f, 0.09f, 1f));

            TextMeshProUGUI markerText = MakeText("MarkerText", markerTile, "#", 30f);
            SetAnchors(markerText.rectTransform, 0f, 0f, 1f, 1f);
            markerText.rectTransform.anchoredPosition = Vector2.zero;
            markerText.rectTransform.sizeDelta = Vector2.zero;
            markerText.alignment = TextAlignmentOptions.Center;
            markerText.fontStyle = FontStyles.Bold;
            markerText.color = new Color(0.66f, 0.61f, 0.54f, 1f);

            for (int i = 0; i < MaxHudSquadEntries; i++)
            {
                SquadEntryView view = BuildSquadEntryTile(squadRow, i);
                _squadEntryViews.Add(view);
            }

            if (_squadEntryViews.Count > 0)
            {
                _playerNameText = _squadEntryViews[0].Name;
                _conditionText = null;
                _hpFillImage = _squadEntryViews[0].HealthFill;
            }
        }

        private SquadEntryView BuildSquadEntryTile(RectTransform parent, int index)
        {
            RectTransform tile = MakeRect("SquadEntry_" + index, parent);
            LayoutElement layout = tile.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 162f;
            layout.minWidth = 148f;
            layout.flexibleWidth = 0f;

            Image tileImage = tile.gameObject.AddComponent<Image>();
            tileImage.color = new Color(0.17f, 0.15f, 0.14f, 1f);

            Button button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = tileImage;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.87f, 0.72f, 1f);
            colors.pressedColor = new Color(0.82f, 0.67f, 0.42f, 1f);
            colors.selectedColor = new Color(0.85f, 0.72f, 0.45f, 1f);
            colors.disabledColor = new Color(0.56f, 0.52f, 0.46f, 0.75f);
            button.colors = colors;
            button.onClick.AddListener(() => OpenSquadManagementTab(MenuHotkeyTab.Squad));

            RectTransform portraitFrame = MakeRect("PortraitFrame", tile);
            SetAnchors(portraitFrame, 0.08f, 0.40f, 0.92f, 0.94f);
            portraitFrame.anchoredPosition = Vector2.zero;
            portraitFrame.sizeDelta = Vector2.zero;
            Image portraitFrameImage = AddImage(portraitFrame, new Color(0.10f, 0.10f, 0.09f, 1f));
            portraitFrameImage.raycastTarget = false;
            Mask portraitMask = portraitFrame.gameObject.AddComponent<Mask>();
            portraitMask.showMaskGraphic = true;

            RectTransform portraitRect = MakeRect("Portrait", portraitFrame);
            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(0f, 18f);
            portraitRect.sizeDelta = new Vector2(40f, 46f);
            Image portraitImage = AddImage(portraitRect, new Color(0.22f, 0.24f, 0.22f, 1f));
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            TextMeshProUGUI initialText = MakeText("PortraitInitial", portraitRect, "?", 36f);
            SetAnchors(initialText.rectTransform, 0f, 0f, 1f, 1f);
            initialText.rectTransform.anchoredPosition = Vector2.zero;
            initialText.rectTransform.sizeDelta = Vector2.zero;
            initialText.alignment = TextAlignmentOptions.Center;
            initialText.fontStyle = FontStyles.Bold;
            initialText.color = new Color(0.86f, 0.82f, 0.73f, 0.9f);

            RectTransform hpTrack = MakeRect("HealthTrack", tile);
            SetAnchors(hpTrack, 0.12f, 0.22f, 0.88f, 0.27f);
            hpTrack.anchoredPosition = Vector2.zero;
            hpTrack.sizeDelta = Vector2.zero;
            AddImage(hpTrack, C_HpTrack);

            RectTransform hpFillRect = MakeRect("HealthFill", hpTrack);
            SetAnchors(hpFillRect, 0f, 0f, 1f, 1f);
            hpFillRect.anchoredPosition = Vector2.zero;
            hpFillRect.sizeDelta = new Vector2(-2f, -2f);
            Image hpFill = AddImage(hpFillRect, C_HpHealthy);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.fillOrigin = 0;
            hpFill.fillAmount = 1f;

            TextMeshProUGUI nameText = MakeText("NameText", tile, "Survivor", 15f);
            SetAnchors(nameText.rectTransform, 0.08f, 0.10f, 0.92f, 0.22f);
            nameText.rectTransform.anchoredPosition = Vector2.zero;
            nameText.rectTransform.sizeDelta = Vector2.zero;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = C_Text;

            TextMeshProUGUI rankText = MakeText("RankText", tile, "LVL 1", 13f);
            SetAnchors(rankText.rectTransform, 0.08f, 0.01f, 0.92f, 0.10f);
            rankText.rectTransform.anchoredPosition = Vector2.zero;
            rankText.rectTransform.sizeDelta = Vector2.zero;
            rankText.alignment = TextAlignmentOptions.Center;
            rankText.fontStyle = FontStyles.Bold;
            rankText.color = C_TextDim;

            return new SquadEntryView
            {
                Root = tile,
                Portrait = portraitImage,
                PortraitInitial = initialText,
                Name = nameText,
                Power = null,
                Rank = rankText,
                HealthFill = hpFill
            };
        }

        private void BuildActionStrip(RectTransform actionRegion)
        {
            RectTransform row = MakeRect("ActionRow", actionRegion);
            SetAnchors(row, 0f, 0f, 1f, 1f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(-4f, -4f);

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false; // buttons use preferredHeight, not full bar height

            CreateActionButton(row, "Inventory", "INVENTORY", "F2 / I", () => OpenSquadManagementTab(MenuHotkeyTab.Inventory));
            CreateActionButton(row, "Crafting", "CRAFTING", "F3", () => OpenSquadManagementTab(MenuHotkeyTab.Crafting));
            CreateActionButton(row, "Map", "MAP", "F4", () => OpenSquadManagementTab(MenuHotkeyTab.Map));
            CreateActionButton(row, "Missions", "MISSIONS", "F5", () => OpenSquadManagementTab(MenuHotkeyTab.Missions));
            CreateActionButton(row, "Menu", "MENU", "TAB", () => SetSquadManagementOpen(!_isSquadManagementOpen));
        }

        private void CreateActionButton(
            RectTransform parent,
            string objectName,
            string label,
            string hint,
            Action onClick)
        {
            RectTransform buttonRect = MakeRect(objectName + "Button", parent);
            LayoutElement layout = buttonRect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 120f;
            layout.minWidth = 90f;
            layout.preferredHeight = 84f;
            layout.minHeight = 72f;

            Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
            buttonImage.color = new Color(0.18f, 0.16f, 0.14f, 1f);

            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.86f, 0.70f, 1f);
            colors.pressedColor = new Color(0.80f, 0.64f, 0.38f, 1f);
            colors.selectedColor = new Color(0.83f, 0.68f, 0.41f, 1f);
            colors.disabledColor = new Color(0.56f, 0.52f, 0.46f, 0.75f);
            button.colors = colors;

            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            TextMeshProUGUI labelText = MakeText("LabelText", buttonRect, label, 16f);
            SetAnchors(labelText.rectTransform, 0.04f, 0.40f, 0.96f, 0.72f);
            labelText.rectTransform.anchoredPosition = Vector2.zero;
            labelText.rectTransform.sizeDelta = Vector2.zero;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = C_Text;

            TextMeshProUGUI hintText = MakeText("HintText", buttonRect, hint, 11f);
            SetAnchors(hintText.rectTransform, 0.04f, 0.10f, 0.96f, 0.30f);
            hintText.rectTransform.anchoredPosition = Vector2.zero;
            hintText.rectTransform.sizeDelta = Vector2.zero;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.fontStyle = FontStyles.Bold;
            hintText.color = C_TextDim;
        }

        private void RefreshBottomBarData(bool force)
        {
            if (_squadEntryViews.Count == 0)
            {
                return;
            }

            if (!force)
            {
                _hudDataRefreshTimer += Time.unscaledDeltaTime;
                if (_hudDataRefreshTimer < HudDataRefreshInterval)
                {
                    return;
                }
            }

            _hudDataRefreshTimer = 0f;
            CollectLiveHudUnits(_liveHudUnits);

            if (_liveHudUnits.Count == 0)
            {
                ApplyDemoBottomBarData();
                return;
            }

            int viewCount = Mathf.Min(_squadEntryViews.Count, _liveHudUnits.Count);
            for (int i = 0; i < viewCount; i++)
            {
                LiveHudUnitContext context = _liveHudUnits[i];
                if (_squadEntryViews[i].Root != null)
                {
                    _squadEntryViews[i].Root.gameObject.SetActive(true);
                }

                ApplyContextToSquadEntry(_squadEntryViews[i], context);
            }

            for (int i = viewCount; i < _squadEntryViews.Count; i++)
            {
                SetSquadEntryHidden(_squadEntryViews[i]);
            }

            if (_health != null)
            {
                float frac = _health.MaxHealth > 0f ? _health.CurrentHealth / _health.MaxHealth : 0f;
                if (_hpFillImage != null)
                {
                    _hpFillImage.fillAmount = Mathf.Clamp01(frac);
                    _hpFillImage.color = frac > 0.65f
                        ? C_HpHealthy
                        : frac > 0.35f ? C_HpInjured : C_HpCritical;
                }
            }
        }

        private void CollectLiveHudUnits(List<LiveHudUnitContext> target)
        {
            target.Clear();
            HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);

            Unit playerUnit = ResolvePlayerUnit();
            TryAddLiveHudContext(target, seenIds, playerUnit, null);

            SquadManager squadManager = SquadManager.Instance != null
                ? SquadManager.Instance
                : FindFirstObjectByType<SquadManager>();

            if (squadManager != null)
            {
                squadManager.RefreshSquadRoster();
                IReadOnlyList<SquadMember> members = squadManager.SquadMembers;

                for (int i = 0; i < members.Count && target.Count < MaxHudSquadEntries; i++)
                {
                    SquadMember member = members[i];
                    Unit unit = member != null ? member.Unit : null;
                    TryAddLiveHudContext(target, seenIds, unit, member);
                }
            }

            if (target.Count >= MaxHudSquadEntries)
            {
                return;
            }

            UnitManager unitManager = UnitManager.Instance != null
                ? UnitManager.Instance
                : FindFirstObjectByType<UnitManager>();

            if (unitManager != null)
            {
                List<Unit> squadUnits = unitManager.GetUnitsByRole(UnitRole.SquadMember);
                for (int i = 0; i < squadUnits.Count && target.Count < MaxHudSquadEntries; i++)
                {
                    Unit unit = squadUnits[i];
                    SquadMember member = unit != null ? unit.GetComponent<SquadMember>() : null;
                    TryAddLiveHudContext(target, seenIds, unit, member);
                }
            }
        }

        private Unit ResolvePlayerUnit()
        {
            Unit boundUnit = _health != null ? _health.GetComponent<Unit>() : null;
            if (boundUnit != null)
            {
                return boundUnit;
            }

            UnitManager unitManager = UnitManager.Instance != null
                ? UnitManager.Instance
                : FindFirstObjectByType<UnitManager>();

            if (unitManager != null)
            {
                List<Unit> players = unitManager.GetUnitsByRole(UnitRole.Player);
                if (players.Count > 0 && players[0] != null)
                {
                    return players[0];
                }
            }

            Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && units[i].Role == UnitRole.Player)
                {
                    return units[i];
                }
            }

            return null;
        }

        private void TryAddLiveHudContext(
            List<LiveHudUnitContext> target,
            HashSet<string> seenIds,
            Unit unit,
            SquadMember member)
        {
            if (unit == null || target.Count >= MaxHudSquadEntries)
            {
                return;
            }

            string id = !string.IsNullOrWhiteSpace(unit.UnitId)
                ? unit.UnitId
                : unit.GetInstanceID().ToString();

            if (!seenIds.Add(id))
            {
                return;
            }

            target.Add(BuildLiveHudContext(unit, member));
        }

        private LiveHudUnitContext BuildLiveHudContext(Unit unit, SquadMember member)
        {
            UnitStats stats = member != null && member.UnitStats != null ? member.UnitStats : unit.Stats;
            UnitHealth health = member != null && member.UnitHealth != null ? member.UnitHealth : unit.Health;

            string displayName = unit.gameObject.name;
            if (unit.Role == UnitRole.Player && CharacterSelectionState.HasSelection)
            {
                displayName = CharacterSelectionState.SelectedCharacterName;
            }

            int powerRating = 1;
            int rank = 1;

            float health01 = 1f;
            if (health != null && health.MaxHealth > 0f)
            {
                health01 = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);

                if (!health.IsDead && Mathf.Approximately(health.CurrentHealth, 0f))
                {
                    health01 = 1f;
                }
            }

            return new LiveHudUnitContext
            {
                Unit = unit,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Survivor" : displayName,
                PowerRating = powerRating,
                Rank = rank,
                Health01 = health01,
                Portrait = ResolvePortraitSprite(unit)
            };
        }

        private static int ResolvePowerRating(UnitStats stats, Unit unit)
        {
            if (stats != null)
            {
                float average = (
                    stats.Strength +
                    stats.Shooting +
                    stats.Melee +
                    stats.Medical +
                    stats.Engineering) / 5f;

                return Mathf.Clamp(Mathf.RoundToInt(average), 1, 99);
            }

            if (unit != null && unit.Role == UnitRole.Player)
            {
                return ResolveSelectionPowerRating();
            }

            return 1;
        }

        private static int ResolveRankFromPowerRating(int powerRating)
        {
            return Mathf.Clamp(Mathf.RoundToInt(powerRating / 12f), 1, 99);
        }

        private static int ResolveSelectionPowerRating()
        {
            float damageSkill = Mathf.Clamp(CharacterSelectionState.SelectedDamage * 5f, 0f, 100f);
            float staminaSkill = Mathf.Clamp(CharacterSelectionState.SelectedStamina, 0f, 100f);
            float carrySkill = Mathf.Clamp(CharacterSelectionState.SelectedCarryCapacity * 2f, 0f, 100f);
            float healthSkill = Mathf.Clamp(CharacterSelectionState.SelectedMaxHealth, 0f, 100f);
            float speedSkill = Mathf.Clamp(CharacterSelectionState.SelectedMoveSpeed * 20f, 0f, 100f);

            float average = (damageSkill + staminaSkill + carrySkill + healthSkill + speedSkill) / 5f;
            return Mathf.Clamp(Mathf.RoundToInt(average), 1, 99);
        }

        private LiveHudUnitContext BuildFallbackPlayerContext()
        {
            Unit playerUnit = ResolvePlayerUnit();

            if (playerUnit != null)
            {
                SquadMember member = playerUnit.GetComponent<SquadMember>();
                return BuildLiveHudContext(playerUnit, member);
            }

            string selectedName = !string.IsNullOrWhiteSpace(CharacterSelectionState.SelectedCharacterName)
                ? CharacterSelectionState.SelectedCharacterName
                : "Player";

            int powerRating = 1;
            int rank = 1;

            return new LiveHudUnitContext
            {
                Unit = null,
                DisplayName = selectedName,
                PowerRating = powerRating,
                Rank = rank,
                Health01 = 1f,
                Portrait = CharacterSelectionState.SelectedPortraitSprite
            };
        }

        private static Sprite ResolvePortraitSprite(Unit unit)
        {
            if (unit == null)
            {
                return null;
            }

            if (unit.Role == UnitRole.Player && CharacterSelectionState.SelectedPortraitSprite != null)
            {
                return CharacterSelectionState.SelectedPortraitSprite;
            }

            SpriteRenderer spriteRenderer = unit.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                return spriteRenderer.sprite;
            }

            return null;
        }

        private void ApplyContextToSquadEntry(SquadEntryView view, LiveHudUnitContext context)
        {
            if (view == null || context == null)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(context.DisplayName) ? "Survivor" : context.DisplayName;

            if (view.Name != null)
            {
                view.Name.text = displayName;
            }

            if (view.Power != null)
            {
                Transform badgeRoot = view.Power.transform.parent;
                if (badgeRoot != null)
                {
                    badgeRoot.gameObject.SetActive(false);
                }
                else
                {
                    view.Power.gameObject.SetActive(false);
                }
            }

            if (view.Rank != null)
            {
                view.Rank.text = "LVL 1";
            }

            if (view.Portrait != null)
            {
                bool hasPortrait = context.Portrait != null;
                view.Portrait.sprite = context.Portrait;
                view.Portrait.color = hasPortrait
                    ? Color.white
                    : new Color(0.22f, 0.24f, 0.22f, 1f);
            }

            if (view.PortraitInitial != null)
            {
                view.PortraitInitial.text = BuildInitials(displayName);
                view.PortraitInitial.gameObject.SetActive(context.Portrait == null);
            }

            if (view.HealthFill != null)
            {
                float clampedHealth = Mathf.Clamp01(context.Health01);
                view.HealthFill.fillAmount = clampedHealth;
                view.HealthFill.color = clampedHealth > 0.65f
                    ? C_HpHealthy
                    : clampedHealth > 0.35f ? C_HpInjured : C_HpCritical;
            }
        }

        private static void SetSquadEntryHidden(SquadEntryView view)
        {
            if (view == null)
            {
                return;
            }

            if (view.Root != null)
            {
                view.Root.gameObject.SetActive(false);
            }
        }

        private void ApplyDemoBottomBarData()
        {
            LiveHudUnitContext playerContext = BuildFallbackPlayerContext();

            if (_squadEntryViews.Count > 0)
            {
                if (_squadEntryViews[0].Root != null)
                {
                    _squadEntryViews[0].Root.gameObject.SetActive(true);
                }

                ApplyContextToSquadEntry(_squadEntryViews[0], playerContext);
            }

            for (int i = 1; i < _squadEntryViews.Count; i++)
            {
                SetSquadEntryHidden(_squadEntryViews[i]);
            }
        }

        private static string BuildInitials(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "?";
            }

            string[] parts = displayName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "?";
            }

            if (parts.Length == 1)
            {
                return parts[0].Substring(0, 1).ToUpperInvariant();
            }

            return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
        }

        // ─── Resolve live refs from a pre-built (baked) hierarchy ────────────
        // Called in Awake when the WorldHUDCanvas child already exists in the scene.
        // Walks the named nodes to re-populate all private refs so Bind() and
        // RefreshHP() work exactly as if BuildHUD() had been called at runtime.
        private void ResolveRefs(Transform canvas)
        {
            _combatHudRoot = canvas.Find("BottomOverlay")?.gameObject;
            _squadEntryViews.Clear();
            _playerNameText = null;
            _conditionText = null;
            _hpFillImage = null;
            _hpSlider = null;
            _hpText = null;

            Transform bar = canvas.Find("BottomBar");
            if (bar == null)
            {
                bar = canvas.Find("BottomOverlay/BottomBar");
            }

            if (bar == null)
            {
                ResolveTimeControls(canvas);
                ResolveDamagePopupRoot(canvas);
                return;
            }

            Transform status = bar.Find("StatusSection");

            if (status != null)
            {
                _playerNameText = status.Find("NameArea/NameText")?.GetComponent<TextMeshProUGUI>();
                _conditionText  = status.Find("NameArea/ConditionText")?.GetComponent<TextMeshProUGUI>();

                Transform hpRow  = status.Find("HPRow");
                Transform barArea = hpRow?.Find("HPBarArea");
                _hpSlider        = barArea?.GetComponent<Slider>();
                _hpFillImage     = barArea?.Find("FillArea/Fill")?.GetComponent<Image>();
                _hpText          = hpRow?.Find("HPValue")?.GetComponent<TextMeshProUGUI>();

                ResolveTimeControls(canvas);
                ResolveDamagePopupRoot(canvas);
                return;
            }

            Transform squadRow = bar.Find("CommandStrip/SquadRegion/SquadRow");
            if (squadRow != null)
            {
                for (int i = 0; i < MaxHudSquadEntries; i++)
                {
                    Transform entry = squadRow.Find("SquadEntry_" + i);
                    if (entry == null)
                    {
                        continue;
                    }

                    SquadEntryView view = new SquadEntryView
                    {
                        Root = entry as RectTransform,
                        Portrait = entry.Find("PortraitFrame/Portrait")?.GetComponent<Image>(),
                        PortraitInitial = entry.Find("PortraitFrame/Portrait/PortraitInitial")?.GetComponent<TextMeshProUGUI>(),
                        Name = entry.Find("NameText")?.GetComponent<TextMeshProUGUI>(),
                        Power = entry.Find("PowerBadge/PowerText")?.GetComponent<TextMeshProUGUI>(),
                        Rank = entry.Find("RankText")?.GetComponent<TextMeshProUGUI>(),
                        HealthFill = entry.Find("HealthTrack/HealthFill")?.GetComponent<Image>()
                    };

                    _squadEntryViews.Add(view);
                }

                if (_squadEntryViews.Count > 0)
                {
                    _playerNameText = _squadEntryViews[0].Name;
                    _conditionText = null;
                    _hpFillImage = _squadEntryViews[0].HealthFill;
                }
            }

            ResolveTimeControls(canvas);
            ResolveDamagePopupRoot(canvas);
        }

        private void BuildDamagePopupRoot(Transform canvasRoot)
        {
            Transform existing = canvasRoot.Find("DamagePopups");
            if (existing != null)
            {
                _damagePopupRoot = existing as RectTransform;
            }
            else
            {
                _damagePopupRoot = MakeRect("DamagePopups", canvasRoot);
            }

            if (_damagePopupRoot == null)
            {
                return;
            }

            _damagePopupRoot.anchorMin = Vector2.zero;
            _damagePopupRoot.anchorMax = Vector2.one;
            _damagePopupRoot.pivot = new Vector2(0.5f, 0.5f);
            _damagePopupRoot.anchoredPosition = Vector2.zero;
            _damagePopupRoot.sizeDelta = Vector2.zero;
        }

        private void ResolveDamagePopupRoot(Transform canvasRoot)
        {
            Transform existing = canvasRoot.Find("DamagePopups");
            if (existing == null)
            {
                BuildDamagePopupRoot(canvasRoot);
                return;
            }

            _damagePopupRoot = existing as RectTransform;
            if (_damagePopupRoot == null)
            {
                return;
            }

            _damagePopupRoot.anchorMin = Vector2.zero;
            _damagePopupRoot.anchorMax = Vector2.one;
            _damagePopupRoot.pivot = new Vector2(0.5f, 0.5f);
            _damagePopupRoot.anchoredPosition = Vector2.zero;
            _damagePopupRoot.sizeDelta = Vector2.zero;
        }

        private void ShowDamagePopup(float amount)
        {
            if (!Application.isPlaying || amount <= 0f || _health == null)
            {
                return;
            }

            ShowDamagePopupForTarget(
                amount,
                _health.transform,
                playerDamagePopupHeight,
                playerDamagePopupLifetime,
                playerDamagePopupRisePixels,
                playerDamagePopupJitter,
                playerDamagePopupFontSize,
                C_DamagePopup,
                includeHpSuffix: true);
        }

        private void ShowDamagePopupForTarget(
            float amount,
            Transform followTarget,
            float popupHeight,
            float popupLifetime,
            float popupRisePixels,
            Vector2 popupJitter,
            float popupFontSize,
            Color popupColor,
            bool includeHpSuffix)
        {
            if (!Application.isPlaying || amount <= 0f || followTarget == null)
            {
                return;
            }

            if (_damagePopupRoot == null)
            {
                Transform canvas = transform.Find("WorldHUDCanvas");
                if (canvas != null)
                {
                    ResolveDamagePopupRoot(canvas);
                }
            }

            if (_damagePopupRoot == null)
            {
                return;
            }

            RectTransform popupRoot = MakeRect("DamagePopup", _damagePopupRoot);
            popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
            popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
            popupRoot.pivot = new Vector2(0.5f, 0.5f);
            popupRoot.anchoredPosition = Vector2.zero;
            popupRoot.sizeDelta = new Vector2(180f, 42f);

            int damageValue = Mathf.Max(1, Mathf.RoundToInt(amount));
            string suffix = includeHpSuffix ? " HP" : string.Empty;
            TextMeshProUGUI label = MakeText("Label", popupRoot, $"-{damageValue}{suffix}", popupFontSize);
            SetAnchors(label.rectTransform, 0f, 0f, 1f, 1f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.color = popupColor;
            AddSoftShadow(label, new Color(0f, 0f, 0f, 0.75f), new Vector2(1.2f, -1.2f));

            DamagePopupView popup = new DamagePopupView
            {
                Root = popupRoot,
                Label = label,
                FollowTarget = followTarget,
                WorldAnchor = followTarget.position + Vector3.up * popupHeight,
                Jitter = new Vector2(
                    UnityEngine.Random.Range(-popupJitter.x, popupJitter.x),
                    UnityEngine.Random.Range(-popupJitter.y, popupJitter.y)),
                Age = 0f,
                Height = popupHeight,
                Lifetime = popupLifetime,
                RisePixels = popupRisePixels,
                BaseColor = popupColor
            };

            _damagePopupViews.Add(popup);
            UpdateDamagePopups();
        }

        private void UpdateDamagePopups()
        {
            if (_damagePopupViews.Count <= 0 || _damagePopupRoot == null)
            {
                return;
            }

            Camera worldCamera = ResolvePopupCamera();
            if (worldCamera == null)
            {
                return;
            }

            Canvas canvas = _damagePopupRoot.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            for (int i = _damagePopupViews.Count - 1; i >= 0; i--)
            {
                DamagePopupView popup = _damagePopupViews[i];

                if (popup == null || popup.Root == null || popup.Label == null)
                {
                    _damagePopupViews.RemoveAt(i);
                    continue;
                }

                popup.Age += Time.unscaledDeltaTime;
                float lifetime = Mathf.Max(0.05f, popup.Lifetime);
                float t = popup.Age / lifetime;

                if (t >= 1f)
                {
                    if (popup.Root != null)
                    {
                        Destroy(popup.Root.gameObject);
                    }

                    _damagePopupViews.RemoveAt(i);
                    continue;
                }

                Vector3 worldAnchor = popup.FollowTarget != null
                    ? popup.FollowTarget.position + Vector3.up * popup.Height
                    : popup.WorldAnchor;

                Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldAnchor);
                if (screenPoint.z <= 0f)
                {
                    popup.Root.gameObject.SetActive(false);
                    continue;
                }

                if (!popup.Root.gameObject.activeSelf)
                {
                    popup.Root.gameObject.SetActive(true);
                }

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_damagePopupRoot, screenPoint, uiCamera, out Vector2 localPoint))
                {
                    localPoint += popup.Jitter;
                    localPoint.y += Mathf.Lerp(0f, popup.RisePixels, t);
                    popup.Root.anchoredPosition = localPoint;
                }

                float alpha = Mathf.Clamp01(1f - t);
                Color color = popup.BaseColor;
                color.a = alpha;
                popup.Label.color = color;

                float scale = Mathf.Lerp(1f, 1.08f, t);
                popup.Root.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private Camera ResolvePopupCamera()
        {
            Transform focusTarget = ResolvePopupFocusTarget();

            if (_popupCamera != null && IsCameraUsable(_popupCamera))
            {
                if (focusTarget == null || CameraCanSeeTarget(_popupCamera, focusTarget))
                {
                    return _popupCamera;
                }
            }

            PlayerFollowCamera followCamera = FindFirstObjectByType<PlayerFollowCamera>();
            if (followCamera != null)
            {
                Camera followViewCamera = followCamera.GetComponent<Camera>();
                if (IsCameraUsable(followViewCamera))
                {
                    _popupCamera = followViewCamera;
                    return _popupCamera;
                }
            }

            Camera mainCamera = Camera.main;
            if (IsCameraUsable(mainCamera))
            {
                _popupCamera = mainCamera;
                return _popupCamera;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Camera bestCandidate = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];

                if (!IsCameraUsable(candidate))
                {
                    continue;
                }

                float score = candidate.depth;

                if (candidate.GetComponent<PlayerFollowCamera>() != null)
                {
                    score += 1000f;
                }

                if (focusTarget != null && CameraCanSeeTarget(candidate, focusTarget))
                {
                    score += 200f;
                }

                if (candidate.CompareTag("MainCamera"))
                {
                    score += 100f;
                }

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestCandidate = candidate;
            }

            _popupCamera = bestCandidate;
            return _popupCamera;
        }

        private Transform ResolvePopupFocusTarget()
        {
            for (int i = _damagePopupViews.Count - 1; i >= 0; i--)
            {
                DamagePopupView popup = _damagePopupViews[i];
                if (popup != null && popup.FollowTarget != null)
                {
                    return popup.FollowTarget;
                }
            }

            return _health != null ? _health.transform : null;
        }

        private static bool IsCameraUsable(Camera camera)
        {
            return camera != null && camera.isActiveAndEnabled && camera.gameObject.activeInHierarchy;
        }

        private static bool CameraCanSeeTarget(Camera camera, Transform target)
        {
            if (!IsCameraUsable(camera) || target == null)
            {
                return false;
            }

            Vector3 samplePoint = target.position + Vector3.up * 1.7f;
            Vector3 viewport = camera.WorldToViewportPoint(samplePoint);

            if (viewport.z <= 0f)
            {
                return false;
            }

            const float margin = 0.25f;
            return viewport.x >= -margin && viewport.x <= 1f + margin
                && viewport.y >= -margin && viewport.y <= 1f + margin;
        }

        private void ClearDamagePopups()
        {
            for (int i = 0; i < _damagePopupViews.Count; i++)
            {
                DamagePopupView popup = _damagePopupViews[i];
                if (popup != null && popup.Root != null)
                {
                    Destroy(popup.Root.gameObject);
                }
            }

            _damagePopupViews.Clear();
        }

        private void ResolveTimeSystem()
        {
            if (_timeSystem == null)
            {
                _timeSystem = FindFirstObjectByType<TimeSystem>();
            }
        }

        private void BuildTimeControls(Transform canvasRoot)
        {
            Transform existing = canvasRoot.Find("TimeControls");
            if (existing != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(existing.gameObject);
                }
                else
                {
                    DestroyImmediate(existing.gameObject);
                }
            }

            _timeControlsRoot = MakeRect("TimeControls", canvasRoot);
            SetAnchors(_timeControlsRoot, 1f, 1f, 1f, 1f);
            _timeControlsRoot.pivot = new Vector2(1f, 1f);
            _timeControlsRoot.anchoredPosition = new Vector2(-12f, -12f);
            _timeControlsRoot.sizeDelta = new Vector2(300f, 42f);

            Image panelImage = _timeControlsRoot.gameObject.AddComponent<Image>();
            panelImage.color = C_TimePanel;
            panelImage.raycastTarget = false;

            HorizontalLayoutGroup layout = _timeControlsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            BuildTimeControlButton(_timeControlsRoot, "PauseButton", "PAUSE", 84f, OnPauseButtonClicked, out _pauseButtonImage);
            BuildTimeControlButton(_timeControlsRoot, "Speed1xButton", "1X", 64f, () => SetGameSpeed(1f), out _speed1xButtonImage);
            BuildTimeControlButton(_timeControlsRoot, "Speed2xButton", "2X", 64f, () => SetGameSpeed(2f), out _speed2xButtonImage);
            BuildTimeControlButton(_timeControlsRoot, "Speed4xButton", "4X", 64f, () => SetGameSpeed(4f), out _speed4xButtonImage);
        }

        private void BuildTimeControlButton(
            RectTransform parent,
            string objectName,
            string label,
            float width,
            Action onClick,
            out Image buttonImage)
        {
            RectTransform buttonRect = MakeRect(objectName, parent);
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            LayoutElement layout = buttonRect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.flexibleWidth = 0f;

            buttonImage = buttonRect.gameObject.AddComponent<Image>();
            buttonImage.color = C_TimeButton;
            buttonImage.raycastTarget = true;

            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.93f, 0.86f, 0.72f, 1f);
            colors.pressedColor = new Color(0.80f, 0.67f, 0.44f, 1f);
            colors.selectedColor = new Color(0.83f, 0.71f, 0.46f, 1f);
            colors.disabledColor = new Color(0.45f, 0.42f, 0.38f, 0.7f);
            button.colors = colors;

            button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            TextMeshProUGUI labelText = MakeText("Label", buttonRect, label, 14f);
            SetAnchors(labelText.rectTransform, 0f, 0f, 1f, 1f);
            labelText.rectTransform.anchoredPosition = Vector2.zero;
            labelText.rectTransform.sizeDelta = Vector2.zero;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = C_Text;
        }

        private void ResolveTimeControls(Transform canvasRoot)
        {
            Transform controls = canvasRoot.Find("TimeControls");
            if (controls == null)
            {
                BuildTimeControls(canvasRoot);
                return;
            }

            _timeControlsRoot = controls as RectTransform;

            Button pauseButton = controls.Find("PauseButton")?.GetComponent<Button>();
            Button speed1xButton = controls.Find("Speed1xButton")?.GetComponent<Button>();
            Button speed2xButton = controls.Find("Speed2xButton")?.GetComponent<Button>();
            Button speed4xButton = controls.Find("Speed4xButton")?.GetComponent<Button>();

            if (pauseButton == null || speed1xButton == null || speed2xButton == null || speed4xButton == null)
            {
                BuildTimeControls(canvasRoot);
                return;
            }

            _pauseButtonImage = pauseButton.targetGraphic as Image;
            _speed1xButtonImage = speed1xButton.targetGraphic as Image;
            _speed2xButtonImage = speed2xButton.targetGraphic as Image;
            _speed4xButtonImage = speed4xButton.targetGraphic as Image;

            pauseButton.onClick.RemoveAllListeners();
            pauseButton.onClick.AddListener(OnPauseButtonClicked);

            speed1xButton.onClick.RemoveAllListeners();
            speed1xButton.onClick.AddListener(() => SetGameSpeed(1f));

            speed2xButton.onClick.RemoveAllListeners();
            speed2xButton.onClick.AddListener(() => SetGameSpeed(2f));

            speed4xButton.onClick.RemoveAllListeners();
            speed4xButton.onClick.AddListener(() => SetGameSpeed(4f));
        }

        private void OnPauseButtonClicked()
        {
            float currentSpeed = GetCurrentGameSpeed();

            if (currentSpeed > 0.0001f)
            {
                _lastNonPausedSpeed = currentSpeed;
            }

            if (currentSpeed <= 0.0001f)
            {
                SetGameSpeed(_lastNonPausedSpeed);
                return;
            }

            ResolveTimeSystem();
            if (_timeSystem != null)
            {
                _timeSystem.PauseGame();
            }
            else
            {
                Time.timeScale = 0f;
            }

            RefreshTimeControlButtons();
        }

        private void SetGameSpeed(float speed)
        {
            float clampedSpeed = Mathf.Clamp(speed, 0f, 10f);
            if (clampedSpeed > 0.0001f)
            {
                _lastNonPausedSpeed = clampedSpeed;
            }

            ResolveTimeSystem();
            if (_timeSystem != null)
            {
                _timeSystem.SetTimeScale(clampedSpeed);
            }
            else
            {
                Time.timeScale = clampedSpeed;
            }

            RefreshTimeControlButtons();
        }

        private float GetCurrentGameSpeed()
        {
            ResolveTimeSystem();
            if (_timeSystem != null)
            {
                return _timeSystem.CurrentTimeScale;
            }

            return Time.timeScale;
        }

        private void RefreshTimeControlButtons()
        {
            if (_pauseButtonImage == null && _speed1xButtonImage == null && _speed2xButtonImage == null && _speed4xButtonImage == null)
            {
                return;
            }

            float currentSpeed = GetCurrentGameSpeed();
            bool isPaused = currentSpeed <= 0.0001f;

            if (!isPaused)
            {
                _lastNonPausedSpeed = currentSpeed;
            }

            ApplyTimeButtonVisual(_pauseButtonImage, isPaused);
            ApplyTimeButtonVisual(_speed1xButtonImage, !isPaused && Mathf.Abs(currentSpeed - 1f) < 0.05f);
            ApplyTimeButtonVisual(_speed2xButtonImage, !isPaused && Mathf.Abs(currentSpeed - 2f) < 0.05f);
            ApplyTimeButtonVisual(_speed4xButtonImage, !isPaused && Mathf.Abs(currentSpeed - 4f) < 0.05f);
        }

        private static void ApplyTimeButtonVisual(Image buttonImage, bool active)
        {
            if (buttonImage == null)
            {
                return;
            }

            buttonImage.color = active ? C_TimeButtonOn : C_TimeButton;
        }

        private void InitializeSquadManagementOverlay()
        {
            if (!enableSquadManagementOverlay)
            {
                _isSquadManagementOpen = false;
                UpdateCombatHudVisibility();
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
                GameObject overlayRoot = new GameObject("SquadManagementUI");
                overlayRoot.transform.SetParent(transform, false);
                squadManagementUI = overlayRoot.AddComponent<ZomberaSquadManagementUI>();
            }

            SetSquadManagementOpen(squadManagementStartsOpen);
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
                SetSquadManagementOpen(!_isSquadManagementOpen);
                return;
            }

            if (_isSquadManagementOpen && WasKeyPressedThisFrame(closeSquadManagementKey))
            {
                SetSquadManagementOpen(false);
            }
        }

        private void HandleTimeControlHotkeys()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (WasKeyPressedThisFrame(pauseToggleKey))
            {
                OnPauseButtonClicked();
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

            _isSquadManagementOpen = true;
            UpdateCombatHudVisibility();
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

        private void SetSquadManagementOpen(bool isOpen)
        {
            _isSquadManagementOpen = isOpen;
            squadManagementUI?.SetVisible(_isSquadManagementOpen);
            UpdateCombatHudVisibility();
        }

        private void UpdateCombatHudVisibility()
        {
            if (_combatHudRoot == null)
            {
                return;
            }

            bool showCombatHud = !hideCombatHudWhileSquadManagementOpen || !_isSquadManagementOpen;
            _combatHudRoot.SetActive(showCombatHud);
        }

        // ─── UI utility helpers ───────────────────────────────────────────────

        static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static void SetAnchors(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.pivot     = new Vector2(minX, minY);
        }

        static Image AddImage(RectTransform rt, Color color)
        {
            Image img  = rt.gameObject.AddComponent<Image>();
            img.color  = color;
            img.raycastTarget = false;
            return img;
        }

        static void AddPanelBolt(RectTransform parent, string name, float anchorX, float anchorY, Vector2 offset)
        {
            RectTransform bolt = MakeRect(name, parent);
            SetAnchors(bolt, anchorX, anchorY, anchorX, anchorY);
            bolt.pivot = new Vector2(0.5f, 0.5f);
            bolt.anchoredPosition = offset;
            bolt.sizeDelta        = new Vector2(10, 10);
            AddImage(bolt, C_Bolt);

            RectTransform core = MakeRect("Core", bolt);
            SetAnchors(core, 0.5f, 0.5f, 0.5f, 0.5f);
            core.pivot = new Vector2(0.5f, 0.5f);
            core.anchoredPosition = Vector2.zero;
            core.sizeDelta        = new Vector2(4, 4);
            AddImage(core, C_BoltCore);
        }

        static void AddSoftShadow(Graphic graphic, Color color, Vector2 offset)
        {
            if (graphic == null)
            {
                return;
            }

            Shadow shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = offset;
            shadow.useGraphicAlpha = true;
        }

        static TextMeshProUGUI MakeText(string name, RectTransform parent, string text, float size)
        {
            RectTransform rt = MakeRect(name, parent);
            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = size;
            tmp.color     = Color.white;
            tmp.textWrappingMode       = TextWrappingModes.NoWrap;
            tmp.overflowMode           = TextOverflowModes.Overflow;
            tmp.raycastTarget          = false;
            return tmp;
        }

        static Slider BuildSlider(RectTransform parent, Color bgColor, Color fgColor, out Image fillImage)
        {
            fillImage = null;

            // Background
            Image bg = AddImage(parent, bgColor);
            bg.raycastTarget = false;

            // Fill area
            RectTransform fillArea = MakeRect("FillArea", parent);
            SetAnchors(fillArea, 0, 0, 1, 1);
            fillArea.anchoredPosition = Vector2.zero;
            fillArea.sizeDelta        = new Vector2(-4, -4);

            // Fill
            RectTransform fill = MakeRect("Fill", fillArea);
            SetAnchors(fill, 0, 0, 1, 1);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta        = Vector2.zero;
            fillImage = AddImage(fill, fgColor);

            // Slider component
            Slider slider = parent.gameObject.AddComponent<Slider>();
            slider.fillRect  = fill;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue  = 0f;
            slider.maxValue  = 1f;
            slider.value     = 1f;
            slider.interactable = false;

            return slider;
        }
    }
}
