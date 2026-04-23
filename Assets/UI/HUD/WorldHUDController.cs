using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Zombera.Characters;
using Zombera.Core;

namespace Zombera.UI
{
    [AddComponentMenu("Zombera/UI/World HUD Controller")]
    [DisallowMultipleComponent]
    public sealed class WorldHUDController : MonoBehaviour
    {
        public enum TabId { None = -1, Squad = 0, Inventory = 1, Crafting = 2, Map = 3, Missions = 4 }

        // ── Serialized ────────────────────────────────────────────────────────

        [Header("Controllers")]
        [SerializeField] private TopBarController topBar;
        [SerializeField] private SquadPortraitStrip portraitStrip;

        [Header("Panels")]
        [SerializeField] private GameObject squadPanel;
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private GameObject craftingPanel;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject missionsPanel;

        [Header("Layout")]
        [SerializeField] private RectTransform panelsRoot;
        [SerializeField] private RectTransform bottomBarRoot;

        [Header("Overlay")]
        [SerializeField] private Image dimOverlay;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.40f;

        [Header("Time Control")]
        [SerializeField] private bool pauseGameWhenMenuOpen = true;

        [Header("Alerts")]
        [SerializeField] private RectTransform alertBanner;
        [SerializeField] private TextMeshProUGUI alertText;
        [SerializeField] private float alertDuration = 4f;

        [Header("Damage Popup")]
        [SerializeField] private bool showDamagePopups = true;
        [SerializeField] private bool showPlayerDamagePopups = true;
        [SerializeField] private bool showEnemyDamagePopups = true;
        [SerializeField, Min(0.1f)] private float playerDamagePopupHeight = 2.1f;
        [SerializeField, Min(0.1f)] private float playerDamagePopupLifetime = 0.95f;
        [SerializeField, Min(0f)] private float playerDamagePopupRisePixels = 52f;
        [SerializeField] private Vector2 playerDamagePopupJitter = new Vector2(16f, 8f);
        [SerializeField, Min(8f)] private float playerDamagePopupFontSize = 24f;
        [SerializeField] private Color playerDamagePopupColor = new Color(0.96f, 0.20f, 0.20f, 1f);
        [SerializeField, Min(0.1f)] private float enemyDamagePopupHeight = 2.2f;
        [SerializeField, Min(0.1f)] private float enemyDamagePopupLifetime = 0.8f;
        [SerializeField, Min(0f)] private float enemyDamagePopupRisePixels = 54f;
        [SerializeField] private Vector2 enemyDamagePopupJitter = new Vector2(12f, 6f);
        [SerializeField, Min(8f)] private float enemyDamagePopupFontSize = 20f;
        [SerializeField] private Color enemyDamagePopupColor = new Color(1f, 0.66f, 0.22f, 1f);

        // ── State ─────────────────────────────────────────────────────────────

        private TabId _activeTab = TabId.None;
        private Coroutine _alertCoroutine;
        private Canvas _canvas;
        private float _panelsBottomInsetWhenBottomBarVisible;
        private bool _layoutInitialized;
        private bool _damageEventsSubscribed;
        private TimeSystem _timeSystem;
        private bool _pausedByMenuTab;
        private RectTransform _damagePopupRoot;
        private Camera _popupCamera;
        private readonly List<DamagePopupView> _damagePopups = new List<DamagePopupView>(16);

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

        public TabId ActiveTab => _activeTab;

        /// <summary>Fired whenever the active tab changes (including closing to None).</summary>
        public event System.Action<TabId> OnTabChanged;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            playerDamagePopupFontSize = Mathf.Max(playerDamagePopupFontSize, 44f);
            enemyDamagePopupFontSize = Mathf.Max(enemyDamagePopupFontSize, 36f);

            // Self-destruct if this ended up in a non-World scene (e.g. built by accident
            // into the main menu scene). This prevents any scripts on child objects from
            // running and interfering with other UI such as the UMA character creator.
            string sceneName = gameObject.scene.name;
            bool inWorldScene = sceneName.IndexOf("World", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!inWorldScene)
            {
                Debug.LogWarning($"[WorldHUDController] Found in scene '{sceneName}' — destroying to avoid menu interference. Run 'Tools/Zombera/World UI/Build World HUD' from the World scene only.", this);
                Destroy(gameObject);
                return;
            }

