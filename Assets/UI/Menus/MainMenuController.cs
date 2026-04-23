using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Zombera.Core;

namespace Zombera.UI.Menus
{
    /// <summary>
    /// Controls top-level main menu actions and routes into menu subpanels.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private static readonly string[] WorldSceneFallbackNames =
        {
            "World_MapMagicStream",
            "World_Map_MagicStream"
        };

        [Header("Root")]
        [SerializeField] private GameObject menuRoot;

        [Header("Buttons")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")]
        [SerializeField] private CharacterCreatorController characterCreatorPanel;
        [SerializeField] private SettingsMenuController settingsPanel;

        [Header("Visuals")]
        [SerializeField] private Graphic mainTitleImage;

        [Header("Flow")]
        [SerializeField] private bool requireCharacterCreationBeforeStart = true;
        [SerializeField] private bool allowCharacterCreatorBypassWhenUiBlocked = false;
        [SerializeField, Min(0.1f)] private float repeatedStartBypassWindowSeconds = 1.5f;

        [Header("Input Fallback")]
        [SerializeField] private bool enablePointerFallbackWhenUiEventsFail = true;

        [Header("Scene")]
        [SerializeField] private string worldSceneName = "World";
        [SerializeField] private bool useLoadingSceneWhenNoGameManager = true;
        [SerializeField] private string loadingSceneName = "Loading";

        public bool IsInitialized { get; private set; }

        private bool hasCompletedCharacterCreation;
        private float lastStartAttemptAt = -10f;
        private int repeatedStartWhileCreatorVisible;
        private int lastStartHandledFrame = -1;

        private void Awake()
        {
            Initialize();
        }

        private void Start()
        {
            EnsureButtonsBound();
            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();

            // Defensive cleanup in case a previous load flow left the persistent overlay alive.
            LoadingScreenOverlay.Hide();
        }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            AutoResolveReferences();
            EnsureButtonsBound();

            characterCreatorPanel?.Initialize();
            settingsPanel?.Initialize();

            if (characterCreatorPanel != null)
            {
                characterCreatorPanel.SelectionConfirmed -= HandleCharacterCreationConfirmed;
                characterCreatorPanel.SelectionConfirmed += HandleCharacterCreationConfirmed;
                characterCreatorPanel.VisibilityChanged -= HandleCharacterCreatorVisibilityChanged;
                characterCreatorPanel.VisibilityChanged += HandleCharacterCreatorVisibilityChanged;
            }

            characterCreatorPanel?.Hide();
            settingsPanel?.Hide();
            Show();
            RefreshTitleVisibility();

            IsInitialized = true;
        }

        private void OnDestroy()
        {
            if (characterCreatorPanel != null)
            {
                characterCreatorPanel.SelectionConfirmed -= HandleCharacterCreationConfirmed;
                characterCreatorPanel.VisibilityChanged -= HandleCharacterCreatorVisibilityChanged;
            }
        }

        private void Update()
        {
            TryHandlePointerFallback();
        }

