using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Debugging;
using Zombera.Debugging.DebugLogging;
using Zombera.UI;
using Zombera.UI.Menus;

namespace Zombera.Core
{
    public sealed partial class GameManager
    {
        private void OnValidate()
        {
            PerfTrace.Enabled = enablePerfTraceLogs;
            DebugLogger.Configure(true, enablePerfTraceLogs);
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
                Debug.LogWarning("[GameManager] WorldManager reference is null.", this);
        }

        private bool ShouldWarnAboutMissingWorldManager()
        {
            if (CurrentState == GameState.Playing || _worldLoadInProgress || _worldSessionStarting) return true;

            var activeScene = SceneManager.GetActiveScene();
            if (IsWorldScene(activeScene)) return true;

            var worldScene = FindLoadedWorldScene();
            return worldScene.IsValid() && worldScene.isLoaded;
        }

        private void ApplyStartupFramePacing(string context)
        {
            if (!enforceStartupFramePacing) return;

            var targetFps = Mathf.Clamp(startupTargetFrameRate, 30, 240);

            if (disableVSyncForStartupFramePacing && QualitySettings.vSyncCount != 0)
                QualitySettings.vSyncCount = 0;

            if (Application.targetFrameRate != targetFps)
                Application.targetFrameRate = targetFps;

            if (!logStartupFramePacing) return;

            Debug.Log(
                "[GameManager] Applied startup frame pacing (" + context + "): targetFrameRate=" +
                Application.targetFrameRate + ", vSync=" + QualitySettings.vSyncCount + ".",
                this);
        }

