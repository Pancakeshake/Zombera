#if UNITY_EDITOR
using NUnit.Framework;
using Zombera.Systems;

namespace Zombera.Tests.Editor
{
    public sealed class MovementTriggerPolicyTests
    {
        private readonly struct Case
        {
            public readonly string Name;
            public readonly MovementTriggerPolicyInput Input;
            public readonly LeftClickMovementPhase ExpectedPhase;
            public readonly MovementTriggerKind ExpectedTrigger;

            public Case(
                string name,
                MovementTriggerPolicyInput input,
                LeftClickMovementPhase expectedPhase,
                MovementTriggerKind expectedTrigger)
            {
                Name = name;
                Input = input;
                ExpectedPhase = expectedPhase;
                ExpectedTrigger = expectedTrigger;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        [Test]
        public void Evaluate_TableDrivenScenarios_ReturnExpectedPhaseAndTrigger()
        {
            var cases = new[]
            {
                new Case(
                    "Press on world enters world press phase",
                    CreateInput(
                        LeftClickMovementPhase.Idle,
                        pointerOverUi: false,
                        pressed: true,
                        released: false,
                        held: true,
                        cursorHasTarget: false,
                        suppressUntilRelease: false,
                        allowHoldMove: false,
                        canEvaluateTrigger: true),
                    LeftClickMovementPhase.WorldPressActive,
                    MovementTriggerKind.None),
                new Case(
                    "Press on target reserves release",
                    CreateInput(
                        LeftClickMovementPhase.Idle,
                        pointerOverUi: false,
                        pressed: true,
                        released: false,
                        held: true,
                        cursorHasTarget: true,
                        suppressUntilRelease: false,
                        allowHoldMove: false,
                        canEvaluateTrigger: true),
                    LeftClickMovementPhase.SuppressNextRelease,
                    MovementTriggerKind.None),
                new Case(
                    "Release over ui clears phase",
                    CreateInput(
                        LeftClickMovementPhase.WorldPressActive,
                        pointerOverUi: true,
                        pressed: false,
                        released: true,
                        held: false,
                        cursorHasTarget: false,
                        suppressUntilRelease: false,
                        allowHoldMove: false,
                        canEvaluateTrigger: true),
                    LeftClickMovementPhase.Idle,
                    MovementTriggerKind.None),
                new Case(
                    "Suppressed release is consumed",
                    CreateInput(
                        LeftClickMovementPhase.SuppressNextRelease,
                        pointerOverUi: false,
                        pressed: false,
                        released: true,
                        held: false,
                        cursorHasTarget: false,
                        suppressUntilRelease: false,
                        allowHoldMove: false,
                        canEvaluateTrigger: true),
                    LeftClickMovementPhase.Idle,
                    MovementTriggerKind.None),
                new Case(
                    "Hold trigger when world press active and hold move enabled",
                    CreateInput(
                        LeftClickMovementPhase.WorldPressActive,
                        pointerOverUi: false,
                        pressed: false,
                        released: false,
                        held: true,
                        cursorHasTarget: false,
                        suppressUntilRelease: false,
                        allowHoldMove: true,
                        canEvaluateTrigger: true),
                    LeftClickMovementPhase.WorldPressActive,
                    MovementTriggerKind.Hold),
                new Case(
                    "Discrete trigger on release from world press",
                    CreateInput(
                        LeftClickMovementPhase.WorldPressActive,
                        pointerOverUi: false,
                        pressed: false,
                        released: true,
                        held: false,
                        cursorHasTarget: false,
                        suppressUntilRelease: false,
                        allowHoldMove: true,
                        canEvaluateTrigger: true),
                    LeftClickMovementPhase.Idle,
                    MovementTriggerKind.Discrete),
                new Case(
                    "No trigger when evaluation is blocked",
                    CreateInput(
                        LeftClickMovementPhase.WorldPressActive,
                        pointerOverUi: false,
                        pressed: false,
                        released: true,
                        held: false,
                        cursorHasTarget: false,
                        suppressUntilRelease: false,
                        allowHoldMove: true,
                        canEvaluateTrigger: false),
                    LeftClickMovementPhase.WorldPressActive,
                    MovementTriggerKind.None)
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var @case = cases[i];
                var result = MovementTriggerPolicy.Evaluate(@case.Input);

                Assert.That(result.NextPhase, Is.EqualTo(@case.ExpectedPhase), $"Case '{@case.Name}' phase mismatch");
                Assert.That(result.TriggerKind, Is.EqualTo(@case.ExpectedTrigger), $"Case '{@case.Name}' trigger mismatch");
            }
        }

        private static MovementTriggerPolicyInput CreateInput(
            LeftClickMovementPhase currentPhase,
            bool pointerOverUi,
            bool pressed,
            bool released,
            bool held,
            bool cursorHasTarget,
            bool suppressUntilRelease,
            bool allowHoldMove,
            bool canEvaluateTrigger)
        {
            return new MovementTriggerPolicyInput
            {
                CurrentPhase = currentPhase,
                PointerOverUi = pointerOverUi,
                Pressed = pressed,
                Released = released,
                Held = held,
                CursorHasTarget = cursorHasTarget,
                SuppressLeftClickMovementUntilRelease = suppressUntilRelease,
                AllowHoldMove = allowHoldMove,
                CanEvaluateMoveTrigger = canEvaluateTrigger
            };
        }
    }
}
#endif
