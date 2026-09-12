#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.Systems;

namespace Zombera.Tests.Editor
{
    public sealed class ControlModeArbitrationAndSuppressionTests
    {
        [Test]
        public void RequestMode_HigherPrioritySameFrame_Wins()
        {
            var go = new GameObject("ModeCoordinator_Test");
            try
            {
                var coordinator = go.AddComponent<PlayerControlModeCoordinator>();

                coordinator.RequestMode(PlayerControlMode.SingleUnit, PlayerControlModeSource.PlayerInput);
                coordinator.RequestMode(PlayerControlMode.SquadRts, PlayerControlModeSource.SelectionManager);

                Assert.That(coordinator.CurrentMode, Is.EqualTo(PlayerControlMode.SquadRts));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RequestMode_LowerPrioritySameFrame_DoesNotOverrideHigherPriority()
        {
            var go = new GameObject("ModeCoordinator_Test");
            try
            {
                var coordinator = go.AddComponent<PlayerControlModeCoordinator>();

                coordinator.RequestMode(PlayerControlMode.BuildMode, PlayerControlModeSource.PlayerInput);
                coordinator.RequestMode(PlayerControlMode.UiBlocked, PlayerControlModeSource.PlayerInput);

                Assert.That(coordinator.CurrentMode, Is.EqualTo(PlayerControlMode.BuildMode));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MovementPolicy_SuppressedRelease_TakesPrecedenceOverDiscreteTrigger()
        {
            var input = new MovementTriggerPolicyInput
            {
                CurrentPhase = LeftClickMovementPhase.SuppressNextRelease,
                PointerOverUi = false,
                Pressed = false,
                Released = true,
                Held = false,
                CursorHasTarget = false,
                SuppressLeftClickMovementUntilRelease = false,
                AllowHoldMove = true,
                CanEvaluateMoveTrigger = true
            };

            var result = MovementTriggerPolicy.Evaluate(input);

            Assert.That(result.NextPhase, Is.EqualTo(LeftClickMovementPhase.Idle));
            Assert.That(result.TriggerKind, Is.EqualTo(MovementTriggerKind.None));
        }
    }
}
#endif
