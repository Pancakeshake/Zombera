using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;
using Zombera.World.Simulation;

namespace Zombera.World
{
    public partial class WorldManager
    {
        private static readonly CharacterSpawnDependencyStage[] SpawnDependencyOrder =
        {
            CharacterSpawnDependencyStage.Terrain,
            CharacterSpawnDependencyStage.Roads,
            CharacterSpawnDependencyStage.Buildings,
            CharacterSpawnDependencyStage.NavMesh,
            CharacterSpawnDependencyStage.Objects
        };

        private readonly List<WorldTileInfo> _spawnReadinessTileBuffer = new(32);

        public bool IsCharacterSpawnDependencyStageReady(CharacterSpawnDependencyStage stage)
        {
            if (!useProceduralStreamingWorld) return true;

            ResolveSpawnDependencyReferences();

            return stage switch
            {
                CharacterSpawnDependencyStage.Terrain => IsTerrainStageReady(),
                CharacterSpawnDependencyStage.Roads => IsRoadStageReady(),
                CharacterSpawnDependencyStage.Buildings => IsBuildingStageReady(),
                CharacterSpawnDependencyStage.NavMesh => IsNavMeshStageReady(),
                CharacterSpawnDependencyStage.Objects => IsObjectStageReady(),
                _ => true
            };
        }

        public static string GetCharacterSpawnDependencyStageLabel(CharacterSpawnDependencyStage stage)
        {
            return stage switch
            {
                CharacterSpawnDependencyStage.Terrain => "terrain",
                CharacterSpawnDependencyStage.Roads => "roads",
                CharacterSpawnDependencyStage.Buildings => "buildings",
                CharacterSpawnDependencyStage.NavMesh => "navmesh",
                CharacterSpawnDependencyStage.Objects => "objects",
                _ => "unknown"
            };
        }

        public bool IsCharacterSpawnDependencyOrderReady(out string pendingStage)
        {
            pendingStage = string.Empty;

            for (var i = 0; i < SpawnDependencyOrder.Length; i++)
            {
                var stage = SpawnDependencyOrder[i];
                if (IsCharacterSpawnDependencyStageReady(stage)) continue;

                pendingStage = GetCharacterSpawnDependencyStageLabel(stage);
                return false;
            }

            return true;
        }

        private bool IsNavMeshStageReady()
        {
            if (navMeshTileService == null || !navMeshTileService.isActiveAndEnabled) return true;

            var focus = ResolveNavMeshSpawnFocusPosition();
            if (navMeshTileService.IsNavMeshReadyNear(focus)) return true;

            return navMeshTileService.HasAnyBakedTiles
                   && navMeshTileService.PendingTileBakeCount == 0
                   && UnitNavUtils.IsNavMeshReadyAt(focus);
        }

        private Vector3 ResolveNavMeshSpawnFocusPosition()
        {
            var spawner = FindFirstObjectByType<PlayerSpawner>();
            if (spawner != null)
            {
                if (spawner.SpawnedPlayer != null)
                    return spawner.SpawnedPlayer.transform.position;

                return spawner.transform.position;
            }

            if (playerTransform != null)
                return playerTransform.position;

            return Vector3.zero;
        }

        private void ResolveSpawnDependencyReferences()
        {
            if (roadNetworkSystem == null)
                roadNetworkSystem = FindFirstObjectByType<WorldRoadNetworkSystem>();

            if (proceduralRoadSystem == null)
                proceduralRoadSystem = FindFirstObjectByType<ProceduralRoadSystem>();

            if (navMeshTileService == null)
                navMeshTileService = FindFirstObjectByType<StreamingNavMeshTileService>();

            if (easyRoadsRoadBridge == null && enableEasyRoadsRoadBridge)
            {
                easyRoadsRoadBridge = GetComponent<EasyRoadsRoadGameplayBridge>();
                if (easyRoadsRoadBridge == null)
                    easyRoadsRoadBridge = GetComponentInChildren<EasyRoadsRoadGameplayBridge>(true);
                if (easyRoadsRoadBridge == null)
                    easyRoadsRoadBridge = FindFirstObjectByType<EasyRoadsRoadGameplayBridge>();
            }

            if (streamedCityBuilder == null && enableStreamedCityBuilder)
            {
                streamedCityBuilder = GetComponent<WorldStreamedCityBuilder>();
                if (streamedCityBuilder == null)
                    streamedCityBuilder = GetComponentInChildren<WorldStreamedCityBuilder>(true);
                if (streamedCityBuilder == null)
                    streamedCityBuilder = FindFirstObjectByType<WorldStreamedCityBuilder>(FindObjectsInactive.Include);
            }

            if (runtimeCityAreaBuilder == null && enableRuntimeCityAreaBuilder)
            {
                runtimeCityAreaBuilder = GetComponent<WorldRuntimeCityAreaBuilder>();
                if (runtimeCityAreaBuilder == null)
                    runtimeCityAreaBuilder = GetComponentInChildren<WorldRuntimeCityAreaBuilder>(true);
                if (runtimeCityAreaBuilder == null)
                    runtimeCityAreaBuilder = FindFirstObjectByType<WorldRuntimeCityAreaBuilder>(FindObjectsInactive.Include);
            }

            if (lootSpawner == null)
                lootSpawner = FindFirstObjectByType<LootSpawner>();

            tileStreamBridge?.RefreshTileMetrics();
        }

