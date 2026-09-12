#region



using System.Collections.Generic;

using UnityEngine;



#endregion



namespace Zombera.Systems

{

    [CreateAssetMenu(fileName = "CursorProfile", menuName = "Zombera/Input/Cursor Profile")]

    public sealed class CursorProfile : ScriptableObject

    {

        private const float HotspotOffsetMin = -1000f;

        private const float HotspotOffsetMax = 1000f;



        [Header("Runtime Calibration")]

        [SerializeField] private bool enableRuntimeHotspotCalibration;



        [SerializeField] [Min(0.25f)] private float runtimeCalibrationStepPixels = 1f;

        [SerializeField] [Min(1f)] private float runtimeCalibrationFastStepPixels = 5f;



        [Header("Default Cursor Hotspot Offset")]

        [Tooltip("Added to the resolved hotspot. Drag sliders during Play mode to tune alignment.")]

        [SerializeField] [Range(-1000f, 1000f)] private float defaultHotspotOffsetX;



        [SerializeField] [Range(-1000f, 1000f)] private float defaultHotspotOffsetY;



        [Header("Attack Cursor Hotspot Offset")]

        [Tooltip("Added to the resolved hotspot. Drag sliders during Play mode to tune alignment.")]

        [SerializeField] [Range(-1000f, 1000f)] private float attackHotspotOffsetX;



        [SerializeField] [Range(-1000f, 1000f)] private float attackHotspotOffsetY;



        [Header("Hotspot Detection")]

        [SerializeField] [Range(0.01f, 1f)] private float autoDetectAlphaThreshold = 0.2f;



        [Header("Icons")]

        [SerializeField] private List<CursorIconDefinition> icons = new();



        public bool EnableRuntimeHotspotCalibration => enableRuntimeHotspotCalibration;

        public float RuntimeCalibrationStepPixels => runtimeCalibrationStepPixels;

        public float RuntimeCalibrationFastStepPixels => runtimeCalibrationFastStepPixels;

        public float AutoDetectAlphaThreshold => autoDetectAlphaThreshold;

        public IReadOnlyList<CursorIconDefinition> Icons => icons;



        public Vector2 GetHotspotOffset(CursorIconIntent intent)

        {

            return intent == CursorIconIntent.Attack

                ? new Vector2(attackHotspotOffsetX, attackHotspotOffsetY)

                : new Vector2(defaultHotspotOffsetX, defaultHotspotOffsetY);

        }



        public void ResetHotspotOffset(CursorIconIntent intent)

        {

            if (intent == CursorIconIntent.Attack)

            {

                attackHotspotOffsetX = 0f;

                attackHotspotOffsetY = 0f;

            }

            else

            {

                defaultHotspotOffsetX = 0f;

                defaultHotspotOffsetY = 0f;

            }



            MarkDirty();

        }



        public void NudgeHotspotOffset(CursorIconIntent intent, Vector2 delta)

        {

            if (intent == CursorIconIntent.Attack)

            {

                attackHotspotOffsetX = ClampOffset(attackHotspotOffsetX + delta.x);

                attackHotspotOffsetY = ClampOffset(attackHotspotOffsetY + delta.y);

            }

            else

            {

                defaultHotspotOffsetX = ClampOffset(defaultHotspotOffsetX + delta.x);

                defaultHotspotOffsetY = ClampOffset(defaultHotspotOffsetY + delta.y);

            }



            MarkDirty();

        }



        public bool HasAnyIcon()

        {

            for (var i = 0; i < icons.Count; i++)

            {

                if (icons[i].Texture != null) return true;

            }



            return false;

        }



        public bool TryGetIcon(CursorIconIntent intent, out CursorIconDefinition definition)

        {

            for (var i = 0; i < icons.Count; i++)

            {

                if (icons[i].Intent != intent) continue;



                definition = icons[i];

                return true;

            }



            definition = default;

            return false;

        }



        public void PopulateCatalog(CursorIconCatalog catalog)

        {

            if (catalog == null) return;



            for (var i = 0; i < icons.Count; i++)

                catalog.SetDefinition(NormalizeIcon(icons[i]));

        }



        public void ConfigureCalibrationInput(CursorRuntimeCalibrationInput input)

        {

            if (input == null) return;



            input.Enabled = enableRuntimeHotspotCalibration;

            input.StepPixels = runtimeCalibrationStepPixels;

            input.FastStepPixels = runtimeCalibrationFastStepPixels;

        }



        private static float ClampOffset(float value)

        {

            return Mathf.Clamp(value, HotspotOffsetMin, HotspotOffsetMax);

        }



        private static CursorIconDefinition NormalizeIcon(CursorIconDefinition icon)

        {

            if (icon.Texture == null) return icon;



            if (icon.Hotspot.sqrMagnitude <= 0.0001f)

            {

                icon.AutoCenterWhenUnset = false;

                icon.AutoDetectWhenUnset = true;

            }



            if (icon.Intent == CursorIconIntent.Attack && icon.AutoAnchor == default)

                icon.AutoAnchor = CursorHotspotAutoAnchor.TopRight;

            else if (icon.AutoAnchor == default)

                icon.AutoAnchor = CursorHotspotAutoAnchor.TopLeft;



            return icon;

        }



        private void MarkDirty()

        {

#if UNITY_EDITOR

            UnityEditor.EditorUtility.SetDirty(this);

#endif

        }

    }

}


