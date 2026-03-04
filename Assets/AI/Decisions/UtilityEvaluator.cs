using UnityEngine;
using Zombera.AI.Brains;
using Zombera.Characters;

namespace Zombera.AI.Decisions
{
    /// <summary>
    /// Scores potential actions and returns the highest utility decision.
    /// This class is intentionally deterministic and side-effect free.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UtilityEvaluator : MonoBehaviour
    {
        [Header("Ranges")]
        [SerializeField] private float attackDistance = 2.25f;
        [SerializeField] private float chaseDistance = 14f;

        [Header("Weights")]
        [SerializeField] private float attackWeight = 1f;
        [SerializeField] private float chaseWeight = 0.75f;
        [SerializeField] private float retreatWeight = 0.6f;
        [SerializeField] private float reloadWeight = 0.4f;
        [SerializeField] private float followWeight = 0.5f;
        [SerializeField] private float wanderWeight = 0.2f;
        [SerializeField] private float idleWeight = 0.1f;

        public UnitDecision Evaluate(UnitBrain brain, UnitSensorFrame sensorFrame)
        {
            UnitDecision best = new UnitDecision
            {
                DecisionType = UnitDecisionType.Idle,
                Score = float.NegativeInfinity,
                Reason = "No evaluation"
            };

            Consider(ref best, EvaluateAttack(sensorFrame));
            Consider(ref best, EvaluateChase(sensorFrame));
            Consider(ref best, EvaluateRetreat(sensorFrame));
            Consider(ref best, EvaluateReload(brain, sensorFrame));
            Consider(ref best, EvaluateFollow(brain, sensorFrame));
            Consider(ref best, EvaluateWander(brain, sensorFrame));
            Consider(ref best, EvaluateIdle(sensorFrame));

            if (float.IsNegativeInfinity(best.Score))
            {
                best = new UnitDecision
                {
                    DecisionType = UnitDecisionType.Idle,
                    Score = 0f,
                    Reason = "Fallback idle decision"
                };
            }

            return best;
        }

        private UnitDecision EvaluateAttack(UnitSensorFrame sensorFrame)
        {
            if (sensorFrame.NearestEnemy == null)
            {
                return DecisionNone(UnitDecisionType.Attack, "No enemy for attack");
            }

            if (sensorFrame.NearestEnemyDistance > attackDistance)
            {
                return DecisionNone(UnitDecisionType.Attack, "Enemy outside attack distance");
            }

            float normalized = 1f - Mathf.Clamp01(sensorFrame.NearestEnemyDistance / Mathf.Max(0.01f, attackDistance));
            float score = attackWeight * (0.7f + normalized * 0.3f);

            return new UnitDecision
            {
                DecisionType = UnitDecisionType.Attack,
                Score = score,
                TargetUnit = sensorFrame.NearestEnemy,
                TargetPosition = sensorFrame.NearestEnemy.transform.position,
                Reason = "Enemy inside attack distance"
            };
        }

        private UnitDecision EvaluateChase(UnitSensorFrame sensorFrame)
        {
            if (sensorFrame.NearestEnemy != null)
            {
                float normalized = 1f - Mathf.Clamp01(sensorFrame.NearestEnemyDistance / Mathf.Max(0.01f, chaseDistance));
                float score = chaseWeight * Mathf.Max(0.05f, normalized);

                return new UnitDecision
                {
                    DecisionType = UnitDecisionType.Chase,
                    Score = score,
                    TargetUnit = sensorFrame.NearestEnemy,
                    TargetPosition = sensorFrame.NearestEnemy.transform.position,
                    Reason = "Enemy available for chase"
                };
            }

            if (sensorFrame.HasHeardNoise)
            {
                float freshness = 1f - Mathf.Clamp01(sensorFrame.LastNoiseAge / 5f);
                float score = chaseWeight * 0.6f * freshness;

                return new UnitDecision
                {
                    DecisionType = UnitDecisionType.Chase,
                    Score = score,
                    TargetPosition = sensorFrame.LastNoisePosition,
                    Reason = "Investigating recent noise"
                };
            }

            return DecisionNone(UnitDecisionType.Chase, "No chase target");
        }

        private UnitDecision EvaluateRetreat(UnitSensorFrame sensorFrame)
        {
            if (sensorFrame.NearestEnemy == null)
            {
                return DecisionNone(UnitDecisionType.Retreat, "No enemy for retreat");
            }

            int enemyPressure = sensorFrame.NearbyEnemyCount;
            int allySupport = Mathf.Max(1, sensorFrame.NearbyAllyCount);

            if (enemyPressure <= allySupport + 1)
            {
                return DecisionNone(UnitDecisionType.Retreat, "Pressure not high enough");
            }

            float imbalance = Mathf.Clamp01((enemyPressure - allySupport) / 6f);

            return new UnitDecision
            {
                DecisionType = UnitDecisionType.Retreat,
                Score = retreatWeight * (0.5f + imbalance * 0.5f),
                TargetUnit = sensorFrame.NearestEnemy,
                TargetPosition = sensorFrame.NearestEnemy.transform.position,
                Reason = "Outnumbered, retreating"
            };
        }

        private UnitDecision EvaluateReload(UnitBrain brain, UnitSensorFrame sensorFrame)
        {
            if (brain == null || brain.UnitCombat == null)
            {
                return DecisionNone(UnitDecisionType.Reload, "Missing combat context");
            }

            // TODO: Replace placeholder once weapon ammo exposure is available from WeaponSystem.
            bool shouldReload = false;

            if (!shouldReload)
            {
                return DecisionNone(UnitDecisionType.Reload, "Reload condition not met");
            }

            float safety = sensorFrame.NearestEnemy == null ? 1f : Mathf.Clamp01(sensorFrame.NearestEnemyDistance / Mathf.Max(0.01f, attackDistance));

            return new UnitDecision
            {
                DecisionType = UnitDecisionType.Reload,
                Score = reloadWeight * safety,
                Reason = "Reload suggested by utility evaluator"
            };
        }

        private UnitDecision EvaluateFollow(UnitBrain brain, UnitSensorFrame sensorFrame)
        {
            if (brain == null || brain.Unit == null)
            {
                return DecisionNone(UnitDecisionType.Follow, "Missing brain context");
            }

            bool shouldPreferFollow = brain.Unit.Role == UnitRole.SquadMember || brain.Unit.Role == UnitRole.Survivor;

            if (!shouldPreferFollow)
            {
                return DecisionNone(UnitDecisionType.Follow, "Role does not prioritize follow");
            }

            if (sensorFrame.NearestEnemy != null && sensorFrame.NearestEnemyDistance <= chaseDistance)
            {
                return DecisionNone(UnitDecisionType.Follow, "Enemy pressure suppresses follow");
            }

            return new UnitDecision
            {
                DecisionType = UnitDecisionType.Follow,
                Score = followWeight,
                Reason = "Follow behavior preferred by role"
            };
        }

        private UnitDecision EvaluateWander(UnitBrain brain, UnitSensorFrame sensorFrame)
        {
            if (sensorFrame.NearestEnemy != null)
            {
                return DecisionNone(UnitDecisionType.Wander, "Enemy present, not wandering");
            }

            return new UnitDecision
            {
                DecisionType = UnitDecisionType.Wander,
                Score = wanderWeight,
                TargetPosition = brain != null ? brain.transform.position : Vector3.zero,
                Reason = "No immediate threats"
            };
        }

        private UnitDecision EvaluateIdle(UnitSensorFrame sensorFrame)
        {
            float calmness = sensorFrame.NearbyEnemyCount <= 0 ? 1f : 0.2f;

            return new UnitDecision
            {
                DecisionType = UnitDecisionType.Idle,
                Score = idleWeight * calmness,
                Reason = "Idle fallback"
            };
        }

        private static void Consider(ref UnitDecision best, UnitDecision candidate)
        {
            if (candidate.Score > best.Score)
            {
                best = candidate;
            }
        }

        private static UnitDecision DecisionNone(UnitDecisionType decisionType, string reason)
        {
            return new UnitDecision
            {
                DecisionType = decisionType,
                Score = float.NegativeInfinity,
                Reason = reason
            };
        }
    }
}