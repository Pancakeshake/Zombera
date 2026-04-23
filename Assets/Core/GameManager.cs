using System;
using System.Collections;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.UI;
using Zombera.UI.Menus;
using Zombera.UI.SquadManagement;
using Zombera.World;
using Zombera.World.Simulation;
#if ENABLE_INPUT_SYSTEM
using InputSystemUIInputModule = UnityEngine.InputSystem.UI.InputSystemUIInputModule;
#endif
using StandaloneInputModule = UnityEngine.EventSystems.StandaloneInputModule;
using UnityUiEventSystem = UnityEngine.EventSystems.EventSystem;

namespace Zombera.Core
{
    /// <summary>
    /// Central bootstrap and game state coordinator.
    /// Controls high-level game flow and initializes core systems.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        private const string RuntimeAudioListenerObjectName = "RuntimeAudioListener";
        private static readonly string[] WorldSceneFallbackNames =
        {
            "World_MapMagicStream",
            "World_Map_MagicStream"
        };

        public static GameManager Instance { get; private set; }

        public GameState CurrentState { get; private set; } = GameState.Booting;
        public bool IsInitialized { get; private set; }

        [Header("Core Systems")]
        [SerializeField] private TimeSystem timeSystem;
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private EventSystem eventSystem;

        [Header("Startup")]
        [SerializeField] private bool initializeOnStart = true;
        [SerializeField] private bool autoStartSessionForTesting;
        [SerializeField] private bool loadMainMenuSceneOnInitialize = true;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private bool loadWorldSceneOnSessionStart = true;
        [SerializeField] private string worldSceneName = "World";

        [Header("Loading")]
        [SerializeField] private bool useIntermediateLoadingScene = true;
        [SerializeField] private string loadingSceneName = "Loading";
        [SerializeField, Min(0f)] private float minimumLoadingScreenSeconds = 1f;

        [Header("Validation")]
        [SerializeField] private bool runReadinessValidationOnInitialize = true;
        [SerializeField] private StartupReadinessValidator startupReadinessValidator;

        [Header("Top-Level Managers")]
        [SerializeField] private UnitManager unitManager;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private AIManager aiManager;
        [SerializeField] private SquadManager squadManager;
        [SerializeField] private ZombieManager zombieManager;
        [SerializeField] private LootManager lootManager;
        [SerializeField] private BaseManager baseManager;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private WorldManager worldManager;

        private bool pendingStartNewGame;
        private bool pendingLoadGame;
        private string pendingLoadSlotId;
        private bool worldLoadInProgress;
        private bool worldSessionStarting;

