using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Combat;

namespace Zombera.Systems
{
    /// <summary>
    /// Drives compact tactical footwork during melee combat.
    /// Keeps the player close to their active encounter opponent with short, controlled
    /// adjustments and a small pressure backstep when crowded.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombatFootwork : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField, Min(0f)] private float scanRadius = 8f;

        [Header("Preferred Range")]
        [Tooltip("Retreat if closer than this.")]
        [SerializeField, Min(0f)] private float preferredRangeMin = 0.82f;
        [Tooltip("Approach if farther than this.")]
        [SerializeField, Min(0f)] private float preferredRangeMax = 1.08f;

        [Header("Footwork Timing")]
        [Tooltip("Minimum seconds between footwork ticks (holds/darts).")]
        [SerializeField, Min(0.1f)] private float intervalMin = 0.55f;
        [SerializeField, Min(0.1f)] private float intervalMax = 1.05f;

        [Header("Dart In / Retreat")]
        [Tooltip("Chance 0-1 that a tick will dart toward the enemy rather than hold.")]
        [SerializeField, Range(0f, 1f)] private float dartChance = 0.16f;
        [Tooltip("How far to dart toward the enemy.")]
        [SerializeField, Min(0f)] private float dartDistance = 0.14f;
        [Tooltip("Seconds after darting in before auto-retreating back.")]
        [SerializeField, Min(0.1f)] private float dartRetreatDelaySeconds = 0.18f;
        [Tooltip("How far to retreat after a dart.")]
        [SerializeField, Min(0f)] private float retreatDistance = 0.2f;

        [Header("Pressure Retreat")]
        [Tooltip("How far to step back when an enemy is inside preferredRangeMin.")]
        [SerializeField, Min(0f)] private float pressureRetreatDistance = 0.22f;

        [Header("NavMesh")]
        [SerializeField, Min(0.1f)] private float navSampleRadius = 2f;

        [Header("Player Input Suppression")]
        [SerializeField, Min(0f)] private float playerMoveSuppressSeconds = 0.8f;

        [Header("Enemy Facing")]
        [SerializeField, Min(0f)] private float faceEnemyDegreesPerSecond = 720f;

        [Header("Encounter Gate")]
        [SerializeField] private bool requireActiveEncounter = true;
        [SerializeField] private CombatEncounterManager encounterManager;
        [SerializeField] private PlayerInputController playerInputController;

        private Unit _playerUnit;
        private UnitController _controller;
        private float _nextFootworkAt;
        private float _playerIssuedMoveAt = -999f;
        private float _dartRetreatAt = -1f;
        private Vector3 _dartRetreatDir;
        private bool _suppressFacingUntilMoveOrderCompletes;

        private readonly List<Unit> _nearbyBuffer = new List<Unit>(16);

        private void Awake()
        {
            _playerUnit = GetComponent<Unit>();
            _controller = GetComponent<UnitController>();
            if (encounterManager == null)
            {
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : FindFirstObjectByType<CombatEncounterManager>();
            }

            if (playerInputController == null)
            {
                playerInputController = GetComponent<PlayerInputController>();
            }

            _nextFootworkAt = Time.time + Random.Range(intervalMin, intervalMax);
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

        private void Update()
        {
            if (_controller == null || _playerUnit == null || !_playerUnit.IsAlive) return;

            if (playerInputController != null && playerInputController.IsRangedCombatActive)
            {
                _dartRetreatAt = -1f;
                return;
            }

            if (IsMoveOrderFacingSuppressed())
            {
                _dartRetreatAt = -1f;
                return;
            }

            Unit nearestEnemy = ResolveCombatTarget();

            if (nearestEnemy == null)
            {
                _dartRetreatAt = -1f;
                return;
            }

            // Always face the nearest enemy.
            if (faceEnemyDegreesPerSecond > 0f && nearestEnemy != null)
                _controller.RotateTowardsPosition(nearestEnemy.transform.position, faceEnemyDegreesPerSecond);

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

        private bool IsMoveOrderFacingSuppressed()
        {
            if (!_suppressFacingUntilMoveOrderCompletes)
            {
                return false;
            }

            if (_controller == null)
            {
                _suppressFacingUntilMoveOrderCompletes = false;
                return false;
            }

            bool moveOrderStillActive = _controller.HasMoveTarget || _controller.IsMoving;
            if (moveOrderStillActive)
            {
                return true;
            }

            _suppressFacingUntilMoveOrderCompletes = false;
            return false;
        }

        private Unit ResolveCombatTarget()
        {
            if (_playerUnit == null)
            {
                return null;
            }

            if (encounterManager == null)
            {
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : FindFirstObjectByType<CombatEncounterManager>();
            }

            if (encounterManager != null
                && encounterManager.IsUnitInEncounter(_playerUnit)
                && encounterManager.TryGetEncounterOpponent(_playerUnit, out Unit encounterOpponent)
                && encounterOpponent != null
                && encounterOpponent.IsAlive)
            {
                return encounterOpponent;
            }

            if (requireActiveEncounter)
            {
                return null;
            }

            return FindNearestEnemy();
        }

        private void PerformFootworkTick(Unit enemy)
        {
            Vector3 toEnemy = enemy.transform.position - transform.position;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;
            if (dist < 0.01f) return;

            Vector3 dirToEnemy = toEnemy / dist;

            // Snap face toward enemy on each tick.
            _controller.RotateTowardsPosition(enemy.transform.position, 0f);

            if (dist < preferredRangeMin)
            {
                // Pressured — step back immediately.
                ExecuteMove(-dirToEnemy, pressureRetreatDistance);
                return;
            }

            // In range or too far: maybe dart in, otherwise hold.
            bool tooFar = dist > preferredRangeMax;
            bool doDart = tooFar || Random.value < dartChance;

            if (doDart)
            {
                // Dart toward the enemy, then schedule an auto-retreat.
                if (ExecuteMove(dirToEnemy, dartDistance))
                {
                    _dartRetreatAt  = Time.time + dartRetreatDelaySeconds;
                    _dartRetreatDir = -dirToEnemy;
                }
            }
            // else: hold — do nothing this tick, just keep facing.
        }

        /// <summary>Issues a move to the nearest valid navmesh point in dir*distance. Returns true if a position was found.</summary>
        private bool ExecuteMove(Vector3 dir, float distance)
        {
            Vector3 target = transform.position + dir * distance;
            if (NavMesh.SamplePosition(target + Vector3.up * 0.5f, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            {
                _controller.MoveTo(hit.position);
                return true;
            }
            return false;
        }

        private Unit FindNearestEnemy()
        {
            if (UnitManager.Instance == null) return null;

            _nearbyBuffer.Clear();
            UnitManager.Instance.FindNearbyEnemies(_playerUnit, scanRadius, _nearbyBuffer);

            Unit nearest = null;
            float nearestSq = float.MaxValue;

            for (int i = 0; i < _nearbyBuffer.Count; i++)
            {
                Unit e = _nearbyBuffer[i];
                if (e == null || e.Health == null || e.Health.IsDead) continue;
                float sq = (e.transform.position - transform.position).sqrMagnitude;
                if (sq < nearestSq) { nearestSq = sq; nearest = e; }
            }

            return nearest;
        }
    }
}

