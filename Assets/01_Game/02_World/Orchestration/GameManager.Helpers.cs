using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.World;
using Zombera.World.City;
using Zombera.World.Roads;

namespace Zombera.Core
{
    public sealed partial class GameManager
    {
        private static string GetLoadingStageLabel(WorldLoadingStage stage)
        {
            return stage switch
            {
                WorldLoadingStage.TerrainGeneration => "Terrain Generation",
                WorldLoadingStage.NavMesh => "Nav Mesh",
                WorldLoadingStage.Roads => "Roads",
                WorldLoadingStage.Buildings => "Buildings",
                WorldLoadingStage.Players => "Players",
                WorldLoadingStage.Zombies => "Zombies",
                _ => "Unknown"
            };
        }

        private static string GetLoadingStageStatus(WorldLoadingStage stage)
        {
            return stage switch
            {
                WorldLoadingStage.TerrainGeneration => "Terrain generation...",
                WorldLoadingStage.NavMesh => "Building nav mesh...",
                WorldLoadingStage.Roads => "Generating roads...",
                WorldLoadingStage.Buildings => "Spawning buildings...",
                WorldLoadingStage.Players => "Spawning players...",
                WorldLoadingStage.Zombies => "Activating zombies...",
                _ => "Loading..."
            };
        }

        private static float GetLoadingStageProgressStart(WorldLoadingStage stage)
        {
            return stage switch
            {
                WorldLoadingStage.TerrainGeneration => 0.640f,
                WorldLoadingStage.NavMesh => 0.740f,
                WorldLoadingStage.Roads => 0.820f,
                WorldLoadingStage.Buildings => 0.890f,
                WorldLoadingStage.Players => 0.940f,
                WorldLoadingStage.Zombies => 0.985f,
                _ => 0.640f
            };
        }

        private static float GetLoadingStageProgressEnd(WorldLoadingStage stage)
        {
            return stage switch
            {
                WorldLoadingStage.TerrainGeneration => 0.740f,
                WorldLoadingStage.NavMesh => 0.820f,
                WorldLoadingStage.Roads => 0.940f,
                WorldLoadingStage.Buildings => 0.940f,
                WorldLoadingStage.Players => 0.985f,
                WorldLoadingStage.Zombies => 0.998f,
                _ => 0.998f
            };
        }

        private static System.Collections.Generic.IEnumerable<UnityEngine.SceneManagement.Scene> LoadedScenes()
        {
            return Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Select(UnityEngine.SceneManagement.SceneManager.GetSceneAt)
                .Where(scene => scene.IsValid() && scene.isLoaded);
        }

