using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;

namespace Zombera.AI.Brains
{
    /// <summary>
    /// Squad-member brain that blends command obedience with local combat autonomy.
    /// </summary>
    public sealed class SquadBrain : UnitBrain
    {
        [Header("Squad Context")]
        [SerializeField] private Transform squadLeader;
        [SerializeField] private float maxFollowDistance = 5f;

        [Header("Combat Tuning")]
        [SerializeField] private float engagementDistance = 12f;

        [Header("Flee / Retreat")]
        [SerializeField, Range(0f, 1f)] private float fleeHealthThreshold = 0.25f;

        private SquadCommandType lastCommand = SquadCommandType.Follow;
        private Vector3 lastCommandTarget;
        private bool hasCommandTarget;

        private void Reset()
        {
            SetTickInterval(0.2f);
        }

        protected override void ConfigureDefaultRole()
        {
            if (Unit != null)
            {
                Unit.SetRole(UnitRole.SquadMember);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventSystem.Instance?.Subscribe<SquadCommandIssuedEvent>(OnSquadCommandIssued);
        }

        protected override void OnDisable()
        {
            EventSystem.Instance?.Unsubscribe<SquadCommandIssuedEvent>(OnSquadCommandIssued);
            base.OnDisable();
        }

        protected override UnitDecision EvaluateDecision(UnitSensorFrame sensorFrame)
        {
            // Flee: if HP is critically low and there is a nearby enemy, retreat toward squad leader.
            if (Unit != null && Unit.Health != null && fleeHealthThreshold > 0f)
            {
                float healthFraction = Unit.Health.MaxHealth > 0f
                    ? Unit.Health.CurrentHealth / Unit.Health.MaxHealth
                    : 1f;

                if (healthFraction <= fleeHealthThreshold && sensorFrame.NearestEnemy != null)
                {
                    return new UnitDecision
                    {
                        DecisionType = UnitDecisionType.Retreat,
                        Score = 1f,
                        TargetPosition = Vector3.zero,
                        Reason = $"Fleeing (HP {healthFraction * 100f:F0}%)"
                    };
                }
            }

            if (sensorFrame.NearestEnemy != null && sensorFrame.NearestEnemyDistance <= engagementDistance)
            {
                if (sensorFrame.NearestEnemyDistance <= AttackRange)
                {
                    return new UnitDecision
                    {
                        DecisionType = UnitDecisionType.Attack,
                        Score = 0.95f,
                        TargetUnit = sensorFrame.NearestEnemy,
                        TargetPosition = sensorFrame.NearestEnemy.transform.position,
                        Reason = "Squad enemy in attack range"
                    };
                }

                if (lastCommand != SquadCommandType.HoldPosition)
                {
                    return new UnitDecision
                    {
                        DecisionType = UnitDecisionType.Chase,
                        Score = 0.85f,
                        TargetUnit = sensorFrame.NearestEnemy,
                        TargetPosition = sensorFrame.NearestEnemy.transform.position,
                        Reason = "Squad engaging nearby enemy"
                    };
                }
            }

            switch (lastCommand)
            {
                case SquadCommandType.HoldPosition:
                    return new UnitDecision
                    {
                        DecisionType = UnitDecisionType.Idle,
                        Score = 0.75f,
                        Reason = "Holding commanded position"
                    };

                case SquadCommandType.Move:
                case SquadCommandType.Defend:
                    if (hasCommandTarget)
                    {
                        return new UnitDecision
                        {
                            DecisionType = UnitDecisionType.Follow,
                            Score = 0.8f,
                            TargetPosition = lastCommandTarget,
                            Reason = "Following squad command target"
                        };
                    }

                    break;
            }

            if (squadLeader != null)
            {
                float distanceToLeader = Vector3.Distance(transform.position, squadLeader.position);

                if (distanceToLeader > maxFollowDistance)
                {
                    return new UnitDecision
                    {
                        DecisionType = UnitDecisionType.Follow,
                        Score = 0.7f,
                        TargetPosition = squadLeader.position,
                        Reason = "Returning to squad leader"
                    };
                }
            }

            return base.EvaluateDecision(sensorFrame);
        }

        private void OnSquadCommandIssued(SquadCommandIssuedEvent gameEvent)
        {
            lastCommand = gameEvent.CommandType;
            lastCommandTarget = gameEvent.TargetPosition;
            hasCommandTarget = gameEvent.TargetPosition != default;

            // Role-aware filtering: passive members skip attack orders;
            // support roles skip Move-only orders when a higher-priority command is active.
            SquadMember member = GetComponent<SquadMember>();

            if (member != null)
            {
                if (lastCommand == SquadCommandType.Attack && member.Stance == MemberStance.Passive)
                {
                    hasCommandTarget = false; // Stand down — passive members do not engage.
                }
            }
        }
    }
}