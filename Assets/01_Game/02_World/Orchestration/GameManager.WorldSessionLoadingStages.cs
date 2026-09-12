using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.UI.Menus;
using Zombera.World;

namespace Zombera.Core
{
    public sealed partial class GameManager
    {
        private IEnumerator WaitForWorldDependencyLoadingStage(
            WorldLoadingStage loadingStage,
            CharacterSpawnDependencyStage dependencyStage,
            float timeoutSeconds,
            Func<float, string> waitingStatusProvider,
            Func<string> bottleneckSummaryProvider)
        {
            yield return WaitForLoadingStageReadiness(
                loadingStage,
                timeoutSeconds,
                () => IsWorldDependencyStageReady(dependencyStage),
                waitingStatusProvider,
                bottleneckSummaryProvider);
        }

        private bool IsWorldDependencyStageReady(CharacterSpawnDependencyStage dependencyStage)
        {
            if (worldManager == null) return true;
            if (!worldManager.UseProceduralStreamingWorld) return true;
            return worldManager.IsCharacterSpawnDependencyStageReady(dependencyStage);
        }

        private IEnumerator WaitForLoadingStageReadiness(
            WorldLoadingStage stage,
            float timeoutSeconds,
            Func<bool> readinessCheck,
            Func<float, string> waitingStatusProvider,
            Func<string> bottleneckSummaryProvider)
        {
            var timeout = Mathf.Max(0f, timeoutSeconds);
            var pollSeconds = Mathf.Max(0.05f, loadingStagePollSeconds);
            var pollWait = new WaitForSecondsRealtime(pollSeconds);
            var stageStartedAt = Time.unscaledTime;
            var stageStartProgress = GetLoadingStageProgressStart(stage);
            var stageEndProgress = GetLoadingStageProgressEnd(stage);
            var defaultStatus = GetLoadingStageStatus(stage);
            var lastProgress = stageStartProgress;
            var lastStatus = string.Empty;
            var timedOut = false;

            LogLoadingStageStart(stage, bottleneckSummaryProvider?.Invoke());
            LoadingScreenOverlay.SetProgress(stageStartProgress, defaultStatus);

            while (!(readinessCheck?.Invoke() ?? true))
            {
                var elapsed = Time.unscaledTime - stageStartedAt;
                if (timeout > 0f && elapsed >= timeout)
                {
                    timedOut = true;
                    break;
                }

                var normalized = timeout > 0f ? Mathf.Clamp01(elapsed / timeout) : 0f;
                var provisionalStageEnd = Mathf.Max(stageStartProgress, stageEndProgress - 0.0005f);
                var progress = Mathf.Lerp(stageStartProgress, provisionalStageEnd, normalized * 0.92f);
                progress = Mathf.Max(lastProgress, progress);

                var status = waitingStatusProvider != null
                    ? waitingStatusProvider(elapsed)
                    : defaultStatus;
                if (string.IsNullOrWhiteSpace(status)) status = defaultStatus;

                if (progress > lastProgress + 0.0005f || !string.Equals(status, lastStatus, StringComparison.Ordinal))
                {
                    LoadingScreenOverlay.SetProgress(progress, status);
                    lastProgress = progress;
                    lastStatus = status;
                }

                yield return pollWait;
            }

            var totalElapsed = Mathf.Max(0f, Time.unscaledTime - stageStartedAt);
            var finalStatus = timedOut ? defaultStatus + " delayed, continuing..." : defaultStatus + " complete";
            var finalProgress = timedOut
                ? Mathf.Max(lastProgress, Mathf.Lerp(stageStartProgress, stageEndProgress, 0.96f))
                : stageEndProgress;
            LoadingScreenOverlay.SetProgress(finalProgress, finalStatus);
            LogLoadingStageCompletion(stage, totalElapsed, timedOut, bottleneckSummaryProvider?.Invoke());
        }

