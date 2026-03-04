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

            // TODO: Add per-fireteam command channels and role-aware command filtering.
        }
    }
}