        private bool SceneNameMatchesWorldCandidate(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return false;

            // Allow UI testing scenes to be treated as world candidates so UI isn't pruned.
            if (string.Equals(sceneName, "3_Ui", StringComparison.OrdinalIgnoreCase) ||
                sceneName.IndexOf("Tester", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // Single-tile stress/dev scenes are played directly (skip MainMenu + world reload).
            if (sceneName.IndexOf("Single_Tile_Stress", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (worldSceneName is { Length: > 0 } configuredWorldScene
                && string.Equals(sceneName, configuredWorldScene, StringComparison.OrdinalIgnoreCase))
                return true;

            return WorldSceneFallbackNames.Any(fallbackName =>
                !string.IsNullOrWhiteSpace(fallbackName)
                && string.Equals(sceneName, fallbackName, StringComparison.OrdinalIgnoreCase));
        }

        private bool TryResolveLoadableWorldScene(out string resolvedWorldSceneName, out bool usedFallback)
        {
            if (!string.IsNullOrWhiteSpace(worldSceneName) && Application.CanStreamedLevelBeLoaded(worldSceneName))
            {
                resolvedWorldSceneName = worldSceneName;
                usedFallback = false;
                return true;
            }

            foreach (var fallbackName in WorldSceneFallbackNames)
            {
                if (string.IsNullOrWhiteSpace(fallbackName)) continue;

                if (!Application.CanStreamedLevelBeLoaded(fallbackName)) continue;

                resolvedWorldSceneName = fallbackName;
                usedFallback = !string.Equals(worldSceneName, fallbackName, StringComparison.OrdinalIgnoreCase);
                return true;
            }

            resolvedWorldSceneName = worldSceneName;
            usedFallback = false;
            return false;
        }

        private static string BuildTerrainStageStatus(float elapsedSeconds)
        {
            return "Terrain generation... (activeTiles=" + StreamedWorldMetrics.ActiveMapMagicTiles
                   + ", applied=" + StreamedWorldMetrics.MapMagicTileAppliedEvents
                   + ", " + elapsedSeconds.ToString("0.0") + "s)";
        }

        private static string BuildTerrainStageBottleneckSummary()
        {
            return "activeTiles=" + StreamedWorldMetrics.ActiveMapMagicTiles
                   + ", tileApplied=" + StreamedWorldMetrics.MapMagicTileAppliedEvents
                   + ", allComplete=" + StreamedWorldMetrics.MapMagicAllCompleteEvents
                   + ", chunksLoaded=" + StreamedWorldMetrics.ChunksLoadedThisSession;
        }

        private string BuildRoadStageStatus(float elapsedSeconds)
        {
            if (proceduralRoadSystem != null && proceduralRoadSystem.isActiveAndEnabled)
            {
                return "Generating roads... (processed=" + proceduralRoadSystem.ProcessedTileCount
                       + ", " + elapsedSeconds.ToString("0.0") + "s)";
            }

            var easyRoadsBridge = ResolveLoadingRoadBridge();
            if (easyRoadsBridge != null && easyRoadsBridge.isActiveAndEnabled)
                return "Generating roads... (pending=" + easyRoadsBridge.PendingRoadSyncRequestCount
                       + ", " + elapsedSeconds.ToString("0.0") + "s)";

            var legacyRoadSystem = ResolveLoadingRoadNetworkSystem();
            if (legacyRoadSystem != null
                && legacyRoadSystem.isActiveAndEnabled
                && legacyRoadSystem.UsesLegacyRoadRuntime)
                return "Generating roads... (pending=" + legacyRoadSystem.PendingTileRoadRequestCount
                       + ", " + elapsedSeconds.ToString("0.0") + "s)";

            return "Generating roads... (" + elapsedSeconds.ToString("0.0") + "s)";
        }

        private string BuildRoadStageBottleneckSummary()
        {
            var easyRoadsBridge = ResolveLoadingRoadBridge();
            if (easyRoadsBridge != null && easyRoadsBridge.isActiveAndEnabled)
                return "mode=EasyRoads, pendingSync=" + easyRoadsBridge.PendingRoadSyncRequestCount
                       + ", processedSync=" + easyRoadsBridge.HasProcessedRoadSync
                       + ", hasRoadData=" + easyRoadsBridge.HasValidRoadData;

            var legacyRoadSystem = ResolveLoadingRoadNetworkSystem();
            if (legacyRoadSystem != null
                && legacyRoadSystem.isActiveAndEnabled
                && legacyRoadSystem.UsesLegacyRoadRuntime)
                return "mode=LegacyRoadRuntime, pendingTileRequests=" + legacyRoadSystem.PendingTileRoadRequestCount
                       + ", processedTileRoads=" + legacyRoadSystem.HasProcessedTileRoads;

            return "road runtime unavailable or disabled";
        }

        private string BuildBuildingStageStatus(float elapsedSeconds)
        {
            var cityBuilder = ResolveLoadingCityBuilder();
            if (cityBuilder != null && cityBuilder.isActiveAndEnabled)
                return "Spawning buildings... (pending=" + cityBuilder.PendingCityTileRequestCount
                       + ", " + elapsedSeconds.ToString("0.0") + "s)";

            return "Spawning buildings... (" + elapsedSeconds.ToString("0.0") + "s)";
        }

        private string BuildBuildingStageBottleneckSummary()
        {
            var cityBuilder = ResolveLoadingCityBuilder();
            if (cityBuilder == null)
                return "cityBuilder=missing";

            return "pendingCityTiles=" + cityBuilder.PendingCityTileRequestCount
                   + ", processedCityTiles=" + cityBuilder.HasProcessedCityTiles
                   + ", hasValidCatalog=" + cityBuilder.HasAnyValidBuildingEntries
                   + ", active=" + cityBuilder.isActiveAndEnabled;
        }

        private static string BuildNavMeshStageStatus(float elapsedSeconds, StreamingNavMeshTileService navMeshService)
        {
            if (navMeshService == null)
                return "Building nav mesh... (service missing, " + elapsedSeconds.ToString("0.0") + "s)";

            return "Building nav mesh... (pending=" + navMeshService.PendingTileBakeCount
                   + ", rebuilding=" + navMeshService.IsBootstrapRebuildInProgress
                   + ", " + elapsedSeconds.ToString("0.0") + "s)";
        }

        private static string BuildNavMeshStageBottleneckSummary(StreamingNavMeshTileService navMeshService)
        {
            if (navMeshService == null) return "navMeshService=missing";

            return "pendingTileBakes=" + navMeshService.PendingTileBakeCount
                   + ", rebuildInProgress=" + navMeshService.IsBootstrapRebuildInProgress
                   + ", hasAnyBakedTiles=" + navMeshService.HasAnyBakedTiles
                   + ", bootstrapHadTriangles=" + navMeshService.LastBootstrapHadTriangles;
        }

        private static Vector3 ResolveNavMeshFocusPosition(PlayerSpawner spawner)
        {
            if (spawner != null && spawner.SpawnedPlayer != null)
                return spawner.SpawnedPlayer.transform.position;

            var playerUnit = UnitManager.Instance?.FindFirstUnitByRole(UnitRole.Player);
            if (playerUnit != null)
                return playerUnit.transform.position;

            if (spawner != null)
                return spawner.transform.position;

            return Vector3.zero;
        }

        private static bool HasNearbyNavMesh(Vector3 worldPosition)
        {
            if (!IsFiniteVector3(worldPosition)) return false;

            var navMeshService = UnityEngine.Object.FindFirstObjectByType<StreamingNavMeshTileService>();
            if (navMeshService != null && navMeshService.isActiveAndEnabled)
                return navMeshService.IsNavMeshReadyNear(worldPosition);

            return Zombera.Characters.UnitNavUtils.TryHasNearbyNavMesh(worldPosition);
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x)
                   && float.IsFinite(value.y)
                   && float.IsFinite(value.z);
        }

        private void LogLoadingStageStart(WorldLoadingStage stage, string details)
        {
            if (!logLoadingStageDiagnostics) return;

            var message = "[GameManager] Loading stage '" + GetLoadingStageLabel(stage) + "' started.";
            if (!string.IsNullOrWhiteSpace(details)) message += " " + details;
            Debug.Log(message, this);
        }

        private void LogLoadingStageCompletion(WorldLoadingStage stage, float elapsedSeconds, bool timedOut,
            string details)
        {
            var message = "[GameManager] Loading stage '" + GetLoadingStageLabel(stage) + "' " +
                          (timedOut ? "timed out" : "completed") + " in " + elapsedSeconds.ToString("0.00") + "s.";

            if (!string.IsNullOrWhiteSpace(details)) message += " " + details;

            var bottleneckThreshold = Mathf.Max(0.1f, loadingStageBottleneckWarningSeconds);
            if (timedOut || elapsedSeconds >= bottleneckThreshold)
            {
                Debug.LogWarning(message, this);
                return;
            }

            if (logLoadingStageDiagnostics)
                Debug.Log(message, this);
        }

        private static string BuildPlayerStageBottleneckSummary(PlayerSpawner spawner)
        {
            if (spawner == null) return "spawner=missing";

            return "provisionalPlayer=" + (spawner.SpawnedPlayer != null)
                   + ", finalized=" + spawner.HasFinalizedWorldPlayerSpawn;
        }

        private static string BuildZombieStageStatus(float elapsedSeconds, ZombieManager manager)
        {
            if (manager == null)
                return "Activating zombies... (manager missing, " + elapsedSeconds.ToString("0.0") + "s)";

            return "Activating zombies... (active=" + manager.ActiveZombieCount +
                   ", validationSpawned=" + manager.HasSpawnedValidationZombieNearPlayer +
                   ", " + elapsedSeconds.ToString("0.0") + "s)";
        }

        private static string BuildZombieStageBottleneckSummary(ZombieManager manager)
        {
            if (manager == null) return "zombieManager=missing";

            return "initialized=" + manager.IsInitialized
                   + ", suppressed=" + manager.IsRuntimeSpawningSuppressed
                   + ", active=" + manager.ActiveZombieCount
                   + ", validationEnabled=" + manager.ValidationSpawnEnabled
                   + ", validationSpawned=" + manager.HasSpawnedValidationZombieNearPlayer;
        }

        private EasyRoadsRoadGameplayBridge ResolveLoadingRoadBridge()
        {
            _loadingRoadBridge = ResolveReference(_loadingRoadBridge);
            if (_loadingRoadBridge == null)
                _loadingRoadBridge = FindFirstObjectByType<EasyRoadsRoadGameplayBridge>();

            return _loadingRoadBridge;
        }

        private WorldRoadNetworkSystem ResolveLoadingRoadNetworkSystem()
        {
            _loadingRoadNetworkSystem = ResolveReference(_loadingRoadNetworkSystem);
            if (_loadingRoadNetworkSystem == null)
                _loadingRoadNetworkSystem = FindFirstObjectByType<WorldRoadNetworkSystem>();

            return _loadingRoadNetworkSystem;
        }

        private WorldStreamedCityBuilder ResolveLoadingCityBuilder()
        {
            _loadingCityBuilder = ResolveReference(_loadingCityBuilder);
            if (_loadingCityBuilder == null)
                _loadingCityBuilder = FindFirstObjectByType<WorldStreamedCityBuilder>();

            return _loadingCityBuilder;
        }

        private static PlayerSpawner FindFirstSpawnerInLoadedScenes()
        {
            var spawners = FindObjectsByType<PlayerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (spawners.Length == 0) return null;

            PlayerSpawner best = null;
            var bestScore = int.MinValue;

            for (var i = 0; i < spawners.Length; i++)
            {
                var candidate = spawners[i];
                if (candidate == null) continue;

                var score = 0;
                if (candidate.isActiveAndEnabled) score += 4;

                var scene = candidate.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded) score += 3;
                if (!IsSpawnerAttachedToUnit(candidate)) score += 2;
                if (candidate.SpawnedPlayer != null) score += 1;

                if (score <= bestScore) continue;
                best = candidate;
                bestScore = score;
            }

            return best ?? spawners[0];
        }

        private void CountTrackedWorldRosterVisualReadiness(PlayerSpawner spawner, out int targets, out int ready)
        {
            targets = 0;
            ready = 0;

            var playerUnit = spawner != null
                ? spawner.SpawnedPlayer
                : UnitManager.Instance?.FindFirstUnitByRole(UnitRole.Player);

            if (playerUnit != null)
            {
                targets++;
                if (HasAnyEnabledRenderer(playerUnit.gameObject)) ready++;
            }

            var roster = (SquadManager.Instance ?? squadManager)?.SquadMembers;
            if (roster == null) return;

            for (var i = 0; i < roster.Count; i++)
            {
                var member = roster[i];
                if (member == null) continue;

                var memberUnit = member.Unit;
                if (memberUnit == null) continue;

                targets++;
                if (HasAnyEnabledRenderer(memberUnit.gameObject)) ready++;
            }
        }

        private bool HasAnyEnabledRenderer(GameObject root)
        {
            _rendererScratch.Clear();
            if (root == null) return false;

            try
            {
                root.GetComponentsInChildren(true, _rendererScratch);

                for (var i = 0; i < _rendererScratch.Count; i++)
                {
                    var renderer = _rendererScratch[i];
                    if (renderer == null) continue;
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    return true;
                }

                return false;
            }
            finally
            {
                _rendererScratch.Clear();
                if (_rendererScratch.Capacity > 512) _rendererScratch.Capacity = 512;
            }
        }

    }
}