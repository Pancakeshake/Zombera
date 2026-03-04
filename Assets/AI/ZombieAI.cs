using UnityEngine;
using Zombera.Characters;

namespace Zombera.AI
{
    /// <summary>
    /// Per-zombie AI controller using tick-based updates for performance.
    /// </summary>
    public sealed class ZombieAI : MonoBehaviour
    {
        [SerializeField] private float aiTickInterval = 0.4f;
        [SerializeField] private ZombieStateMachine stateMachine;
        [SerializeField] private UnitHealth unitHealth;
        [SerializeField] private Unit unit;

        public float AITickInterval => aiTickInterval;
        public bool IsActive { get; private set; }

        private float tickTimer;

        private void Awake()
        {
            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }
        }

        public void Initialize()
        {
            IsActive = true;
            tickTimer = 0f;
            unitHealth?.ResetHealthToMax();

            if (unit != null)
            {
                unit.SetRole(UnitRole.Zombie);
                unit.SetOptionalAI(this);
            }

            if (stateMachine != null)
            {
                stateMachine.SetState(ZombieState.Idle);
            }

            // TODO: Bind perception sensors and blackboard references.
        }

        public void SetActive(bool active)
        {
            IsActive = active;
        }

        private void Update()
        {
            if (!IsActive || unitHealth == null || unitHealth.IsDead)
            {
                return;
            }

            tickTimer += Time.deltaTime;

            if (tickTimer < aiTickInterval)
            {
                return;
            }

            tickTimer = 0f;
            TickAI();
        }

        private void TickAI()
        {
            stateMachine?.TickStateMachine();

            // TODO: Run perception queries and threat updates at controlled cadence.
            // TODO: Adapt tick interval dynamically by distance/visibility.
        }
    }
}