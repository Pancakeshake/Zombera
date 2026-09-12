#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Debugging;
using Zombera.Systems;
using Zombera.UI;
using Zombera.UI.Menus;
using Zombera.UI.SquadManagement;
using Zombera.World;
using Zombera.World.City;
using Zombera.World.Roads;
using Zombera.World.Simulation;
using UnityUiEventSystem = UnityEngine.EventSystems.EventSystem;

#endregion

// ReSharper disable MergeIntoLogicalPattern
// ReSharper disable InvertIf
// ReSharper disable ConvertIfStatementToReturnStatement

namespace Zombera.Core
{
    /// <summary>
    ///     Central bootstrap and game state coordinator.
    ///     Controls high-level game flow and initializes core systems.
    /// </summary>
    public sealed partial class GameManager : MonoBehaviour, IGameManagerGateway
    {
        private const string RuntimeAudioListenerObjectName = "RuntimeAudioListener";
        private const float ProvisionalPlayerSpawnTimeoutSeconds = 8f;
        private const float FinalWorldPlayerSpawnTimeoutSeconds = 45f;
        private const float CharacterVisualsReadyTimeoutSeconds = 30f;
        private const float SpawnPollIntervalSeconds = 0.05f;
        private const float CharacterVisualsPollIntervalSeconds = 0.1f;
        private const float LoadingProgressPrepareWorldLoad = 0.08f;
        private const float LoadingProgressSceneLoadStart = 0.10f;
        private const float LoadingProgressSceneLoadEnd = 0.36f;
        private const float LoadingProgressSceneDataPrepEnd = 0.43f;
        private const float LoadingProgressSceneActivationStart = 0.44f;
        private const float LoadingProgressSceneActivationEnd = 0.50f;
        private const float LoadingProgressSceneLoaded = 0.52f;
        private const float LoadingProgressWorldSessionBootstrapStart = 0.54f;
        private const float LoadingProgressPrewarmStart = 0.56f;
        private const float LoadingProgressProvisionalPlayerStart = 0.59f;
        private const float LoadingProgressWorldSimulationStart = 0.61f;

        private static readonly WaitForSecondsRealtime SpawnPollWait = new(SpawnPollIntervalSeconds);
        private static readonly WaitForSecondsRealtime CharacterVisualsPollWait =
            new(CharacterVisualsPollIntervalSeconds);

        private enum WorldLoadingStage
        {
            TerrainGeneration,
            NavMesh,
            Roads,
            Buildings,
            Players,
            Zombies
        }

        private static readonly string[] WorldSceneFallbackNames =
        {
            "World_MapMagicStream",
            "World_Map_MagicStream"
        };

        [Header("Debug / Performance")] [SerializeField]
        private bool enablePerfTraceLogs;

        [SerializeField] private bool enforceMainMenuWorldRuntimeGuards = true;
        [SerializeField] private bool logMainMenuRuntimeSnapshot = false;

        [Header("Core Systems")] [SerializeField]
        private TimeSystem timeSystem;

        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private CoreEventBus eventSystem;

        [Header("Startup")] [SerializeField] private bool initializeOnStart = true;

        [SerializeField] private bool autoStartSessionForTesting;
        [SerializeField] private bool loadMainMenuSceneOnInitialize = true;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Tooltip(
            "If you press Play with the World scene open, load MainMenu anyway (Single) so character creation runs without World in memory. " +
            "Disable to keep the legacy behavior (stay on World when it is the start scene). Auto-start session for testing skips this.")]
        [SerializeField]
        private bool loadMainMenuWhenInitialSceneIsWorld = true;

        [SerializeField] private bool loadWorldSceneOnSessionStart = true;
        [SerializeField] private string worldSceneName = "World";

        [Header("Loading")] [SerializeField] private bool useIntermediateLoadingScene = true;

        [SerializeField] private string loadingSceneName = "Loading";
        [SerializeField] [Min(0f)] private float minimumLoadingScreenSeconds = 1f;
        [SerializeField] [Min(5f)] private float worldSceneLoadTimeoutSeconds = 90f;
        [SerializeField] [Min(2f)] private float worldSceneActivationTimeoutSeconds = 30f;

        [Header("Loading Stage Pipeline")]
        [SerializeField] private bool enforceOrderedLoadingStages = true;

        [SerializeField] [Min(0.05f)] private float loadingStagePollSeconds = 0.1f;
        [SerializeField] [Min(1f)] private float terrainGenerationStageTimeoutSeconds = 45f;
        [SerializeField] [Min(1f)] private float navMeshStageTimeoutSeconds = 45f;
        [SerializeField] [Min(1f)] private float roadsStageTimeoutSeconds = 45f;
        [SerializeField] [Min(1f)] private float zombieStageTimeoutSeconds = 12f;

