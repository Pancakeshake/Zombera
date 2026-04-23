using System.Collections.Generic;
using MapMagic.Core;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Zombera.World.Simulation;

namespace Zombera.World
{
    /// <summary>
    /// Coordinates chunk streaming and world simulation ticks.
    /// </summary>
    public sealed class WorldManager : MonoBehaviour
    {
        [Header("Streaming")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private ChunkLoader chunkLoader;
        [SerializeField] private ChunkGenerator chunkGenerator;
        [SerializeField] private ChunkCache chunkCache;
        [SerializeField] private RegionSystem regionSystem;

        [Header("Spawners")]
        [SerializeField] private MapSpawner mapSpawner;
        [SerializeField] private LootSpawner lootSpawner;

        [Header("Dynamic Events")]
        [SerializeField] private WorldEventSystem worldEventSystem;
        [SerializeField] private WorldSimulationManager worldSimulationManager;
        [SerializeField] private ZombieManager zombieManager;

        [Header("Simulation")]
        [SerializeField] private float chunkStreamingTickInterval = 0.25f;
        [SerializeField] private float worldSimulationInterval = 10f;
        [SerializeField] private bool initializeOnStart = true;

        [Header("Procedural Streaming")]
        [Tooltip("When enabled, skips prototype static map spawn, binds MapMagic tile streaming to chunk loading, and seeds the world from World Seed instead of a random session tick.")]
        [SerializeField] private bool useProceduralStreamingWorld = true;
        [SerializeField, Min(1)] private int worldSeed = 12345;
        [Tooltip("When true (and procedural streaming is on), World Seed is replaced with a time-derived value each play session.")]
        [SerializeField] private bool randomizeWorldSeedEachSession;
        [SerializeField] private MapMagicTileStreamBridge tileStreamBridge;

        public bool IsSimulationActive { get; private set; }
        public bool UseProceduralStreamingWorld => useProceduralStreamingWorld;
        public MapMagicTileStreamBridge TileStreamBridge => tileStreamBridge;

        private float chunkStreamingTickTimer;
        private float worldSimulationTimer;
        private readonly List<Unit> playerUnitBuffer = new List<Unit>();

        private void Awake()
        {
            EnsureProceduralStreamingBridge();
            ResolveRuntimeReferences();
        }

        private void OnDestroy()
        {
            ProceduralWorldSession.Clear();
            StreamedWorldChunkState.Clear();
        }

        private void Start()
        {
            if (initializeOnStart)
            {
                if (IsWorldSessionStateActive())
                {
                    InitializeWorld();
                }
            }
        }

        public void InitializeWorld()
        {
            if (IsSimulationActive)
            {
                return;
            }

            if (!IsWorldSessionStateActive())
            {
                // World scene may be loaded additively behind MainMenu/CharacterCreator.
                // Avoid spawning zombies/loot/chunks until the actual world session starts.
                return;
            }

            ResolveRuntimeReferences();
            EnsureProceduralStreamingBridge();

            TryResolvePlayerTransform();
            EnsureZombieManagerReady();

            IsSimulationActive = true;
            chunkStreamingTickTimer = 0f;
            worldSimulationTimer = worldSimulationInterval;

            StreamedWorldMetrics.ResetSession();
            StreamedWorldChunkState.Clear();
            chunkCache?.Clear();

            int sessionSeed = unchecked((int)System.DateTime.UtcNow.Ticks);
            MapMagicObject mapMagic = FindFirstObjectByType<MapMagicObject>();
            string graphVersion = string.Empty;
            if (mapMagic != null && mapMagic.graph != null)
            {
                graphVersion = mapMagic.graph.IdsVersionsHash();
            }

            if (useProceduralStreamingWorld)
            {
                sessionSeed = randomizeWorldSeedEachSession
                    ? unchecked((int)System.DateTime.UtcNow.Ticks)
                    : worldSeed;

                ProceduralWorldSession.Begin(sessionSeed, graphVersion);
                chunkGenerator?.SetWorldSeed(sessionSeed);
            }
            else
            {
                ProceduralWorldSession.Clear();
            }

            if (!useProceduralStreamingWorld)
            {
                mapSpawner?.SpawnPrototypeMap();
            }

            lootSpawner?.PrimePrototypeLoot();
            worldSimulationManager?.InjectWorldEventSystem(worldEventSystem);
            worldSimulationManager?.InitializeSimulation(playerTransform);

            worldSimulationManager?.SetSimulationSeed(sessionSeed);

            if (useProceduralStreamingWorld && tileStreamBridge != null)
            {
                tileStreamBridge.Bind(mapMagic);
                chunkLoader?.SetMapMagicTileStreamBridge(tileStreamBridge);
            }

            Vector3 streamOrigin = playerTransform != null ? playerTransform.position : Vector3.zero;
            chunkLoader?.UpdateStreaming(streamOrigin, regionSystem, chunkGenerator, tileStreamBridge);
            Random.InitState(sessionSeed);
        }

        private static bool IsWorldSessionStateActive()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                // Prototype scenes without GameManager treat WorldManager as authoritative.
                return true;
            }

