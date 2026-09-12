#if UNITY_EDITOR
using NUnit.Framework;
using Zombera.Systems;

namespace Zombera.Tests.Editor
{
    public sealed class PlayerControlModePolicyTests
    {
        [Test]
        public void Evaluate_ReturnsBuildMode_WhenBuildFlagIsActive()
        {
            var context = new PlayerControlModeContext(
                true,
                false,
                true,
                true,
                3,
                2,
                false,
                false);

            var mode = PlayerControlModePolicy.Evaluate(context);

            Assert.That(mode, Is.EqualTo(PlayerControlMode.BuildMode));
        }

        [Test]
        public void Evaluate_ReturnsUiBlocked_WhenPointerIsOverUi()
        {
            var context = new PlayerControlModeContext(
                false,
                true,
                true,
                true,
                3,
                2,
                false,
                false);

            var mode = PlayerControlModePolicy.Evaluate(context);

            Assert.That(mode, Is.EqualTo(PlayerControlMode.UiBlocked));
        }

        [Test]
        public void Evaluate_ReturnsSquadRts_WhenHybridDisabled()
        {
            var context = new PlayerControlModeContext(
                false,
                false,
                false,
                false,
                1,
                2,
                false,
                false);

            var mode = PlayerControlModePolicy.Evaluate(context);

            Assert.That(mode, Is.EqualTo(PlayerControlMode.SquadRts));
        }

        [Test]
        public void Evaluate_ReturnsSquadRts_WhenSelectionThresholdReached()
        {
            var context = new PlayerControlModeContext(
                false,
                false,
                true,
                true,
                2,
                2,
                false,
                false);

            var mode = PlayerControlModePolicy.Evaluate(context);

            Assert.That(mode, Is.EqualTo(PlayerControlMode.SquadRts));
        }

        [Test]
        public void Evaluate_ReturnsSingleUnit_WhenThresholdNotReachedAndModifierInactive()
        {
            var context = new PlayerControlModeContext(
                false,
                false,
                true,
                true,
                1,
                2,
                true,
                false);

            var mode = PlayerControlModePolicy.Evaluate(context);

            Assert.That(mode, Is.EqualTo(PlayerControlMode.SingleUnit));
            Assert.That(PlayerControlModePolicy.ShouldCaptureRtsMouseInput(context), Is.False);
        }
    }
}
#endif