            _canvas = GetComponent<Canvas>();

            // Stay hidden until GameState.Playing — prevents rendering over main menu.
            if (_canvas != null) _canvas.enabled = false;
        }

        private void OnEnable()
        {
            Zombera.Core.EventSystem.Instance?.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            TrySubscribeDamageEvents();
        }

        private void OnDisable()
        {
            Zombera.Core.EventSystem.Instance?.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            UnsubscribeDamageEvents();
            ReleaseMenuPauseIfOwned();
            ClearDamagePopups();
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            bool gameplay = evt.NewState == GameState.Playing || evt.NewState == GameState.Paused;
            if (_canvas != null) _canvas.enabled = gameplay;
            if (!gameplay)
            {
                CloseTab();
                ClearDamagePopups();
            }
        }

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Start()
        {
            EnsureInventoryPanelController();
            _timeSystem = FindFirstObjectByType<TimeSystem>();
            SetAllPanels(false);
            if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
            if (alertBanner != null) alertBanner.gameObject.SetActive(false);
            InitializeLayoutState();
            UpdateLayoutForTabState(panelOpen: false);
        }

        private void EnsureInventoryPanelController()
        {
            if (inventoryPanel == null)
            {
                return;
            }

            if (inventoryPanel.GetComponent<InventoryPanelController>() != null)
            {
                return;
            }

            inventoryPanel.AddComponent<InventoryPanelController>();
        }

