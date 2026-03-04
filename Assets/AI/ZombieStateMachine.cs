using UnityEngine;

namespace Zombera.AI
{
    /// <summary>
    /// Handles zombie state transitions: Idle, Wander, Investigate, Chase, Attack, CallHorde.
    /// </summary>
    public sealed class ZombieStateMachine : MonoBehaviour
    {
        public ZombieState CurrentState { get; private set; } = ZombieState.Idle;

        public void SetState(ZombieState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            ExitState(CurrentState);
            CurrentState = newState;
            EnterState(CurrentState);
        }

        public void TickStateMachine()
        {
            switch (CurrentState)
            {
                case ZombieState.Idle:
                    TickIdle();
                    break;
                case ZombieState.Wander:
                    TickWander();
                    break;
                case ZombieState.Investigate:
                    TickInvestigate();
                    break;
                case ZombieState.Chase:
                    TickChase();
                    break;
                case ZombieState.Attack:
                    TickAttack();
                    break;
                case ZombieState.CallHorde:
                    TickCallHorde();
                    break;
            }
        }

        private void EnterState(ZombieState state)
        {
            // TODO: Configure speed, animation, and perception profile per state.
            _ = state;
        }

        private void ExitState(ZombieState state)
        {
            // TODO: Cleanup state-specific timers and temporary targets.
            _ = state;
        }

        private void TickIdle()
        {
            // TODO: Transition to wander/investigate on stimuli.
        }

        private void TickWander()
        {
            // TODO: Follow roaming path and scan for targets/noise.
        }

        private void TickInvestigate()
        {
            // TODO: Move to investigation point and escalate/de-escalate state.
        }

        private void TickChase()
        {
            // TODO: Pursue target with path updates and aggression checks.
        }

        private void TickAttack()
        {
            // TODO: Execute melee attacks and evaluate retargeting.
        }

        private void TickCallHorde()
        {
            // TODO: Signal HordeManager and return to chase/attack state.
        }
    }

    public enum ZombieState
    {
        Idle,
        Wander,
        Investigate,
        Chase,
        Attack,
        CallHorde
    }
}