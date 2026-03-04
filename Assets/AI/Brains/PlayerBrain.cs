using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.AI.Brains
{
    /// <summary>
    /// Lightweight brain wrapper for player-controlled units.
    /// Player control bypasses utility decisions but keeps shared UnitBrain architecture.
    /// </summary>
    public sealed class PlayerBrain : UnitBrain
    {
        [SerializeField] private PlayerInputController playerInputController;

        private void Reset()
        {
            SetTickInterval(0.2f);
        }

        protected override void ConfigureDefaultRole()
        {
            if (Unit != null)
            {
                Unit.SetRole(UnitRole.Player);
            }

            if (playerInputController == null)
            {
                playerInputController = GetComponent<PlayerInputController>();
            }
        }

        protected override bool ShouldUseDecisionSystem(UnitSensorFrame sensorFrame)
        {
            _ = sensorFrame;
            return false;
        }

        protected override void RunManualControl(UnitSensorFrame sensorFrame)
        {
            if (playerInputController == null)
            {
                playerInputController = GetComponent<PlayerInputController>();
            }

            if (playerInputController != null)
            {
                playerInputController.enabled = true;
            }

            TransitionState(UnitBrainStateType.Idle, "Player input is authoritative", sensorFrame, default);

            // TODO: Add optional aim-assist and contextual suggestion hooks.
            // TODO: Add runtime toggle for AI autopilot and accessibility behavior.
        }
    }
}