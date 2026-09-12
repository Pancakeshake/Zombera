#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UMA;
using UMA.CharacterSystem;
using Zombera.Core;
using Object = UnityEngine.Object;

#endregion

// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable InvertIf
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable UseDeconstruction
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable ConvertToConstant

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Runtime customization UI for Phase 3 controls (body, hair/beard, skin, presets).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed partial class CharacterCreatorCustomizationController : MonoBehaviour
    {
        private const string HeightDnaName = "height";

        // Tone gradient endpoints are sourced from CharacterCreatorStyle — no duplicate definitions here.

        private const string MaleSymbol = "\u2642";
        private const string FemaleSymbol = "\u2640";

        [Header("Catalog")] [SerializeField] private CharacterAppearanceCatalog optionCatalog;

        private static Sprite _runtimeSolidSprite;
        [SerializeField] private RectTransform customizationPanelRoot;

        [Header("Colors")] [SerializeField] private Color panelTint = CharacterCreatorStyle.CustomizationPanelTint;

        [SerializeField] private Color tabNormalTint = CharacterCreatorStyle.TabNormalTint;
        [SerializeField] private Color tabActiveTint = CharacterCreatorStyle.TabActiveTint;
        [SerializeField] private Color panelBackgroundTint = new(1f, 1f, 1f, 1f);

        [Header("Layout")] [SerializeField] private Vector2 panelAnchorMin = new(0.62f, 0.12f);

        [SerializeField] private Vector2 panelAnchorMax = new(0.98f, 0.90f);

        [Tooltip(
            "Clamp manual slider output to a safe DNA range to avoid extreme body morphs. Applies to all non-height controls.")]
        [SerializeField]
        private Vector2 bodyDnaOutputRange = new(0.40f, 0.60f);

        [Tooltip("Even tighter clamp for proportion controls that can look uncanny quickly (head/limb/feet).")]
        [SerializeField]
        private Vector2 lockedBodyDnaOutputRange = new(0.45f, 0.55f);

        [Tooltip(
            "Height DNA is scaled by Body Slider Output Max. This range is expressed in normalized [0..1] of that max.")]
        [SerializeField]
        private Vector2 heightDnaOutputRangeNormalized = new(0.46f, 0.54f);

        [Header("Randomization")]
        [Tooltip("Default range for body sliders when using Randomize. Keep narrow to avoid extreme proportions.")]
        [SerializeField]
        private Vector2 randomBodySliderRange = new(0.48f, 0.52f);

        [Tooltip("Height has outsized silhouette impact; randomize it even more subtly than other controls.")]
        [SerializeField]
        private Vector2 randomHeightSliderRange = new(0.49f, 0.51f);

        [SerializeField] private Vector2 randomLockedProportionSliderRange = new(0.495f, 0.505f);
        [SerializeField] private Vector2 randomSkinToneRange = new(0.40f, 0.65f);
        [SerializeField] private Vector2 randomHairToneRange = new(0.25f, 0.75f);
        [SerializeField] private Vector2 randomEyeToneRange = new(0.35f, 0.75f);

        [Header("Preview Camera")] [SerializeField]
        private Camera previewCamera;

        [SerializeField] private bool autoFramePreviewCamera;

        private readonly List<string> _beardOptions = new();
        private readonly Dictionary<string, Slider> _bodyControlSliders = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TMP_Text> _bodyControlValueLabels = new(StringComparer.OrdinalIgnoreCase);

        [Header("Body Controls")] [Range(0.1f, 1f)]
        private const float BodySliderOutputMax = 0.7f;

        [Header("Phase 3")] private const bool EnableRuntimeCustomizationUi = true;
        private readonly List<string> _hairOptions = new();

        [Min(0.1f)] private const float MaxPreviewDistance = 12f;
        [Min(0.1f)] private const float MinPreviewDistance = 1.6f;

        [Header("Panel Background")] private const bool PanelBackgroundPreserveAspect = false;

        [Min(0f)] private const float PanelInnerPadding = 16f;
        [Min(0f)] private const float PanelSpacing = 10f;
        [Min(1)] private const int PostApplyRefitFrames = 4;
        [Range(0f, 0.8f)] private const float PreviewBottomPadding = 0.08f;
        private const float PreviewDistanceOffset = 0.10f;
        [Range(0f, 1f)] private const float PreviewHorizontalPadding = 0.22f;
        [Range(0f, 0.8f)] private const float PreviewTopPadding = 0.24f;
        [Range(0f, 0.35f)] private const float PreviewVerticalCenterBias = 0.05f;
        [Range(0f, 1f)] private const float PreviewVerticalPadding = 0.16f;

        private readonly List<string> _raceOptions = new();
        [Min(0f)] private const float RowSpacing = 8f;
        [Min(6f)] private const float SliderHandleVisualSize = 12f;
        [Min(18f)] private const float SliderHitAreaHeight = 40f;
        [Min(2f)] private const float SliderTrackVisualHeight = 4f;
        private Button _beardNextButton;
        private int _beardOptionIndex;
        private Button _beardPrevButton;
        private TMP_Text _beardValueLabel;

        private Button _bodyTabButton;

        private RectTransform _bodyTabRoot;
        private CharacterAppearanceProfile _currentProfile = CharacterAppearanceProfile.CreateDefault();
        private Slider _eyeToneSlider;
        private TMP_Text _eyeToneValueLabel;
        private Button _hairNextButton;
        private int _hairOptionIndex;
        private Button _hairPrevButton;
        private Button _hairTabButton;
        private RectTransform _hairTabRoot;
        private Slider _hairToneSlider;
        private TMP_Text _hairToneValueLabel;
        private TMP_Text _hairValueLabel;

        private bool _isInitialized;
        private Button _presetsTabButton;
        private RectTransform _presetsTabRoot;

        private GameObject _previewAvatar;

        private TMP_Text _raceHeaderLabel;
        private Button _raceNextButton;

        private int _raceOptionIndex;
        private Button _racePrevButton;
        private TMP_Text _raceValueLabel;
        private Button _randomAppearanceButton;
        private int _remainingAutoFramePasses;
        private Button _resetAppearanceButton;
        private Button _skinTabButton;
        private RectTransform _skinTabRoot;

        private Slider _skinToneSlider;
        private TMP_Text _skinToneValueLabel;

        private TMP_Text _statusLabel;
        private bool _suppressCallbacks;
        private Action _uiInteractionSfxCallback;
        private Action _sliderUiInteractionSfxCallback;

        /// <summary>
        ///     Re-wires sliders/buttons after the panel is shown or prefab hierarchy changes (restyle tools nest controls).
        /// </summary>
        public void EnsureCustomizationInputBindings()
        {
            if (!Application.isPlaying || !EnableRuntimeCustomizationUi) return;

            BuildOrResolveRuntimeUi();
            BindUiEvents();
        }

        public void BuildOrRefreshEditorUi()
        {
            BuildOrResolveRuntimeUi();
            BindUiEvents();

            EnsureCurrentProfileFromState();
            RefreshRaceOptions();
            RefreshWardrobeOptions();
            SyncUiFromCurrentProfile();
        }

        public void RebuildUiFromScratch()
        {
            DestroyAllCustomizationPanelRoots();

            customizationPanelRoot = null;
            _bodyTabRoot = null;
            _hairTabRoot = null;
            _skinTabRoot = null;
            _presetsTabRoot = null;
            _raceHeaderLabel = null;
            _raceValueLabel = null;
            _hairValueLabel = null;
            _beardValueLabel = null;
            _skinToneSlider = null;
            _hairToneSlider = null;
            _eyeToneSlider = null;
            _racePrevButton = null;
            _raceNextButton = null;
            _hairPrevButton = null;
            _hairNextButton = null;
            _beardPrevButton = null;
            _beardNextButton = null;
            _randomAppearanceButton = null;
            _resetAppearanceButton = null;
            _bodyControlSliders.Clear();
            _bodyControlValueLabels.Clear();

            BuildOrRefreshEditorUi();
        }

        public void ApplySavedProfile()
        {
            EnsureCurrentProfileFromState();
            RefreshRaceOptions();
            RefreshWardrobeOptions();
            SyncUiFromCurrentProfile();
            ApplyCurrentProfileToPreview(
                true,
                false,
                true,
                _previewAvatar != null && _previewAvatar.activeInHierarchy);
            RequestAutoFramePasses();
        }

        public string CaptureProfileJson()
        {
            CaptureCurrentProfileFromPreview();
            _currentProfile.Sanitize();
            var profileJson = CharacterAppearanceProfile.Serialize(_currentProfile);
            CharacterSelectionState.SetAppearanceProfileJson(profileJson, false);
            return profileJson;
        }

        /// <summary>
        ///     When the appearance profile has a race name, reports whether it maps to the male gender option (for name pools,
        ///     etc.).
        /// </summary>
        public bool TryGetPreviewProfilePrefersMaleGender(out bool preferMale)
        {
            preferMale = false;

            if (_currentProfile == null || string.IsNullOrWhiteSpace(_currentProfile.raceName)) return false;

            preferMale = CharacterCreatorUtility.IsMaleRaceName(_currentProfile.raceName);
            return true;
        }

        [ContextMenu("Rebuild Customization UI (Edit Mode)")]
        private void RebuildCustomizationUiFromContextMenu()
        {
            RebuildUiFromScratch();
        }

        private void TryResolveCatalog()
        {
            if (optionCatalog != null) return;

            optionCatalog = Resources.Load<CharacterAppearanceCatalog>("CharacterAppearanceCatalog");

            if (optionCatalog == null)
            {
                var catalogs = Resources.LoadAll<CharacterAppearanceCatalog>("");
                if (catalogs.Length > 0) optionCatalog = catalogs[0];
            }
        }

        private static RectTransform EnsureTabRoot(RectTransform parent, string objectName)
        {
            return CharacterCreatorUIFactory.EnsureTabRoot(parent, objectName);
        }

        private static void StripTabRootLayoutElement(RectTransform tabRoot)
        {
            CharacterCreatorUIFactory.StripTabRootLayoutElement(tabRoot);
        }

        private RectTransform ResolvePanelRectTransform()
        {
            var selfRect = transform as RectTransform;
            if (selfRect != null) return selfRect;

            if (customizationPanelRoot != null) return customizationPanelRoot.parent as RectTransform;

            return null;
        }

        private static RectTransform CreateRectTransform(string objectName, Transform parent)
        {
            return CharacterCreatorUIFactory.CreateRectTransform(objectName, parent);
        }

        private static RectTransform EnsureDirectChildRectTransform(Transform parent, string childName)
        {
            return CharacterCreatorUIFactory.EnsureDirectChildRectTransform(parent, childName);
        }

        private static RectTransform CreateRow(Transform parent, string rowName, float preferredHeight)
        {
            return CharacterCreatorUIFactory.CreateRow(parent, rowName, preferredHeight, RowSpacing);
        }

        private static TMP_Text CreateLabel(
            Transform parent,
            string objectName,
            string value,
            float fontSize,
            FontStyles fontStyle,
            float preferredWidth = -1f)
        {
            return CharacterCreatorUIFactory.CreateLabel(parent, objectName, value, fontSize, fontStyle, preferredWidth);
        }

        private Button CreateCycleButton(Transform parent, string objectName, string label, Action callback)
        {
            return CharacterCreatorUIFactory.CreateCycleButton(parent, objectName, label, callback, tabNormalTint, () => PlayUiInteractionSfx());
        }

        private static void ConfigureGenderRow(RectTransform raceRow)
        {
            if (raceRow == null) return;

            var layout = raceRow.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) return;

            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void ConfigureGenderButton(Button button, string symbol)
        {
            if (button == null) return;

            button.gameObject.SetActive(true);
            button.interactable = true;

            var layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 54f;
            layout.minWidth = 54f;
            layout.preferredHeight = 34f;
            layout.flexibleWidth = 0f;

            var image = button.targetGraphic as Image;
            if (image != null)
            {
                image.sprite = GetSolidSprite();
                image.type = Image.Type.Sliced;
            }

            var legacyText = button.GetComponentInChildren<Text>(true);
            legacyText?.gameObject.SetActive(false);

            var glyph = FindChildComponent<TMP_Text>(button.transform, "GenderGlyph");
            if (glyph == null)
            {
                var glyphRect = CreateRectTransform("GenderGlyph", button.transform);
                glyphRect.anchorMin = Vector2.zero;
                glyphRect.anchorMax = Vector2.one;
                glyphRect.offsetMin = Vector2.zero;
                glyphRect.offsetMax = Vector2.zero;

                glyph = glyphRect.gameObject.AddComponent<TextMeshProUGUI>();
                glyph.raycastTarget = false;
                glyph.textWrappingMode = TextWrappingModes.NoWrap;
            }

            glyph.text = symbol;
            glyph.fontSize = 23f;
            glyph.fontStyle = FontStyles.Bold;
            glyph.alignment = TextAlignmentOptions.Center;
            glyph.enableAutoSizing = false;
            glyph.raycastTarget = false;
        }

        private Button CreateTabButton(Transform parent, string objectName, string label, Action callback)
        {
            return CharacterCreatorUIFactory.CreateTabButton(parent, objectName, label, callback, tabNormalTint, () => PlayUiInteractionSfx());
        }

        private Button CreateActionButton(Transform parent, string objectName, string label, Action callback)
        {
            return CharacterCreatorUIFactory.CreateActionButton(parent, objectName, label, callback, tabNormalTint, () => PlayUiInteractionSfx());
        }

        private static Slider CreateSlider(Transform parent, string objectName)
        {
            return CharacterCreatorUIFactory.CreateSlider(parent, objectName, SliderHitAreaHeight, SliderTrackVisualHeight, SliderHandleVisualSize);
        }

        private static void ConfigureSliderInteractionPresentation(Slider slider)
        {
            CharacterCreatorUIFactory.ConfigureSliderInteractionPresentation(slider, SliderHitAreaHeight, SliderTrackVisualHeight, SliderHandleVisualSize);
        }

        private static Sprite GetSolidSprite()
        {
            return CharacterCreatorUIFactory.GetSolidSprite();
        }

        private static RectTransform FindChildRectTransform(Transform parent, string childName)
        {
            return CharacterCreatorUIFactory.FindChildRectTransform(parent, childName);
        }

        private static T FindChildComponent<T>(Transform parent, string childName) where T : Component
        {
            return CharacterCreatorUIFactory.FindChildComponent<T>(parent, childName);
        }

        }
        }
