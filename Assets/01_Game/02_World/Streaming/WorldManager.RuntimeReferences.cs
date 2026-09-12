using UnityEngine;
using Zombera.Characters.Work;
using Zombera.Factions;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.World
{
    public partial class WorldManager
    {
        private void ResolveRuntimeReferences()
        {
            chunkLoader = ResolveReference(chunkLoader);
            chunkGenerator = ResolveReference(chunkGenerator);
            chunkCache = ResolveReference(chunkCache);
            regionSystem = ResolveReference(regionSystem);
            mapStateService = ResolveReference(mapStateService);
            mapSpawner = ResolveReference(mapSpawner);
            lootSpawner = ResolveReference(lootSpawner);
            worldEventSystem = ResolveReference(worldEventSystem);
            worldSimulationManager = ResolveReference(worldSimulationManager);
            tileStreamBridge = ResolveReference(tileStreamBridge);
            streamedCityBuilder = ResolveReference(streamedCityBuilder);
            roadNetworkSystem = ResolveReference(roadNetworkSystem);
            easyRoadsRoadBridge = ResolveReference(easyRoadsRoadBridge);
            proceduralRoadSystem = ResolveReference(proceduralRoadSystem);
            roadGameplayService = ResolveReference(roadGameplayService);
            worldBuildingMaterializer = ResolveReference(worldBuildingMaterializer);
            zombieManager = ResolveReference(zombieManager);
            if (streamedCityCatalog == null)
                streamedCityCatalog = Resources.Load<StreamedCityCatalog>(DefaultStreamedCityCatalogResourcesPath);

            EnsureUnifiedRoadGameplayStackProvisioned();
            EnsureProceduralStreamingBridge();
            EnsureProceduralRoadSystemProvisioned();
            EnsureStreamedCityBuilderProvisioned();
            EnsureWorldBuildingMaterializerConfigured();

            EnsureMapStateServiceProvisioned();
            EnsureMapMarkerManagerProvisioned();
            EnsureWorkManagerProvisioned();
            EnsureFactionManagerProvisioned();
        }

        private void EnsureFactionManagerProvisioned()
        {
            var factionManager = ResolveReference(GetComponent<FactionManager>());
            if (factionManager == null) factionManager = ResolveReference(FindFirstObjectByType<FactionManager>());

            if (factionManager == null && IsWorldSessionStateActive())
                factionManager = gameObject.AddComponent<FactionManager>();
        }

        private void EnsureWorkManagerProvisioned()
        {
            var workManager = ResolveReference(GetComponent<WorkManager>());
            if (workManager == null) workManager = ResolveReference(FindFirstObjectByType<WorkManager>());

            if (workManager == null && IsWorldSessionStateActive())
                workManager = gameObject.AddComponent<WorkManager>();

            workManager?.RefreshRosterFromSquad();
        }

        private void EnsureMapMarkerManagerProvisioned()
        {
            var markerManager = ResolveReference(GetComponent<MapMarkerManager>());
            if (markerManager == null) markerManager = ResolveReference(FindFirstObjectByType<MapMarkerManager>());

            if (markerManager == null && IsWorldSessionStateActive())
                markerManager = gameObject.AddComponent<MapMarkerManager>();

            if (markerManager == null) return;

            markerManager.Configure(mapStateService);
        }

        private void EnsureMapStateServiceProvisioned()
        {
            mapStateService = ResolveReference(mapStateService);

            if (mapStateService == null && IsWorldSessionStateActive())
                mapStateService = gameObject.AddComponent<MapStateService>();

            if (mapStateService == null) return;

            var fogTextureBuilder = mapStateService.GetComponent<FogOfWarTextureBuilder>();
            if (fogTextureBuilder == null && IsWorldSessionStateActive())
                fogTextureBuilder = mapStateService.gameObject.AddComponent<FogOfWarTextureBuilder>();

            fogTextureBuilder?.Configure(mapStateService);
            mapStateService.Configure(chunkLoader, regionSystem, fogTextureBuilder);
        }

        private void ResolveRuntimeReferencesIfNeeded(bool force = false)
        {
            if (!force)
            {
                if (Time.unscaledTime < _nextReferenceResolveAt) return;
                if (!HasMissingRuntimeReferences()) return;
            }

            ResolveRuntimeReferences();
            _nextReferenceResolveAt = Time.unscaledTime + 2f;
        }

        private bool HasMissingRuntimeReferences()
        {
            return chunkLoader == null
                   || chunkGenerator == null
                   || chunkCache == null
                   || regionSystem == null
                   || mapStateService == null
                   || mapSpawner == null
                   || lootSpawner == null
                   || worldEventSystem == null
                   || worldSimulationManager == null
                   || (useProceduralStreamingWorld && tileStreamBridge == null)
                   || (useProceduralStreamingWorld && proceduralRoadSystem == null)
                   || (useProceduralStreamingWorld && enableStreamedCityBuilder && streamedCityBuilder == null)
                   || (useProceduralStreamingWorld && enableEasyRoadsRoadBridge && easyRoadsRoadBridge == null)
                   || (useProceduralStreamingWorld && useSingleRoadGameplayStackObject && roadGameplayService == null)
                   || zombieManager == null;
        }

        private void EnsureUnifiedRoadGameplayStackProvisioned()
        {
            if (!useProceduralStreamingWorld || !useSingleRoadGameplayStackObject) return;

            roadGameplayService = ResolveReference(roadGameplayService);

            if (roadGameplayService == null && IsWorldSessionStateActive())
            {
                var objectName = string.IsNullOrWhiteSpace(roadGameplayStackObjectName)
                    ? "RoadGameplayService"
                    : roadGameplayStackObjectName;
                var stackObject = new GameObject(objectName);
                if (transform.parent != null)
                    stackObject.transform.SetParent(transform.parent, false);
                roadGameplayService = stackObject.AddComponent<RoadGameplayService>();
            }

            if (roadGameplayService == null) return;

            var stackRoot = roadGameplayService.gameObject;
            tileStreamBridge = ResolveReference(stackRoot.GetComponent<WorldTileStreamSource>());
            if (tileStreamBridge == null)
                tileStreamBridge = ResolveWorldTileStreamFromBackend();
            if (tileStreamBridge == null)
                LogMissingTileStreamBridgeOnce();

            if (!enableEasyRoadsRoadBridge) return;

            easyRoadsRoadBridge = ProvisionComponent<EasyRoadsRoadGameplayBridge>(stackRoot);
            if (easyRoadsRoadBridge != null)
                easyRoadsRoadBridge.Configure(tileStreamBridge);

            DisableDuplicateRoadStackComponents(tileStreamBridge, easyRoadsRoadBridge);
        }

        private void EnsureProceduralStreamingBridge()
        {
            if (!useProceduralStreamingWorld) return;

            if (tileStreamBridge != null) return;

            tileStreamBridge = ResolveWorldTileStreamFromBackend();
            if (tileStreamBridge != null) return;

            var preferredHost = useSingleRoadGameplayStackObject && roadGameplayService != null
                ? roadGameplayService.gameObject
                : gameObject;

            tileStreamBridge = ResolveReference(preferredHost.GetComponent<WorldTileStreamSource>());
            if (tileStreamBridge == null)
                tileStreamBridge = ResolveReference(FindFirstObjectByType<WorldTileStreamSource>(FindObjectsInactive.Include));

            if (tileStreamBridge == null)
                LogMissingTileStreamBridgeOnce();
        }

        private WorldTileStreamSource ResolveWorldTileStreamFromBackend()
        {
            var backend = WorldGenerationBackend;
            return backend?.TileStream;
        }

        private void LogMissingTileStreamBridgeOnce()
        {
            if (_loggedMissingTileStreamBridge) return;
            _loggedMissingTileStreamBridge = true;
            Debug.LogWarning(
                "[WorldManager] No WorldTileStreamSource found. Assign one in the scene " +
                "(e.g. Legacy MapMagicTileStreamBridge) or wire IWorldGenerationBackend.TileStream. " +
                "WorldManager will not AddComponent a Legacy concrete type.",
                this);
        }

        private void EnsureProceduralRoadSystemProvisioned()
        {
            if (!useProceduralStreamingWorld)
            {
                proceduralRoadSystem = ResolveReference(proceduralRoadSystem);
                if (proceduralRoadSystem != null) proceduralRoadSystem.enabled = false;
                return;
            }

            if (tileStreamBridge == null) EnsureProceduralStreamingBridge();

            var preferredHost = useSingleRoadGameplayStackObject && roadGameplayService != null
                ? roadGameplayService.gameObject
                : gameObject;

            proceduralRoadSystem = ResolveReference(preferredHost.GetComponent<ProceduralRoadSystem>());
            if (proceduralRoadSystem == null)
                proceduralRoadSystem = ResolveReference(proceduralRoadSystem);

            if (proceduralRoadSystem == null && IsWorldSessionStateActive())
                proceduralRoadSystem = preferredHost.AddComponent<ProceduralRoadSystem>();

            if (proceduralRoadSystem != null)
            {
                proceduralRoadSystem.Configure(tileStreamBridge, roadGameplayService);
                // Only run road streaming during an active world session; menu states keep it disabled.
                proceduralRoadSystem.enabled = IsWorldSessionStateActive();
                DisableDuplicateRoadStackComponentInstances(proceduralRoadSystem);
            }
        }

        private void EnsureStreamedCityBuilderProvisioned()
        {
            if (!useProceduralStreamingWorld || !enableStreamedCityBuilder)
            {
                streamedCityBuilder = ResolveReference(streamedCityBuilder);
                if (streamedCityBuilder != null) streamedCityBuilder.enabled = false;
                EnsureEasyRoadsRoadBridgeProvisioned();
                return;
            }

            if (tileStreamBridge == null) EnsureProceduralStreamingBridge();

            streamedCityBuilder = ResolveReference(streamedCityBuilder);
            if (streamedCityBuilder == null)
                streamedCityBuilder = ResolveReference(FindFirstObjectByType<WorldStreamedCityBuilder>(FindObjectsInactive.Include));

            if (streamedCityBuilder == null)
            {
                EnsureEasyRoadsRoadBridgeProvisioned();
                return;
            }

            if (streamedCityCatalog == null)
                streamedCityCatalog = Resources.Load<StreamedCityCatalog>(DefaultStreamedCityCatalogResourcesPath);

            streamedCityBuilder.Configure(tileStreamBridge, streamedCityCatalog);
            streamedCityBuilder.enabled = streamedCityBuilder.HasAnyValidBuildingEntries;
            EnsureEasyRoadsRoadBridgeProvisioned();

            if (!streamedCityBuilder.HasAnyValidBuildingEntries && !_loggedMissingCityCatalogWarning)
            {
                Debug.LogWarning(
                    "[WorldManager] Streamed city builder is enabled but has no valid building source. " +
                    "Assign a StreamedCityCatalog on WorldManager, create one at Resources/World/StreamedCityCatalog, " +
                    "or assign a fallback prefab on StreamedMapMagicCityBuilder.",
                    this);
                _loggedMissingCityCatalogWarning = true;
            }
            else if (streamedCityBuilder.HasAnyValidBuildingEntries)
            {
                _loggedMissingCityCatalogWarning = false;
            }

            var legacySpawner = GetComponent<Zombera.World.Spawning.WorldBuildingSpawner>();
            if (legacySpawner != null && legacySpawner.enabled) legacySpawner.enabled = false;
        }

        private void EnsureWorldBuildingMaterializerConfigured()
        {
            worldBuildingMaterializer = ResolveReference(worldBuildingMaterializer);
            if (!useProceduralStreamingWorld)
            {
                if (worldBuildingMaterializer != null)
                {
                    worldBuildingMaterializer.DestroyAllViews();
                    worldBuildingMaterializer.enabled = false;
                }

                return;
            }

            if (tileStreamBridge == null)
                EnsureProceduralStreamingBridge();

            if (worldBuildingMaterializer == null)
                return;

            worldBuildingMaterializer.Configure(ResolveWorldStateManager(), tileStreamBridge);
            worldBuildingMaterializer.enabled = IsWorldSessionStateActive();
        }

        private WorldStateManager ResolveWorldStateManager()
        {
            if (WorldGenerationBackend is WorldBuilderService service && service.StateManager != null)
                return service.StateManager;

            var stateManager = GetComponent<WorldStateManager>();
            if (stateManager == null)
                stateManager = GetComponentInChildren<WorldStateManager>(true);
            if (stateManager == null)
                stateManager = FindFirstObjectByType<WorldStateManager>(FindObjectsInactive.Include);
            return stateManager;
        }

        private void EnsureEasyRoadsRoadBridgeProvisioned()
        {
            if (!useProceduralStreamingWorld || !enableEasyRoadsRoadBridge)
            {
                easyRoadsRoadBridge = ResolveReference(easyRoadsRoadBridge);
                if (easyRoadsRoadBridge != null) easyRoadsRoadBridge.enabled = false;
                return;
            }

            if (tileStreamBridge == null) EnsureProceduralStreamingBridge();

            var preferredHost = useSingleRoadGameplayStackObject && roadGameplayService != null
                ? roadGameplayService.gameObject
                : gameObject;

            easyRoadsRoadBridge = ResolveReference(preferredHost.GetComponent<EasyRoadsRoadGameplayBridge>());
            if (easyRoadsRoadBridge == null && IsWorldSessionStateActive())
                easyRoadsRoadBridge = preferredHost.AddComponent<EasyRoadsRoadGameplayBridge>();

            if (easyRoadsRoadBridge != null)
            {
                easyRoadsRoadBridge.Configure(tileStreamBridge);
                easyRoadsRoadBridge.enabled = true;
            }
        }

        private static void DisableDuplicateRoadStackComponents(
            WorldTileStreamSource preferredTileBridge,
            EasyRoadsRoadGameplayBridge preferredRoadBridge)
        {
            DisableDuplicateRoadStackComponentInstances(preferredTileBridge);
            DisableDuplicateRoadStackComponentInstances(preferredRoadBridge);
        }

        private static void DisableDuplicateRoadStackComponentInstances<T>(T preferred) where T : Behaviour
        {
            if (preferred == null) return;

            var instances = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance == null || instance == preferred) continue;
                if (!instance.enabled) continue;
                instance.enabled = false;
            }
        }

        private T ProvisionComponent<T>(GameObject host) where T : Component
        {
            if (host == null) return null;
            var comp = host.GetComponent<T>();
            if (comp == null && IsWorldSessionStateActive())
                comp = host.AddComponent<T>();
            return comp;
        }

        private T ResolveReference<T>(T currentReference) where T : Component
        {
            if (currentReference != null) return currentReference;

            currentReference = GetComponent<T>();
            if (currentReference == null) currentReference = GetComponentInChildren<T>(true);
            if (currentReference == null)
                currentReference = FindFirstObjectByType<T>(FindObjectsInactive.Include);

            return currentReference;
        }
    }
}