        private bool IsTerrainStageReady()
        {
            var threshold = ResolveContentReadyTileThreshold();
            if (StreamedWorldMetrics.InitialPlayAreaCompleteEvents > 0)
                return true;
            if (StreamedWorldMetrics.ContentReadyTiles >= threshold)
                return true;
            if (StreamedWorldMetrics.TileAppliedForGameplayEvents >= threshold)
                return true;

            return CountTilesAtOrAbove(WorldTileState.ContentReady) >= threshold;
        }

        private int ResolveContentReadyTileThreshold()
        {
            return ProceduralWorldSession.IsSingleTileStressSession() ? 1 : 4;
        }

        private int CountTilesAtOrAbove(WorldTileState minimum)
        {
            if (tileStreamBridge == null)
                return 0;

            tileStreamBridge.CopyTilesAtOrAbove(minimum, _spawnReadinessTileBuffer);
            return _spawnReadinessTileBuffer.Count;
        }

        private bool IsRoadStageReady()
        {
            if (enableRuntimeCityAreaBuilder
                && runtimeCityAreaBuilder != null
                && runtimeCityAreaBuilder.isActiveAndEnabled)
            {
                if (!IsTerrainStageReady())
                    return false;

                if (runtimeCityAreaBuilder.SpawnCityRoadMeshes)
                    return runtimeCityAreaBuilder.HasCompletedRoadsForFocusTile();

                return true;
            }

            if (enablePolylineRoadGraph && roadGameplayService != null && roadGameplayService.RoadGraph != null
                && roadGameplayService.RoadGraph.Segments.Count > 0)
            {
                return true;
            }

            if (proceduralRoadSystem != null && proceduralRoadSystem.isActiveAndEnabled)
            {
                if (proceduralRoadSystem.ProcessedTileCount > 0)
                {
                    var requiredProcessedTiles = ProceduralWorldSession.IsSingleTileStressSession() ? 1 : 2;
                    return StreamedWorldMetrics.InitialPlayAreaCompleteEvents > 0
                           || proceduralRoadSystem.ProcessedTileCount >= requiredProcessedTiles;
                }

                if (ProceduralWorldSession.IsSingleTileStressSession() && IsTerrainStageReady())
                    return true;

                return false;
            }

            if (enableEasyRoadsRoadBridge && easyRoadsRoadBridge != null && easyRoadsRoadBridge.isActiveAndEnabled)
            {
                if (easyRoadsRoadBridge.HasProcessedRoadSync || easyRoadsRoadBridge.HasValidRoadData)
                    return true;

                return IsTerrainStageReady() && easyRoadsRoadBridge.PendingRoadSyncRequestCount == 0;
            }

            if (roadNetworkSystem == null || !roadNetworkSystem.isActiveAndEnabled) return true;
            if (!roadNetworkSystem.UsesLegacyRoadRuntime) return true;
            if (roadNetworkSystem.HasProcessedTileRoads) return true;

            return IsTerrainStageReady() && roadNetworkSystem.PendingTileRoadRequestCount == 0;
        }

        private bool IsBuildingStageReady()
        {
            if (worldGenerationManager != null && worldGenerationManager.isActiveAndEnabled)
                return worldGenerationManager.IsBuildingsReady;

            if (enableRuntimeCityAreaBuilder
                && runtimeCityAreaBuilder != null
                && runtimeCityAreaBuilder.isActiveAndEnabled)
            {
                if (!IsTerrainStageReady())
                    return false;

                return runtimeCityAreaBuilder.HasCompletedCityBuildForFocusTile();
            }

            if (!enableStreamedCityBuilder) return true;
            if (streamedCityBuilder == null || !streamedCityBuilder.isActiveAndEnabled) return true;
            if (streamedCityBuilder.HasProcessedCityTiles) return true;

            return IsTerrainStageReady() && streamedCityBuilder.PendingCityTileRequestCount == 0;
        }

        private bool IsObjectStageReady()
        {
            var lootReady = lootSpawner == null || lootSpawner.HasCompletedInitialObjectBootstrap;
            if (!lootReady)
                return false;

            if (chunkLoader == null || !chunkLoader.isActiveAndEnabled)
                return IsTerrainStageReady();

            if (StreamedWorldMetrics.ChunksLoadedThisSession > 0)
                return true;

            // Chunk merge may lag first ContentReady tiles; don't hard-block forever.
            return IsTerrainStageReady() && StreamedWorldMetrics.InitialPlayAreaCompleteEvents > 0;
        }
    }
}
