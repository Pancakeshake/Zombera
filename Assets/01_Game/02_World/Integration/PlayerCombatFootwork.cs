#region

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Combat;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Drives compact tactical footwork during melee combat.
    ///     Keeps the player close to their active encounter opponent with short, controlled
    ///     adjustments and a small pressure backstep when crowded.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombatFootwork : MonoBehaviour
    {
        [Header("Detection")] [SerializeField] [Min(0f)]
        private float scanRadius = 8f;

        [Header("Preferred Range")] [Tooltip("Retreat if closer than this.")] [SerializeField] [Min(0f)]
        private float preferredRangeMin = 0.82f;

        [Tooltip("Approach if farther than this.")] [SerializeField] [Min(0f)]
        private float preferredRangeMax = 1.08f;

        [Header("Footwork Timing")]
        [Tooltip("Minimum seconds between footwork ticks (holds/darts).")]
        [SerializeField]
        [Min(0.1f)]
        private float intervalMin = 0.55f;

        [SerializeField] [Min(0.1f)] private float intervalMax = 1.05f;

        [Header("Dart In / Retreat")]
        [Tooltip("Chance 0-1 that a tick will dart toward the enemy rather than hold.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float dartChance = 0.16f;

        [Tooltip("How far to dart toward the enemy.")] [SerializeField] [Min(0f)]
        private float dartDistance = 0.14f;

        [Tooltip("Seconds after darting in before auto-retreating back.")] [SerializeField] [Min(0.1f)]
        private float dartRetreatDelaySeconds = 0.18f;

        [Tooltip("How far to retreat after a dart.")] [SerializeField] [Min(0f)]
        private float retreatDistance = 0.2f;

        [Header("Pressure Retreat")]
        [Tooltip("How far to step back when an enemy is inside preferredRangeMin.")]
        [SerializeField]
        [Min(0f)]
        private float pressureRetreatDistance = 0.22f;

        [Header("NavMesh")] [SerializeField] [Min(0.1f)]
        private float navSampleRadius = 2f;

        [Header("Player Input Suppression")] [SerializeField] [Min(0f)]
        private float playerMoveSuppressSeconds = 0.8f;

        [Header("Enemy Facing")] [SerializeField] [Min(0f)]
        private float faceEnemyDegreesPerSecond = 720f;

        [Header("Encounter Gate")] [SerializeField]
        private bool requireActiveEncounter = true;

        [SerializeField] private CombatEncounterManager encounterManager;
        [SerializeField] private PlayerInputController playerInputController;

        private readonly List<Unit> _nearbyBuffer = new(16);
        private UnitController _controller;
        private float _dartRetreatAt = -1f;
        private Vector3 _dartRetreatDir;
        private float _nextFootworkAt;
        private float _playerIssuedMoveAt = -999f;

        private Unit _playerUnit;
        private bool _suppressFacingUntilMoveOrderCompletes;

        private void Awake()
        {
            _playerUnit = GetComponent<Unit>();
            _controller = GetComponent<UnitController>();
            if (encounterManager == null)
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : FindFirstObjectByType<CombatEncounterManager>();

            if (playerInputController == null) playerInputController = GetComponent<PlayerInputController>();

            _nextFootworkAt = Time.time + Random.Range(intervalMin, intervalMax);
        }

        private void Update()
        {
            if (_controller == null || _playerUnit == null || !_playerUnit.IsAlive) return;

            if ((playerInputController != null && playerInputController.IsRangedCombatActive)
                || IsMoveOrderFacingSuppressed())
            {
                _dartRetreatAt = -1f;
                return;
            }

            var nearestEnemy = ResolveCombatTarget();

            if (nearestEnemy == null)
            {
                _dartRetreatAt = -1f;
                return;
            }

            // Always face the nearest enemy.
            var faceTurnSpeed = CombatTuningConfig.FacingTurnSpeedOr(faceEnemyDegreesPerSecond);
            if (faceTurnSpeed > 0f)
                _controller.RotateTowardsPosition(nearestEnemy.transform.position, faceTurnSpeed);

            if (Time.time - _playerIssuedMoveAt < playerMoveSuppressSeconds) return;
            if (_playerUnit.Stats != null && _playerUnit.Stats.CurrentPosture != PostureState.Upright) return;

            // Handle deferred retreat after a dart-in.
            if (_dartRetreatAt > 0f && Time.time >= _dartRetreatAt)
            {
                _dartRetreatAt = -1f;
                ExecuteMove(_dartRetreatDir, retreatDistance);
                return;
            }

            if (Time.time < _nextFootworkAt) return;
            _nextFootworkAt = Time.time + Random.Range(intervalMin, intervalMax);

            PerformFootworkTick(nearestEnemy);
        }

        /// <summary>Call whenever the player manually issues a move command.</summary>
        public void NotifyPlayerIssuedMove()
        {
            _playerIssuedMoveAt = Time.time;
            _dartRetreatAt = -1f;
            _suppressFacingUntilMoveOrderCompletes = true;
            _nextFootworkAt = Time.time + Mathf.Max(playerMoveSuppressSeconds,
                Random.Range(intervalMin, intervalMax));
        }

        private bool IsMoveOrderFacingSuppressed()
        {
            if (!_suppressFacingUntilMoveOrderCompletes) return false;

            if (_controller == null)
            {
                _suppressFacingUntilMoveOrderCompletes = false;
                return false;
            }

            var moveOrderStillActive = _controller.HasMoveTarget || _controller.IsMoving;
            if (moveOrderStillActive) return true;

            _suppressFacingUntilMoveOrderCompletes = false;
            return false;
        }

        private Unit ResolveCombatTarget()
        {
            if (_playerUnit == null) return null;

            encounterManager ??= CombatEncounterManager.Instance != null
                ? CombatEncounterManager.Instance
                : FindFirstObjectByType<CombatEncounterManager>();

            if (encounterManager != null
                && encounterManager.IsUnitInEncounter(_playerUnit)
                && encounterManager.TryGetEncounterOpponent(_playerUnit, out var encounterOpponent)
                && encounterOpponent != null
                && encounterOpponent.IsAlive)
                return encounterOpponent;

            return requireActiveEncounter ? null : FindNearestEnemy();
        }

        private void PerformFootworkTick(Unit enemy)
        {
            var toEnemy = enemy.transform.position - transform.position;
            toEnemy.y = 0f;
            var dist = toEnemy.magnitude;
            if (dist < 0.01f) return;

            var dirToEnemy = toEnemy / dist;

            // Snap face toward enemy on each tick.
            _controller.RotateTowardsPosition(enemy.transform.position, 0f);

            if (dist < preferredRangeMin)
            {
                // Pressured — step back immediately.
                ExecuteMove(-dirToEnemy, pressureRetreatDistance);
                return;
            }

            // In range or too far: maybe dart in, otherwise hold.
            var tooFar = dist > preferredRangeMax;
            var doDart = tooFar || Random.value < dartChance;

            if (!doDart) return;

            // Dart toward the enemy, then schedule an auto-retreat.
            if (!ExecuteMove(dirToEnemy, dartDistance)) return;

            _dartRetreatAt = Time.time + dartRetreatDelaySeconds;
            _dartRetreatDir = -dirToEnemy;
            // else: hold — do nothing this tick, just keep facing.
        }

        /// <summary>Issues a move to the nearest valid navmesh point in dir*distance. Returns true if a position was found.</summary>
        private bool ExecuteMove(Vector3 dir, float distance)
        {
            var target = transform.position + dir * distance;
            if (!NavMesh.SamplePosition(target + Vector3.up * 0.5f, out var hit, navSampleRadius, NavMesh.AllAreas))
                return false;

            _controller.MoveTo(hit.position);
            return true;
        }

        private Unit FindNearestEnemy()
        {
            if (UnitManager.Instance == null) return null;

            _nearbyBuffer.Clear();
            UnitManager.Instance.FindNearbyEnemies(_playerUnit, scanRadius, _nearbyBuffer);

            Unit nearest = null;
            var nearestSq = float.MaxValue;

            for (var i = 0; i < _nearbyBuffer.Count; i++)
            {
                var e = _nearbyBuffer[i];
                if (e == null || e.Health == null || e.Health.IsDead) continue;
                var sq = (e.transform.position - transform.position).sqrMagnitude;
                if (sq >= nearestSq) continue;

                nearestSq = sq;
                nearest = e;
            }

            return nearest;
            }
    }
}