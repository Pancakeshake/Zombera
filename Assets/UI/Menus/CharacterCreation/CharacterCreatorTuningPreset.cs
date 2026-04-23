using UnityEngine;

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    /// Stores editor-tuned character creator style values so look-and-feel can be reused.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterCreatorTuningPreset", menuName = "Zombera/Character Creation/Tuning Preset")]
    public sealed class CharacterCreatorTuningPreset : ScriptableObject
    {
        [Header("Canvas")]
        public Vector2 canvasReferenceResolution = new Vector2(768f, 1365f);

        [Range(0f, 1f)]
        public float canvasMatchWidthOrHeight = 0.5f;

        [Header("Controller")]
        public bool enableRuntimeCustomizationUi = true;
        public Color panelTint = new Color(0.05f, 0.04f, 0.04f, 0.88f);
        public Color tabNormalTint = new Color(0.13f, 0.12f, 0.12f, 0.96f);
        public Color tabActiveTint = new Color(0.45f, 0.18f, 0.09f, 0.99f);
        public Vector2 panelAnchorMin = new Vector2(0.14f, 0.06f);
        public Vector2 panelAnchorMax = new Vector2(0.86f, 0.95f);
        public float panelInnerPadding = 18f;
        public float panelSpacing = 12f;
        public float rowSpacing = 10f;
        public bool autoFramePreviewCamera = false;

        [Header("Title")]
        public float titlePreferredHeight = 52f;
        public float titleFontSize = 23f;

        [Header("Gender Row")]
        public float raceRowPreferredHeight = 56f;
        public float raceRowSpacing = 16f;
        public float genderButtonWidth = 130f;
        public float genderButtonHeight = 56f;
        public float genderGlyphFontSize = 30f;

        [Header("Tab Row")]
        public float tabRowPreferredHeight = 48f;
        public float tabRowSpacing = 2f;
        public float tabButtonPreferredHeight = 44f;
        public float tabLabelFontSize = 18f;

        [Header("Body Tab")]
        public float bodySelectorRowPreferredHeight = 64f;
        public float bodyLabelFontSize = 23f;
        public float bodyValueFontSize = 22f;
        public float bodySliderRowPreferredHeight = 72f;
        public float bodySliderLabelFontSize = 16f;
        public float bodySliderValueFontSize = 16f;
    }
}