        private void ResetTransientGameplayState()
        {
            // Reset pending session flags so stale state doesn't bleed into the next load.
            _pendingStartNewGame = false;
            _pendingLoadGame = false;
            _pendingLoadSlotId = null;
            _worldLoadInProgress = false;
            _worldSessionStarting = false;

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

        private bool IsMainMenuScene(Scene scene)
        {
            return scene.IsValid()
                   && scene.isLoaded
                   && !string.IsNullOrWhiteSpace(mainMenuSceneName)
                   && string.Equals(scene.name, mainMenuSceneName, System.StringComparison.OrdinalIgnoreCase);
        }

        private static Scene FindLoadedSceneByName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return default;

            return LoadedScenes()
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.name, sceneName, System.StringComparison.OrdinalIgnoreCase));
        }

        private bool IsWorldScene(Scene scene)
        {
            return scene.IsValid() && SceneNameMatchesWorldCandidate(scene.name);
        }

        private Scene FindLoadedWorldScene()
        {
            return LoadedScenes().FirstOrDefault(IsWorldScene);
        }

        private string GetWorldSceneCandidatesSummary()
        {
            var summary = string.IsNullOrWhiteSpace(worldSceneName)
                ? "(no configured world scene)"
                : $"'{worldSceneName}'";

            var fallbackSummary = WorldSceneFallbackNames
                .Where(fallbackName => !string.IsNullOrWhiteSpace(fallbackName))
                .Where(fallbackName => string.IsNullOrWhiteSpace(worldSceneName)
                                       || !string.Equals(worldSceneName, fallbackName,
                                           System.StringComparison.OrdinalIgnoreCase))
                .Select(fallbackName => $"'{fallbackName}'")
                .ToArray();

            if (fallbackSummary.Length <= 0) return summary;

            return $"{summary}, {string.Join(", ", fallbackSummary)}";
        }

        private bool IsLoadingScene(Scene scene)
        {
            return !string.IsNullOrWhiteSpace(loadingSceneName)
                   && string.Equals(scene.name, loadingSceneName, System.StringComparison.OrdinalIgnoreCase);
        }

        private bool HasPendingSessionRequest()
        {
            return _pendingStartNewGame || _pendingLoadGame;
        }

        private void ClearPendingSessionRequest()
        {
            _pendingStartNewGame = false;
            _pendingLoadGame = false;
            _pendingLoadSlotId = null;
        }

        private void Awake()
        {
            Application.runInBackground = true;

            var persistentRoot = transform.root.gameObject;

            if (_instance != null && _instance != this)
            {
                Debug.LogWarning(
                    $"[GameManager] Duplicate instance detected on '{name}'. Keeping '{_instance.name}' and destroying duplicate component.",
                    this);
                enabled = false;
                // Never destroy the whole root from duplicate singleton checks.
                // Scene roots may also host unrelated runtime systems.
                Destroy(this);
                return;
            }

            Instance = this;
            GameManagerGateway.Instance = this;
            DontDestroyOnLoad(persistentRoot);
            UmaGlobalLibraryService.Initialize();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            EnsureSingleAudioListener();
            ApplyStartupFramePacing("Awake");
            PruneGameplayUiOwnershipForScene(SceneManager.GetActiveScene());
        }

        private void Start()
        {
            if (!initializeOnStart) return;

            InitializeSystems();

            if (autoStartSessionForTesting) StartNewGame();
        }

        private void OnDestroy()
        {
            if (_instance != this) return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            ShutdownDiscoveredGameSystems();
            LoadingScreenOverlay.Hide();
            if (ReferenceEquals(GameManagerGateway.Instance, this)) GameManagerGateway.Instance = null;
            Instance = null;
        }

        public void InitializeSystems()
        {
            if (IsInitialized) return;

            PerfTrace.Enabled = enablePerfTraceLogs;
            DebugLogger.Configure(true, enablePerfTraceLogs);

            ResolveRuntimeReferences();
            UmaGlobalLibraryService.Initialize();

            InitializeDiscoveredGameSystems();
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
            if (CurrentState == newState) return;

            var previous = CurrentState;
            OnExitState(previous);
            CurrentState = newState;
            WorldSessionGate.NotifyStateChanged(CurrentState);
            SyncGameplayUiVisibility(CurrentState);

            if (CurrentState == GameState.Playing || CurrentState == GameState.Paused)
                EnsureUnityUiEventSystemPresent();

            CoreEventBus.PublishGlobal(new GameStateChangedEvent
            {
                PreviousState = previous,
                NewState = CurrentState
            });

            OnEnterState(CurrentState);

            if (CurrentState == GameState.Playing
                || (previous == GameState.Playing && CurrentState != GameState.Paused))
                ClearCrossSceneAppearanceReferences();
        }

        private void OnEnterState(GameState state)
        {
            switch (state)
            {
                case GameState.Playing:
                    timeSystem?.ResumeGame();
                    worldManager?.SetSimulationActive(true);
                    ApplyWorldRuntimeStateGuards(state, "OnEnterState.Playing");
                    break;
                case GameState.Paused:
                    timeSystem?.PauseGame();
                    ApplyWorldRuntimeStateGuards(state, "OnEnterState.Paused");
                    break;
                case GameState.LoadingWorld:
                    // Keep simulation time running while world bootstrap coroutines execute.
                    timeSystem?.ResumeGame();
                    ApplyWorldRuntimeStateGuards(state, "OnEnterState.LoadingWorld");
                    break;
                case GameState.MainMenu:
                    timeSystem?.PauseGame();
                    worldManager?.SetSimulationActive(false);
                    zombieManager?.Shutdown();
                    ApplyWorldRuntimeStateGuards(state, "OnEnterState.MainMenu");
                    var mainMenuScene = FindLoadedSceneByName(mainMenuSceneName);
                    if (mainMenuScene.IsValid()) UnloadLoadedWorldScenesLeavingMainMenu(mainMenuScene);
                    break;
                case GameState.Booting:
                    timeSystem?.PauseGame();
                    worldManager?.SetSimulationActive(false);
                    zombieManager?.Shutdown();
                    ApplyWorldRuntimeStateGuards(state, "OnEnterState.Booting");
                    break;
                default:
                    break;
            }
        }

        private static void OnExitState(GameState state)
        {
            _ = state;
        }

        public void StartNewGame()
        {
            // Compatibility wrapper: Medium tier with a one-time non-zero seed.
            var seed = UnityEngine.Random.Range(1, int.MaxValue);
            StartNewGame(new WorldSessionRequest(WorldMapSizeTier.Medium, seed));
        }

        public void StartNewGame(WorldSessionRequest request)
        {
            _pendingWorldSessionRequest = request;
            _hasPendingWorldSessionRequest = true;
            if (_pendingWorldSessionRequest.Seed == 0)
            {
                _pendingWorldSessionRequest = new WorldSessionRequest(
                    _pendingWorldSessionRequest.Tier,
                    UnityEngine.Random.Range(1, int.MaxValue));
            }

            SetGameState(GameState.LoadingWorld);
            LoadingScreenOverlay.Show("Preparing new session...");
            LoadingScreenOverlay.SetProgress(0f, "Preparing new session...");

            _pendingStartNewGame = true;
            _pendingLoadGame = false;
            _pendingLoadSlotId = null;

            // Initialize a default slot for the new game so autosave can function.
            if (saveManager != null)
            {
                string newSlotId = $"NewGame_{DateTime.Now:yyyyMMdd_HHmm}";
                saveManager.SetActiveSlot(newSlotId);
            }

            ClearEasyBuildPersistenceForFreshSession();

            DisableUnityUiEventSystemsForSceneTransition();

            if (TryLoadLoadingSceneForSessionStart()) return;

            BeginPendingWorldLoadOrSession();
        }

        public void LoadGame(string slotId)
        {
            SetGameState(GameState.LoadingWorld);
            LoadingScreenOverlay.Show("Preparing saved session...");
            LoadingScreenOverlay.SetProgress(0f, "Preparing saved session...");

            _pendingStartNewGame = false;
            _pendingLoadGame = true;
            _pendingLoadSlotId = slotId;

            DisableUnityUiEventSystemsForSceneTransition();

            if (TryLoadLoadingSceneForSessionStart()) return;

            BeginPendingWorldLoadOrSession();
        }

        public void QuitToMainMenu()
        {
            ResolveRuntimeReferences();
            worldManager?.SetSimulationActive(false);
            ResetTransientGameplayState();
            LoadingScreenOverlay.Hide();
            SetGameState(GameState.MainMenu);

            var worldScene = FindLoadedWorldScene();

            if (worldScene.IsValid() && worldScene.isLoaded)
                SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = mode;

            EnsureDisplayCameraPresentForLoadingScene(scene);

            if (reapplyStartupFramePacingOnSceneLoad) ApplyStartupFramePacing("SceneLoaded:" + scene.name);

            UmaGlobalLibraryService.RefreshCache(force: true);
            EnsureSingleAudioListener();
            EnsureUnityUiEventSystemPresent();
            ClearCrossSceneAppearanceReferences();
            ResolveRuntimeReferences();
            unitManager?.RefreshRegistry();
            squadManager?.RefreshSquadRoster();
            SyncGameplayUiVisibility(CurrentState);
            PruneGameplayUiOwnershipForScene(scene);
            ApplyWorldRuntimeStateGuards(CurrentState, $"OnSceneLoaded:{scene.name}");

            if (HasPendingSessionRequest() && IsLoadingScene(scene))
            {
                BeginPendingWorldLoadOrSession();
                return;
            }

            TryFinalizePendingWorldSession(scene);

            if (!HasPendingSessionRequest()
                && (CurrentState == GameState.Booting || CurrentState == GameState.MainMenu))
            {
                var mainMenuScene = FindLoadedSceneByName(mainMenuSceneName);
                if (mainMenuScene.IsValid()) UnloadLoadedWorldScenesLeavingMainMenu(mainMenuScene);
            }

            if (loadMainMenuWhenInitialSceneIsWorld
                && !autoStartSessionForTesting
                && scene.IsValid()
                && scene.isLoaded
                && IsMainMenuScene(scene))
                UnloadLoadedWorldScenesLeavingMainMenu(scene);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            _ = scene;
            UmaGlobalLibraryService.RefreshCache(force: true);
            ClearCrossSceneAppearanceReferences();
        }
    }
}