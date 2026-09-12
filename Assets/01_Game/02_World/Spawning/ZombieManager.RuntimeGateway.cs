#region

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.AI;
using Zombera.AI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Data;
using Zombera.World;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.Systems
{
    public sealed partial class ZombieManager
    {
        private void EnsureRuntimeDependenciesResolved()
        {
            var runtimeSpawner = ResolveZombieSpawnerDependency();
            QueueRuntimeSpawnerPrewarmIfNeeded(runtimeSpawner);
            ResolveHordeManagerDependency();
        }

        private ZombieSpawner GetRuntimeZombieSpawner()
        {
            EnsureRuntimeDependenciesResolved();
            return zombieSpawner;
        }

        private ZombieHordeManager GetRuntimeHordeManager()
        {
            EnsureRuntimeDependenciesResolved();
            return hordeManager;
        }

        private ZombieSpawner ResolveZombieSpawnerDependency()
        {
            if (zombieSpawner != null)
                return zombieSpawner;

            zombieSpawner = FindFirstObjectByType<ZombieSpawner>();
            if (zombieSpawner == null && autoCreateSpawnerWhenMissing)
            {
                var runtimeSpawner = new GameObject("RuntimeZombieSpawner");
                runtimeSpawner.transform.SetParent(transform, false);
                zombieSpawner = runtimeSpawner.AddComponent<ZombieSpawner>();
            }

            return zombieSpawner;
        }

        private void QueueRuntimeSpawnerPrewarmIfNeeded(ZombieSpawner resolvedSpawner)
        {
            if (resolvedSpawner == null)
                return;

            if (defaultZombiePrefab != null)
                resolvedSpawner.SetZombiePrefab(defaultZombiePrefab);

            if (!prewarmRuntimeSpawnerPool || _runtimeSpawnerPrewarmed || _runtimeSpawnerPrewarmQueued ||
                runtimeSpawnerPrewarmCount <= 0 || !CanQueueRuntimePrewarm())
                return;

            _runtimeSpawnerPrewarmQueued = true;
            StartCoroutine(PrewarmZombieSpawnerPoolDeferred());
        }

        private void ResolveHordeManagerDependency()
        {
            if (hordeManager == null)
                hordeManager = FindFirstObjectByType<ZombieHordeManager>();
        }

        private IEnumerator PrewarmZombieSpawnerPoolDeferred()
        {
            if (zombieSpawner == null)
            {
                _runtimeSpawnerPrewarmQueued = false;
                yield break;
            }

            var target = Mathf.Max(0, runtimeSpawnerPrewarmCount);
            if (target <= 0)
            {
                _runtimeSpawnerPrewarmed = true;
                _runtimeSpawnerPrewarmQueued = false;
                yield break;
            }

            var perFrame = Mathf.Clamp(runtimeSpawnerPrewarmPerFrame, 1, 64);
            while (!_runtimeSpawnerPrewarmed)
            {
                if (ShouldStopPrewarmLoop()) break;

                _runtimeSpawnerPrewarmed = zombieSpawner.PrewarmPoolStep(target, perFrame);
                if (_runtimeSpawnerPrewarmed) break;

                yield return null;
            }

            _runtimeSpawnerPrewarmQueued = false;
        }

        private bool ShouldStopPrewarmLoop()
        {
            return !IsInitialized || zombieSpawner == null;
        }

        private bool CanQueueRuntimePrewarm()
        {
            if (!IsInitialized) return false;

            if (Time.time - _initializationTime < Mathf.Max(0f, runtimeSpawnerPrewarmStartDelaySeconds))
                return false;

            if (!prewarmOnlyDuringGameplayState) return true;

            if (GameManager.Instance is not { CurrentState: var state }) return true;

            return state is GameState.Playing or GameState.Paused;
        }
    }
}