        [SerializeField] [Min(0.1f)] private float loadingStageBottleneckWarningSeconds = 8f;
        [SerializeField] private bool logLoadingStageDiagnostics = true;
        [SerializeField] private bool holdZombieSpawningUntilZombieStage = true;

        [Header("Frame Pacing")]
        [Tooltip("When enabled, applies startup frame pacing to target a stable framerate on play start.")]
        [SerializeField]
        private bool enforceStartupFramePacing = true;

        [SerializeField] [Range(30, 240)] private int startupTargetFrameRate = 60;

        [Tooltip("Disables v-sync so Application.targetFrameRate can control pacing predictably.")]
        [SerializeField]
        private bool disableVSyncForStartupFramePacing = true;

        [Tooltip("Re-applies startup frame pacing on scene load to counter quality profile overrides.")]
        [SerializeField]
        private bool reapplyStartupFramePacingOnSceneLoad = true;

        [SerializeField] private bool logStartupFramePacing;

        [Header("Loading Screen Prewarm")]
        [Tooltip("Run staged critical-asset warmup while the loading overlay is visible.")]
        [SerializeField]
        private bool enableStagedLoadingScreenPrewarm = true;

        [Tooltip("Time budget in milliseconds for preload work each frame.")]
        [SerializeField]
        [Range(0.5f, 8f)]
        private float loadingScreenPrewarmBudgetMs = 1.5f;

        [Tooltip("Maximum number of preload steps attempted each frame.")]
        [SerializeField]
        [Range(1, 64)]
        private int loadingScreenPrewarmMaxStepsPerFrame = 4;

        [Header("Validation")] [SerializeField]
        private bool runReadinessValidationOnInitialize = true;

        [SerializeField] private StartupReadinessValidator startupReadinessValidator;

        [Header("Top-Level Managers")] [SerializeField]
        private UnitManager unitManager;

        [SerializeField] private CombatManager combatManager;
        [SerializeField] private AIManager aiManager;
        [SerializeField] private SquadManager squadManager;
        [SerializeField] private ZombieManager zombieManager;
        [SerializeField] private LootManager lootManager;
        [SerializeField] private BaseManager baseManager;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private WorldManager worldManager;
        [SerializeField] private ProceduralRoadSystem proceduralRoadSystem;
        private bool _pendingLoadGame;
        private string _pendingLoadSlotId;
        private bool _isRestoringFromSave;

        private bool _pendingStartNewGame;
        private WorldSessionRequest _pendingWorldSessionRequest;
        private bool _hasPendingWorldSessionRequest;
        private bool _worldLoadInProgress;
        private bool _worldSessionStarting;
        private readonly List<Renderer> _rendererScratch = new(32);
        private readonly List<IGameSystem> _orderedGameSystems = new(16);
        private readonly HashSet<IGameSystem> _orderedGameSystemLookup = new();
        private EasyRoadsRoadGameplayBridge _loadingRoadBridge;
        private WorldRoadNetworkSystem _loadingRoadNetworkSystem;
        private WorldStreamedCityBuilder _loadingCityBuilder;
        private static GameManager _instance;
        private static bool _warnedAboutMissingInstance;

        public static bool HasInstance => _instance != null;

        public static GameManager Instance
        {
            get
            {
                if (_instance == null && !_warnedAboutMissingInstance)
                {
                    _warnedAboutMissingInstance = true;
                    Debug.LogWarning("[GameManager] Instance accessed before Awake assigned a runtime instance.");
                }

                return _instance;
            }
            private set
            {
                _instance = value;
                if (_instance != null) _warnedAboutMissingInstance = false;
            }
        }

        public GameState CurrentState { get; private set; } = GameState.Booting;
        public bool IsInitialized { get; private set; }
        public bool IsLoadingSession => _isRestoringFromSave;

        /// <summary>Authoritative save orchestrator for the active play session (child of GameManager).</summary>
        public SaveManager ActiveSaveManager => ResolveReference(saveManager);

        private void BeginWorldSession(bool applyCharacterSelection)
        {
            if (_worldSessionStarting) return;

            StartCoroutine(BeginWorldSessionRoutine(applyCharacterSelection));
        }

        private void ApplyLoadGame(string slotId)
        {
            ResolveRuntimeReferences();
            PrepareLoadSession(slotId);
        }

        public void PrepareLoadSession(string slotId)
        {
            _isRestoringFromSave = true;

            if (saveManager != null)
            {
                saveManager.SetActiveSlot(slotId);
                saveManager.PreloadLoadData(slotId);
            }
            else
            {
                Debug.LogError($"[GameManager] Cannot prepare load session for slot '{slotId}': SaveManager is missing.", this);
                saveSystem?.LoadGame(slotId);
            }
        }
    }
}