        public void Show()
        {
            if (menuRoot != null)
            {
                menuRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            if (menuRoot != null)
            {
                menuRoot.SetActive(false);
            }
        }

        private void HandleStartGameRequested()
        {
            // UI listeners and pointer fallback can both fire on the same frame.
            // Ignore duplicate same-frame start requests so a single click cannot bypass creator flow.
            if (lastStartHandledFrame == Time.frameCount)
            {
                return;
            }

            lastStartHandledFrame = Time.frameCount;
            AutoResolveReferences();
            float now = Time.unscaledTime;

            if (requireCharacterCreationBeforeStart && !hasCompletedCharacterCreation)
            {
                if (characterCreatorPanel == null)
                {
                    if (allowCharacterCreatorBypassWhenUiBlocked)
                    {
                        Debug.LogWarning("[MainMenuController] Character creator is missing; bypassing requirement and starting session.", this);
                        hasCompletedCharacterCreation = true;
                        StartSession();
                        return;
                    }

                    Debug.LogError("[MainMenuController] Character creator is required before start, but Character Creator Panel is not assigned.", this);
                    return;
                }

                if (!characterCreatorPanel.IsVisible)
                {
                    repeatedStartWhileCreatorVisible = 0;
                    lastStartAttemptAt = now;
                    Debug.Log("[MainMenuController] Start requested: showing character creator first.", this);
                    characterCreatorPanel.Show();
                    return;
                }

                if (now - lastStartAttemptAt <= repeatedStartBypassWindowSeconds)
                {
                    repeatedStartWhileCreatorVisible++;
                }
                else
                {
                    repeatedStartWhileCreatorVisible = 1;
                }

                lastStartAttemptAt = now;

                if (allowCharacterCreatorBypassWhenUiBlocked && repeatedStartWhileCreatorVisible >= 2)
                {
                    Debug.LogWarning("[MainMenuController] Repeated Start clicks while creator is visible; bypassing creator requirement.", this);
                    hasCompletedCharacterCreation = true;
                    StartSession();
                    return;
                }

                characterCreatorPanel.Show();
                return;
            }

            Debug.Log("[MainMenuController] Start requirements satisfied; starting session.", this);
            StartSession();
        }

        private void HandleCharacterCreationConfirmed(string characterName)
        {
            _ = characterName;
            hasCompletedCharacterCreation = true;
            StartSession();
        }

        private void StartSession()
        {
            // Hide the character creator panel BEFORE the world session starts so
            // WorldHUDCanvas (sortingOrder=Hud/10) cannot render over it when
            // GameState.Playing fires and enables the canvas.
            characterCreatorPanel?.Hide();
            PrepareCharacterCreatorsForSceneTransition();
            characterCreatorPanel?.PrepareForSceneTransition();
            Debug.Log("[MainMenuController] StartSession invoked.", this);

            if (GameManager.Instance != null)
            {
                if (!GameManager.Instance.IsInitialized)
                {
                    GameManager.Instance.InitializeSystems();
                }

                Debug.Log("[MainMenuController] Delegating world start to GameManager.", this);
                GameManager.Instance.StartNewGame();
                Hide();
                return;
            }

            if (!string.IsNullOrWhiteSpace(worldSceneName))
            {
                if (!TryResolveLoadableWorldScene(worldSceneName, out string resolvedWorldSceneName))
                {
                    Debug.LogError($"[MainMenuController] No loadable world scene found. Checked {GetWorldSceneCandidateSummary(worldSceneName)}.", this);
                    return;
                }

                if (useLoadingSceneWhenNoGameManager &&
                    !string.IsNullOrWhiteSpace(loadingSceneName) &&
                    Application.CanStreamedLevelBeLoaded(loadingSceneName))
                {
                    SceneManager.LoadScene(loadingSceneName);
                    Hide();
                    return;
                }

                SceneManager.LoadScene(resolvedWorldSceneName);
                Hide();
            }
        }

        private static bool TryResolveLoadableWorldScene(string configuredWorldSceneName, out string resolvedWorldSceneName)
        {
            if (!string.IsNullOrWhiteSpace(configuredWorldSceneName)
                && Application.CanStreamedLevelBeLoaded(configuredWorldSceneName))
            {
                resolvedWorldSceneName = configuredWorldSceneName;
                return true;
            }

            for (int i = 0; i < WorldSceneFallbackNames.Length; i++)
            {
                string fallbackName = WorldSceneFallbackNames[i];
                if (string.IsNullOrWhiteSpace(fallbackName))
                {
                    continue;
                }

                if (!Application.CanStreamedLevelBeLoaded(fallbackName))
                {
                    continue;
                }

                resolvedWorldSceneName = fallbackName;
                return true;
            }

            resolvedWorldSceneName = configuredWorldSceneName;
            return false;
        }

        private static string GetWorldSceneCandidateSummary(string configuredWorldSceneName)
        {
            string summary = string.IsNullOrWhiteSpace(configuredWorldSceneName)
                ? "(no configured world scene)"
                : $"'{configuredWorldSceneName}'";

            for (int i = 0; i < WorldSceneFallbackNames.Length; i++)
            {
                string fallbackName = WorldSceneFallbackNames[i];
                if (string.IsNullOrWhiteSpace(fallbackName))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(configuredWorldSceneName)
                    && string.Equals(configuredWorldSceneName, fallbackName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                summary += $", '{fallbackName}'";
            }

            return summary;
        }

        private void PrepareCharacterCreatorsForSceneTransition()
        {
            CharacterCreatorController[] creators =
                FindObjectsByType<CharacterCreatorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Scene activeScene = gameObject.scene;

            for (int i = 0; i < creators.Length; i++)
            {
                CharacterCreatorController creator = creators[i];

                if (creator == null || creator.gameObject.scene != activeScene)
                {
                    continue;
                }

                creator.PrepareForSceneTransition();
            }
        }

        private void HandleSettingsRequested()
        {
            settingsPanel?.Show();
        }

        private void HandleCharacterCreatorVisibilityChanged(bool isVisible)
        {
            SetMainTitleVisible(!isVisible);
        }

        private void RefreshTitleVisibility()
        {
            bool isCharacterCreatorOpen = characterCreatorPanel != null && characterCreatorPanel.IsVisible;
            SetMainTitleVisible(!isCharacterCreatorOpen);
        }

        private void SetMainTitleVisible(bool isVisible)
        {
            if (mainTitleImage != null)
            {
                mainTitleImage.enabled = isVisible;
            }
        }

        private static void HandleQuitRequested()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void TryHandlePointerFallback()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!enablePointerFallbackWhenUiEventsFail)
            {
                return;
            }

            // Fallback is only for main menu button interaction. If main menu root is hidden
            // (e.g. transition/start flow), do not process fallback clicks.
            if (menuRoot != null && !menuRoot.activeInHierarchy)
            {
                return;
            }

            // Never run fallback while subpanels are open; their own buttons should own input.
            if (characterCreatorPanel != null && characterCreatorPanel.IsVisible)
            {
                return;
            }

            if (settingsPanel != null && settingsPanel.IsVisible)
            {
                return;
            }

            // If EventSystem is currently over UI, let normal UI processing handle the click.
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (startGameButton == null || settingsButton == null || quitButton == null)
            {
                EnsureButtonsBound();
            }

            if (!TryReadPointerDownPosition(out Vector2 screenPosition))
            {
                return;
            }

            if (TryInvokeIfPointerOverButton(startGameButton, screenPosition))
            {
                Debug.Log("[MainMenuController] Pointer fallback invoked Start Game.", this);
                return;
            }

            if (TryInvokeIfPointerOverButton(settingsButton, screenPosition))
            {
                Debug.Log("[MainMenuController] Pointer fallback invoked Settings.", this);
                return;
            }

            if (TryInvokeIfPointerOverButton(quitButton, screenPosition))
            {
                Debug.Log("[MainMenuController] Pointer fallback invoked Quit.", this);
            }
        }

