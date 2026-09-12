#region

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Tick-based autonomous behavior for squad members.
    ///     Priority: attack visible threats > retreat when heavily outnumbered > assist wounded allies > idle.
    /// </summary>
    public class SquadController : MonoBehaviour
    {
        private void OnEnable()
        {
            RuntimeGameplayAiRegistry.RegisterSquad(this);
        }

        private void OnDisable()
        {
            RuntimeGameplayAiRegistry.UnregisterSquad(this);
        }
        [SerializeField] private float aiTickInterval = 0.2f;
        [SerializeField] private float detectionRadius = 20f;
        [SerializeField] private Unit unit;
        [SerializeField] private UnitCombat unitCombat;
        [SerializeField] private UnitController unitController;
        [SerializeField] private UnitHealth unitHealth;

        [Header("Retreat")]
        [Tooltip("Retreat when enemies outnumber nearby allies by at least this ratio.")]
        [SerializeField]
        [Min(1f)]
        private float retreatThreatRatio = 2.5f;

        [SerializeField] [Min(1f)] private float retreatDistance = 8f;

        [Header("Assist")]
        [Tooltip("Move to assist a wounded ally (below this HP fraction) when no enemies are visible.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float woundedHpThreshold = 0.4f;

        [SerializeField] [Min(0.5f)] private float assistRadius = 30f;

        private readonly List<UnitHealth> _visibleTargets = new();
        private readonly List<Unit> _nearbyEnemyBuffer = new();
        private readonly List<Unit> _nearbyAllyBuffer = new();
        private float _tickTimer;

        protected virtual void Awake()
        {
            if (unit == null) unit = GetComponent<Unit>();
            if (unitCombat == null) unitCombat = GetComponent<UnitCombat>();
            if (unitController == null) unitController = GetComponent<UnitController>();
            if (unitHealth == null) unitHealth = GetComponent<UnitHealth>();

            if (unit == null) return;

            unit.SetRole(UnitRole.SquadMember);
            unit.SetOptionalAI(this);
        }

        protected virtual void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer < aiTickInterval) return;
            _tickTimer = 0f;
            TickAI();
        }

        protected virtual void TickAI()
        {
            if (unit == null || !unit.IsAlive || unitCombat == null) return;

            var nearbyAllyCount = RefreshPerception();

            if (ShouldRetreat(nearbyAllyCount))
            {
                ExecuteRetreat();
                return;
            }

            if (_visibleTargets.Count > 0)
            {
                unitCombat.ExecuteAttack(_visibleTargets);
                return;
            }

            if (TryAssistWounded()) return;

            unitController?.Stop();
        }

        private int RefreshPerception()
        {
            _visibleTargets.Clear();

            if (UnitManager.Instance == null) return 0;

            UnitManager.Instance.FindNearbyEnemies(
                unit, Zombera.Combat.CombatTuningConfig.AttackScanRadiusOr(detectionRadius), _nearbyEnemyBuffer);
            AddAliveEnemyTargets(_nearbyEnemyBuffer);

            UnitManager.Instance.FindNearbyAllies(
                unit, Zombera.Combat.CombatTuningConfig.AttackScanRadiusOr(detectionRadius), _nearbyAllyBuffer);
            return _nearbyAllyBuffer.Count;
        }

        private void AddAliveEnemyTargets(List<Unit> nearbyEnemies)
        {
            if (nearbyEnemies == null) return;

            for (var i = 0; i < nearbyEnemies.Count; i++)
            {
                var enemy = nearbyEnemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                _visibleTargets.Add(enemy.Health);
            }
        }

        private bool ShouldRetreat(int nearbyAllyCount)
        {
            if (_visibleTargets.Count <= 0 || nearbyAllyCount <= 0 || unitHealth == null) return false;

            var ratio = (float)_visibleTargets.Count / Mathf.Max(1, nearbyAllyCount);
            return ratio >= retreatThreatRatio;
        }

        private void ExecuteRetreat()
        {
            if (unitController == null || _visibleTargets.Count == 0) return;

            var threatCentroid = Vector3.zero;
            for (var i = 0; i < _visibleTargets.Count; i++)
            {
                threatCentroid += _visibleTargets[i].transform.position;
            }
            threatCentroid /= _visibleTargets.Count;

            var away = transform.position - threatCentroid;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f) away = transform.forward;
            away.Normalize();

            unitController.MoveTo(transform.position + away * retreatDistance);
        }

        private bool TryAssistWounded()
        {
            if (UnitManager.Instance == null) return false;

            UnitManager.Instance.FindNearbyAllies(unit, assistRadius, _nearbyAllyBuffer);
            if (_nearbyAllyBuffer.Count == 0) return false;

            Unit mostWounded = null;
            var lowestHpRatio = 1f;

            for (var i = 0; i < _nearbyAllyBuffer.Count; i++)
            {
                var ally = _nearbyAllyBuffer[i];
                if (ally == null || ally.Health == null || ally.Health.IsDead) continue;

                var hpRatio = ally.Health.MaxHealth > 0f
                    ? ally.Health.CurrentHealth / ally.Health.MaxHealth
                    : 1f;

                if (hpRatio < woundedHpThreshold && hpRatio < lowestHpRatio)
                {
                    lowestHpRatio = hpRatio;
                    mostWounded = ally;
                }
            }

            if (mostWounded == null) return false;

            unitController?.MoveTo(mostWounded.transform.position);
            return true;
        }
    }
}