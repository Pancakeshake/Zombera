#region



using UnityEngine;



#if ENABLE_INPUT_SYSTEM

using UnityEngine.InputSystem;

#endif



#endregion



namespace Zombera.Systems

{

    /// <summary>

    ///     Play-mode keyboard controls for live hotspot calibration tuning on a <see cref="CursorProfile"/>.

    /// </summary>

    public sealed class CursorRuntimeCalibrationInput

    {

        private bool _calibrateAttackOffset;

        private CursorIconIntent _calibrationTarget = CursorIconIntent.Default;



        public bool Enabled { get; set; } = true;

        public float StepPixels { get; set; } = 1f;

        public float FastStepPixels { get; set; } = 5f;

        public bool LogChanges { get; set; } = true;

        public KeyCode NudgeLeftKey { get; set; } = KeyCode.LeftArrow;

        public KeyCode NudgeRightKey { get; set; } = KeyCode.RightArrow;

        public KeyCode NudgeUpKey { get; set; } = KeyCode.UpArrow;

        public KeyCode NudgeDownKey { get; set; } = KeyCode.DownArrow;

        public KeyCode ToggleTargetKey { get; set; } = KeyCode.BackQuote;

        public KeyCode ResetOffsetKey { get; set; } = KeyCode.Backspace;

        public KeyCode LogValuesKey { get; set; } = KeyCode.Return;



        public CursorIconIntent CalibrationTarget => _calibrationTarget;



        public bool Tick(CursorProfile profile, out bool requiresIconRefresh)

        {

            requiresIconRefresh = false;

            if (!Application.isPlaying || !Enabled || profile == null) return false;



            if (WasKeyPressedThisFrame(ToggleTargetKey))

            {

                _calibrateAttackOffset = !_calibrateAttackOffset;

                _calibrationTarget = _calibrateAttackOffset

                    ? CursorIconIntent.Attack

                    : CursorIconIntent.Default;



                if (LogChanges)

                    Debug.Log($"[CursorCalibration] Target switched to {_calibrationTarget}.");

            }



            if (WasKeyPressedThisFrame(ResetOffsetKey))

            {

                profile.ResetHotspotOffset(_calibrationTarget);

                requiresIconRefresh = true;

                if (LogChanges) LogValues(profile);

                return true;

            }



            var step = IsFastCalibrationModifierHeld()

                ? Mathf.Max(1f, FastStepPixels)

                : Mathf.Max(0.25f, StepPixels);

            var delta = BuildCalibrationDelta(step);

            if (delta.sqrMagnitude <= 0.0001f)

            {

                if (WasKeyPressedThisFrame(LogValuesKey) && LogChanges) LogValues(profile);

                return false;

            }



            profile.NudgeHotspotOffset(_calibrationTarget, delta);

            requiresIconRefresh = true;

            if (LogChanges) LogValues(profile);

            return true;

        }



        private Vector2 BuildCalibrationDelta(float step)

        {

            var delta = Vector2.zero;

            if (WasKeyPressedThisFrame(NudgeLeftKey)) delta.x -= step;

            if (WasKeyPressedThisFrame(NudgeRightKey)) delta.x += step;

            if (WasKeyPressedThisFrame(NudgeUpKey)) delta.y -= step;

            if (WasKeyPressedThisFrame(NudgeDownKey)) delta.y += step;

            return delta;

        }



        public static void LogValues(CursorProfile profile)

        {

            if (profile == null) return;



            var defaultOffset = profile.GetHotspotOffset(CursorIconIntent.Default);

            var attackOffset = profile.GetHotspotOffset(CursorIconIntent.Attack);



            Debug.Log(

                "[CursorCalibration] Hotspot offsets | " +

                $"default=({defaultOffset.x:0.##}, {defaultOffset.y:0.##}) " +

                $"attack=({attackOffset.x:0.##}, {attackOffset.y:0.##})");

        }



        private static bool IsFastCalibrationModifierHeld()

        {

#if ENABLE_INPUT_SYSTEM

            var keyboard = Keyboard.current;

            if (keyboard != null)

                return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

#endif



            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        }



        private static bool WasKeyPressedThisFrame(KeyCode keyCode)

        {

#if ENABLE_INPUT_SYSTEM

            var keyboard = Keyboard.current;

            if (keyboard != null && TryMapKeyCode(keyCode, out var mappedKey))

                return keyboard[mappedKey].wasPressedThisFrame;

#endif



            return Input.GetKeyDown(keyCode);

        }



#if ENABLE_INPUT_SYSTEM

        private static bool TryMapKeyCode(KeyCode keyCode, out Key mappedKey)

        {

            switch (keyCode)

            {

                case KeyCode.LeftArrow:

                    mappedKey = Key.LeftArrow;

                    return true;

                case KeyCode.RightArrow:

                    mappedKey = Key.RightArrow;

                    return true;

                case KeyCode.UpArrow:

                    mappedKey = Key.UpArrow;

                    return true;

                case KeyCode.DownArrow:

                    mappedKey = Key.DownArrow;

                    return true;

                case KeyCode.BackQuote:

                    mappedKey = Key.Backquote;

                    return true;

                case KeyCode.Backspace:

                    mappedKey = Key.Backspace;

                    return true;

                case KeyCode.Return:

                case KeyCode.KeypadEnter:

                    mappedKey = Key.Enter;

                    return true;

                default:

                    mappedKey = Key.None;

                    return false;

            }

        }

#endif

    }

}


