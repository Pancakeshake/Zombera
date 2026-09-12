#region

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World;

#endregion

namespace Zombera.Characters
{
    public sealed class WorldSpawnCoordinator
    {
        private readonly MonoBehaviour _host;
        private readonly System.Action _onSpawnPlayer;
        private readonly System.Func<WorldManager> _worldManagerProvider;

        private bool _active;
        private float _totalTargetSeconds;
        private float _completedTargetSeconds;
        private float _currentStageStartedAt;
        private float _currentStageTargetSeconds;
        private string _currentStageLabel;

        private static readonly CharacterSpawnDependencyStage[] DependencyStages =
        {
            CharacterSpawnDependencyStage.Terrain,
            CharacterSpawnDependencyStage.Roads,
            CharacterSpawnDependencyStage.Buildings,
            CharacterSpawnDependencyStage.NavMesh,
            CharacterSpawnDependencyStage.Objects
        };

        public WorldSpawnCoordinator(
            MonoBehaviour host, 
            System.Action onSpawnPlayer, 
            System.Func<WorldManager> worldManagerProvider)
        {
            _host = host;
            _onSpawnPlayer = onSpawnPlayer;
            _worldManagerProvider = worldManagerProvider;
        }

        public bool TryGetProgress(out float progress01, out string status)
        {
            if (!_active)
            {
                progress01 = 0f;
                status = string.Empty;
                return false;
            }

            var stageElapsed = Time.unscaledTime - _currentStageStartedAt;
            var stageProgress = _currentStageTargetSeconds > 0f 
                ? Mathf.Clamp01(stageElapsed / _currentStageTargetSeconds) 
                : 1f;

            var weightedCurrentStage = _currentStageTargetSeconds * stageProgress;
            var totalElapsedWeighted = _completedTargetSeconds + weightedCurrentStage;

            progress01 = _totalTargetSeconds > 0f 
                ? Mathf.Clamp01(totalElapsedWeighted / _totalTargetSeconds) 
                : 1f;
            status = _currentStageLabel;
            return true;
        }

        public void ResetTracking()
        {
            _active = false;
            _totalTargetSeconds = 0f;
            _completedTargetSeconds = 0f;
            _currentStageStartedAt = 0f;
            _currentStageTargetSeconds = 0f;
            _currentStageLabel = string.Empty;
        }

