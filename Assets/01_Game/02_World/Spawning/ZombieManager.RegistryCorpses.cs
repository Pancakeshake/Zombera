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
using Zombera.Debugging.DebugLogging;
using Zombera.World;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.Systems
{
    public sealed partial class ZombieManager
    {
        private void AddZombieToRegistry(ZombieController zombie)
        {
            if (zombie == null) return;

            if (_activeZombies.Add(zombie))
                _activeZombieRegistryVersion++;
        }

        private void RemoveZombieFromRegistry(ZombieController zombie)
        {
            if (zombie == null) return;

            if (_activeZombies.Remove(zombie))
                _activeZombieRegistryVersion++;
        }

        private List<ZombieController> CopyActiveZombies(List<ZombieController> result = null)
        {
            var zombies = result ?? new List<ZombieController>(_activeZombies.Count);
            zombies.Clear();

            foreach (var zombie in _activeZombies)
                if (zombie != null)
                    zombies.Add(zombie);

            return zombies;
        }

        private void BootstrapFromWorldState()
        {
            var sceneZombies = FindObjectsByType<ZombieController>(FindObjectsSortMode.None);

            foreach (var zombie in sceneZombies)
                if (zombie != null)
                    AddZombieToRegistry(zombie);

            if (sceneZombies.Length > 0)
                DebugLogger.LogTrace(
                    LogCategory.World,
                    $"[ZombieManager] Bootstrapped {sceneZombies.Length} zombie(s) from world state.",
                    this);
        }

        private void ProcessPendingDeadZombieReturns()
        {
            if (_pendingDeadZombieReturns == null || _pendingDeadZombieReturns.Count == 0) return;

            _pendingDeadZombieReturnBuffer.Clear();

            foreach (var (zombie, returnAt) in _pendingDeadZombieReturns)
            {
                if (zombie == null)
                {
                    _pendingDeadZombieReturnBuffer.Add(zombie);
                    continue;
                }

                var health = zombie.GetComponent<UnitHealth>();
                if (health is { IsDead: false })
                {
                    _pendingDeadZombieReturnBuffer.Add(zombie);
                    continue;
                }

                if (Time.time < returnAt) continue;

                _pendingDeadZombieReturnBuffer.Add(zombie);
                ReturnZombieToPoolOrDestroy(zombie);
            }

            foreach (var zombie in _pendingDeadZombieReturnBuffer)
                _pendingDeadZombieReturns.Remove(zombie);

            _pendingDeadZombieReturnBuffer.Clear();
        }

        private void ReturnZombieToPoolOrDestroy(ZombieController zombie)
        {
            if (zombie == null) return;

            if (zombieSpawner != null)
            {
                zombieSpawner.ReturnToPool(zombie);
                return;
            }

            Destroy(zombie.gameObject);
        }

        private void PruneInvalidActiveZombies()
        {
            if (_activeZombies.Count <= 0) return;

            _invalidZombieBuffer.Clear();

            foreach (var zombie in _activeZombies)
                if (zombie == null)
                    _invalidZombieBuffer.Add(zombie);

            foreach (var zombie in _invalidZombieBuffer)
                _activeZombies.Remove(zombie);

            if (_invalidZombieBuffer.Count > 0)
                _activeZombieRegistryVersion++;

            _invalidZombieBuffer.Clear();
        }
    }
}
