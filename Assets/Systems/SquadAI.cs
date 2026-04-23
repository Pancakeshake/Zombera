using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;

namespace Zombera.Systems
{
    /// <summary>
    /// Tick-based autonomous behavior for squad members.
    /// Priority: attack visible threats > retreat when heavily outnumbered > assist wounded allies > idle.
    /// </summary>
    public sealed class SquadAI : MonoBehaviour
    {
        [SerializeField] private float aiTickInterval = 0.2f;
        [SerializeField] private float detectionRadius = 20f;
        [SerializeField] private Unit unit;
        [SerializeField] private UnitCombat unitCombat;
        [SerializeField] private UnitController unitController;
        [SerializeField] private UnitHealth unitHealth;

        [Header("Retreat")]
        [Tooltip("Retreat when enemies outnumber nearby allies by at least this ratio.")]
        [SerializeField, Min(1f)] private float retreatThreatRatio = 2.5f;
        [SerializeField, Min(1f)] private float retreatDistance = 8f;

        [Header("Assist")]
        [Tooltip("Move to assist a wounded ally (below this HP fraction) when no enemies are visible.")]
        [SerializeField, Range(0f, 1f)] private float woundedHpThreshold = 0.4f;
        [SerializeField, Min(0.5f)] private float assistRadius = 30f;

        private readonly List<UnitHealth> visibleTargets = new List<UnitHealth>();
        private float tickTimer;

        private void Awake()
        {
            if (unit == null) unit = GetComponent<Unit>();
            if (unitCombat == null) unitCombat = GetComponent<UnitCombat>();
            if (unitController == null) unitController = GetComponent<UnitController>();
            if (unitHealth == null) unitHealth = GetComponent<UnitHealth>();

            if (unit != null)
            {
                unit.SetRole(UnitRole.SquadMember);
                unit.SetOptionalAI(this);
            }
        }

        private void Update()
        {
            tickTimer += Time.deltaTime;
            if (tickTimer < aiTickInterval) return;
            tickTimer = 0f;
            TickAI();
        }

        private void TickAI()
        {
            if (unit == null || !unit.IsAlive || unitCombat == null) return;

            // --- Gather visible enemies ---
            visibleTargets.Clear();
            int nearbyAllyCount = 0;

            if (UnitManager.Instance != null)
            {
                List<Unit> nearbyEnemies = UnitManager.Instance.FindNearbyEnemies(unit, detectionRadius);
                for (int i = 0; i < nearbyEnemies.Count; i++)
                {
                    Unit enemy = nearbyEnemies[i];
                    if (enemy != null && enemy.Health != null && !enemy.Health.IsDead)
                        visibleTargets.Add(enemy.Health);
                }

                List<Unit> nearbyAllies = UnitManager.Instance.FindNearbyAllies(unit, detectionRadius);
                nearbyAllyCount = nearbyAllies != null ? nearbyAllies.Count : 0;
            }

            // --- Threat-aware retreat ---
            if (visibleTargets.Count > 0 && nearbyAllyCount > 0)
            {
                float ratio = (float)visibleTargets.Count / Mathf.Max(1, nearbyAllyCount);
                if (ratio >= retreatThreatRatio && unitHealth != null)
                {
                    ExecuteRetreat();
                    return;
                }
            }

            // --- Attack ---
            if (visibleTargets.Count > 0)
            {
                unitCombat.ExecuteAttack(visibleTargets);
                return;
            }

            // --- Assist wounded ally ---
            if (TryAssistWounded()) return;

            // --- Idle ---
            unitController?.Stop();
        }

        private void ExecuteRetreat()
        {
            if (unitController == null || visibleTargets.Count == 0) return;

            // Average threat position.
            Vector3 threatCentroid = Vector3.zero;
            for (int i = 0; i < visibleTargets.Count; i++)
                threatCentroid += visibleTargets[i].transform.position;
            threatCentroid /= visibleTargets.Count;

            Vector3 away = transform.position - threatCentroid;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f) away = transform.forward;
            away.Normalize();

            unitController.MoveTo(transform.position + away * retreatDistance);
        }

        private bool TryAssistWounded()
        {
            if (UnitManager.Instance == null) return false;

            List<Unit> nearbyAllies = UnitManager.Instance.FindNearbyAllies(unit, assistRadius);
            if (nearbyAllies == null || nearbyAllies.Count == 0) return false;

            Unit mostWounded = null;
            float lowestRatio = woundedHpThreshold;

            for (int i = 0; i < nearbyAllies.Count; i++)
            {
                Unit ally = nearbyAllies[i];
                if (ally == null || ally.Health == null || ally.Health.IsDead) continue;

                float hpRatio = ally.Health.MaxHealth > 0f
                    ? ally.Health.CurrentHealth / ally.Health.MaxHealth
                    : 1f;

                if (hpRatio < lowestRatio)
                {
                    lowestRatio = hpRatio;
                    mostWounded = ally;
                }
            }

            if (mostWounded == null) return false;

            unitController?.MoveTo(mostWounded.transform.position);
            return true;
        }
    }
}