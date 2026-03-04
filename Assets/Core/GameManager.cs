using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Systems;
using Zombera.World;

namespace Zombera.Core
{
    /// <summary>
    /// Central bootstrap and game state coordinator.
    /// Controls high-level game flow and initializes core systems.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
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
        [SerializeField] private bool loadWorldSceneOnSessionStart = true;
        [SerializeField] private string worldSceneName = "World";

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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
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
            RunReadinessValidation();

            // TODO: Register non-core systems (world, squad, UI) during boot flow.
            // TODO: Add dependency validation and startup diagnostics.
        }

        public void SetGameState(GameState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            CurrentState = newState;

            // TODO: Dispatch state transition events through EventSystem.
            // TODO: Add per-state enter/exit hooks.
        }

        public void StartNewGame()
        {
            SetGameState(GameState.LoadingWorld);

            pendingLoadGame = false;
            pendingLoadSlotId = null;

            if (TryLoadWorldSceneForSessionStart())
            {
                pendingStartNewGame = true;
                return;
            }

            BeginWorldSession();

            // TODO: Reset runtime data and initialize world seed/session.
            // TODO: Transition to World scene once systems are ready.
        }

        public void LoadGame(string slotId)
        {
            SetGameState(GameState.LoadingWorld);

            pendingStartNewGame = false;

            if (TryLoadWorldSceneForSessionStart())
            {
                pendingLoadGame = true;
                pendingLoadSlotId = slotId;
                return;
            }

            ApplyLoadGame(slotId);
            BeginWorldSession();

            // TODO: Restore simulation objects from save payload.
            // TODO: Validate save compatibility/versioning.
        }

        public void QuitToMainMenu()
        {
            ResolveRuntimeReferences();
            worldManager?.SetSimulationActive(false);
            SetGameState(GameState.MainMenu);

            // TODO: Unload active world scene and release streamed chunk data.
            // TODO: Reset transient combat and AI state.
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
                Instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;

            ResolveRuntimeReferences();
            unitManager?.RefreshRegistry();
            squadManager?.RefreshSquadRoster();

            if (pendingLoadGame && IsWorldScene(scene))
            {
                string slotId = pendingLoadSlotId;
                pendingLoadGame = false;
                pendingLoadSlotId = null;

                ApplyLoadGame(slotId);
                BeginWorldSession();
                return;
            }

            if (pendingStartNewGame && IsWorldScene(scene))
            {
                pendingStartNewGame = false;
                BeginWorldSession();
            }
        }

        private void BeginWorldSession()
        {
            ResolveRuntimeReferences();
            unitManager?.RefreshRegistry();
            squadManager?.RefreshSquadRoster();
            worldManager?.InitializeWorld();
            SetGameState(GameState.Playing);
            RunReadinessValidation();
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

        private bool TryLoadWorldSceneForSessionStart()
        {
            if (!loadWorldSceneOnSessionStart || string.IsNullOrWhiteSpace(worldSceneName))
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.IsValid() && string.Equals(activeScene.name, worldSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            SceneManager.LoadScene(worldSceneName);
            return true;
        }

        private bool IsWorldScene(Scene scene)
        {
            if (string.IsNullOrWhiteSpace(worldSceneName))
            {
                return false;
            }

            return string.Equals(scene.name, worldSceneName, StringComparison.OrdinalIgnoreCase);
        }

        private void RunReadinessValidation()
        {
            if (!runReadinessValidationOnInitialize)
            {
                return;
            }

            if (startupReadinessValidator == null)
            {
                startupReadinessValidator = FindObjectOfType<StartupReadinessValidator>();
            }

            startupReadinessValidator?.RunValidation();
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

            return FindObjectOfType<T>();
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