        private IEnumerator WaitForNavMeshLoadingStage(PlayerSpawner spawner)
        {
            var stage = WorldLoadingStage.NavMesh;
            var timeout = Mathf.Max(0f, navMeshStageTimeoutSeconds);
            var pollSeconds = Mathf.Max(0.05f, loadingStagePollSeconds);
            var pollWait = new WaitForSecondsRealtime(pollSeconds);
            var stageStartedAt = Time.unscaledTime;
            var stageStartProgress = GetLoadingStageProgressStart(stage);
            var stageEndProgress = GetLoadingStageProgressEnd(stage);
            var defaultStatus = GetLoadingStageStatus(stage);
            var lastProgress = stageStartProgress;
            var lastStatus = string.Empty;
            var timedOut = false;
            var nextRebuildRequestAt = 0f;

            var navMeshService = FindFirstObjectByType<StreamingNavMeshTileService>();

            LogLoadingStageStart(stage, BuildNavMeshStageBottleneckSummary(navMeshService));
            LoadingScreenOverlay.SetProgress(stageStartProgress, defaultStatus);

            while (true)
            {
                var focusPosition = ResolveNavMeshFocusPosition(spawner);
                var navMeshReady = !worldManager || !worldManager.UseProceduralStreamingWorld ||
                                   HasNearbyNavMesh(focusPosition);
                if (navMeshReady) break;

                var now = Time.unscaledTime;
                var elapsed = now - stageStartedAt;
                if (timeout > 0f && elapsed >= timeout)
                {
                    timedOut = true;
                    break;
                }

                if (navMeshService != null
                    && now >= nextRebuildRequestAt
                    && navMeshService.PendingTileBakeCount == 0
                    && !navMeshService.IsBootstrapRebuildInProgress)
                {
                    navMeshService.RebuildAllDeployedTilesInPlayerScene(focusPosition);
                    nextRebuildRequestAt = now + Mathf.Max(0.5f, pollSeconds * 8f);
                }

                var normalized = timeout > 0f ? Mathf.Clamp01(elapsed / timeout) : 0f;
                var provisionalStageEnd = Mathf.Max(stageStartProgress, stageEndProgress - 0.0005f);
                var progress = Mathf.Lerp(stageStartProgress, provisionalStageEnd, normalized * 0.92f);
                progress = Mathf.Max(lastProgress, progress);
                var status = BuildNavMeshStageStatus(elapsed, navMeshService);

                if (progress > lastProgress + 0.0005f || !string.Equals(status, lastStatus, StringComparison.Ordinal))
                {
                    LoadingScreenOverlay.SetProgress(progress, status);
                    lastProgress = progress;
                    lastStatus = status;
                }

                yield return pollWait;
            }

            var totalElapsed = Mathf.Max(0f, Time.unscaledTime - stageStartedAt);
            var finalStatus = timedOut ? defaultStatus + " delayed, continuing..." : defaultStatus + " complete";
            var finalProgress = timedOut
                ? Mathf.Max(lastProgress, Mathf.Lerp(stageStartProgress, stageEndProgress, 0.96f))
                : stageEndProgress;
            LoadingScreenOverlay.SetProgress(finalProgress, finalStatus);
            LogLoadingStageCompletion(stage, totalElapsed, timedOut, BuildNavMeshStageBottleneckSummary(navMeshService));
        }

        private IEnumerator WaitForPlayerLoadingStage(PlayerSpawner spawner)
        {
            if (spawner != null)
            {
                // Signal the spawner to begin its final world-snap and agent-enable logic now 
                // that terrain, roads, and buildings are confirmed ready (or timed out).
                spawner.RequestFinalWorldSpawn();
            }

            var stage = WorldLoadingStage.Players;
            var stageStartedAt = Time.unscaledTime;
            var stageStartProgress = GetLoadingStageProgressStart(stage);
            var stageEndProgress = GetLoadingStageProgressEnd(stage);
            var spawnFinalizeProgress = Mathf.Lerp(stageStartProgress, stageEndProgress, 0.72f);

            LogLoadingStageStart(stage, BuildPlayerStageBottleneckSummary(spawner));
            LoadingScreenOverlay.SetProgress(stageStartProgress, GetLoadingStageStatus(stage));

            yield return WaitForFinalWorldPlayerSpawnIfPresent(
                spawner,
                stageStartProgress,
                spawnFinalizeProgress,
                "Spawning players...",
                false);

            LoadingScreenOverlay.SetProgress(Mathf.Max(stageStartProgress, spawnFinalizeProgress),
                "Finalizing player visuals...");
            yield return WaitForWorldCharacterVisualsReady(
                spawner,
                spawnFinalizeProgress,
                stageEndProgress,
                "Finalizing player visuals...");

            var playerFinalized = spawner == null || spawner.HasFinalizedWorldPlayerSpawn;
            var elapsed = Mathf.Max(0f, Time.unscaledTime - stageStartedAt);
            LogLoadingStageCompletion(stage, elapsed, !playerFinalized, BuildPlayerStageBottleneckSummary(spawner));
        }

