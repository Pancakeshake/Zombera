using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Debugging;
using Zombera.Systems;
using Zombera.UI;
using Zombera.UI.Menus;
using Zombera.World;

namespace Zombera.Core
{
    public sealed partial class GameManager
    {
        private IEnumerator BeginWorldSessionRoutine(bool applyCharacterSelection)
        {
            _worldSessionStarting = true;
            var zombieSuppressionHeld = false;

            try
            {
                LoadingScreenOverlay.Show("Initializing world systems...");
                LoadingScreenOverlay.SetProgress(LoadingProgressWorldSessionBootstrapStart,
                    "Initializing world systems...");

                using (PerfTrace.Measure("WorldSession: EnsureWorldRuntimeComponentsPresent", this))
                {
                    EnsureWorldRuntimeComponentsPresent();
                }

                using (PerfTrace.Measure("WorldSession: ResolveRuntimeReferences", this))
                {
                    ResolveRuntimeReferences();
                }

                using (PerfTrace.Measure("WorldSession: Refresh registries", this))
                {
                    unitManager?.RefreshRegistry();
                    squadManager?.RefreshSquadRoster();
                }

                using (PerfTrace.Measure("WorldSession: Clear cross-scene appearance bindings", this))
                {
                    ClearCrossSceneAppearanceBindingsForAllAvatars();
                }

                if (enableStagedLoadingScreenPrewarm)
                {
                    LoadingScreenOverlay.SetProgress(LoadingProgressPrewarmStart, "Preloading critical assets...");
                    yield return RunStagedLoadingScreenPrewarm(FindFirstSpawnerInLoadedScenes());
                }

                LoadingScreenOverlay.SetProgress(LoadingProgressProvisionalPlayerStart,
                    "Registering provisional player...");
                var spawner = FindFirstSpawnerInLoadedScenes();
                if (spawner != null)
                {
                    spawner.isLoadingSaveSession = _isRestoringFromSave;
                }
                yield return EnsureProvisionalPlayerRegisteredForWorldInit(spawner);

                if (applyCharacterSelection)
                {
                    using (PerfTrace.Measure("WorldSession: Apply character selection (stats)", this))
                    {
                        CharacterSelectionApplier.ApplyToActivePlayer();
                    }
                }

                RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();

                if (holdZombieSpawningUntilZombieStage)
                {
                    ApplyZombieLoadingSuppression(true, "world session bootstrap (pre-init)");
                    zombieSuppressionHeld = true;
                }

                LoadingScreenOverlay.SetProgress(LoadingProgressWorldSimulationStart, "Starting world simulation...");
                using (PerfTrace.Measure("WorldSession: WorldManager.InitializeWorld", worldManager))
                {
                    if (_hasPendingWorldSessionRequest)
                    {
                        worldManager?.ApplySessionRequest(_pendingWorldSessionRequest);
                        _hasPendingWorldSessionRequest = false;
                    }

                    if (_isRestoringFromSave &&
                        saveManager != null &&
                        saveManager.TryGetDeferredProceduralWorld(out var proceduralWorldData) &&
                        worldManager != null &&
                        !worldManager.TryPrepareLoadedProceduralWorld(proceduralWorldData, out var prepareError))
                    {
                        throw new InvalidOperationException(
                            "Saved procedural world failed pre-world preparation: " + prepareError);
                    }

                    worldManager?.InitializeWorld();
                }

                if (zombieSuppressionHeld)
                    ApplyZombieLoadingSuppression(true, "world session bootstrap (post-init)");

                // Keep loading overlay for at least one world frame while systems settle.
                yield return null;

                if (enforceOrderedLoadingStages)
                {
                    yield return WaitForWorldDependencyLoadingStage(
                        WorldLoadingStage.TerrainGeneration,
                        CharacterSpawnDependencyStage.Terrain,
                        terrainGenerationStageTimeoutSeconds,
                        BuildTerrainStageStatus,
                        BuildTerrainStageBottleneckSummary);

                    yield return WaitForWorldDependencyLoadingStage(
                        WorldLoadingStage.Roads,
                        CharacterSpawnDependencyStage.Roads,
                        roadsStageTimeoutSeconds,
                        BuildRoadStageStatus,
                        BuildRoadStageBottleneckSummary);

                    yield return WaitForNavMeshLoadingStage(spawner);
                    yield return WaitForPlayerLoadingStage(spawner);

                    if (zombieSuppressionHeld)
                    {
                        ApplyZombieLoadingSuppression(false, "zombie stage release");
                        zombieSuppressionHeld = false;
                    }

                    yield return WaitForZombieLoadingStage();
                }
                else
                {
                    var fallbackNavStart = GetLoadingStageProgressStart(WorldLoadingStage.NavMesh);
                    var fallbackPlayerStart = GetLoadingStageProgressStart(WorldLoadingStage.Players);
                    var fallbackPlayerEnd = GetLoadingStageProgressEnd(WorldLoadingStage.Players);
                    LoadingScreenOverlay.SetProgress(fallbackNavStart, "Building navigation...");
                    yield return WaitForFinalWorldPlayerSpawnIfPresent(
                        spawner,
                        fallbackNavStart,
                        fallbackPlayerStart,
                        "Building navigation...",
                        true);

                    LoadingScreenOverlay.SetProgress(fallbackPlayerStart, "Generating characters...");
                    yield return WaitForWorldCharacterVisualsReady(
                        spawner,
                        fallbackPlayerStart,
                        fallbackPlayerEnd,
                        "Generating characters...");
                }

                SetGameState(GameState.Playing);
                RunReadinessValidation(StartupReadinessValidator.ValidationMode.WorldSession);

                yield return null;
                LoadingScreenOverlay.SetProgress(1f, "Ready");
                yield return LoadingScreenOverlay.WaitForVisualProgress(1f, 2.5f);

                if (_isRestoringFromSave && saveManager != null)
                {
                    saveManager.ApplyDeferredRestoration();
                    _isRestoringFromSave = false;
                }

                LoadingScreenOverlay.Hide();
                }
                finally
                {
                if (zombieSuppressionHeld)
                    ApplyZombieLoadingSuppression(false, "world session bootstrap finalize");

                    HandleWorldSessionStartupFailure(
                        "World session startup aborted before completion. Returning to main menu.");

                _worldSessionStarting = false;
                _isRestoringFromSave = false;
                }
        }

