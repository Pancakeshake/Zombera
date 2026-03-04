using UnityEngine;
using Zombera.Systems;

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

        [Header("Top-Level Managers")]
        [SerializeField] private UnitManager unitManager;
        [SerializeField] private SquadManager squadManager;
        [SerializeField] private ZombieManager zombieManager;
        [SerializeField] private LootManager lootManager;
        [SerializeField] private BaseManager baseManager;
        [SerializeField] private SaveManager saveManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void InitializeSystems()
        {
            if (IsInitialized)
            {
                return;
            }

            eventSystem?.Initialize();
            timeSystem?.Initialize();
            saveSystem?.Initialize();
            saveManager?.Initialize();
            zombieManager?.Initialize();
            lootManager?.Initialize();
            baseManager?.Initialize();
            unitManager?.RefreshRegistry();

            _ = squadManager;

            IsInitialized = true;
            SetGameState(GameState.MainMenu);

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

            // TODO: Reset runtime data and initialize world seed/session.
            // TODO: Transition to World scene once systems are ready.
        }

        public void LoadGame(string slotId)
        {
            SetGameState(GameState.LoadingWorld);
            if (saveManager != null)
            {
                saveManager.LoadGame(slotId);
            }
            else
            {
                saveSystem?.LoadGame(slotId);
            }

            // TODO: Restore simulation objects from save payload.
            // TODO: Validate save compatibility/versioning.
        }

        public void QuitToMainMenu()
        {
            SetGameState(GameState.MainMenu);

            // TODO: Unload active world scene and release streamed chunk data.
            // TODO: Reset transient combat and AI state.
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                baseManager?.Shutdown();
                lootManager?.Shutdown();
                zombieManager?.Shutdown();
                saveManager?.Shutdown();
                saveSystem?.Shutdown();
                timeSystem?.Shutdown();
                eventSystem?.Shutdown();
                Instance = null;
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