        private IEnumerator WaitForZombieLoadingStage()
        {
            var stage = WorldLoadingStage.Zombies;
            var timeout = Mathf.Max(0f, zombieStageTimeoutSeconds);
            var pollSeconds = Mathf.Max(0.05f, loadingStagePollSeconds);
            var pollWait = new WaitForSecondsRealtime(pollSeconds);
            var stageStartedAt = Time.unscaledTime;
            var stageStartProgress = GetLoadingStageProgressStart(stage);
            var stageEndProgress = GetLoadingStageProgressEnd(stage);
            var defaultStatus = GetLoadingStageStatus(stage);
            var lastProgress = stageStartProgress;
            var lastStatus = string.Empty;
            var timedOut = false;

            var manager = ResolveZombieManagerForLoading();

            LogLoadingStageStart(stage, BuildZombieStageBottleneckSummary(manager));
            LoadingScreenOverlay.SetProgress(stageStartProgress, defaultStatus);

            while (!IsZombieLoadingStageReady(manager))
            {
                var elapsed = Time.unscaledTime - stageStartedAt;
                if (timeout > 0f && elapsed >= timeout)
                {
                    timedOut = true;
                    break;
                }

                var normalized = timeout > 0f ? Mathf.Clamp01(elapsed / timeout) : 0f;
                var provisionalStageEnd = Mathf.Max(stageStartProgress, stageEndProgress - 0.0005f);
                var progress = Mathf.Lerp(stageStartProgress, provisionalStageEnd, normalized * 0.92f);
                progress = Mathf.Max(lastProgress, progress);
                var status = BuildZombieStageStatus(elapsed, manager);

                if (progress > lastProgress + 0.0005f || !string.Equals(status, lastStatus, StringComparison.Ordinal))
                {
                    LoadingScreenOverlay.SetProgress(progress, status);
                    lastProgress = progress;
                    lastStatus = status;
                }

                yield return pollWait;
                manager = ResolveZombieManagerForLoading();
            }

            var totalElapsed = Mathf.Max(0f, Time.unscaledTime - stageStartedAt);
            var finalStatus = timedOut ? defaultStatus + " delayed, continuing..." : defaultStatus + " complete";
            var finalProgress = timedOut
                ? Mathf.Max(lastProgress, Mathf.Lerp(stageStartProgress, stageEndProgress, 0.96f))
                : stageEndProgress;
            LoadingScreenOverlay.SetProgress(finalProgress, finalStatus);
            LogLoadingStageCompletion(stage, totalElapsed, timedOut, BuildZombieStageBottleneckSummary(manager));
        }

        private static IEnumerator WaitForFinalWorldPlayerSpawnIfPresent(
            PlayerSpawner spawner,
            float progressStart,
            float progressEnd,
            string defaultStatus,
            bool includeOrderedDependencyStatus)
        {
            spawner ??= FindFirstSpawnerInLoadedScenes();
            if (spawner == null) yield break;

            var boundedStart = Mathf.Clamp01(progressStart);
            var boundedEnd = Mathf.Clamp01(Mathf.Max(progressStart, progressEnd));
            var statusPrefix = string.IsNullOrWhiteSpace(defaultStatus) ? "Spawning players..." : defaultStatus;
            var start = Time.unscaledTime;
            var lastProgress = boundedStart;
            var lastStatus = string.Empty;

            while (Time.unscaledTime - start < FinalWorldPlayerSpawnTimeoutSeconds)
            {
                if (spawner.HasFinalizedWorldPlayerSpawn) yield break;

                var elapsed = Time.unscaledTime - start;
                float progress;
                var status = statusPrefix;

                if (includeOrderedDependencyStatus
                    && spawner.TryGetOrderedSpawnDependencyProgress(out var orderedProgress, out var orderedStatus))
                {
                    progress = Mathf.Lerp(boundedStart, boundedEnd, Mathf.Clamp01(orderedProgress));
                    if (!string.IsNullOrWhiteSpace(orderedStatus)) status = orderedStatus;
                }
                else
                {
                    var fallbackProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, FinalWorldPlayerSpawnTimeoutSeconds));
                    progress = Mathf.Lerp(boundedStart, boundedEnd, fallbackProgress);

                    if (spawner.SpawnedPlayer == null)
                        status = "Registering player...";
                }

