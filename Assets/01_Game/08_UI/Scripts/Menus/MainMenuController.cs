#region

using System;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.Systems;
using Zombera.UI.SettingsV2;
using EventSystem = UnityEngine.EventSystems.EventSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#endregion

// ReSharper disable LoopCanBeConvertedToQuery

namespace Zombera.UI.Menus
{
    /// <summary>
    ///     Controls top-level main menu actions and routes into menu subpanels.
    /// </summary>
    public sealed partial class MainMenuController : MonoBehaviour
    {
        [Header("Root")] [SerializeField] private GameObject menuRoot;

        [Header("Buttons")] [SerializeField] private Button continueButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")] [SerializeField] private CharacterCreatorController characterCreatorPanel;

        [SerializeField] private SettingsPageController settingsPanel;
        [SerializeField] private SaveGameMenuController loadSavePanel;

        [Header("Visuals")] [SerializeField] private Graphic mainTitleImage;

        [Header("Audio")]
        [SerializeField]
        private AudioClip menuClickSfx;

        [SerializeField] private string menuClickSfxAssetPath = "Assets/03_ThirdParty/Universal Sound FX/BUTTONS/BUTTON_Click_Electric_Sander_Crop_02_mono.wav";

        [SerializeField] [Range(0f, 1f)] private float menuClickVolume = 1f;

        [Header("Load")] [SerializeField] private string loadGameSlotId = string.Empty;

        [SerializeField] private bool disableLoadButtonWhenSaveMissing = true;
        [SerializeField] private string loadGameSaveFolderName = "Saves";

        [Header("Flow")] [SerializeField] private bool requireCharacterCreationBeforeStart = true;

        [SerializeField] private bool loadCharacterCreatorOnStartRequest = true;
        [SerializeField] private bool allowCharacterCreatorBypassWhenUiBlocked;
        [SerializeField] [Min(0.1f)] private float repeatedStartBypassWindowSeconds = 1.5f;

        [Header("Input Fallback")] [SerializeField]
        private bool enablePointerFallbackWhenUiEventsFail = true;

        [Header("Scene")] [SerializeField] private string worldSceneName = "World";

        [SerializeField] private bool useLoadingSceneWhenNoGameManager = true;
        [SerializeField] private string loadingSceneName = "Loading";

        [Header("New Game World")]
        [SerializeField] private WorldMapSizeTier selectedMapSizeTier = WorldMapSizeTier.Medium;

        [SerializeField] private int worldSeed;
        [SerializeField] private Dropdown mapSizeTierDropdown;
        [SerializeField] private InputField worldSeedInputField;

        private readonly Dictionary<Button, UnityAction> _boundClickHandlers = new();
        private AudioSource _uiClickAudioSource;

        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            Initialize();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (menuClickSfx == null && !string.IsNullOrWhiteSpace(menuClickSfxAssetPath))
                menuClickSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(menuClickSfxAssetPath);
        }
#endif

        private void Start()
        {
            EnsureDisplayCameraPresent();
            EnsureButtonsBound();
            RefreshLoadGameButtonState();
            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();

            // Defensive cleanup in case a previous load flow left the persistent overlay alive.
            LoadingScreenOverlay.Hide();
        }

        private void EnsureDisplayCameraPresent()
        {
            var activeScene = gameObject.scene;
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Camera previewReference = null;

            foreach (var cam in cameras)
            {
                if (cam == null || cam.gameObject.scene != activeScene || !cam.gameObject.activeInHierarchy || !cam.enabled)
                    continue;

                if (cam.targetTexture == null && cam.targetDisplay == 0 && cam.cameraType == CameraType.Game)
                    return;

                if (previewReference == null && cam.cameraType == CameraType.Game)
                    previewReference = cam;
            }

            var cameraObject = new GameObject("MainMenuDisplayCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(transform, false);

            var displayCamera = cameraObject.AddComponent<Camera>();
            displayCamera.enabled = true;
            displayCamera.cameraType = CameraType.Game;
            displayCamera.targetDisplay = 0;
            displayCamera.targetTexture = null;
            displayCamera.clearFlags = CameraClearFlags.Skybox;
            displayCamera.nearClipPlane = 0.1f;
            displayCamera.farClipPlane = 3000f;

            if (previewReference != null)
            {
                cameraObject.transform.SetPositionAndRotation(previewReference.transform.position,
                    previewReference.transform.rotation);
                displayCamera.fieldOfView = previewReference.fieldOfView;
            }
            else
            {
                cameraObject.transform.position = new Vector3(0f, 2f, -8f);
                cameraObject.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            }

            if (FindFirstObjectByType<AudioListener>() == null)
                cameraObject.AddComponent<AudioListener>();

            if (Application.isEditor || Debug.isDebugBuild)
                Debug.Log("[MainMenuController] Created fallback display camera for MainMenu because only preview/RT cameras were available.",
                    this);
        }

        private void Update()
        {
            _ = enablePointerFallbackWhenUiEventsFail;
            TryHandlePointerFallback();
        }

        private void OnDestroy()
        {
            var ss = FindFirstObjectByType<Zombera.Core.SaveSystem>();
            if (ss != null) ss.SaveListChanged -= RefreshLoadGameButtonState;

            if (characterCreatorPanel == null) return;

            characterCreatorPanel.SelectionConfirmed -= HandleCharacterCreationConfirmed;
            characterCreatorPanel.VisibilityChanged -= HandleCharacterCreatorVisibilityChanged;
        }

        public void Initialize()
        {
            if (IsInitialized) return;

            AutoResolveReferences();
            EnsureButtonsBound();

            // Ensure Save System is ready
            var sm = SaveManagerGateway.ResolveActive();
            if (sm != null && !sm.IsInitialized) sm.Initialize();
            var ss = FindFirstObjectByType<Zombera.Core.SaveSystem>();
            if (ss != null)
            {
                if (!ss.IsInitialized) ss.Initialize();
                ss.SaveListChanged -= RefreshLoadGameButtonState;
                ss.SaveListChanged += RefreshLoadGameButtonState;
            }

            if (!loadCharacterCreatorOnStartRequest) characterCreatorPanel?.Initialize();

            if (characterCreatorPanel != null)
            {
                characterCreatorPanel.SelectionConfirmed -= HandleCharacterCreationConfirmed;
                characterCreatorPanel.SelectionConfirmed += HandleCharacterCreationConfirmed;
                characterCreatorPanel.VisibilityChanged -= HandleCharacterCreatorVisibilityChanged;
                characterCreatorPanel.VisibilityChanged += HandleCharacterCreatorVisibilityChanged;
            }

            ForceHideCharacterCreatorPanelsAtMenuBoot();
            settingsPanel?.Hide();
            loadSavePanel?.Hide();
            EnsureUiClickAudioSource();
            RefreshLoadGameButtonState();
            Show();
            RefreshTitleVisibility();

            IsInitialized = true;
            }

        public void Show()
        {
            RefreshLoadGameButtonState();
            if (menuRoot != null) menuRoot.SetActive(true);
        }

        public void Hide()
        {
            if (menuRoot != null) menuRoot.SetActive(false);
        }

        // Split concerns are implemented in partial files.
    }
}