        private static bool TryReadPointerDownPosition(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }
#endif

            try
            {
                if (Input.GetMouseButtonDown(0))
                {
                    screenPosition = Input.mousePosition;
                    return true;
                }
            }
            catch (System.InvalidOperationException)
            {
                // Ignore legacy input API when project input backend does not support it.
            }

            screenPosition = Vector2.zero;
            return false;
        }

        private static bool TryInvokeIfPointerOverButton(Button button, Vector2 screenPosition)
        {
            if (button == null || !button.isActiveAndEnabled || !button.interactable)
            {
                return false;
            }

            RectTransform rectTransform = button.transform as RectTransform;
            if (rectTransform == null)
            {
                return false;
            }

            Camera eventCamera = ResolveEventCamera(button);
            bool containsPointer = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);
            if (!containsPointer && eventCamera != null)
            {
                containsPointer = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, null);
            }

            if (!containsPointer)
            {
                return false;
            }

            button.onClick.Invoke();
            return true;
        }

        private static Camera ResolveEventCamera(Button button)
        {
            if (button == null)
            {
                return null;
            }

            Canvas parentCanvas = button.GetComponentInParent<Canvas>();
            if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return parentCanvas.worldCamera;
        }

        private void EnsureButtonsBound()
        {
            AutoResolveReferences();
            BindButton(startGameButton, HandleStartGameRequested);
            BindButton(settingsButton, HandleSettingsRequested);
            BindButton(quitButton, HandleQuitRequested);
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(callback);
            button.onClick.AddListener(callback);
        }

        private void AutoResolveReferences()
        {
            if (menuRoot == null)
            {
                menuRoot = gameObject;
            }

            if (startGameButton == null)
            {
                startGameButton = FindButtonByName("StartGame");
            }

            if (settingsButton == null)
            {
                settingsButton = FindButtonByName("SettingsButton");

                if (settingsButton == null)
                {
                    settingsButton = FindButtonByName("Settings");
                }
            }

            if (quitButton == null)
            {
                quitButton = FindButtonByName("QuitButton");

                if (quitButton == null)
                {
                    quitButton = FindButtonByName("Quit");
                }
            }

            if (characterCreatorPanel == null)
            {
                characterCreatorPanel = FindPanelController<CharacterCreatorController>("CharacterCreatorPanel");
            }

            if (settingsPanel == null)
            {
                settingsPanel = FindPanelController<SettingsMenuController>("SettingsPanel");
            }

            if (mainTitleImage == null && menuRoot != null)
            {
                Transform titleTransform = FindTransformByName(menuRoot.transform, "MainTitleImage");

                if (titleTransform == null)
                {
                    titleTransform = FindTransformByName(menuRoot.transform, "TitleImage");
                }

                if (titleTransform == null)
                {
                    Canvas parentCanvas = menuRoot.GetComponentInParent<Canvas>();

                    if (parentCanvas != null)
                    {
                        titleTransform = FindTransformByName(parentCanvas.transform, "MainTitleImage");

                        if (titleTransform == null)
                        {
                            titleTransform = FindTransformByName(parentCanvas.transform, "TitleImage");
                        }

                        if (titleTransform == null)
                        {
                            titleTransform = FindTransformByName(parentCanvas.transform, "Screen Space Overlay");
                        }

                        if (titleTransform == null)
                        {
                            Graphic canvasGraphic = parentCanvas.GetComponent<Graphic>();

                            if (canvasGraphic != null)
                            {
                                mainTitleImage = canvasGraphic;
                            }
                        }
                    }
                }

                if (mainTitleImage == null && titleTransform != null)
                {
                    mainTitleImage = titleTransform.GetComponent<Graphic>();

                    if (mainTitleImage == null)
                    {
                        mainTitleImage = titleTransform.GetComponentInChildren<Graphic>(true);
                    }
                }
            }
        }

        private Button FindButtonByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            if (menuRoot != null)
            {
                Button[] buttons = menuRoot.GetComponentsInChildren<Button>(true);

                for (int index = 0; index < buttons.Length; index++)
                {
                    Button button = buttons[index];

                    if (button != null && string.Equals(button.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return button;
                    }
                }
            }

            Button[] allButtons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int index = 0; index < allButtons.Length; index++)
            {
                Button button = allButtons[index];

                if (button != null && string.Equals(button.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return button;
                }
            }

            return null;
        }

        private TPanel FindPanelController<TPanel>(string panelObjectName) where TPanel : MonoBehaviour
        {
            TPanel panelController = null;

            if (menuRoot != null)
            {
                panelController = menuRoot.GetComponentInChildren<TPanel>(true);

                if (panelController != null)
                {
                    return panelController;
                }

                if (!string.IsNullOrWhiteSpace(panelObjectName))
                {
                    Transform panelTransform = FindTransformByName(menuRoot.transform, panelObjectName);

                    if (panelTransform != null)
                    {
                        panelController = panelTransform.GetComponent<TPanel>();

                        if (panelController == null)
                        {
                            panelController = panelTransform.gameObject.AddComponent<TPanel>();
                            Debug.LogWarning($"[MainMenuController] Auto-added {typeof(TPanel).Name} to '{panelTransform.name}'.", panelTransform.gameObject);
                        }

                        return panelController;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(panelObjectName))
            {
                GameObject panelObject = GameObject.Find(panelObjectName);
                if (panelObject != null)
                {
                    panelController = panelObject.GetComponent<TPanel>();

                    if (panelController == null)
                    {
                        panelController = panelObject.AddComponent<TPanel>();
                        Debug.LogWarning($"[MainMenuController] Auto-added {typeof(TPanel).Name} to '{panelObject.name}'.", panelObject);
                    }

                    return panelController;
                }
            }

            return Object.FindFirstObjectByType<TPanel>(FindObjectsInactive.Include);
        }

        private static Transform FindTransformByName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            if (string.Equals(root.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                Transform found = FindTransformByName(root.GetChild(childIndex), objectName);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}