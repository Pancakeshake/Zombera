using System;
using System.Collections;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Simulation;
using Random = UnityEngine.Random;

namespace Zombera.World
{
    public partial class WorldManager
    {
        /// <summary>Applies MainMenu New Game tier/seed before InitializeWorld.</summary>
        public void ApplySessionRequest(WorldSessionRequest request)
        {
            if (request.Seed != 0)
                worldSeed = request.Seed;
            _pendingMapSizeTier = request.Tier;
            _hasPendingSessionRequest = true;
            randomizeWorldSeedEachSession = false;
        }

        public void InitializeWorld()
        {
            if (IsSimulationActive) return;

            if (!IsWorldSessionStateActive()) return;

            ResolveRuntimeReferencesIfNeeded(force: true);
            TrySuppressEasyBuildAutomaticPersistence();
            EnsureProceduralStreamingBridge();
            EnsureStreamedCityBuilderProvisioned();
            EnforceWorldSpawnedBuildingMode();

            TryResolvePlayerTransform();
            EnsureZombieManagerReady();

            if (useProceduralStreamingWorld && worldGenerationManager != null)
                worldGenerationManager.StartGeneration();

            IsSimulationActive = true;
            _chunkStreamingTickTimer = 0f;
            _worldSimulationTimer = worldSimulationInterval;
            _nextValidationZombieAttemptAt = 0f;
            _validationZombieAttemptFinished = false;

            var preserveStressSession = ProceduralWorldSession.IsSingleTileStressSession();
            if (!preserveStressSession)
            {
                StreamedWorldMetrics.ResetSession();
                StreamedWorldChunkState.Clear();
                chunkCache?.Clear();
            }

            var sessionSeed = unchecked((int)DateTime.UtcNow.Ticks);
            var backend = WorldGenerationBackend;
            var useFirstPartyBackend = backend is WorldBuilderService;

            var graphVersion = preserveStressSession
                ? ProceduralWorldSession.GraphVersion
                : string.Empty;

            if (useProceduralStreamingWorld)
            {
                if (_hasPendingSessionRequest)
                {
                    sessionSeed = worldSeed;
                    _hasPendingSessionRequest = false;
                }
                else
                {
                    sessionSeed = preserveStressSession
                        ? ProceduralWorldSession.WorldSeed
                        : randomizeWorldSeedEachSession
                            ? unchecked((int)DateTime.UtcNow.Ticks)
                            : worldSeed;
                }

                if (useFirstPartyBackend)
                    BeginFirstPartyProceduralSession(backend, sessionSeed);
                else
                    RefreshProceduralSession(sessionSeed, graphVersion);
            }
            else
            {
                ProceduralWorldSession.Clear();
            }

            if (!useProceduralStreamingWorld) mapSpawner?.SpawnPrototypeMap();

            lootSpawner?.PrimePrototypeLoot();
            worldSimulationManager?.InjectWorldEventSystem(worldEventSystem);
            worldSimulationManager?.InitializeSimulation(playerTransform);

            worldSimulationManager?.SetSimulationSeed(sessionSeed);

            if (useProceduralStreamingWorld && tileStreamBridge != null)
            {
                chunkLoader?.SetMapMagicTileStreamBridge(tileStreamBridge);
                streamedCityBuilder?.Configure(tileStreamBridge, streamedCityCatalog);
                if (streamedCityBuilder != null)
                    streamedCityBuilder.enabled = enableStreamedCityBuilder && streamedCityBuilder.HasAnyValidBuildingEntries;

                if (proceduralRoadSystem != null)
                {
                    proceduralRoadSystem.Configure(tileStreamBridge, roadGameplayService);
                    proceduralRoadSystem.enabled = true;
                }

                if (easyRoadsRoadBridge != null)
                {
                    easyRoadsRoadBridge.Configure(tileStreamBridge);
                    easyRoadsRoadBridge.enabled = enableEasyRoadsRoadBridge;
                }

                if (navMeshTileService != null)
                    navMeshTileService.ConfigureWorldTileStream(tileStreamBridge, backend);

                EnsureWorldBuildingMaterializerConfigured();
            }

            var streamOrigin = playerTransform != null ? playerTransform.position : Vector3.zero;
            chunkLoader?.UpdateStreaming(streamOrigin, regionSystem, chunkGenerator, tileStreamBridge);
            Random.InitState(sessionSeed);
        }

