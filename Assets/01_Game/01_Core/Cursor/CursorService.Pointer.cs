#region



using UnityEngine;



#if ENABLE_INPUT_SYSTEM

using UnityEngine.InputSystem;

#endif



#endregion



namespace Zombera.Systems

{

    public static partial class CursorService

    {

        private static CursorIconIntent _activeIconIntent = CursorIconIntent.Default;

        private static CursorProfile _activeProfile;

        private static Vector2 _appliedHotspot;



        public static Vector2 AppliedHotspot => _appliedHotspot;



        public static CursorIconIntent ActiveIconIntent => _activeIconIntent;



        public static event System.Action<CursorIconIntent> ActiveIconIntentChanged;



        internal static void BindProfile(CursorProfile profile)

        {

            _activeProfile = profile;

        }



        internal static void UnbindProfile(CursorProfile profile)

        {

            if (_activeProfile == profile) _activeProfile = null;

        }



        public static void SetActiveIconIntent(CursorIconIntent intent)

        {

            if (_activeIconIntent == intent) return;



            _activeIconIntent = intent;

            ActiveIconIntentChanged?.Invoke(intent);

        }



        public static Vector2 GetActiveCalibrationOffsetPixels()

        {

            return _activeProfile != null

                ? _activeProfile.GetHotspotOffset(_activeIconIntent)

                : Vector2.zero;

        }



        internal static void RegisterAppliedHotspot(Vector2 appliedHotspot)

        {

            _appliedHotspot = appliedHotspot;

        }



        /// <summary>

        ///     Screen position of the OS cursor hotspot — same point Unity uses for mouse reads.

        /// </summary>

        public static bool TryGetCursorVisualScreenPosition(out Vector2 screenPosition)

        {

            return TryGetGameplayPointerScreenPosition(out screenPosition);

        }



        public static bool TryGetGameplayPointerScreenPosition(out Vector2 screenPosition)

        {

            return TryGetGameplayPointerScreenPosition(null, out screenPosition);

        }



#if ENABLE_INPUT_SYSTEM

        public static bool TryGetGameplayPointerScreenPosition(InputAction pointerAction, out Vector2 screenPosition)

#else

        public static bool TryGetGameplayPointerScreenPosition(object pointerAction, out Vector2 screenPosition)

#endif

        {

            return TryGetRawPointerScreenPosition(pointerAction, out screenPosition);

        }



        public static bool TryGetRawPointerScreenPosition(out Vector2 screenPosition)

        {

            return TryGetRawPointerScreenPosition(null, out screenPosition);

        }



#if ENABLE_INPUT_SYSTEM

        public static bool TryGetRawPointerScreenPosition(InputAction pointerAction, out Vector2 screenPosition)

#else

        public static bool TryGetRawPointerScreenPosition(object pointerAction, out Vector2 screenPosition)

#endif

        {

            screenPosition = default;



#if ENABLE_INPUT_SYSTEM

            if (pointerAction != null)

            {

                screenPosition = pointerAction.ReadValue<Vector2>();

                return true;

            }



            if (Mouse.current != null)

            {

                screenPosition = Mouse.current.position.ReadValue();

                return true;

            }

#endif



            if (!Input.mousePresent) return false;



            screenPosition = Input.mousePosition;

            return true;

        }

    }

}