                progress = Mathf.Max(lastProgress, progress);
                if (progress > lastProgress + 0.0005f || !string.Equals(status, lastStatus, StringComparison.Ordinal))
                {
                    LoadingScreenOverlay.SetProgress(progress, status);
                    lastProgress = progress;
                    lastStatus = status;
                }

                // If we at least have a provisional player for a while, don't stall loading indefinitely.
                if (spawner.SpawnedPlayer != null && Time.unscaledTime - start >= 15f)
                {
                    LoadingScreenOverlay.SetProgress(Mathf.Max(lastProgress, boundedEnd),
                        "Player registration took too long, continuing...");
                    Debug.LogWarning(
                        "[GameManager] Finalized world spawn did not complete within 15s, but provisional player exists. Continuing startup.",
                        spawner);
                    yield break;
                }

                yield return SpawnPollWait;
            }

            Debug.LogWarning(
                "[GameManager] Timed out waiting for PlayerSpawner to finalize world spawn (NavMesh snap / agent / UI wiring). " +
                "Continuing session startup; gameplay may be partially uninitialized.",
                spawner);

            LoadingScreenOverlay.SetProgress(Mathf.Max(lastProgress, boundedEnd),
                "Player spawn timeout reached, continuing...");
        }

        private IEnumerator WaitForWorldCharacterVisualsReady(
            PlayerSpawner spawner,
            float progressStart,
            float progressEnd,
            string statusPrefix)
        {
            // Goal: keep the loading overlay up until the player + startup squad have finished their appearance builds,
            // so we don't see units popping from "default" to "styled" after the world becomes interactive.
            spawner ??= FindFirstSpawnerInLoadedScenes();
            var boundedStart = Mathf.Clamp01(progressStart);
            var boundedEnd = Mathf.Clamp01(Mathf.Max(progressStart, progressEnd));
            var labelPrefix = string.IsNullOrWhiteSpace(statusPrefix) ? "Generating characters..." : statusPrefix;
            var start = Time.unscaledTime;
            var lastReady = -1;
            var lastDisplayedTarget = -1;
            var timedOut = true;

            while (Time.unscaledTime - start < CharacterVisualsReadyTimeoutSeconds)
            {
                var expectedRoster = spawner != null ? spawner.GetExpectedStartupSquadTotalRosterCount() : 1;

                CountTrackedWorldRosterVisualReadiness(spawner, out var targets, out var ready);

                // When startup squad is deferred-spawned, we must wait for the roster to actually exist
                // (otherwise we'd exit early before squadmates even spawn).
                if (targets >= expectedRoster && ready >= targets)
                {
                    timedOut = false;
                    break;
                }

                // Provide gentle progress feedback without fighting the existing smoothing.
                var denom = Mathf.Max(1f, expectedRoster);
                var normalized = Mathf.Clamp01(ready / denom);
                var displayed = Mathf.Lerp(boundedStart, boundedEnd, normalized);
                var displayedTarget = Mathf.Max(targets, expectedRoster);
                if (ready != lastReady || displayedTarget != lastDisplayedTarget)
                {
                    LoadingScreenOverlay.SetProgress(displayed,
                        labelPrefix + " (" + ready + "/" + displayedTarget + ")");
                    lastReady = ready;
                    lastDisplayedTarget = displayedTarget;
                }

                yield return CharacterVisualsPollWait;
            }

            if (timedOut)
            {
                Debug.LogWarning(
                    "[GameManager] Timed out waiting for world character visuals to finish. Continuing startup.",
                    this);
                LoadingScreenOverlay.SetProgress(boundedEnd, labelPrefix + " delayed, continuing...");
            }
            else
            {
                LoadingScreenOverlay.SetProgress(boundedEnd, labelPrefix + " complete");
            }
        }
    }
}