            GameState state = gm.CurrentState;
            return state == GameState.LoadingWorld || state == GameState.Playing || state == GameState.Paused;
        }

        public void SetSimulationActive(bool active)
        {
            IsSimulationActive = active;
            worldSimulationManager?.SetSimulationActive(active);

            // Pause/resume each subsystem independently so streaming can continue
            // while the world simulation is frozen (e.g. during UI overlays).
            if (chunkLoader != null)
            {
                chunkLoader.enabled = active;
            }

            if (lootSpawner != null)
            {
                lootSpawner.enabled = active;
            }
        }

        public void ForceRefreshChunks()
        {
            if (!TryResolvePlayerTransform())
            {
                return;
            }

            chunkLoader?.UpdateStreaming(playerTransform.position, regionSystem, chunkGenerator, tileStreamBridge);

            // Clear and reload stale chunks to handle hard transitions (teleport/scene change).
            chunkCache?.Clear();
        }

        /// <summary>
        /// Teleports the given transform to a target position and forces a chunk refresh
        /// so streaming and simulation layers are immediately re-evaluated at the new location.
        /// </summary>
        public void TeleportPlayer(Transform target, Vector3 destination)
        {
            if (target == null)
            {
                return;
            }

            target.position = destination;
            playerTransform = target;
            ForceRefreshChunks();
        }

        private void Update()
        {
            if (!IsSimulationActive)
            {
                return;
            }

            TrySpawnValidationZombieNearPlayer();

            chunkStreamingTickTimer += Time.deltaTime;
            worldSimulationTimer += Time.deltaTime;

            if (chunkStreamingTickTimer >= chunkStreamingTickInterval)
            {
                chunkStreamingTickTimer = 0f;
                RunChunkStreamingTick();
            }

            if (worldSimulationTimer >= worldSimulationInterval)
            {
                worldSimulationTimer = 0f;
                RunWorldSimulationTick();
            }
        }

        private void RunChunkStreamingTick()
        {
            ResolveRuntimeReferences();

            if (!TryResolvePlayerTransform())
            {
                return;
            }

            chunkLoader?.UpdateStreaming(playerTransform.position, regionSystem, chunkGenerator, tileStreamBridge);
            tileStreamBridge?.RefreshTileMetrics();
            worldSimulationManager?.RefreshSimulationLayers(playerTransform.position);

            // Near-player systems: sync loot spawner, ambient audio, and short-range spawners.
            lootSpawner?.TickNearPlayer(playerTransform.position);
        }