        private IEnumerator RunStagedLoadingScreenPrewarm(PlayerSpawner spawner)
        {
            var budgetMs = Mathf.Clamp(loadingScreenPrewarmBudgetMs, 0.5f, 8f);
            var maxSteps = Mathf.Clamp(loadingScreenPrewarmMaxStepsPerFrame, 1, 64);

            for (var guardFrames = 0; guardFrames < 600; guardFrames++)
            {
                if (spawner == null && guardFrames % 20 == 0)
                    spawner = FindFirstSpawnerInLoadedScenes();

                var frameStart = Time.realtimeSinceStartup;
                var steps = 0;
                var allDone = false;

                while (steps < maxSteps)
                {
                    allDone = RunLoadingScreenPrewarmStep(spawner);
                    steps++;
                    if (allDone) break;

                    var elapsedMs = (Time.realtimeSinceStartup - frameStart) * 1000f;
                    if (elapsedMs >= budgetMs) break;
                }

                if (allDone) yield break;

                var nudgedProgress = Mathf.Clamp(
                    LoadingProgressPrewarmStart + Mathf.Min(0.02f, guardFrames * 0.0002f),
                    0f,
                    LoadingProgressProvisionalPlayerStart - 0.001f);
                LoadingScreenOverlay.SetProgress(nudgedProgress, "Preloading critical assets...");
                yield return null;
            }
        }

        private bool RunLoadingScreenPrewarmStep(PlayerSpawner spawner)
        {
            var allDone = true;

            if (spawner != null)
                allDone &= spawner.TryWarmupCriticalPrefabsStep();

            if (zombieManager != null)
                allDone &= zombieManager.TryRunLoadingScreenPrewarmStep();

            return allDone;
        }

        private static IEnumerator EnsureProvisionalPlayerRegisteredForWorldInit(PlayerSpawner spawner)
        {
            spawner ??= FindFirstSpawnerInLoadedScenes();
            if (spawner == null) yield break;

            spawner.EnsureProvisionalPlayerForWorldSession();

            var start = Time.unscaledTime;

            while (Time.unscaledTime - start < ProvisionalPlayerSpawnTimeoutSeconds)
            {
                if (spawner.SpawnedPlayer != null) yield break;

                yield return SpawnPollWait;
            }

            Debug.LogWarning(
                "[GameManager] Timed out waiting for PlayerSpawner to register a provisional player before InitializeWorld. " +
                "World streaming may briefly use a fallback origin until the player exists.",
                spawner);
        }

        private void ApplyZombieLoadingSuppression(bool suppressed, string reason)
        {
            var manager = ResolveZombieManagerForLoading();
            if (manager == null) return;

            manager.SetRuntimeSpawningSuppressed(suppressed);

            if (!logLoadingStageDiagnostics) return;

            Debug.Log(
                "[GameManager] Zombie runtime spawning " + (suppressed ? "suppressed" : "released") +
                " (" + reason + ").",
                this);
        }

        private ZombieManager ResolveZombieManagerForLoading()
        {
            zombieManager = ResolveReference(zombieManager);
            if (zombieManager == null)
                zombieManager = FindFirstObjectByType<ZombieManager>();

            return zombieManager;
        }

        private static bool IsZombieLoadingStageReady(ZombieManager manager)
        {
            if (manager == null) return true;
            if (!manager.IsInitialized) return false;
            return !manager.IsRuntimeSpawningSuppressed;
        }
    }
}
