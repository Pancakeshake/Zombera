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
                playerInputController.enabled = !autoPilotEnabled;
            }

            if (autoPilotEnabled)
            {
                // Autopilot: fall through to the utility decision system.
                _ = base.EvaluateDecision(sensorFrame);
                return;
            }

            TransitionState(UnitBrainStateType.Idle, "Player input is authoritative", sensorFrame, default);
        }

        /// <summary>
        /// When true the brain's utility decision system controls the unit instead of the player.
        /// Toggle from accessibility settings or cinematic sequences.
        /// </summary>
        public bool AutoPilotEnabled
        {
            get => autoPilotEnabled;
            set
            {
                autoPilotEnabled = value;

                if (playerInputController != null)
                {
                    playerInputController.enabled = !value;
                }
            }
        }

        /// <summary>
        /// When true nearby enemies within aimAssistRadius contribute a soft steering
        /// force toward the closest threat each frame.
        /// </summary>
        public bool AimAssistEnabled
        {
            get => aimAssistEnabled;
            set => aimAssistEnabled = value;
        }

        [SerializeField] private bool aimAssistEnabled;
#pragma warning disable CS0414
        [SerializeField, Min(0f)] private float aimAssistRadius = 8f;
#pragma warning restore CS0414
        [SerializeField] private bool autoPilotEnabled;
    }
}