        private void Update()
        {
            if (!_damageEventsSubscribed)
            {
                TrySubscribeDamageEvents();
            }

            UpdateDamagePopups();

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if      (kb.f1Key.wasPressedThisFrame) ToggleTab(TabId.Squad);
            else if (kb.f2Key.wasPressedThisFrame) ToggleTab(TabId.Inventory);
            else if (kb.f3Key.wasPressedThisFrame) ToggleTab(TabId.Crafting);
            else if (kb.f4Key.wasPressedThisFrame) ToggleTab(TabId.Map);
            else if (kb.f5Key.wasPressedThisFrame) ToggleTab(TabId.Missions);
            else if (kb.escapeKey.wasPressedThisFrame && _activeTab != TabId.None)
                CloseTab();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void OpenTab(TabId tab)
        {
            if (_activeTab == tab) return;
            ApplyTab(tab);
        }

        public void ToggleTab(TabId tab) =>
            ApplyTab(_activeTab == tab ? TabId.None : tab);

        public void CloseTab() => ApplyTab(TabId.None);

        /// <summary>Display a timed alert banner.</summary>
        public void ShowAlert(string message, bool warning = false)
        {
            if (alertText != null)
            {
                alertText.text  = message;
                alertText.color = warning
                    ? new Color(0.95f, 0.55f, 0.20f, 1f)
                    : new Color(0.85f, 0.82f, 0.72f, 1f);
            }

            if (_alertCoroutine != null) StopCoroutine(_alertCoroutine);
            _alertCoroutine = StartCoroutine(AlertRoutine());
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void ApplyTab(TabId tab)
        {
            _activeTab = tab;

            if (squadPanel)     squadPanel.SetActive(tab    == TabId.Squad);
            if (inventoryPanel) inventoryPanel.SetActive(tab == TabId.Inventory);
            if (craftingPanel)  craftingPanel.SetActive(tab  == TabId.Crafting);
            if (mapPanel)       mapPanel.SetActive(tab       == TabId.Map);
            if (missionsPanel)  missionsPanel.SetActive(tab  == TabId.Missions);

            bool panelOpen = tab != TabId.None;
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(panelOpen);
                Color c = dimOverlay.color;
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
                if (!panelOpen)
                {
                    _pausedByMenuTab = false;
                }

                return;
            }

            TimeSystem timeSystem = ResolveTimeSystem();
            if (timeSystem == null)
            {
                if (!panelOpen)
                {
                    _pausedByMenuTab = false;
                }

                return;
            }

            if (panelOpen)
            {
                if (_pausedByMenuTab)
                {
                    return;
                }

                if (!timeSystem.IsPaused)
                {
                    timeSystem.PauseGame();
                    _pausedByMenuTab = true;
                }

                return;
            }

            ReleaseMenuPauseIfOwned();
        }

        private void ReleaseMenuPauseIfOwned()
        {
            if (!_pausedByMenuTab)
            {
                return;
            }

            _pausedByMenuTab = false;

            if (!pauseGameWhenMenuOpen)
            {
                return;
            }

            TimeSystem timeSystem = ResolveTimeSystem();
            if (timeSystem == null || !timeSystem.IsPaused)
            {
                return;
            }

            if (GameManager.Instance != null)
            {
                GameState state = GameManager.Instance.CurrentState;
                if (state != GameState.Playing && state != GameState.Paused)
                {
                    return;
                }
            }

            timeSystem.ResumeGame();
        }

        private TimeSystem ResolveTimeSystem()
        {
            if (_timeSystem == null)
            {
                _timeSystem = FindFirstObjectByType<TimeSystem>();
            }

            return _timeSystem;
        }

        private void InitializeLayoutState()
        {
            if (panelsRoot == null && squadPanel != null)
            {
                panelsRoot = squadPanel.transform.parent as RectTransform;
            }

            if (bottomBarRoot == null && portraitStrip != null)
            {
                bottomBarRoot = portraitStrip.transform.parent as RectTransform;
            }

            if (panelsRoot != null)
            {
                _panelsBottomInsetWhenBottomBarVisible = panelsRoot.offsetMin.y;
                _layoutInitialized = true;
            }
        }

        private void UpdateLayoutForTabState(bool panelOpen)
        {
            if (!_layoutInitialized)
            {
                InitializeLayoutState();
            }

            if (bottomBarRoot != null)
            {
                bottomBarRoot.gameObject.SetActive(!panelOpen);
            }

            if (panelsRoot != null)
            {
                Vector2 min = panelsRoot.offsetMin;
                min.y = panelOpen ? 0f : _panelsBottomInsetWhenBottomBarVisible;
                panelsRoot.offsetMin = min;
            }
        }

        private void SetAllPanels(bool active)
        {
            if (squadPanel)     squadPanel.SetActive(active);
            if (inventoryPanel) inventoryPanel.SetActive(active);
            if (craftingPanel)  craftingPanel.SetActive(active);
            if (mapPanel)       mapPanel.SetActive(active);
            if (missionsPanel)  missionsPanel.SetActive(active);
        }

        private IEnumerator AlertRoutine()
        {
            if (alertBanner == null) yield break;
            alertBanner.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(alertDuration);
            alertBanner.gameObject.SetActive(false);
        }

        private void TrySubscribeDamageEvents()
        {
            if (_damageEventsSubscribed)
            {
                return;
            }

            if (Zombera.Core.EventSystem.Instance == null)
            {
                return;
            }

            Zombera.Core.EventSystem.Instance.Subscribe<UnitDamagedEvent>(OnUnitDamaged);
            _damageEventsSubscribed = true;
        }

        private void UnsubscribeDamageEvents()
        {
            if (!_damageEventsSubscribed)
            {
                return;
            }

            Zombera.Core.EventSystem.Instance?.Unsubscribe<UnitDamagedEvent>(OnUnitDamaged);
            _damageEventsSubscribed = false;
        }

        private void OnUnitDamaged(UnitDamagedEvent gameEvent)
        {
            if (!Application.isPlaying || !showDamagePopups || gameEvent.Amount <= 0f || gameEvent.UnitObject == null)
            {
                return;
            }

            bool isPlayer = gameEvent.Role == UnitRole.Player;
            bool isEnemy = gameEvent.Role == UnitRole.Zombie
                           || gameEvent.Role == UnitRole.Enemy
                           || gameEvent.Role == UnitRole.Bandit;

            if (isPlayer)
            {
                if (!showPlayerDamagePopups)
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
                    playerDamagePopupColor,
                    includeHpSuffix: true);
                return;
            }

            if (!isEnemy || !showEnemyDamagePopups)
            {
                return;
            }

            ShowDamagePopupForTarget(
                gameEvent.Amount,
                gameEvent.UnitObject.transform,
                enemyDamagePopupHeight,
                enemyDamagePopupLifetime,
                enemyDamagePopupRisePixels,
                enemyDamagePopupJitter,
                enemyDamagePopupFontSize,
                enemyDamagePopupColor,
                includeHpSuffix: false);
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
            if (amount <= 0f || followTarget == null)
            {
                return;
            }

            if (!EnsureDamagePopupRoot())
            {
                return;
            }

            RectTransform popupRoot = MakeRect("DamagePopup", _damagePopupRoot);
            popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
            popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
            popupRoot.pivot = new Vector2(0.5f, 0.5f);
            popupRoot.anchoredPosition = Vector2.zero;
            popupRoot.sizeDelta = new Vector2(220f, 48f);

            int damageValue = Mathf.Max(1, Mathf.RoundToInt(amount));
            string suffix = includeHpSuffix ? " HP" : string.Empty;
            TextMeshProUGUI label = MakeText("Label", popupRoot, $"-{damageValue}{suffix}", popupFontSize);
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.color = popupColor;

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

            _damagePopups.Add(popup);
            UpdateDamagePopups();
        }