        private void BeginFirstPartyProceduralSession(IWorldGenerationBackend backend, int sessionSeed)
        {
            var session = BuildBackendSession(backend, sessionSeed);
            var planFingerprint = _hasPendingSavedPlanFingerprint ? _pendingSavedPlanFingerprint : 0UL;
            ProceduralWorldSession.Begin(
                sessionSeed,
                ProceduralWorldSession.FirstPartyGraphVersion,
                session.Tier,
                session.TilesPerSide,
                0,
                0,
                session.ProfileVersion,
                planFingerprint);
            chunkGenerator?.SetWorldSeed(sessionSeed);
            worldSimulationManager?.SetSimulationSeed(sessionSeed);

            if (backend.TileStream != null)
                tileStreamBridge = backend.TileStream;

            var scope = BuildInitialPlayScope(session);
            StartCoroutine(backend.InitializeSession(session, scope));
            _hasPendingSavedPlanFingerprint = false;
            _pendingSavedPlanFingerprint = 0UL;
            Random.InitState(sessionSeed);
        }

        private static WorldBuildScope BuildInitialPlayScope(WorldMapSession session)
        {
            var tileSize = session.TileSizeMeters > 0f ? session.TileSizeMeters : 1000f;
            var tiles = Mathf.Max(1, session.TilesPerSide);
            var center = tiles / 2;
            const int radius = 1;
            var min = Mathf.Max(0, center - radius);
            var max = Mathf.Min(tiles - 1, center + radius);
            var origin = session.WorldOriginXZ;
            var side = max - min + 1;
            var bounds = new Rect(
                origin.x + min * tileSize,
                origin.y + min * tileSize,
                side * tileSize,
                side * tileSize);
            return WorldBuildScope.InitialPlayArea(bounds);
        }

        private WorldMapSession BuildBackendSession(IWorldGenerationBackend backend, int sessionSeed)
        {
            var tier = _pendingMapSizeTier;
            if (backend is WorldBuilderService service && service.Profile != null)
            {
                var profile = service.Profile;
                var mapSize = profile.MapSizeSettings;
                if (mapSize != null)
                    return mapSize.CreateSession(tier, sessionSeed, profile.ProfileVersion);

                return WorldMapSession.CreateWithOceanRing(
                    tier,
                    sessionSeed,
                    profile.ProfileVersion,
                    Vector2.zero,
                    DefaultCoreTilesForTier(tier),
                    DefaultOceanRingTilesForTier(tier),
                    WorldMapSizeSettings.TileSizeMeters);
            }

            return WorldMapSession.CreateWithOceanRing(
                tier,
                sessionSeed,
                1,
                Vector2.zero,
                DefaultCoreTilesForTier(tier),
                DefaultOceanRingTilesForTier(tier),
                WorldMapSizeSettings.TileSizeMeters);
        }

        private static int DefaultCoreTilesForTier(WorldMapSizeTier tier) =>
            tier switch
            {
                WorldMapSizeTier.Small => 4,
                WorldMapSizeTier.Large => 10,
                _ => 8
            };

        private static int DefaultOceanRingTilesForTier(WorldMapSizeTier tier) =>
            tier == WorldMapSizeTier.Large ? 3 : 0;

        private static bool IsWorldSessionStateActive()
        {
            var gm = GameManager.Instance;
            if (gm == null) return true;

            var state = gm.CurrentState;
            return state == GameState.LoadingWorld || state == GameState.Playing || state == GameState.Paused;
        }

        public void SetSimulationActive(bool active)
        {
            if (active && !IsWorldSessionStateActive()) active = false;

            IsSimulationActive = active;
            worldSimulationManager?.SetSimulationActive(active);

            if (chunkLoader != null) chunkLoader.enabled = active;

            if (lootSpawner != null) lootSpawner.enabled = active;
            if (worldEventSystem != null) worldEventSystem.enabled = active;

            if (tileStreamBridge != null)
                tileStreamBridge.enabled = active && useProceduralStreamingWorld;

            if (proceduralRoadSystem != null)
                proceduralRoadSystem.enabled = active && useProceduralStreamingWorld;

            if (navMeshTileService != null)
                navMeshTileService.enabled = active && useProceduralStreamingWorld;

            if (easyRoadsRoadBridge != null)
                easyRoadsRoadBridge.enabled = active && useProceduralStreamingWorld && enableEasyRoadsRoadBridge;

            if (streamedCityBuilder != null)
            {
                var shouldEnableStreamedCityBuilder = active
                                                      && useProceduralStreamingWorld
                                                      && enableStreamedCityBuilder
                                                      && streamedCityBuilder.HasAnyValidBuildingEntries;
                streamedCityBuilder.enabled = shouldEnableStreamedCityBuilder;
            }

            if (worldBuildingMaterializer != null)
            {
                worldBuildingMaterializer.enabled = active && useProceduralStreamingWorld;
                if (!worldBuildingMaterializer.enabled)
                    worldBuildingMaterializer.DestroyAllViews();
            }

            if (!active)
            {
                zombieManager?.Shutdown();

                _nextValidationZombieAttemptAt = 0f;
                _validationZombieAttemptFinished = false;
            }
        }
    }
}
