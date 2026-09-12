#region

#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;
using Zombera.Systems;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.UI
{
    /// <summary>
    ///     Applies runtime cursor hotspot calibration to UI pointer bindings.
    /// </summary>
    internal sealed class CursorCalibrationInputProcessor : InputProcessor<Vector2>
    {
        public override Vector2 Process(Vector2 value, InputControl control)
        {
            // Hotspot tuning is applied via Cursor.SetCursor only. UI pointer stays at hardware position.
            return value;
        }
    }

    internal static class CursorCalibrationInputProcessorRegistration
    {
        private static bool _registered;

        public static void EnsureRegistered()
        {
            if (_registered) return;

            InputSystem.RegisterProcessor<CursorCalibrationInputProcessor>("CursorCalibration");
            _registered = true;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterOnLoad()
        {
            EnsureRegistered();
        }
    }
}
#endif

#endregion
