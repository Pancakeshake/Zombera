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
        [SerializeField] private bool includeDeadUnits;

        private readonly List<Unit> enemyBuffer = new List<Unit>();

        public Unit NearestEnemy { get; private set; }
        public float NearestEnemyDistance { get; private set; } = float.PositiveInfinity;
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

                if (distance < NearestEnemyDistance)
                {
                    NearestEnemy = candidate;
                    NearestEnemyDistance = distance;
                }
            }

            EnemyCount = enemyBuffer.Count;

            // TODO: Add line-of-sight and field-of-view constraints.
            // TODO: Add threat scoring (closest is not always highest priority).
        }

        private void ResetReadings()
        {
            enemyBuffer.Clear();
            NearestEnemy = null;
            NearestEnemyDistance = float.PositiveInfinity;
            EnemyCount = 0;
        }
    }
}