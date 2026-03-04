using UnityEngine;
using Zombera.Characters;

namespace Zombera.AI.Brains
{
    /// <summary>
    /// Zombie-specific brain profile.
    /// Prioritizes enemy pursuit and melee pressure.
    /// </summary>
    public sealed class ZombieBrain : UnitBrain
    {
        [Header("Zombie Priorities")]
        [SerializeField] private float chaseStartDistance = 16f;
        [SerializeField] private float noiseInvestigateWeight = 0.6f;

        private void Reset()
        {
            SetTickInterval(0.4f);
        }

        protected override void ConfigureDefaultRole()
        {
            if (Unit != null)
            {
                Unit.SetRole(UnitRole.Zombie);
            }
        }

        protected override UnitDecision EvaluateDecision(UnitSensorFrame sensorFrame)
        {
            if (sensorFrame.NearestEnemy != null)
            {
                if (sensorFrame.NearestEnemyDistance <= AttackRange)
                {
                    return new UnitDecision
                    {
                        DecisionType = UnitDecisionType.Attack,
                        Score = 1f,
                        TargetUnit = sensorFrame.NearestEnemy,
                        TargetPosition = sensorFrame.NearestEnemy.transform.position,
                        Reason = "Zombie enemy within attack range"
                    };
                }

                if (sensorFrame.NearestEnemyDistance <= chaseStartDistance)
                {
                    return new UnitDecision
                    {
                        DecisionType = UnitDecisionType.Chase,
                        Score = 0.9f,
                        TargetUnit = sensorFrame.NearestEnemy,
                        TargetPosition = sensorFrame.NearestEnemy.transform.position,
                        Reason = "Zombie enemy detected in chase radius"
                    };
                }
            }

            if (sensorFrame.HasHeardNoise)
            {
                return new UnitDecision
                {
                    DecisionType = UnitDecisionType.Chase,
                    Score = noiseInvestigateWeight,
                    TargetPosition = sensorFrame.LastNoisePosition,
                    Reason = "Zombie investigating nearby noise"
                };
            }

            UnitDecision fallback = base.EvaluateDecision(sensorFrame);

            if (fallback.DecisionType == UnitDecisionType.Follow)
            {
                fallback.DecisionType = UnitDecisionType.Wander;
                fallback.Reason = "Zombie fallback converted follow -> wander";
            }

            // TODO: Inject horde influence and aggression modifiers.
            // TODO: Differentiate zombie archetypes (runner/tank/spitter) via profile data.
            return fallback;
        }
    }
}