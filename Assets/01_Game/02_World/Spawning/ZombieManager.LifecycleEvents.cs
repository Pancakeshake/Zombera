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
        private void Awake()
        {
            _spawnValidationPath = new NavMeshPath();
            EnsureRuntimeDependenciesResolved();
        }

        private void Update()
        {
            if (!IsInitialized) return;

            ProcessPendingDeadZombieReturns();

            if (enableRealtimeAmbientSpawning && TryResolvePlayerPosition(out var playerPosition))
                TickAmbientSpawnFromSchedulers(playerPosition);
        }

        private void OnZombieSpawned(ZombieSpawnedEvent gameEvent)
        {
            if (gameEvent.Zombie == null) return;

            var zombie = gameEvent.Zombie.GetComponent<ZombieController>();
            if (zombie == null) return;

            RegisterZombie(zombie);
            _pendingDeadZombieReturns.Remove(zombie);
        }

        private void OnUnitDeath(UnitDeathEvent gameEvent)
        {
            if (gameEvent.Role != UnitRole.Zombie) return;

            var zombie = gameEvent.UnitObject != null ? gameEvent.UnitObject.GetComponent<ZombieController>() : null;

            if (zombie == null) return;

            UnregisterZombie(zombie);

            if (!keepDeadZombieCorpses)
            {
                ReturnZombieToPoolOrDestroy(zombie);
                return;
            }

            var returnDelay = Mathf.Max(0f, deadZombieReturnDelaySeconds);

            if (returnDelay <= 0f)
            {
                // Keep corpse indefinitely unless explicitly cleaned up elsewhere.
                _pendingDeadZombieReturns.Remove(zombie);
                return;
            }

            _pendingDeadZombieReturns[zombie] = Time.time + returnDelay;
        }

        private void OnWorldSimulationTick(WorldSimulationTickEvent gameEvent)
        {
            TickAmbientSpawnFromSchedulers(gameEvent.PlayerPosition);
        }

        private void TickAmbientSpawnFromSchedulers(Vector3 playerPosition)
        {
            // Intentional dual trigger: Update provides realtime pressure while simulation tick
            // ensures ambient spawning still progresses during fixed-step world updates.
            // CanEvaluateAmbientSpawn() handles cadence throttling to avoid spawn-rate drift.
            TickAmbientSpawn(playerPosition);
        }
    }
}
