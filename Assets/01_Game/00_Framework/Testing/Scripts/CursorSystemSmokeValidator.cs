#region

using UnityEngine;
using Zombera.Systems;

#endregion

namespace Zombera.Testing
{
    /// <summary>
    ///     Lightweight runtime checks for cursor ownership and pointer APIs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CursorSystemSmokeValidator : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart) RunSmokeChecks();
        }

        [ContextMenu("Run Cursor Smoke Checks")]
        public void RunSmokeChecks()
        {
            var passed = 0;
            var failed = 0;

            RunCheck("Default presentation is free pointer", CheckDefaultPresentation, ref passed, ref failed);
            RunCheck("Menu modal request/release", CheckMenuModalRequestCycle, ref passed, ref failed);
            RunCheck("Gameplay pointer API", CheckGameplayPointerApi, ref passed, ref failed);
            RunCheck("Raw vs gameplay delta matches calibration", CheckCalibrationDelta, ref passed, ref failed);

            Debug.Log($"[CursorSmoke] Completed. passed={passed}, failed={failed}");
        }

        private static void RunCheck(
            string name,
            System.Func<bool> check,
            ref int passed,
            ref int failed)
        {
            if (check())
            {
                passed++;
                Debug.Log($"[CursorSmoke] PASS: {name}");
            }
            else
            {
                failed++;
                Debug.LogError($"[CursorSmoke] FAIL: {name}");
            }
        }

        private static bool CheckDefaultPresentation()
        {
            var presentation = CursorService.CurrentPresentation;
            return presentation.Visible && presentation.LockMode == CursorLockMode.None;
        }

        private static bool CheckMenuModalRequestCycle()
        {
            var beforeCount = CursorService.ActiveRequestCount;
            using (var handle = CursorService.RequestMenuModal())
            {
                if (CursorService.ActiveRequestCount != beforeCount + 1) return false;
            }

            return CursorService.ActiveRequestCount == beforeCount;
        }

        private static bool CheckGameplayPointerApi()
        {
            if (!Input.mousePresent && CursorService.TryGetGameplayPointerScreenPosition(out _)) return false;
            if (Input.mousePresent && !CursorService.TryGetGameplayPointerScreenPosition(out _)) return false;
            return true;
        }

        private static bool CheckCalibrationDelta()
        {
            if (!CursorService.TryGetRawPointerScreenPosition(out var raw)) return !Input.mousePresent;
            if (!CursorService.TryGetGameplayPointerScreenPosition(out var gameplay)) return false;

            return (gameplay - raw).sqrMagnitude <= 0.01f;
        }
    }
}