        private void Awake()
        {
            GameObject persistentRoot = transform.root.gameObject;

            if (Instance != null && Instance != this)
            {
                enabled = false;
                // Never destroy the whole root from duplicate singleton checks.
                // Scene roots may also host unrelated runtime systems.
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(persistentRoot);
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureSingleAudioListener();
            PruneGameplayUiOwnershipForScene(SceneManager.GetActiveScene());
        }

        private void Start()
        {
            if (!initializeOnStart)
            {
                return;
            }

            InitializeSystems();

            if (autoStartSessionForTesting)
            {
                StartNewGame();
            }
        }

        public void InitializeSystems()
        {
            if (IsInitialized)
            {
                return;
            }

            ResolveRuntimeReferences();

            eventSystem?.Initialize();
            timeSystem?.Initialize();
            saveSystem?.Initialize();
            combatManager?.Initialize();
            aiManager?.Initialize();
            saveManager?.Initialize();
            zombieManager?.Initialize();
            lootManager?.Initialize();
            baseManager?.Initialize();
            unitManager?.RefreshRegistry();
            squadManager?.RefreshSquadRoster();

            IsInitialized = true;
            SetGameState(GameState.MainMenu);

            // Surface missing-reference diagnostics before scene load.
            LogMissingSystemWarnings();

            RunReadinessValidation(StartupReadinessValidator.ValidationMode.BootOrMenu);
            TryLoadMainMenuSceneOnInitialize();
        }

        public void SetGameState(GameState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            GameState previous = CurrentState;
            OnExitState(previous);
            CurrentState = newState;
            SyncGameplayUiVisibility(CurrentState);

            if (CurrentState == GameState.Playing || CurrentState == GameState.Paused)
            {
                EnsureUnityUiEventSystemPresent();
            }

            EventSystem.PublishGlobal(new GameStateChangedEvent
            {
                PreviousState = previous,
                NewState = CurrentState
            });

            OnEnterState(CurrentState);
        }

        private void OnEnterState(GameState state)
        {
            switch (state)
            {
                case GameState.Playing:
                    timeSystem?.ResumeGame();
                    worldManager?.SetSimulationActive(true);
                    break;
                case GameState.Paused:
                    timeSystem?.PauseGame();
                    break;
                case GameState.MainMenu:
                    timeSystem?.ResumeGame();
                    worldManager?.SetSimulationActive(false);
                    break;
            }
        }

        private static void OnExitState(GameState state)
        {
            _ = state;
        }

        public void StartNewGame()
        {
            SetGameState(GameState.LoadingWorld);
            LoadingScreenOverlay.Show("Preparing new session...");
            LoadingScreenOverlay.SetProgress(0f, "Preparing new session...");

            pendingStartNewGame = true;
            pendingLoadGame = false;
            pendingLoadSlotId = null;

            DisableUnityUiEventSystemsForSceneTransition();

            if (TryLoadLoadingSceneForSessionStart())
            {
                return;
            }

            BeginPendingWorldLoadOrSession();
        }

        public void LoadGame(string slotId)
        {
            SetGameState(GameState.LoadingWorld);
            LoadingScreenOverlay.Show("Preparing saved session...");
            LoadingScreenOverlay.SetProgress(0f, "Preparing saved session...");

            pendingStartNewGame = false;
            pendingLoadGame = true;
            pendingLoadSlotId = slotId;

            DisableUnityUiEventSystemsForSceneTransition();

            if (TryLoadLoadingSceneForSessionStart())
            {
                return;
            }

            BeginPendingWorldLoadOrSession();
        }

        public void QuitToMainMenu()
        {
            ResolveRuntimeReferences();
            worldManager?.SetSimulationActive(false);
            ResetTransientGameplayState();
            LoadingScreenOverlay.Hide();
            SetGameState(GameState.MainMenu);

            Scene worldScene = FindLoadedWorldScene();

            if (worldScene.IsValid() && worldScene.isLoaded)
            {
                SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                baseManager?.Shutdown();
                lootManager?.Shutdown();
                zombieManager?.Shutdown();
                aiManager?.Shutdown();
                combatManager?.Shutdown();
                saveManager?.Shutdown();
                saveSystem?.Shutdown();
                timeSystem?.Shutdown();
                eventSystem?.Shutdown();
                LoadingScreenOverlay.Hide();
                Instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = mode;

            EnsureSingleAudioListener();
            EnsureUnityUiEventSystemPresent();
            ClearCrossSceneUmaReferences();
            ResolveRuntimeReferences();
            unitManager?.RefreshRegistry();
            squadManager?.RefreshSquadRoster();
            SyncGameplayUiVisibility(CurrentState);
            PruneGameplayUiOwnershipForScene(scene);

            if (HasPendingSessionRequest() && IsLoadingScene(scene))
            {
                BeginPendingWorldLoadOrSession();
                return;
            }

            TryFinalizePendingWorldSession(scene);
        }

        private void BeginWorldSession(bool applyCharacterSelection)
        {
            if (worldSessionStarting)
            {
                return;
            }

            StartCoroutine(BeginWorldSessionRoutine(applyCharacterSelection));
        }

        private IEnumerator BeginWorldSessionRoutine(bool applyCharacterSelection)
        {
            worldSessionStarting = true;
            LoadingScreenOverlay.Show("Initializing world systems...");
            LoadingScreenOverlay.SetProgress(0.97f, "Initializing world systems...");

            EnsureWorldRuntimeComponentsPresent();
            ResolveRuntimeReferences();
            unitManager?.RefreshRegistry();
            squadManager?.RefreshSquadRoster();

            LoadingScreenOverlay.SetProgress(0.965f, "Registering player...");
            yield return EnsureProvisionalPlayerRegisteredForWorldInit();

            if (applyCharacterSelection)
            {
                ApplyCharacterSelectionToActivePlayer();
            }

            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();
            LoadingScreenOverlay.SetProgress(0.99f, "Building world simulation...");
            worldManager?.InitializeWorld();

            // Keep loading overlay for at least one world frame while systems settle.
            yield return null;

            LoadingScreenOverlay.SetProgress(0.992f, "Building navigation...");
            yield return WaitForFinalWorldPlayerSpawnIfPresent();

            SetGameState(GameState.Playing);
            RunReadinessValidation(StartupReadinessValidator.ValidationMode.WorldSession);

            yield return null;
            LoadingScreenOverlay.SetProgress(1f, "Ready");
            yield return LoadingScreenOverlay.WaitForVisualProgress(1f, 2.5f);
            LoadingScreenOverlay.Hide();
            worldSessionStarting = false;
        }

        private static IEnumerator EnsureProvisionalPlayerRegisteredForWorldInit()
        {
            PlayerSpawner spawner = FindFirstObjectByType<PlayerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (spawner == null)
            {
                yield break;
            }

            spawner.EnsureProvisionalPlayerForWorldSession();

            float timeout = 8f;
            float start = Time.unscaledTime;

            while (Time.unscaledTime - start < timeout)
            {
                if (spawner.SpawnedPlayer != null)
                {
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning(
                "[GameManager] Timed out waiting for PlayerSpawner to register a provisional player before InitializeWorld. " +
                "World streaming may briefly use a fallback origin until the player exists.",
                spawner);
        }

        private static IEnumerator WaitForFinalWorldPlayerSpawnIfPresent()
        {
            PlayerSpawner spawner = FindFirstObjectByType<PlayerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (spawner == null)
            {
                yield break;
            }

            float timeout = 120f;
            float start = Time.unscaledTime;

            while (Time.unscaledTime - start < timeout)
            {
                if (spawner.HasFinalizedWorldPlayerSpawn)
                {
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning(
                "[GameManager] Timed out waiting for PlayerSpawner to finalize world spawn (NavMesh snap / agent / UI wiring). " +
                "Continuing session startup; gameplay may be partially uninitialized.",
                spawner);
        }

        private void ApplyCharacterSelectionToActivePlayer()
        {
            if (UnitManager.Instance == null || !CharacterSelectionState.HasSelection)
            {
                return;
            }

            var players = UnitManager.Instance.GetUnitsByRole(UnitRole.Player);

            if (players.Count <= 0 || players[0] == null)
            {
                return;
            }

            Unit player = players[0];

            if (!string.IsNullOrWhiteSpace(CharacterSelectionState.SelectedCharacterName))
            {
                player.name = CharacterSelectionState.SelectedCharacterName;
            }

            ApplySelectedCharacterBuild(player);
        }

        private static void ApplySelectedCharacterBuild(Unit player)
        {
            if (player == null)
            {
                return;
            }

            if (player.Controller != null)
            {
                player.Controller.SetMoveSpeed(CharacterSelectionState.SelectedMoveSpeed);
            }

            if (player.Inventory != null)
            {
                player.Inventory.SetWeightLimit(CharacterSelectionState.SelectedCarryCapacity);
            }

            if (player.Stats != null)
            {
                player.Stats.ResetAllSkillsToLevelOne();
                player.Stats.SetStamina(CharacterSelectionState.SelectedStamina);
                player.Stats.SetStrengthBaseHealth(CharacterSelectionState.SelectedMaxHealth, refillCurrentHealth: true);
            }
            else if (player.Health != null)
            {
                player.Health.SetMaxHealth(CharacterSelectionState.SelectedMaxHealth, true);
            }
        }

        private void ApplyLoadGame(string slotId)
        {
            ResolveRuntimeReferences();

            if (saveManager != null)
            {
                saveManager.LoadGame(slotId);
            }
            else
            {
                saveSystem?.LoadGame(slotId);
            }
        }

        private void LogMissingSystemWarnings()
        {
            if (eventSystem == null) Debug.LogWarning("[GameManager] EventSystem reference is null.", this);
            if (timeSystem == null) Debug.LogWarning("[GameManager] TimeSystem reference is null.", this);
            if (saveSystem == null) Debug.LogWarning("[GameManager] SaveSystem reference is null.", this);
            if (unitManager == null) Debug.LogWarning("[GameManager] UnitManager reference is null.", this);
            if (combatManager == null) Debug.LogWarning("[GameManager] CombatManager reference is null.", this);
            if (aiManager == null) Debug.LogWarning("[GameManager] AIManager reference is null.", this);
            if (squadManager == null) Debug.LogWarning("[GameManager] SquadManager reference is null.", this);
            if (zombieManager == null) Debug.LogWarning("[GameManager] ZombieManager reference is null.", this);
            if (lootManager == null) Debug.LogWarning("[GameManager] LootManager reference is null.", this);
            if (baseManager == null) Debug.LogWarning("[GameManager] BaseManager reference is null.", this);
            if (saveManager == null) Debug.LogWarning("[GameManager] SaveManager reference is null.", this);
            if (worldManager == null && ShouldWarnAboutMissingWorldManager())
            {
                Debug.LogWarning("[GameManager] WorldManager reference is null.", this);
            }
        }

        private bool ShouldWarnAboutMissingWorldManager()
        {
            if (CurrentState == GameState.Playing || worldLoadInProgress || worldSessionStarting)
            {
                return true;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (IsWorldScene(activeScene))
            {
                return true;
            }

            Scene worldScene = FindLoadedWorldScene();
            return worldScene.IsValid() && worldScene.isLoaded;
        }

        private void ResetTransientGameplayState()
        {
            // Reset pending session flags so stale state doesn't bleed into the next load.
            pendingStartNewGame = false;
            pendingLoadGame = false;
            pendingLoadSlotId = null;
            worldLoadInProgress = false;
            worldSessionStarting = false;

            // Reset AI and combat managers to a clean pre-session state.
            aiManager?.Shutdown();
            combatManager?.Shutdown();
            zombieManager?.Shutdown();
            unitManager?.RefreshRegistry();
            squadManager?.RefreshSquadRoster();

            // Re-initialize the managers so they are ready for the next session.
            combatManager?.Initialize();
            aiManager?.Initialize();
            zombieManager?.Initialize();
        }

        private bool TryLoadLoadingSceneForSessionStart()
        {
            if (!useIntermediateLoadingScene || string.IsNullOrWhiteSpace(loadingSceneName))
            {
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(loadingSceneName))
            {
                Debug.LogWarning($"[GameManager] Loading scene '{loadingSceneName}' is not in Build Settings. Falling back to direct world load.", this);
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.IsValid() && string.Equals(activeScene.name, loadingSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            DisableUnityUiEventSystemsForSceneTransition();
            SceneManager.LoadScene(loadingSceneName, LoadSceneMode.Single);
            return true;
        }

        private void BeginPendingWorldLoadOrSession()
        {
            if (!HasPendingSessionRequest())
            {
                return;
            }

            LoadingScreenOverlay.SetProgress(0.08f, "Preparing world load...");

            if (TryLoadWorldSceneForSessionStart())
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            TryFinalizePendingWorldSession(activeScene);
        }

        private bool TryLoadWorldSceneForSessionStart()
        {
            if (!loadWorldSceneOnSessionStart)
            {
                return false;
            }

            if (!TryResolveLoadableWorldScene(out string targetWorldSceneName, out bool usedFallback))
            {
                Debug.LogError($"[GameManager] No loadable world scene found. Checked {GetWorldSceneCandidatesSummary()}.", this);
                return false;
            }

            if (usedFallback)
            {
                Debug.LogWarning($"[GameManager] Configured world scene '{worldSceneName}' was not loadable. Using '{targetWorldSceneName}' instead.", this);
            }

            if (worldLoadInProgress)
            {
                return true;
            }

            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.IsValid() && string.Equals(activeScene.name, targetWorldSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            worldSceneName = targetWorldSceneName;
            StartCoroutine(LoadWorldSceneAsync(targetWorldSceneName));
            return true;
        }

        private IEnumerator LoadWorldSceneAsync(string targetWorldSceneName)
        {
            worldLoadInProgress = true;
            LoadingScreenOverlay.Show("Loading world scene...");
            float displayedProgress = 0.08f;
            LoadingScreenOverlay.SetProgress(displayedProgress, "Loading world scene...");

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetWorldSceneName, LoadSceneMode.Single);

            if (loadOperation == null)
            {
                worldLoadInProgress = false;
                yield break;
            }

            // Keep the loading scene visible until world data is ready to activate.
            loadOperation.allowSceneActivation = false;

            while (loadOperation.progress < 0.9f)
            {
                float normalized = Mathf.Clamp01(loadOperation.progress / 0.9f);
                float targetProgress = Mathf.Lerp(0.10f, 0.84f, normalized);
                displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, Time.unscaledDeltaTime * 0.70f);
                LoadingScreenOverlay.SetProgress(displayedProgress, "Loading world scene...");
                yield return null;
            }

            float minimumDuration = Mathf.Max(0f, minimumLoadingScreenSeconds);
            float elapsed = 0f;
            float finalizeStartProgress = displayedProgress;

            while (elapsed < minimumDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = minimumDuration > 0f ? Mathf.Clamp01(elapsed / minimumDuration) : 1f;
                displayedProgress = Mathf.Lerp(finalizeStartProgress, 0.92f, t);
                LoadingScreenOverlay.SetProgress(displayedProgress, "Preparing world data...");
                yield return null;
            }

            yield return null;
            displayedProgress = Mathf.Max(displayedProgress, 0.93f);
            LoadingScreenOverlay.SetProgress(displayedProgress, "Activating world scene...");

            // Avoid one-frame duplicate UGUI EventSystem overlap during async scene activation.
            DisableUnityUiEventSystemsForSceneTransition();
            loadOperation.allowSceneActivation = true;

            float activationElapsed = 0f;

            while (!loadOperation.isDone)
            {
                activationElapsed += Time.unscaledDeltaTime;
                float activationTarget = Mathf.Lerp(0.93f, 0.97f, Mathf.Clamp01(activationElapsed / 0.75f));
                displayedProgress = Mathf.Max(displayedProgress, activationTarget);
                LoadingScreenOverlay.SetProgress(displayedProgress, "Activating world scene...");
                yield return null;
            }

            LoadingScreenOverlay.SetProgress(Mathf.Max(displayedProgress, 0.97f), "World scene loaded.");
            worldLoadInProgress = false;
        }

        private void TryLoadMainMenuSceneOnInitialize()
        {
            if (!loadMainMenuSceneOnInitialize || autoStartSessionForTesting || string.IsNullOrWhiteSpace(mainMenuSceneName))
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.IsValid() && string.Equals(activeScene.name, mainMenuSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (IsWorldScene(activeScene))
            {
                return;
            }

            DisableUnityUiEventSystemsForSceneTransition();

            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void EnsureSingleAudioListener()
        {
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (listeners == null || listeners.Length == 0)
            {
                EnsureAudioListenerOnPreferredCamera();
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            AudioListener preferred = null;

            Camera taggedMainCamera = Camera.main;
            if (taggedMainCamera != null)
            {
                preferred = taggedMainCamera.GetComponent<AudioListener>();
            }

            if (preferred == null)
            {
                for (int i = 0; i < listeners.Length; i++)
                {
                    AudioListener candidate = listeners[i];

                    if (candidate != null && candidate.gameObject.scene == activeScene && candidate.gameObject.activeInHierarchy)
                    {
                        preferred = candidate;
                        break;
                    }
                }
            }

            if (preferred == null)
            {
                for (int i = 0; i < listeners.Length; i++)
                {
                    AudioListener candidate = listeners[i];
                    if (candidate != null && candidate.gameObject.activeInHierarchy)
                    {
                        preferred = candidate;
                        break;
                    }
                }
            }

            if (preferred == null)
            {
                preferred = listeners[0];
            }

            if (preferred == null || !preferred.gameObject.activeInHierarchy)
            {
                EnsureAudioListenerOnPreferredCamera();
                listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (listeners == null || listeners.Length == 0)
                {
                    return;
                }

                preferred = listeners[0];
            }

            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];

                if (listener == null || listener == preferred)
                {
                    continue;
                }

                listener.enabled = false;
            }

            if (preferred != null && preferred.gameObject.activeInHierarchy)
            {
                preferred.enabled = true;
            }
        }

        private static void EnsureAudioListenerOnPreferredCamera()
        {
            Camera camera = Camera.main;

            if (camera == null)
            {
                Scene activeScene = SceneManager.GetActiveScene();
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera candidate = cameras[i];
                    if (candidate != null && candidate.gameObject.scene == activeScene && candidate.gameObject.activeInHierarchy)
                    {
                        camera = candidate;
                        break;
                    }
                }

                if (camera == null)
                {
                    for (int i = 0; i < cameras.Length; i++)
                    {
                        Camera candidate = cameras[i];
                        if (candidate != null && candidate.gameObject.activeInHierarchy)
                        {
                            camera = candidate;
                            break;
                        }
                    }
                }
            }

            if (camera == null)
            {
                EnsureFallbackAudioListener();
                return;
            }

            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener == null)
            {
                listener = camera.gameObject.AddComponent<AudioListener>();
            }

            listener.enabled = true;
        }

        private static void EnsureFallbackAudioListener()
        {
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener existing = listeners[i];
                if (existing != null && existing.gameObject.activeInHierarchy)
                {
                    existing.enabled = true;
                    return;
                }
            }

            GameObject fallback = GameObject.Find(RuntimeAudioListenerObjectName);
            if (fallback == null)
            {
                fallback = new GameObject(RuntimeAudioListenerObjectName);
                DontDestroyOnLoad(fallback);
            }

            if (!fallback.activeSelf)
            {
                fallback.SetActive(true);
            }

            AudioListener listener = fallback.GetComponent<AudioListener>();
            if (listener == null)
            {
                listener = fallback.AddComponent<AudioListener>();
            }

            listener.enabled = true;
        }

        private static void EnsureUnityUiEventSystemPresent()
        {
            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();
        }

        private static bool ShouldAutoCreateUnityUiEventSystem(Scene activeScene)
        {
            GraphicRaycaster[] raycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < raycasters.Length; i++)
            {
                GraphicRaycaster raycaster = raycasters[i];

                if (raycaster == null)
                {
                    continue;
                }

                if (raycaster.gameObject.scene == activeScene)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ConfigureUiInputModule(GameObject eventSystemObject)
        {
#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputSystemModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule == null)
            {
                inputSystemModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            }

            EnsureInputSystemUiActions(inputSystemModule);

            StandaloneInputModule legacyModule = eventSystemObject.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(legacyModule);
                }
                else
                {
                    DestroyImmediate(legacyModule);
                }
            }
#else
            if (eventSystemObject.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static void EnsureInputSystemUiActions(InputSystemUIInputModule module)
        {
            if (module == null)
            {
                return;
            }

            bool hasEssentialActions =
                module.point != null && module.point.action != null &&
                module.leftClick != null && module.leftClick.action != null &&
                module.submit != null && module.submit.action != null;

            if (!hasEssentialActions)
            {
                module.AssignDefaultActions();
            }

            if (module.actionsAsset != null && !module.actionsAsset.enabled)
            {
                module.actionsAsset.Enable();
            }
        }
#endif

        private static void DisableUnityUiEventSystemsForSceneTransition()
        {
            UnityUiEventSystem[] systems = FindObjectsByType<UnityUiEventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < systems.Length; i++)
            {
                UnityUiEventSystem candidate = systems[i];
                if (candidate == null)
                {
                    continue;
                }

                candidate.enabled = false;

                UnityEngine.EventSystems.BaseInputModule[] modules = candidate.GetComponents<UnityEngine.EventSystems.BaseInputModule>();
                for (int m = 0; m < modules.Length; m++)
                {
                    if (modules[m] != null)
                    {
                        modules[m].enabled = false;
                    }
                }
            }
        }

        private bool IsWorldScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return false;
            }

            return SceneNameMatchesWorldCandidate(scene.name);
        }

        private bool SceneNameMatchesWorldCandidate(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(worldSceneName)
                && string.Equals(sceneName, worldSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            for (int i = 0; i < WorldSceneFallbackNames.Length; i++)
            {
                string fallbackName = WorldSceneFallbackNames[i];
                if (string.IsNullOrWhiteSpace(fallbackName))
                {
                    continue;
                }

                if (string.Equals(sceneName, fallbackName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveLoadableWorldScene(out string resolvedWorldSceneName, out bool usedFallback)
        {
            if (!string.IsNullOrWhiteSpace(worldSceneName) && Application.CanStreamedLevelBeLoaded(worldSceneName))
            {
                resolvedWorldSceneName = worldSceneName;
                usedFallback = false;
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
                usedFallback = !string.Equals(worldSceneName, fallbackName, StringComparison.OrdinalIgnoreCase);
                return true;
            }

            resolvedWorldSceneName = worldSceneName;
            usedFallback = false;
            return false;
        }

        private Scene FindLoadedWorldScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                if (IsWorldScene(scene))
                {
                    return scene;
                }
            }

            return default;
        }

        private string GetWorldSceneCandidatesSummary()
        {
            string summary = string.IsNullOrWhiteSpace(worldSceneName)
                ? "(no configured world scene)"
                : $"'{worldSceneName}'";

            for (int i = 0; i < WorldSceneFallbackNames.Length; i++)
            {
                string fallbackName = WorldSceneFallbackNames[i];
                if (string.IsNullOrWhiteSpace(fallbackName))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(worldSceneName)
                    && string.Equals(worldSceneName, fallbackName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                summary += $", '{fallbackName}'";
            }

            return summary;
        }

        private bool IsLoadingScene(Scene scene)
        {
            if (string.IsNullOrWhiteSpace(loadingSceneName))
            {
                return false;
            }

            return string.Equals(scene.name, loadingSceneName, StringComparison.OrdinalIgnoreCase);
        }

        private bool HasPendingSessionRequest()
        {
            return pendingStartNewGame || pendingLoadGame;
        }

        private void EnsureWorldRuntimeComponentsPresent()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            if (!IsWorldScene(activeScene))
            {
                return;
            }

            GameObject runtimeRoot = null;

            _ = FindOrCreateWorldComponent<WorldManager>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<RegionSystem>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<ChunkLoader>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<ChunkGenerator>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<ChunkCache>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<MapSpawner>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<LootSpawner>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<WorldEventSystem>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<WorldSimulationManager>(ref runtimeRoot);
            _ = FindOrCreateWorldComponent<ZombieManager>(ref runtimeRoot);

            EnsureSceneLevelPlayerSpawner(activeScene, ref runtimeRoot);
        }

        private static T FindOrCreateWorldComponent<T>(ref GameObject runtimeRoot) where T : Component
        {
            T existing = FindFirstObjectByType<T>();

            if (existing != null)
            {
                return existing;
            }

            if (runtimeRoot == null)
            {
                runtimeRoot = GameObject.Find("RuntimeWorldSystems");

                if (runtimeRoot == null)
                {
                    runtimeRoot = new GameObject("RuntimeWorldSystems");
                }
            }

            return runtimeRoot.AddComponent<T>();
        }

        private static void EnsureSceneLevelPlayerSpawner(Scene activeScene, ref GameObject runtimeRoot)
        {
            PlayerSpawner[] spawners = FindObjectsByType<PlayerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < spawners.Length; i++)
            {
                PlayerSpawner spawner = spawners[i];
                if (spawner == null || spawner.gameObject.scene != activeScene)
                {
                    continue;
                }

                if (!IsSpawnerAttachedToUnit(spawner))
                {
                    return;
                }
            }

            if (runtimeRoot == null)
            {
                runtimeRoot = GameObject.Find("RuntimeWorldSystems");

                if (runtimeRoot == null)
                {
                    runtimeRoot = new GameObject("RuntimeWorldSystems");
                }
            }

            runtimeRoot.AddComponent<PlayerSpawner>();
        }

        private static bool IsSpawnerAttachedToUnit(PlayerSpawner spawner)
        {
            if (spawner == null)
            {
                return false;
            }

            return spawner.GetComponent<Unit>() != null
                || spawner.GetComponentInParent<Unit>() != null
                || spawner.GetComponentInChildren<Unit>(true) != null;
        }

        private void TryFinalizePendingWorldSession(Scene loadedScene)
        {
            if (!HasPendingSessionRequest() || !IsWorldScene(loadedScene))
            {
                return;
            }

            if (pendingLoadGame)
            {
                string slotId = pendingLoadSlotId;
                pendingLoadGame = false;
                pendingLoadSlotId = null;
                pendingStartNewGame = false;
                ApplyLoadGame(slotId);
                BeginWorldSession(applyCharacterSelection: false);
                return;
            }

            if (pendingStartNewGame)
            {
                pendingStartNewGame = false;
                pendingLoadSlotId = null;
                BeginWorldSession(applyCharacterSelection: true);
            }
        }

        private void RunReadinessValidation(StartupReadinessValidator.ValidationMode mode)
        {
            if (!runReadinessValidationOnInitialize)
            {
                return;
            }

            if (startupReadinessValidator == null)
            {
                startupReadinessValidator = FindFirstObjectByType<StartupReadinessValidator>();
            }

            startupReadinessValidator?.RunValidation(mode);
        }

        private void ResolveRuntimeReferences()
        {
            eventSystem = ResolveReference(eventSystem);
            timeSystem = ResolveReference(timeSystem);
            saveSystem = ResolveReference(saveSystem);
            unitManager = ResolveReference(unitManager);
            combatManager = ResolveReference(combatManager);
            aiManager = ResolveReference(aiManager);
            squadManager = ResolveReference(squadManager);
            zombieManager = ResolveReference(zombieManager);
            lootManager = ResolveReference(lootManager);
            baseManager = ResolveReference(baseManager);
            saveManager = ResolveReference(saveManager);
            worldManager = ResolveReference(worldManager);
        }

        private static T ResolveReference<T>(T currentReference) where T : Component
        {
            if (currentReference != null)
            {
                return currentReference;
            }

            return FindFirstObjectByType<T>();
        }

        private static void ClearCrossSceneUmaReferences()
        {
            DynamicCharacterAvatar[] avatars = FindObjectsByType<DynamicCharacterAvatar>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < avatars.Length; i++)
            {
                DynamicCharacterAvatar avatar = avatars[i];
                if (avatar == null)
                {
                    continue;
                }

                Scene ownerScene = avatar.gameObject.scene;
                if (!ownerScene.IsValid())
                {
                    continue;
                }

                if (avatar.context != null && avatar.context.gameObject.scene != ownerScene)
                {
                    avatar.context = null;
                }

                if (avatar.umaGenerator != null && avatar.umaGenerator.gameObject.scene != ownerScene)
                {
                    avatar.umaGenerator = null;
                }

                if (avatar.umaData != null &&
                    avatar.umaData.umaGenerator != null &&
                    avatar.umaData.umaGenerator.gameObject.scene != ownerScene)
                {
                    avatar.umaData.umaGenerator = null;
                }
            }
        }

        private void PruneGameplayUiOwnershipForScene(Scene scene)
        {
            if (!scene.IsValid() || IsWorldScene(scene))
            {
                return;
            }

            WorldHUD[] worldHuds = FindObjectsByType<WorldHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < worldHuds.Length; i++)
            {
                WorldHUD worldHud = worldHuds[i];
                if (worldHud == null || worldHud.gameObject.scene != scene)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(worldHud.gameObject);
                }
                else
                {
                    DestroyImmediate(worldHud.gameObject);
                }
            }

            ZomberaSquadManagementUI[] squadUis = FindObjectsByType<ZomberaSquadManagementUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < squadUis.Length; i++)
            {
                ZomberaSquadManagementUI squadUi = squadUis[i];
                if (squadUi == null || squadUi.gameObject.scene != scene)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(squadUi.gameObject);
                }
                else
                {
                    DestroyImmediate(squadUi.gameObject);
                }
            }
        }

        private static void SyncGameplayUiVisibility(GameState state)
        {
            bool shouldShowGameplayUi = state == GameState.Playing || state == GameState.Paused;

            WorldHUD[] worldHuds = FindObjectsByType<WorldHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < worldHuds.Length; i++)
            {
                WorldHUD worldHud = worldHuds[i];

                if (worldHud == null)
                {
                    continue;
                }

                if (worldHud.gameObject.activeSelf == shouldShowGameplayUi)
                {
                    continue;
                }

                worldHud.gameObject.SetActive(shouldShowGameplayUi);
            }

            HUDManager[] hudManagers = FindObjectsByType<HUDManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < hudManagers.Length; i++)
            {
                HUDManager hudManager = hudManagers[i];
                hudManager?.SetVisible(shouldShowGameplayUi);
            }

            if (shouldShowGameplayUi)
            {
                return;
            }

            ZomberaSquadManagementUI[] squadUis = FindObjectsByType<ZomberaSquadManagementUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < squadUis.Length; i++)
            {
                ZomberaSquadManagementUI squadUi = squadUis[i];
                squadUi?.SetVisible(false);
            }
        }
    }

    /// <summary>
    /// Shared interface for top-level game systems.
    /// </summary>
    public interface IGameSystem
    {
        bool IsInitialized { get; }
        void Initialize();
        void Shutdown();
    }

    /// <summary>
    /// High-level game lifecycle states.
    /// </summary>
    public enum GameState
    {
        Booting,
        MainMenu,
        LoadingWorld,
        Playing,
        Paused
    }
}