        private bool EnsureDamagePopupRoot()
        {
            if (_damagePopupRoot == null)
            {
                Transform existing = transform.Find("DamagePopups");
                if (existing == null)
                {
                    _damagePopupRoot = MakeRect("DamagePopups", transform);
                    CanvasGroup group = _damagePopupRoot.gameObject.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    group.blocksRaycasts = false;
                }
                else
                {
                    _damagePopupRoot = existing as RectTransform;
                }
            }

            if (_damagePopupRoot == null)
            {
                return false;
            }

            _damagePopupRoot.anchorMin = Vector2.zero;
            _damagePopupRoot.anchorMax = Vector2.one;
            _damagePopupRoot.pivot = new Vector2(0.5f, 0.5f);
            _damagePopupRoot.anchoredPosition = Vector2.zero;
            _damagePopupRoot.sizeDelta = Vector2.zero;
            _damagePopupRoot.SetAsLastSibling();
            return true;
        }

        private void UpdateDamagePopups()
        {
            if (_damagePopups.Count == 0)
            {
                return;
            }

            if (!EnsureDamagePopupRoot())
            {
                return;
            }

            Camera worldCamera = ResolvePopupCamera();
            if (!IsCameraUsable(worldCamera))
            {
                return;
            }

            for (int i = _damagePopups.Count - 1; i >= 0; i--)
            {
                DamagePopupView popup = _damagePopups[i];
                if (popup == null || popup.Root == null || popup.Label == null)
                {
                    _damagePopups.RemoveAt(i);
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

                    _damagePopups.RemoveAt(i);
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

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_damagePopupRoot, screenPoint, null, out Vector2 localPoint))
                {
                    localPoint += popup.Jitter;
                    localPoint.y += Mathf.Lerp(0f, popup.RisePixels, t);
                    popup.Root.anchoredPosition = localPoint;
                }

                Color color = popup.BaseColor;
                color.a = Mathf.Clamp01(1f - t);
                popup.Label.color = color;

                float scale = Mathf.Lerp(1f, 1.08f, t);
                popup.Root.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private Camera ResolvePopupCamera()
        {
            if (IsCameraUsable(_popupCamera))
            {
                return _popupCamera;
            }

            if (IsCameraUsable(Camera.main))
            {
                _popupCamera = Camera.main;
                return _popupCamera;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (IsCameraUsable(cameras[i]))
                {
                    _popupCamera = cameras[i];
                    return _popupCamera;
                }
            }

            return null;
        }

        private static bool IsCameraUsable(Camera camera)
        {
            return camera != null && camera.isActiveAndEnabled && camera.gameObject.activeInHierarchy;
        }

        private void ClearDamagePopups()
        {
            for (int i = 0; i < _damagePopups.Count; i++)
            {
                DamagePopupView popup = _damagePopups[i];
                if (popup != null && popup.Root != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(popup.Root.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(popup.Root.gameObject);
                    }
                }
            }

            _damagePopups.Clear();
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static TextMeshProUGUI MakeText(string name, RectTransform parent, string text, float size)
        {
            RectTransform rt = MakeRect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