        public IEnumerator FinalizeWorldSpawnRoutine(
            bool deferUntilReady,
            float fallbackTimeout,
            float pollSeconds,
            float terrainTarget,
            float roadTarget,
            float buildingTarget,
            float navMeshTarget,
            float objectTarget)
        {
            ResetTracking();

            if (deferUntilReady)
            {
                var worldManager = _worldManagerProvider?.Invoke();
                if (worldManager != null && worldManager.EnforceOrderedCharacterSpawn)
                {
                    BeginTracking(terrainTarget, roadTarget, buildingTarget, navMeshTarget, objectTarget);

                    var totalTimeout = worldManager.MaxSecondsToWaitForSpawnDependencyOrder;
                    if (totalTimeout <= 0f) totalTimeout = Mathf.Max(0f, fallbackTimeout);

                    var stageTargetBudget = terrainTarget + roadTarget + buildingTarget + navMeshTarget + objectTarget;

                    if (stageTargetBudget > 0f)
                    {
                        var boundedTimeout = stageTargetBudget + Mathf.Max(1f, pollSeconds * 2f);
                        totalTimeout = totalTimeout > 0f ? Mathf.Min(totalTimeout, boundedTimeout) : boundedTimeout;
                    }

                    var pollWait = new WaitForSecondsRealtime(Mathf.Max(0.05f, pollSeconds));
                    var orderedStart = Time.unscaledTime;
                    var skippedStages = new List<string>(4);

                    var stopOrderedStageLoop = false;
                    for (var i = 0; i < DependencyStages.Length; i++)
                    {
                        var stage = DependencyStages[i];
                        var stageLabel = WorldManager.GetCharacterSpawnDependencyStageLabel(stage);
                        var stageTimeout = ResolveStageTimeout(stage, terrainTarget, roadTarget, buildingTarget, navMeshTarget, objectTarget);
                        
                        BeginStage(stageLabel, stageTimeout);

                        if (stageTimeout <= 0f)
                        {
                            AddSkippedStage(skippedStages, stageLabel);
                            LogStageSkip(stageLabel, 0f, stageTimeout, "stage target set to zero");
                            CompleteStage(0f);
                            continue;
                        }

                        var stageStart = Time.unscaledTime;
                        var stageReady = worldManager.IsCharacterSpawnDependencyStageReady(stage);

                        while (!stageReady)
                        {
                            var now = Time.unscaledTime;
                            var stageElapsed = now - stageStart;
                            var totalElapsed = now - orderedStart;

                            if (totalTimeout > 0f && totalElapsed >= totalTimeout)
                            {
                                AddSkippedStage(skippedStages, stageLabel);
                                LogStageSkip(stageLabel, stageElapsed, stageTimeout, "global ordered-spawn timeout reached");
                                CompleteStage(Mathf.Min(stageElapsed, stageTimeout));

                                for (var r = i + 1; r < DependencyStages.Length; r++)
                                {
                                    var remainingStage = DependencyStages[r];
                                    var remainingLabel = WorldManager.GetCharacterSpawnDependencyStageLabel(remainingStage);
                                    AddSkippedStage(skippedStages, remainingLabel);
                                }

                                stopOrderedStageLoop = true;
                                break;
                            }

                            if (stageElapsed >= stageTimeout)
                            {
                                AddSkippedStage(skippedStages, stageLabel);
                                LogStageSkip(stageLabel, stageElapsed, stageTimeout, "stage target exceeded");
                                CompleteStage(stageTimeout);
                                break;
                            }

                            yield return pollWait;
                            stageReady = worldManager.IsCharacterSpawnDependencyStageReady(stage);
                        }

                        if (stopOrderedStageLoop) break;

                        if (stageReady) CompleteStage(stageTimeout);
                    }

                    EndTracking();

                    if (skippedStages.Count > 0)
                    {
                        var totalWaitSeconds = Time.unscaledTime - orderedStart;
                        Debug.LogWarning(
                            "[WorldSpawnCoordinator] Ordered spawn continued after skipped stages: " +
                            string.Join(", ", skippedStages) +
                            ". Total ordered wait=" + totalWaitSeconds.ToString("0.00") + "s.",
                            _host);
                    }
                }
            }

            _onSpawnPlayer?.Invoke();
            EndTracking();
        }

        private void BeginTracking(float terrain, float road, float building, float navMesh, float obj)
        {
            _active = true;
            _completedTargetSeconds = 0f;
            _currentStageLabel = string.Empty;
            _currentStageStartedAt = Time.unscaledTime;
            _currentStageTargetSeconds = 0f;
            _totalTargetSeconds = terrain + road + building + navMesh + obj;
        }

        private void BeginStage(string label, float targetSeconds)
        {
            _currentStageLabel = label ?? string.Empty;
            _currentStageStartedAt = Time.unscaledTime;
            _currentStageTargetSeconds = Mathf.Max(0f, targetSeconds);
        }

        private void CompleteStage(float consumedSeconds)
        {
            if (!_active) return;
            _completedTargetSeconds = Mathf.Max(0f, _completedTargetSeconds + Mathf.Max(0f, consumedSeconds));
        }

        private void EndTracking()
        {
            _active = false;
        }

        private float ResolveStageTimeout(CharacterSpawnDependencyStage stage, float terrain, float road, float building, float navMesh, float obj)
        {
            return stage switch
            {
                CharacterSpawnDependencyStage.Terrain => Mathf.Max(0f, terrain),
                CharacterSpawnDependencyStage.Roads => Mathf.Max(0f, road),
                CharacterSpawnDependencyStage.Buildings => Mathf.Max(0f, building),
                CharacterSpawnDependencyStage.NavMesh => Mathf.Max(0f, navMesh),
                CharacterSpawnDependencyStage.Objects => Mathf.Max(0f, obj),
                _ => 0f
            };
        }

        private static void AddSkippedStage(List<string> skippedStages, string stageLabel)
        {
            if (skippedStages == null || string.IsNullOrWhiteSpace(stageLabel)) return;
            if (skippedStages.Contains(stageLabel)) return;
            skippedStages.Add(stageLabel);
        }

        private void LogStageSkip(string stageLabel, float waitedSeconds, float targetSeconds, string reason)
        {
            Debug.LogWarning(
                "[WorldSpawnCoordinator] Ordered spawn stage skipped: '" + stageLabel + "' after " +
                waitedSeconds.ToString("0.00") + "s (target=" + targetSeconds.ToString("0.00") +
                "s, reason=" + reason + ").",
                _host);
        }
    }
}
