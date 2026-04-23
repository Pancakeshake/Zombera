using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.AI.Sensors
{
    /// <summary>
    /// Collects nearby hostile units for a brain tick.
    /// Responsibilities:
    /// - query hostile units from UnitManager
    /// - cache nearest enemy and distance
    /// - expose detected enemy count for utility scoring
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySensor : MonoBehaviour
    {
        [SerializeField] private float detectionRadius = 14f;
        [SerializeField, Range(0f, 360f)] private float fieldOfViewAngle = 220f;
        [SerializeField] private LayerMask losObstacleMask;
        [SerializeField] private bool includeDeadUnits;

        private readonly List<Unit> enemyBuffer = new List<Unit>();

        public Unit NearestEnemy { get; private set; }
        public float NearestEnemyDistance { get; private set; } = float.PositiveInfinity;
        public Unit HighestThreatEnemy { get; private set; }
        public int EnemyCount { get; private set; }
        public float DetectionRadius => detectionRadius;
        public IReadOnlyList<Unit> Enemies => enemyBuffer;

        public void Sense(Unit self)
        {
            ResetReadings();

            if (self == null || UnitManager.Instance == null)
            {
                return;
            }

            UnitManager.Instance.FindNearbyEnemies(self, detectionRadius, enemyBuffer);

            Vector3 selfPosition = self.transform.position;

            for (int i = enemyBuffer.Count - 1; i >= 0; i--)
            {
                Unit candidate = enemyBuffer[i];

                if (candidate == null || (!includeDeadUnits && (candidate.Health == null || candidate.Health.IsDead)))
                {
                    enemyBuffer.RemoveAt(i);
                    continue;
                }

                float distance = Vector3.Distance(selfPosition, candidate.transform.position);

                // Stealth reduces the effective detection radius against this candidate.
                float stealthMultiplier = candidate.Stats != null ? candidate.Stats.GetStealthDetectionRadiusMultiplier() : 1f;
                float postureMultiplier = candidate.Stats != null ? candidate.Stats.GetPostureDetectionMultiplier() : 1f;
                float effectiveRadius = detectionRadius * stealthMultiplier * postureMultiplier;

                if (distance > effectiveRadius)
                {
                    // The candidate successfully evades detection — award stealth XP to them.
                    candidate.Stats?.RecordUndetectedTime(Time.deltaTime);
                    enemyBuffer.RemoveAt(i);
                    continue;
                }

                // FoV check — skip candidates outside the sensor's cone.
                if (fieldOfViewAngle < 360f)
                {
                    Vector3 dirFlat = candidate.transform.position - selfPosition;
                    dirFlat.y = 0f;
                    Vector3 fwdFlat = transform.forward;
                    fwdFlat.y = 0f;
                    if (Vector3.Angle(fwdFlat.normalized, dirFlat.normalized) > fieldOfViewAngle * 0.5f)
                    {
                        enemyBuffer.RemoveAt(i);
                        continue;
                    }
                }

                // LoS check — discard candidates behind solid obstacles.
                if (losObstacleMask != 0)
                {
                    const float eyeHeight = 1.6f;
                    Vector3 eyePos = selfPosition + Vector3.up * eyeHeight;
                    Vector3 targetEyePos = candidate.transform.position + Vector3.up * eyeHeight;
                    if (Physics.Linecast(eyePos, targetEyePos, losObstacleMask))
                    {
                        enemyBuffer.RemoveAt(i);
                        continue;
                    }
                }

                if (distance < NearestEnemyDistance)
                {
                    NearestEnemy = candidate;
                    NearestEnemyDistance = distance;
                }

                // Threat score: proximity urgency + wounded bonus (low-HP targets are easier kills).
                float healthRatio = (candidate.Health != null && candidate.Health.MaxHealth > 0f)
                    ? candidate.Health.CurrentHealth / candidate.Health.MaxHealth
                    : 1f;
                float threatScore = 1f / Mathf.Max(0.1f, distance) + (1f - Mathf.Clamp01(healthRatio));
                float bestThreat = HighestThreatEnemy == null ? float.NegativeInfinity
                    : 1f / Mathf.Max(0.1f, Vector3.Distance(selfPosition, HighestThreatEnemy.transform.position))
                      + (1f - Mathf.Clamp01(
                            HighestThreatEnemy.Health != null && HighestThreatEnemy.Health.MaxHealth > 0f
                                ? HighestThreatEnemy.Health.CurrentHealth / HighestThreatEnemy.Health.MaxHealth
                                : 1f));
                if (threatScore > bestThreat)
                {
                    HighestThreatEnemy = candidate;
                }
            }

            EnemyCount = enemyBuffer.Count;
        }

        private void ResetReadings()
        {
            enemyBuffer.Clear();
            NearestEnemy = null;
            NearestEnemyDistance = float.PositiveInfinity;
            HighestThreatEnemy = null;
            EnemyCount = 0;
        }
    }
}