        private void RunWorldSimulationTick()
        {
            if (!TryResolvePlayerTransform())
            {
                return;
            }

            EnsureZombieManagerReady();

            if (worldSimulationManager != null)
            {
                // Region/horde work can grow with world size; maxRegionsTickedPerFrame on WorldSimulationManager caps heavy region passes when wired.
                worldSimulationManager.TickSimulation(worldSimulationInterval, playerTransform.position);
            }
            else
            {
                worldEventSystem?.TickDynamicEvents(playerTransform.position);
            }

            EventSystem.PublishGlobal(new WorldSimulationTickEvent
            {
                DeltaTime = worldSimulationInterval,
                PlayerPosition = playerTransform.position
            });

            if (EventSystem.Instance == null)
            {
                zombieManager?.TickAmbientSpawn(playerTransform.position);
            }

            // Tick lightweight ambient systems that need sub-simulation-interval updates.
            worldEventSystem?.TickDynamicEvents(playerTransform.position);
        }

        private bool TryResolvePlayerTransform()
        {
            if (playerTransform != null)
            {
                return true;
            }

            if (UnitManager.Instance == null)
            {
                return false;
            }

            List<Unit> players = UnitManager.Instance.GetUnitsByRole(UnitRole.Player, playerUnitBuffer);

            if (players.Count <= 0 || players[0] == null)
            {
                return false;
            }

            playerTransform = players[0].transform;
            return true;
        }

        private void ResolveRuntimeReferences()
        {
            chunkLoader = ResolveReference(chunkLoader);
            chunkGenerator = ResolveReference(chunkGenerator);
            chunkCache = ResolveReference(chunkCache);
            regionSystem = ResolveReference(regionSystem);
            mapSpawner = ResolveReference(mapSpawner);
            lootSpawner = ResolveReference(lootSpawner);
            worldEventSystem = ResolveReference(worldEventSystem);
            worldSimulationManager = ResolveReference(worldSimulationManager);
            zombieManager = ResolveReference(zombieManager);
            EnsureProceduralStreamingBridge();
        }

        private void EnsureProceduralStreamingBridge()
        {
            if (!useProceduralStreamingWorld)
            {
                return;
            }

            if (tileStreamBridge != null)
            {
                return;
            }

            tileStreamBridge = GetComponent<MapMagicTileStreamBridge>();
            if (tileStreamBridge == null)
            {
                tileStreamBridge = GetComponentInChildren<MapMagicTileStreamBridge>(true);
            }

            if (tileStreamBridge == null)
            {
                tileStreamBridge = gameObject.AddComponent<MapMagicTileStreamBridge>();
            }
        }

        /// <summary>Restores deterministic procedural session data and chunk deltas from a save slot.</summary>
        public void ApplyLoadedProceduralWorld(ProceduralWorldSaveData data)
        {
            if (data == null || !data.hasData)
            {
                return;
            }

            worldSeed = data.worldSeed;
            ProceduralWorldSession.Begin(
                data.worldSeed,
                string.IsNullOrEmpty(data.graphVersion) ? string.Empty : data.graphVersion);
            chunkGenerator?.SetWorldSeed(data.worldSeed);
            worldSimulationManager?.SetSimulationSeed(data.worldSeed);
            Random.InitState(data.worldSeed);
            StreamedWorldChunkState.ImportFromSave(data);
        }

        private void EnsureZombieManagerReady()
        {
            if (zombieManager == null)
            {
                zombieManager = FindFirstObjectByType<ZombieManager>();
            }

            if (zombieManager == null)
            {
                GameObject runtimeRoot = GameObject.Find("RuntimeWorldSystems");

                if (runtimeRoot == null)
                {
                    runtimeRoot = new GameObject("RuntimeWorldSystems");
                }

                zombieManager = runtimeRoot.AddComponent<ZombieManager>();
            }

            if (zombieManager != null && !zombieManager.IsInitialized)
            {
                zombieManager.Initialize();
            }
        }

        private static T ResolveReference<T>(T currentReference) where T : Component
        {
            if (currentReference != null)
            {
                return currentReference;
            }

            return FindFirstObjectByType<T>();
        }

        private void TrySpawnValidationZombieNearPlayer()
        {
            if (!TryResolvePlayerTransform())
            {
                return;
            }

            EnsureZombieManagerReady();
            zombieManager?.TrySpawnValidationZombieNearPlayer(playerTransform.position);
        }
    }
}