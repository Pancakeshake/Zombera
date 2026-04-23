using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Core;

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    /// Runtime customization UI for Phase 3 controls (body, hair/beard, skin, presets).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CharacterCreatorCustomizationController : MonoBehaviour
    {
        [Header("Phase 3")]
        [SerializeField] private bool enableRuntimeCustomizationUi = true;
        [SerializeField] private RectTransform customizationPanelRoot;

        [Header("Colors")]
        [SerializeField] private Color panelTint = new Color(0.07f, 0.07f, 0.07f, 0.88f);
        [SerializeField] private Color tabNormalTint = new Color(0.16f, 0.16f, 0.16f, 0.95f);
        [SerializeField] private Color tabActiveTint = new Color(0.40f, 0.12f, 0.07f, 0.98f);

        [Header("Panel Background")]
        [SerializeField] private Sprite panelBackgroundSprite;
        [SerializeField] private Color panelBackgroundTint = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private bool panelBackgroundPreserveAspect;

        [Header("Layout")]
        [SerializeField] private Vector2 panelAnchorMin = new Vector2(0.62f, 0.12f);
        [SerializeField] private Vector2 panelAnchorMax = new Vector2(0.98f, 0.90f);
        [SerializeField, Min(0f)] private float panelInnerPadding = 16f;
        [SerializeField, Min(0f)] private float panelSpacing = 10f;
        [SerializeField, Min(0f)] private float rowSpacing = 8f;
        [SerializeField, Min(18f)] private float sliderHitAreaHeight = 40f;
        [SerializeField, Min(2f)] private float sliderTrackVisualHeight = 4f;
        [SerializeField, Min(6f)] private float sliderHandleVisualSize = 12f;

        [Header("Body Controls")]
        [SerializeField, Range(0.1f, 1f)] private float bodySliderOutputMax = 0.7f;
        [Tooltip("Clamp manual slider output to a safe DNA range to avoid extreme body morphs. Applies to all non-height controls.")]
        [SerializeField] private Vector2 bodyDnaOutputRange = new Vector2(0.40f, 0.60f);
        [Tooltip("Even tighter clamp for proportion controls that can look uncanny quickly (head/limb/feet).")]
        [SerializeField] private Vector2 lockedBodyDnaOutputRange = new Vector2(0.45f, 0.55f);
        [Tooltip("Height DNA is scaled by Body Slider Output Max. This range is expressed in normalized [0..1] of that max.")]
        [SerializeField] private Vector2 heightDnaOutputRangeNormalized = new Vector2(0.46f, 0.54f);

        [Header("Randomization")]
        [Tooltip("Default range for body sliders when using Randomize. Keep narrow to avoid extreme proportions.")]
        [SerializeField] private Vector2 randomBodySliderRange = new Vector2(0.48f, 0.52f);
        [Tooltip("Height has outsized silhouette impact; randomize it even more subtly than other controls.")]
        [SerializeField] private Vector2 randomHeightSliderRange = new Vector2(0.49f, 0.51f);
        [SerializeField] private Vector2 randomLockedProportionSliderRange = new Vector2(0.495f, 0.505f);
        [SerializeField] private Vector2 randomSkinToneRange = new Vector2(0.40f, 0.65f);
        [SerializeField] private Vector2 randomHairToneRange = new Vector2(0.25f, 0.75f);
        [SerializeField] private Vector2 randomEyeToneRange = new Vector2(0.35f, 0.75f);

        [Header("Preview Camera")]
        [SerializeField] private Camera previewCamera;
        [SerializeField] private bool autoFramePreviewCamera = true;
        [SerializeField, Range(0f, 0.35f)] private float previewVerticalCenterBias = 0.05f;
        [SerializeField, Range(0f, 1f)] private float previewVerticalPadding = 0.16f;
        [SerializeField, Range(0f, 1f)] private float previewHorizontalPadding = 0.22f;
        [SerializeField, Range(0f, 0.8f)] private float previewTopPadding = 0.24f;
        [SerializeField, Range(0f, 0.8f)] private float previewBottomPadding = 0.08f;
        [SerializeField] private float previewDistanceOffset = 0.10f;
        [SerializeField, Min(0.1f)] private float minPreviewDistance = 1.6f;
        [SerializeField, Min(0.1f)] private float maxPreviewDistance = 12f;
        [SerializeField, Min(1)] private int postApplyRefitFrames = 4;

        private static readonly DnaControlDefinition[] BodyControlDefinitions =
        {
            new DnaControlDefinition("height", "Height"),
            new DnaControlDefinition("upperMuscle", "Upper Muscle"),
            new DnaControlDefinition("lowerMuscle", "Lower Muscle"),
            new DnaControlDefinition("belly", "Belly"),
            new DnaControlDefinition("headSize", "Head Size"),
            new DnaControlDefinition("armLength", "Arm Length"),
            new DnaControlDefinition("forearmLength", "Forearm Length"),
            new DnaControlDefinition("legSeparation", "Leg Separation"),
            new DnaControlDefinition("feetSize", "Feet Size")
        };

        private static readonly string[] LockedBodyControlDnaNames =
        {
            "headSize",
            "armLength",
            "forearmLength",
            "feetSize"
        };

        private const string HeightDnaName = "height";

        private static readonly Color SkinToneDark = new Color(0.27f, 0.18f, 0.12f, 1f);
        private static readonly Color SkinToneLight = new Color(0.98f, 0.86f, 0.78f, 1f);
        private static readonly Color HairToneDark = new Color(0.11f, 0.08f, 0.05f, 1f);
        private static readonly Color HairToneLight = new Color(0.79f, 0.66f, 0.46f, 1f);
        private static readonly Color EyeToneDark = new Color(0.16f, 0.24f, 0.30f, 1f);
        private static readonly Color EyeToneLight = new Color(0.57f, 0.78f, 0.95f, 1f);

        private const string MaleSymbol = "\u2642";
        private const string FemaleSymbol = "\u2640";

        private static Sprite runtimeSolidSprite;

        private DynamicCharacterAvatar previewAvatar;
        private CharacterAppearanceProfile currentProfile = CharacterAppearanceProfile.CreateDefault();

        private bool isInitialized;
        private bool suppressCallbacks;
        private int remainingAutoFramePasses;
        private int activeBodyControlIndex;

        private readonly List<string> raceOptions = new List<string>();
        private readonly List<string> hairOptions = new List<string>();
        private readonly List<string> beardOptions = new List<string>();

        private int raceOptionIndex;
        private int hairOptionIndex;
        private int beardOptionIndex;

        private TMP_Text raceHeaderLabel;
        private TMP_Text raceValueLabel;
        private TMP_Text hairValueLabel;
        private TMP_Text beardValueLabel;
        private TMP_Text bodyControlNameLabel;
        private TMP_Text activeBodyControlHeaderLabel;
        private TMP_Text activeBodyControlValueLabel;

        private Slider activeBodyControlSlider;
        private readonly Dictionary<string, Slider> bodyControlSliders = new Dictionary<string, Slider>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TMP_Text> bodyControlValueLabels = new Dictionary<string, TMP_Text>(StringComparer.OrdinalIgnoreCase);

        private Slider skinToneSlider;
        private TMP_Text skinToneValueLabel;
        private Slider hairToneSlider;
        private TMP_Text hairToneValueLabel;
        private Slider eyeToneSlider;
        private TMP_Text eyeToneValueLabel;

        private Button bodyTabButton;
        private Button hairTabButton;
        private Button skinTabButton;
        private Button presetsTabButton;
        private Button racePrevButton;
        private Button raceNextButton;
        private Button hairPrevButton;
        private Button hairNextButton;
        private Button beardPrevButton;
        private Button beardNextButton;
        private Button randomAppearanceButton;
        private Button resetAppearanceButton;

        private RectTransform bodyTabRoot;
        private RectTransform hairTabRoot;
        private RectTransform skinTabRoot;
        private RectTransform presetsTabRoot;

        private TMP_Text statusLabel;

#if UNITY_EDITOR
        private bool editorUiRefreshQueued;

        private void OnEnable()
        {
            QueueEditorPanelUiRefresh();
        }

        private void OnDisable()
        {
            UnityEditor.EditorApplication.delayCall -= RefreshEditorPanelUiAfterDelay;
            editorUiRefreshQueued = false;
        }

        private void OnValidate()
        {
            QueueEditorPanelUiRefresh();
        }

        private void QueueEditorPanelUiRefresh()
        {
            if (Application.isPlaying || !enableRuntimeCustomizationUi)
            {
                return;
            }

            if (editorUiRefreshQueued)
            {
                return;
            }

            editorUiRefreshQueued = true;
            UnityEditor.EditorApplication.delayCall += RefreshEditorPanelUiAfterDelay;
        }

        private void RefreshEditorPanelUiAfterDelay()
        {
            UnityEditor.EditorApplication.delayCall -= RefreshEditorPanelUiAfterDelay;
            editorUiRefreshQueued = false;

            if (this == null || !isActiveAndEnabled)
            {
                return;
            }

            if (Application.isPlaying || !enableRuntimeCustomizationUi)
            {
                return;
            }

            BuildOrResolveRuntimeUi();
        }
#endif

        public void Initialize(DynamicCharacterAvatar avatar)
        {
            SetPreviewAvatar(avatar);

            if (isInitialized)
            {
                ApplySavedProfile();
                return;
            }

            if (enableRuntimeCustomizationUi)
            {
                BuildOrResolveRuntimeUi();
                BindUiEvents();
            }

            isInitialized = true;
            ApplySavedProfile();
        }

        public void SetPreviewAvatar(DynamicCharacterAvatar avatar)
        {
            previewAvatar = avatar;
            TryResolvePreviewCamera();
            RequestAutoFramePasses();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || remainingAutoFramePasses <= 0)
            {
                return;
            }

            remainingAutoFramePasses--;
            TryAutoFramePreviewCamera();
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
            bodyTabRoot = null;
            hairTabRoot = null;
            skinTabRoot = null;
            presetsTabRoot = null;
            raceHeaderLabel = null;
            raceValueLabel = null;
            hairValueLabel = null;
            beardValueLabel = null;
            skinToneSlider = null;
            hairToneSlider = null;
            eyeToneSlider = null;
            racePrevButton = null;
            raceNextButton = null;
            hairPrevButton = null;
            hairNextButton = null;
            beardPrevButton = null;
            beardNextButton = null;
            randomAppearanceButton = null;
            resetAppearanceButton = null;
            bodyControlNameLabel = null;
            activeBodyControlHeaderLabel = null;
            activeBodyControlValueLabel = null;
            activeBodyControlSlider = null;
            activeBodyControlIndex = 0;
            bodyControlSliders.Clear();
            bodyControlValueLabels.Clear();

            BuildOrRefreshEditorUi();
        }

        public void ApplySavedProfile()
        {
            EnsureCurrentProfileFromState();
            RefreshRaceOptions();
            RefreshWardrobeOptions();
            SyncUiFromCurrentProfile();
            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: false);
            TryAutoFramePreviewCamera();
            RequestAutoFramePasses();
        }

        public string CaptureProfileJson()
        {
            CaptureCurrentProfileFromPreview();
            currentProfile.Sanitize();
            return CharacterAppearanceProfile.Serialize(currentProfile);
        }

        [ContextMenu("Rebuild Customization UI (Edit Mode)")]
        private void RebuildCustomizationUiFromContextMenu()
        {
            RebuildUiFromScratch();
        }

        private void BuildOrResolveRuntimeUi()
        {
            RectTransform panelRect = ResolvePanelRectTransform();
            if (panelRect == null)
            {
                return;
            }

            customizationPanelRoot = ResolveExistingCustomizationPanelRoot(panelRect);
            if (customizationPanelRoot == null)
            {
                customizationPanelRoot = CreateRectTransform("CustomizationPanelRoot", panelRect);
                customizationPanelRoot.anchorMin = panelAnchorMin;
                customizationPanelRoot.anchorMax = panelAnchorMax;
                customizationPanelRoot.offsetMin = Vector2.zero;
                customizationPanelRoot.offsetMax = Vector2.zero;
            }

            EnsureCustomizationPanelRootComponents();

            EnsureRuntimeUiElements();
        }

        private RectTransform ResolveExistingCustomizationPanelRoot(RectTransform panelRect)
        {
            RectTransform existingRoot = null;

            if (customizationPanelRoot != null && string.Equals(customizationPanelRoot.name, "CustomizationPanelRoot", StringComparison.Ordinal))
            {
                existingRoot = customizationPanelRoot;
            }

            if (existingRoot == null)
            {
                if (string.Equals(panelRect.name, "CustomizationPanelRoot", StringComparison.Ordinal))
                {
                    existingRoot = panelRect;
                }
                else
                {
                    existingRoot = FindChildRectTransform(panelRect, "CustomizationPanelRoot");
                }
            }

            return RemoveDuplicateCustomizationPanelRoots(panelRect, existingRoot);
        }

        private RectTransform RemoveDuplicateCustomizationPanelRoots(RectTransform panelRect, RectTransform keepRoot)
        {
            if (panelRect == null)
            {
                return keepRoot;
            }

            if (keepRoot != null && keepRoot != panelRect && !keepRoot.IsChildOf(panelRect))
            {
                keepRoot = null;
            }

            RectTransform[] candidates = panelRect.GetComponentsInChildren<RectTransform>(true);
            for (int index = candidates.Length - 1; index >= 0; index--)
            {
                RectTransform candidate = candidates[index];
                if (candidate == null || !string.Equals(candidate.name, "CustomizationPanelRoot", StringComparison.Ordinal))
                {
                    continue;
                }

                if (keepRoot == null)
                {
                    keepRoot = candidate;
                    continue;
                }

                if (candidate == keepRoot)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(candidate.gameObject);
                }
                else
                {
                    DestroyImmediate(candidate.gameObject);
                }
            }

            return keepRoot;
        }

        private void EnsureCustomizationPanelRootComponents()
        {
            if (customizationPanelRoot == null)
            {
                return;
            }

            Image rootBackground = customizationPanelRoot.GetComponent<Image>();
            if (rootBackground == null)
            {
                rootBackground = customizationPanelRoot.gameObject.AddComponent<Image>();
            }

            Sprite existingRootSprite = rootBackground.sprite;

            rootBackground.sprite = GetSolidSprite();
            rootBackground.type = Image.Type.Sliced;
            rootBackground.color = panelTint;
            rootBackground.raycastTarget = false;

            EnsurePanelBackgroundImage(existingRootSprite);

            VerticalLayoutGroup rootLayout = customizationPanelRoot.GetComponent<VerticalLayoutGroup>();
            if (rootLayout == null)
            {
                rootLayout = customizationPanelRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            int padding = Mathf.RoundToInt(panelInnerPadding);
            rootLayout.padding = new RectOffset(padding, padding, padding, padding);
            rootLayout.spacing = panelSpacing;
            rootLayout.childAlignment = TextAnchor.UpperCenter;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = false;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;
        }

        private void EnsurePanelBackgroundImage(Sprite fallbackSprite)
        {
            if (customizationPanelRoot == null)
            {
                return;
            }

            RectTransform backgroundRect = FindChildRectTransform(customizationPanelRoot, "PanelBackgroundImage");
            if (backgroundRect == null)
            {
                backgroundRect = CreateRectTransform("PanelBackgroundImage", customizationPanelRoot);
            }

            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            backgroundRect.SetAsFirstSibling();

            LayoutElement layout = backgroundRect.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = backgroundRect.gameObject.AddComponent<LayoutElement>();
            }

            layout.ignoreLayout = true;

            Image backgroundImage = backgroundRect.GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = backgroundRect.gameObject.AddComponent<Image>();
            }

            Sprite existingBackgroundSprite = backgroundImage.sprite;
            Sprite effectiveSprite = panelBackgroundSprite != null
                ? panelBackgroundSprite
                : existingBackgroundSprite;

            if (effectiveSprite == null && fallbackSprite != null && fallbackSprite != GetSolidSprite())
            {
                effectiveSprite = fallbackSprite;
            }

            backgroundImage.sprite = effectiveSprite;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = panelBackgroundPreserveAspect;
            backgroundImage.color = effectiveSprite != null ? panelBackgroundTint : Color.clear;
            backgroundImage.raycastTarget = false;
        }

        private void DestroyAllCustomizationPanelRoots()
        {
            RectTransform panelRect = transform as RectTransform;
            if (panelRect == null)
            {
                if (customizationPanelRoot != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(customizationPanelRoot.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(customizationPanelRoot.gameObject);
                    }
                }

                return;
            }

            RectTransform[] candidates = panelRect.GetComponentsInChildren<RectTransform>(true);
            for (int index = candidates.Length - 1; index >= 0; index--)
            {
                RectTransform candidate = candidates[index];
                if (candidate == null || !string.Equals(candidate.name, "CustomizationPanelRoot", StringComparison.Ordinal))
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(candidate.gameObject);
                }
                else
                {
                    DestroyImmediate(candidate.gameObject);
                }
            }
        }

        private void EnsureRuntimeUiElements()
        {
            RectTransform raceRow = FindChildRectTransform(customizationPanelRoot, "RaceRow");
            if (raceRow == null)
            {
                raceRow = CreateRow(customizationPanelRoot, "RaceRow", 34f);
            }

            ConfigureGenderRow(raceRow);

            raceHeaderLabel = FindChildComponent<TMP_Text>(raceRow, "RaceLabel");
            raceValueLabel = FindChildComponent<TMP_Text>(raceRow, "RaceValue");

            if (raceHeaderLabel != null)
            {
                raceHeaderLabel.gameObject.SetActive(false);
            }

            if (raceValueLabel != null)
            {
                raceValueLabel.gameObject.SetActive(false);
            }

            racePrevButton = FindChildComponent<Button>(raceRow, "RacePrevButton");
            if (racePrevButton == null)
            {
                racePrevButton = CreateActionButton(raceRow, "RacePrevButton", MaleSymbol, () => SetRaceByGender(preferMale: true));
            }

            ConfigureGenderButton(racePrevButton, MaleSymbol);

            raceNextButton = FindChildComponent<Button>(raceRow, "RaceNextButton");
            if (raceNextButton == null)
            {
                raceNextButton = CreateActionButton(raceRow, "RaceNextButton", FemaleSymbol, () => SetRaceByGender(preferMale: false));
            }

            ConfigureGenderButton(raceNextButton, FemaleSymbol);

            RectTransform tabRow = FindChildRectTransform(customizationPanelRoot, "TabRow");
            if (tabRow == null)
            {
                tabRow = CreateRow(customizationPanelRoot, "TabRow", 32f);
                HorizontalLayoutGroup layout = tabRow.gameObject.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 4f;

                bodyTabButton = CreateTabButton(tabRow, "BodyTabButton", "Body", () => SetActiveTab("Body"));
                hairTabButton = CreateTabButton(tabRow, "HairTabButton", "Hair", () => SetActiveTab("Hair"));
                skinTabButton = CreateTabButton(tabRow, "SkinTabButton", "Skin", () => SetActiveTab("Skin"));
                presetsTabButton = CreateTabButton(tabRow, "PresetsTabButton", "Presets", () => SetActiveTab("Presets"));
            }
            else
            {
                bodyTabButton = FindChildComponent<Button>(tabRow, "BodyTabButton");
                hairTabButton = FindChildComponent<Button>(tabRow, "HairTabButton");
                skinTabButton = FindChildComponent<Button>(tabRow, "SkinTabButton");
                presetsTabButton = FindChildComponent<Button>(tabRow, "PresetsTabButton");
            }

            RectTransform tabContent = FindChildRectTransform(customizationPanelRoot, "TabContentRoot");
            if (tabContent == null)
            {
                tabContent = CreateRectTransform("TabContentRoot", customizationPanelRoot);
                LayoutElement contentLayout = tabContent.gameObject.AddComponent<LayoutElement>();
                contentLayout.flexibleHeight = 1f;
            }

            bodyTabRoot = EnsureTabRoot(tabContent, "BodyTabRoot");
            hairTabRoot = EnsureTabRoot(tabContent, "HairTabRoot");
            skinTabRoot = EnsureTabRoot(tabContent, "SkinTabRoot");
            presetsTabRoot = EnsureTabRoot(tabContent, "PresetsTabRoot");

            EnsureBodyTabUi();
            EnsureHairTabUi();
            EnsureSkinTabUi();
            EnsurePresetsTabUi();

            SetActiveTab("Body");
        }

        private void EnsureBodyTabUi()
        {
            if (bodyTabRoot == null)
            {
                return;
            }

            RemoveDeprecatedBodySelectorControls();

            bodyControlSliders.Clear();
            bodyControlValueLabels.Clear();

            for (int index = 0; index < BodyControlDefinitions.Length; index++)
            {
                DnaControlDefinition definition = BodyControlDefinitions[index];

                EnsureTwoLineSliderControl(
                    bodyTabRoot,
                    definition.DnaName + "Row",
                    definition.DisplayName,
                    definition.DnaName + "Label",
                    definition.DnaName + "Slider",
                    definition.DnaName + "Value",
                    56f,
                    out Slider slider,
                    out TMP_Text valueLabel);

                if (slider != null)
                {
                    bodyControlSliders[definition.DnaName] = slider;
                }

                if (valueLabel != null)
                {
                    bodyControlValueLabels[definition.DnaName] = valueLabel;
                }
            }

            RefreshAllBodyControlUi();
            RebindBodySliderEvents();
        }

        private void RemoveDeprecatedBodySelectorControls()
        {
            if (bodyTabRoot == null)
            {
                return;
            }

            string[] deprecatedNames =
            {
                "BodyControlSelectorRow",
                "BodyControlSliderRow"
            };

            for (int index = 0; index < deprecatedNames.Length; index++)
            {
                RectTransform legacyRow = FindChildRectTransform(bodyTabRoot, deprecatedNames[index]);
                if (legacyRow != null)
                {
                    DestroyUnityObject(legacyRow.gameObject);
                }
            }

            bodyControlNameLabel = null;
            activeBodyControlHeaderLabel = null;
            activeBodyControlValueLabel = null;
            activeBodyControlSlider = null;
        }

        private void EnsureHairTabUi()
        {
            if (hairTabRoot == null)
            {
                return;
            }

            RectTransform hairRow = FindChildRectTransform(hairTabRoot, "HairRow");
            if (hairRow == null)
            {
                hairRow = CreateRow(hairTabRoot, "HairRow", 34f);
                CreateLabel(hairRow, "HairLabel", "Hair", 16f, FontStyles.Bold, 76f);
                hairPrevButton = CreateCycleButton(hairRow, "HairPrevButton", "<", () => CycleHair(-1));
                hairValueLabel = CreateLabel(hairRow, "HairValue", "None", 15f, FontStyles.Bold);
                hairNextButton = CreateCycleButton(hairRow, "HairNextButton", ">", () => CycleHair(1));
            }
            else
            {
                hairPrevButton = FindChildComponent<Button>(hairRow, "HairPrevButton");
                hairValueLabel = FindChildComponent<TMP_Text>(hairRow, "HairValue");
                hairNextButton = FindChildComponent<Button>(hairRow, "HairNextButton");
            }

            RectTransform beardRow = FindChildRectTransform(hairTabRoot, "BeardRow");
            if (beardRow == null)
            {
                beardRow = CreateRow(hairTabRoot, "BeardRow", 34f);
                CreateLabel(beardRow, "BeardLabel", "Beard", 16f, FontStyles.Bold, 76f);
                beardPrevButton = CreateCycleButton(beardRow, "BeardPrevButton", "<", () => CycleBeard(-1));
                beardValueLabel = CreateLabel(beardRow, "BeardValue", "None", 15f, FontStyles.Bold);
                beardNextButton = CreateCycleButton(beardRow, "BeardNextButton", ">", () => CycleBeard(1));
            }
            else
            {
                beardPrevButton = FindChildComponent<Button>(beardRow, "BeardPrevButton");
                beardValueLabel = FindChildComponent<TMP_Text>(beardRow, "BeardValue");
                beardNextButton = FindChildComponent<Button>(beardRow, "BeardNextButton");
            }

            TMP_Text helpText = FindChildComponent<TMP_Text>(hairTabRoot, "HairHelp");
            if (helpText == null)
            {
                helpText = CreateLabel(
                    hairTabRoot,
                    "HairHelp",
                    "Hair and beard options are filtered by the selected gender.",
                    12f,
                    FontStyles.Italic);
                helpText.color = new Color(0.90f, 0.86f, 0.78f, 0.86f);
                helpText.textWrappingMode = TextWrappingModes.Normal;
                LayoutElement layout = helpText.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 40f;
            }
        }

        private void EnsureSkinTabUi()
        {
            if (skinTabRoot == null)
            {
                return;
            }

            CreateToneRow(
                skinTabRoot,
                "SkinToneRow",
                "Skin Tone",
                out skinToneSlider,
                out skinToneValueLabel,
                HandleSkinToneChanged);

            CreateToneRow(
                skinTabRoot,
                "HairToneRow",
                "Hair Tone",
                out hairToneSlider,
                out hairToneValueLabel,
                HandleHairToneChanged);

            CreateToneRow(
                skinTabRoot,
                "EyeToneRow",
                "Eye Tone",
                out eyeToneSlider,
                out eyeToneValueLabel,
                HandleEyeToneChanged);
        }

        private void EnsurePresetsTabUi()
        {
            if (presetsTabRoot == null)
            {
                return;
            }

            randomAppearanceButton = FindChildComponent<Button>(presetsTabRoot, "RandomAppearanceButton");
            if (randomAppearanceButton == null)
            {
                randomAppearanceButton = CreateActionButton(presetsTabRoot, "RandomAppearanceButton", "RANDOMIZE", RandomizeAppearance);
                randomAppearanceButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            }

            resetAppearanceButton = FindChildComponent<Button>(presetsTabRoot, "ResetAppearanceButton");
            if (resetAppearanceButton == null)
            {
                resetAppearanceButton = CreateActionButton(presetsTabRoot, "ResetAppearanceButton", "RESET", ResetAppearance);
                resetAppearanceButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            }

            statusLabel = FindChildComponent<TMP_Text>(presetsTabRoot, "StatusLabel");
            if (statusLabel == null)
            {
                statusLabel = CreateLabel(
                    presetsTabRoot,
                    "StatusLabel",
                    "Use Randomize for variety, then fine tune in other tabs.",
                    12f,
                    FontStyles.Italic);
                statusLabel.textWrappingMode = TextWrappingModes.Normal;
                statusLabel.color = new Color(0.91f, 0.87f, 0.79f, 0.88f);

                LayoutElement layout = statusLabel.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 60f;
            }
        }

        private void CreateToneRow(
            RectTransform parent,
            string rowName,
            string displayName,
            out Slider slider,
            out TMP_Text valueLabel,
            Action<float> callback)
        {
            EnsureTwoLineSliderControl(
                parent,
                rowName,
                displayName,
                rowName + "Label",
                rowName + "Slider",
                rowName + "Value",
                56f,
                out slider,
                out valueLabel);

            if (slider != null)
            {
                slider.onValueChanged.RemoveAllListeners();
                slider.onValueChanged.AddListener(value => callback?.Invoke(value));
            }
        }

        private void EnsureTwoLineSliderControl(
            RectTransform parent,
            string controlRootName,
            string displayName,
            string labelName,
            string sliderName,
            string valueName,
            float preferredHeight,
            out Slider slider,
            out TMP_Text valueLabel)
        {
            slider = null;
            valueLabel = null;

            if (parent == null)
            {
                return;
            }

            RectTransform controlRoot = FindChildRectTransform(parent, controlRootName);
            if (controlRoot == null)
            {
                controlRoot = CreateRectTransform(controlRootName, parent);
            }

            PrepareTwoLineSliderControlContainer(controlRoot, preferredHeight);

            RectTransform headerRow = FindChildRectTransform(controlRoot, "HeaderRow");
            if (headerRow == null)
            {
                ClearChildren(controlRoot);
                headerRow = CreateRow(controlRoot, "HeaderRow", 22f);

                HorizontalLayoutGroup headerLayout = headerRow.GetComponent<HorizontalLayoutGroup>();
                if (headerLayout != null)
                {
                    headerLayout.spacing = Mathf.Max(4f, rowSpacing * 0.5f);
                }
            }

            TMP_Text headerLabel = FindChildComponent<TMP_Text>(headerRow, labelName);
            if (headerLabel == null)
            {
                headerLabel = CreateLabel(headerRow, labelName, displayName, 14f, FontStyles.Normal);
            }

            headerLabel.text = displayName;

            valueLabel = FindChildComponent<TMP_Text>(headerRow, valueName);
            if (valueLabel == null)
            {
                valueLabel = CreateLabel(headerRow, valueName, "0.50", 13f, FontStyles.Bold, 52f);
            }

            valueLabel.alignment = TextAlignmentOptions.MidlineRight;

            slider = FindChildComponent<Slider>(controlRoot, sliderName);
            if (slider == null)
            {
                slider = CreateSlider(controlRoot, sliderName);
            }

            ConfigureSliderInteractionPresentation(slider);

            LayoutElement sliderLayout = slider.gameObject.GetComponent<LayoutElement>();
            if (sliderLayout == null)
            {
                sliderLayout = slider.gameObject.AddComponent<LayoutElement>();
            }

            sliderLayout.flexibleWidth = 1f;
            float hitHeight = Mathf.Max(18f, sliderHitAreaHeight);
            sliderLayout.minHeight = hitHeight;
            sliderLayout.preferredHeight = hitHeight;

            LayoutElement controlLayout = controlRoot.GetComponent<LayoutElement>();
            if (controlLayout != null)
            {
                // Keep the row tall enough for header + spacing + expanded slider hit area.
                float requiredHeight = 22f + 2f + hitHeight;
                controlLayout.preferredHeight = Mathf.Max(controlLayout.preferredHeight, requiredHeight);
            }
        }

        private static void PrepareTwoLineSliderControlContainer(RectTransform controlRoot, float preferredHeight)
        {
            if (controlRoot == null)
            {
                return;
            }

            HorizontalLayoutGroup horizontalLayout = controlRoot.GetComponent<HorizontalLayoutGroup>();
            if (horizontalLayout != null)
            {
                DestroyComponent(horizontalLayout);
            }

            VerticalLayoutGroup verticalLayout = controlRoot.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout == null)
            {
                verticalLayout = controlRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            verticalLayout.padding = new RectOffset(0, 0, 0, 0);
            verticalLayout.spacing = 2f;
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = false;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            LayoutElement layoutElement = controlRoot.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = controlRoot.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                if (child == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void DestroyComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void BindUiEvents()
        {
            RebindButton(bodyTabButton, () => SetActiveTab("Body"));
            RebindButton(hairTabButton, () => SetActiveTab("Hair"));
            RebindButton(skinTabButton, () => SetActiveTab("Skin"));
            RebindButton(presetsTabButton, () => SetActiveTab("Presets"));

            RebindButton(racePrevButton, () => SetRaceByGender(preferMale: true));
            RebindButton(raceNextButton, () => SetRaceByGender(preferMale: false));
            RebindButton(hairPrevButton, () => CycleHair(-1));
            RebindButton(hairNextButton, () => CycleHair(1));
            RebindButton(beardPrevButton, () => CycleBeard(-1));
            RebindButton(beardNextButton, () => CycleBeard(1));

            RebindButton(randomAppearanceButton, RandomizeAppearance);
            RebindButton(resetAppearanceButton, ResetAppearance);

            RebindBodySliderEvents();

            if (skinToneSlider != null)
            {
                skinToneSlider.onValueChanged.RemoveAllListeners();
                skinToneSlider.onValueChanged.AddListener(HandleSkinToneChanged);
            }

            if (hairToneSlider != null)
            {
                hairToneSlider.onValueChanged.RemoveAllListeners();
                hairToneSlider.onValueChanged.AddListener(HandleHairToneChanged);
            }

            if (eyeToneSlider != null)
            {
                eyeToneSlider.onValueChanged.RemoveAllListeners();
                eyeToneSlider.onValueChanged.AddListener(HandleEyeToneChanged);
            }
        }

        private void RebindBodySliderEvents()
        {
            foreach (KeyValuePair<string, Slider> pair in bodyControlSliders)
            {
                Slider slider = pair.Value;
                if (slider == null)
                {
                    continue;
                }

                string dnaName = pair.Key;
                slider.onValueChanged.RemoveAllListeners();
                slider.onValueChanged.AddListener(value => HandleBodyControlSliderChanged(dnaName, value));
            }
        }

        private static void RebindButton(Button button, Action callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback?.Invoke());
        }

        private void EnsureCurrentProfileFromState()
        {
            string profileJson = CharacterSelectionState.HasSelection
                ? CharacterSelectionState.SelectedAppearanceProfileJson
                : string.Empty;

            if (string.IsNullOrWhiteSpace(profileJson))
            {
                CharacterSelectionState.GetProfileDefaults(out _, out _, out profileJson);
            }

            if (string.IsNullOrWhiteSpace(profileJson))
            {
                CaptureCurrentProfileFromPreview();
                return;
            }

            currentProfile = CharacterAppearanceProfile.Deserialize(profileJson);
            currentProfile.Sanitize();
        }

        private void CaptureCurrentProfileFromPreview()
        {
            if (previewAvatar == null)
            {
                currentProfile = CharacterAppearanceProfile.CreateDefault();
                return;
            }

            UmaAppearanceOperationReport report = UmaAppearanceService.TryCaptureProfile(previewAvatar, out CharacterAppearanceProfile profile);
            if (!report.Success)
            {
                currentProfile = CharacterAppearanceProfile.CreateDefault();
                return;
            }

            currentProfile = profile;
            currentProfile.Sanitize();
        }

        private void RefreshRaceOptions()
        {
            raceOptions.Clear();

            UMAContextBase context = UMAContextBase.Instance;
            if (context != null)
            {
                string femaleRace = ResolvePreferredHumanRaceName(context, preferMale: false);
                string maleRace = ResolvePreferredHumanRaceName(context, preferMale: true);

                if (!string.IsNullOrWhiteSpace(femaleRace))
                {
                    raceOptions.Add(femaleRace);
                }

                if (!string.IsNullOrWhiteSpace(maleRace) &&
                    !string.Equals(femaleRace, maleRace, StringComparison.OrdinalIgnoreCase))
                {
                    raceOptions.Add(maleRace);
                }
            }

            if (raceOptions.Count == 0)
            {
                raceOptions.Add("HumanFemale");
                raceOptions.Add("HumanMale");
            }

            raceOptionIndex = FindOptionIndex(raceOptions, currentProfile.raceName);
            if (raceOptionIndex < 0)
            {
                bool currentIsMale = IsMaleRaceName(currentProfile.raceName);
                raceOptionIndex = FindRaceIndexByGender(raceOptions, currentIsMale);
            }

            if (raceOptionIndex < 0)
            {
                raceOptionIndex = 0;
                currentProfile.raceName = raceOptions[raceOptionIndex];
            }

            currentProfile.raceName = raceOptions[raceOptionIndex];
        }

        private void RefreshWardrobeOptions()
        {
            hairOptions.Clear();
            beardOptions.Clear();

            hairOptions.Add(string.Empty);
            beardOptions.Add(string.Empty);

            UMAContextBase context = UMAContextBase.Instance;
            if (context != null && !string.IsNullOrWhiteSpace(currentProfile.raceName))
            {
                Dictionary<string, List<UMATextRecipe>> recipesBySlot = context.GetRecipes(currentProfile.raceName);
                AddWardrobeRecipesForSlot(recipesBySlot, "Hair", hairOptions);
                AddWardrobeRecipesForSlot(recipesBySlot, "Beard", beardOptions);
                AddWardrobeRecipesForSlot(recipesBySlot, "FacialHair", beardOptions);
            }

            DeduplicateAndSortOptions(hairOptions);
            DeduplicateAndSortOptions(beardOptions);

            hairOptionIndex = FindOptionIndex(hairOptions, currentProfile.wardrobeSelection.hairRecipeName);
            beardOptionIndex = FindOptionIndex(beardOptions, currentProfile.wardrobeSelection.beardRecipeName);

            if (hairOptionIndex < 0)
            {
                hairOptionIndex = 0;
                currentProfile.wardrobeSelection.hairRecipeName = string.Empty;
            }

            if (beardOptionIndex < 0)
            {
                beardOptionIndex = 0;
                currentProfile.wardrobeSelection.beardRecipeName = string.Empty;
            }
        }

        private static void AddWardrobeRecipesForSlot(
            Dictionary<string, List<UMATextRecipe>> recipesBySlot,
            string slotName,
            List<string> targetOptions)
        {
            if (recipesBySlot == null || targetOptions == null)
            {
                return;
            }

            foreach (KeyValuePair<string, List<UMATextRecipe>> pair in recipesBySlot)
            {
                if (!string.Equals(pair.Key, slotName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                List<UMATextRecipe> recipes = pair.Value;
                if (recipes == null)
                {
                    continue;
                }

                for (int index = 0; index < recipes.Count; index++)
                {
                    UMATextRecipe recipe = recipes[index];
                    if (recipe == null || string.IsNullOrWhiteSpace(recipe.name))
                    {
                        continue;
                    }

                    targetOptions.Add(recipe.name.Trim());
                }
            }
        }

        private static void DeduplicateAndSortOptions(List<string> options)
        {
            if (options == null)
            {
                return;
            }

            List<string> nonEmpty = options
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            options.Clear();
            options.Add(string.Empty);
            options.AddRange(nonEmpty);
        }

        private void SyncUiFromCurrentProfile()
        {
            if (!enableRuntimeCustomizationUi || customizationPanelRoot == null)
            {
                return;
            }

            suppressCallbacks = true;

            UpdateGenderButtonState();

            if (hairValueLabel != null)
            {
                hairValueLabel.text = GetOptionDisplayValue(GetOptionValue(hairOptions, hairOptionIndex));
            }

            if (beardValueLabel != null)
            {
                beardValueLabel.text = GetOptionDisplayValue(GetOptionValue(beardOptions, beardOptionIndex));
            }

            RefreshAllBodyControlUi();

            if (skinToneSlider != null)
            {
                float tone = EstimateTone(currentProfile.skinColor, SkinToneDark, SkinToneLight);
                skinToneSlider.SetValueWithoutNotify(tone);
                SetToneValueLabel(skinToneValueLabel, tone);
            }

            if (hairToneSlider != null)
            {
                float tone = EstimateTone(currentProfile.hairColor, HairToneDark, HairToneLight);
                hairToneSlider.SetValueWithoutNotify(tone);
                SetToneValueLabel(hairToneValueLabel, tone);
            }

            if (eyeToneSlider != null)
            {
                float tone = EstimateTone(currentProfile.eyeColor, EyeToneDark, EyeToneLight);
                eyeToneSlider.SetValueWithoutNotify(tone);
                SetToneValueLabel(eyeToneValueLabel, tone);
            }

            suppressCallbacks = false;
        }

        private void RefreshAllBodyControlUi()
        {
            for (int index = 0; index < BodyControlDefinitions.Length; index++)
            {
                DnaControlDefinition definition = BodyControlDefinitions[index];
                float dnaValue = GetDnaValue(definition.DnaName, 0.5f);
                float sliderValue = ConvertBodyDnaToSliderValue(definition.DnaName, dnaValue);

                if (bodyControlSliders.TryGetValue(definition.DnaName, out Slider slider) && slider != null)
                {
                    slider.SetValueWithoutNotify(sliderValue);
                }

                SetBodyControlValueLabel(definition.DnaName, sliderValue);
            }
        }

        private void HandleBodyControlSliderChanged(string dnaName, float value)
        {
            if (suppressCallbacks || string.IsNullOrWhiteSpace(dnaName))
            {
                return;
            }

            float sliderValue = Mathf.Clamp01(value);
            float dnaValue = ConvertBodySliderToDnaValue(dnaName, sliderValue);

            SetDnaValue(dnaName, dnaValue);
            SetBodyControlValueLabel(dnaName, sliderValue);
            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: false, refitCamera: false);
        }

        private float ConvertBodySliderToDnaValue(string dnaName, float sliderValue)
        {
            float t = Mathf.Clamp01(sliderValue);

            if (!string.Equals(dnaName, HeightDnaName, StringComparison.OrdinalIgnoreCase))
            {
                Vector2 range = NormalizeRange(bodyDnaOutputRange, 0.40f, 0.60f);
                if (IsLockedBodyControl(dnaName))
                {
                    range = NormalizeRange(lockedBodyDnaOutputRange, 0.45f, 0.55f);
                }

                return Mathf.Clamp01(Mathf.Lerp(range.x, range.y, t));
            }

            float maxOutput = Mathf.Clamp(bodySliderOutputMax, 0.1f, 1f);
            Vector2 heightN = NormalizeRange(heightDnaOutputRangeNormalized, 0.46f, 0.54f);
            float min = Mathf.Clamp01(heightN.x) * maxOutput;
            float max = Mathf.Clamp01(heightN.y) * maxOutput;
            if (max <= min)
            {
                min = 0.46f * maxOutput;
                max = 0.54f * maxOutput;
            }

            return Mathf.Clamp01(Mathf.Lerp(min, max, t));
        }

        private float ConvertBodyDnaToSliderValue(string dnaName, float dnaValue)
        {
            float v = Mathf.Clamp01(dnaValue);

            if (!string.Equals(dnaName, HeightDnaName, StringComparison.OrdinalIgnoreCase))
            {
                Vector2 range = NormalizeRange(bodyDnaOutputRange, 0.40f, 0.60f);
                if (IsLockedBodyControl(dnaName))
                {
                    range = NormalizeRange(lockedBodyDnaOutputRange, 0.45f, 0.55f);
                }

                float denom = Mathf.Max(0.0001f, range.y - range.x);
                return Mathf.Clamp01((v - range.x) / denom);
            }

            float maxOutput = Mathf.Clamp(bodySliderOutputMax, 0.1f, 1f);
            Vector2 heightN = NormalizeRange(heightDnaOutputRangeNormalized, 0.46f, 0.54f);
            float min = Mathf.Clamp01(heightN.x) * maxOutput;
            float max = Mathf.Clamp01(heightN.y) * maxOutput;
            float heightDenom = Mathf.Max(0.0001f, max - min);
            return Mathf.Clamp01((v - min) / heightDenom);
        }

        private void SetBodyControlValueLabel(string dnaName, float value)
        {
            if (string.IsNullOrWhiteSpace(dnaName))
            {
                return;
            }

            if (!bodyControlValueLabels.TryGetValue(dnaName, out TMP_Text label) || label == null)
            {
                return;
            }

            label.text = value.ToString("0.00");
        }

        private void UpdateGenderButtonState()
        {
            if (raceOptions.Count == 0)
            {
                SetGenderButtonState(racePrevButton, isActive: false, isMaleButton: true);
                SetGenderButtonState(raceNextButton, isActive: false, isMaleButton: false);
                return;
            }

            string selectedRace = GetOptionValue(raceOptions, raceOptionIndex);
            bool isMale = IsMaleRaceName(selectedRace);
            SetGenderButtonState(racePrevButton, isActive: isMale, isMaleButton: true);
            SetGenderButtonState(raceNextButton, isActive: !isMale, isMaleButton: false);
        }

        private void SetGenderButtonState(Button button, bool isActive, bool isMaleButton)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.targetGraphic as Image;
            if (image != null)
            {
                Color activeTint = isMaleButton
                    ? new Color(0.20f, 0.41f, 0.66f, 0.98f)
                    : new Color(0.74f, 0.32f, 0.54f, 0.98f);

                image.color = isActive
                    ? activeTint
                    : tabNormalTint;
            }

            TMP_Text glyph = FindChildComponent<TMP_Text>(button.transform, "GenderGlyph");
            if (glyph != null)
            {
                glyph.color = isActive
                    ? new Color(0.98f, 0.95f, 0.90f, 1f)
                    : new Color(0.90f, 0.84f, 0.74f, 0.94f);
            }
        }

        private void SetRaceByGender(bool preferMale)
        {
            if (suppressCallbacks || raceOptions.Count == 0)
            {
                return;
            }

            int targetIndex = FindRaceIndexByGender(raceOptions, preferMale);
            if (targetIndex < 0)
            {
                return;
            }

            if (targetIndex == raceOptionIndex)
            {
                UpdateGenderButtonState();
                return;
            }

            raceOptionIndex = targetIndex;
            currentProfile.raceName = raceOptions[raceOptionIndex];
            currentProfile.wardrobeSelection.hairRecipeName = string.Empty;
            currentProfile.wardrobeSelection.beardRecipeName = string.Empty;

            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: true);
            RefreshWardrobeOptions();
            SyncUiFromCurrentProfile();
        }

        private static int FindRaceIndexByGender(List<string> options, bool preferMale)
        {
            if (options == null || options.Count == 0)
            {
                return -1;
            }

            for (int index = 0; index < options.Count; index++)
            {
                string candidate = options[index];
                if (preferMale == IsMaleRaceName(candidate))
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool IsMaleRaceName(string raceName)
        {
            string normalized = NormalizeRaceNameToken(raceName);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            if (normalized.Contains("female") || normalized.Contains("girl") || normalized.Contains("woman"))
            {
                return false;
            }

            return normalized.Contains("male") || normalized.Contains("boy") || normalized.Contains("man");
        }

        private static string ResolvePreferredHumanRaceName(UMAContextBase context, bool preferMale)
        {
            if (context == null)
            {
                return string.Empty;
            }

            RaceData[] races = context.GetAllRaces();
            if (races == null || races.Length == 0)
            {
                return string.Empty;
            }

            string bestMatch = string.Empty;
            int bestScore = int.MinValue;

            for (int index = 0; index < races.Length; index++)
            {
                RaceData race = races[index];
                if (race == null || string.IsNullOrWhiteSpace(race.raceName))
                {
                    continue;
                }

                string raceName = race.raceName.Trim();
                string normalized = NormalizeRaceNameToken(raceName);
                if (string.IsNullOrEmpty(normalized) || !normalized.Contains("human"))
                {
                    continue;
                }

                bool isMale = IsMaleRaceName(raceName);
                if (preferMale != isMale)
                {
                    continue;
                }

                int score = 0;
                if (preferMale)
                {
                    if (string.Equals(normalized, "humanmale", StringComparison.Ordinal))
                    {
                        score += 200;
                    }
                    else if (normalized.StartsWith("humanmale", StringComparison.Ordinal))
                    {
                        score += 120;
                    }
                }
                else
                {
                    if (string.Equals(normalized, "humanfemale", StringComparison.Ordinal))
                    {
                        score += 200;
                    }
                    else if (normalized.StartsWith("humanfemale", StringComparison.Ordinal))
                    {
                        score += 120;
                    }
                }

                if (normalized.Contains("highpoly"))
                {
                    score -= 20;
                }

                score -= raceName.Length;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = raceName;
                }
            }

            return bestMatch;
        }

        private static string NormalizeRaceNameToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] buffer = new char[value.Length];
            int count = 0;

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!char.IsLetterOrDigit(character))
                {
                    continue;
                }

                buffer[count] = char.ToLowerInvariant(character);
                count++;
            }

            return new string(buffer, 0, count);
        }

        private void ApplyCurrentProfileToPreview(bool rebuildCharacter, bool recaptureAfterApply, bool refitCamera = true)
        {
            if (previewAvatar == null)
            {
                return;
            }

            currentProfile.Sanitize();

            UmaAppearanceOperationReport report = UmaAppearanceService.TryApplyProfile(previewAvatar, currentProfile, rebuildCharacter);
            if (!report.Success)
            {
                Debug.LogWarning("[CharacterCreatorCustomizationController] Appearance apply failed. " + report.ToMultilineString(), this);
                return;
            }

            if (recaptureAfterApply)
            {
                UmaAppearanceOperationReport captureReport = UmaAppearanceService.TryCaptureProfile(previewAvatar, out CharacterAppearanceProfile profile);
                if (captureReport.Success)
                {
                    currentProfile = profile;
                    currentProfile.Sanitize();
                }
            }

            if (refitCamera)
            {
                TryAutoFramePreviewCamera();
                RequestAutoFramePasses();
            }
        }

        private void RequestAutoFramePasses()
        {
            if (!autoFramePreviewCamera)
            {
                return;
            }

            remainingAutoFramePasses = Mathf.Max(remainingAutoFramePasses, Mathf.Max(1, postApplyRefitFrames));
        }

        private Camera TryResolvePreviewCamera()
        {
            if (previewCamera != null && previewCamera.gameObject.scene == gameObject.scene)
            {
                return previewCamera;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Camera fallbackRenderTextureCamera = null;
            Camera fallbackSceneCamera = null;

            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera == null || camera.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                string lowerName = camera.name.ToLowerInvariant();
                bool isPreviewNamed = lowerName.Contains("preview") || lowerName.Contains("uma");

                if (camera.targetTexture != null)
                {
                    if (isPreviewNamed)
                    {
                        previewCamera = camera;
                        return previewCamera;
                    }

                    if (fallbackRenderTextureCamera == null)
                    {
                        fallbackRenderTextureCamera = camera;
                    }
                }
                else if (fallbackSceneCamera == null)
                {
                    fallbackSceneCamera = camera;
                }
            }

            previewCamera = fallbackRenderTextureCamera != null
                ? fallbackRenderTextureCamera
                : fallbackSceneCamera;

            return previewCamera;
        }

        private void TryAutoFramePreviewCamera()
        {
            if (!autoFramePreviewCamera || previewAvatar == null)
            {
                return;
            }

            Camera camera = TryResolvePreviewCamera();
            if (camera == null)
            {
                return;
            }

            if (!TryGetAvatarBounds(out Bounds bounds))
            {
                return;
            }

            Bounds expandedBounds = ExpandPreviewBounds(bounds);

            Vector3 target = expandedBounds.center + Vector3.up * (expandedBounds.extents.y * previewVerticalCenterBias);

            if (camera.orthographic)
            {
                camera.orthographicSize = expandedBounds.extents.y * (1f + previewVerticalPadding);
                float orthographicDistance = Mathf.Clamp(minPreviewDistance + previewDistanceOffset, minPreviewDistance, maxPreviewDistance);
                camera.transform.position = target - camera.transform.forward * orthographicDistance;
                return;
            }

            float verticalFovRadians = Mathf.Max(1f, camera.fieldOfView) * Mathf.Deg2Rad;
            float aspect = ResolveCameraAspect(camera);
            float horizontalFovRadians = 2f * Mathf.Atan(Mathf.Tan(verticalFovRadians * 0.5f) * Mathf.Max(0.1f, aspect));

            float halfHeight = expandedBounds.extents.y * (1f + previewVerticalPadding);
            float halfWidth = Mathf.Max(expandedBounds.extents.x, expandedBounds.extents.z) * (1f + previewHorizontalPadding);

            float distanceForHeight = halfHeight / Mathf.Tan(verticalFovRadians * 0.5f);
            float distanceForWidth = halfWidth / Mathf.Tan(horizontalFovRadians * 0.5f);
            float distance = Mathf.Clamp(
                Mathf.Max(distanceForHeight, distanceForWidth) + previewDistanceOffset,
                minPreviewDistance,
                maxPreviewDistance);

            camera.transform.position = target - camera.transform.forward * distance;
        }

        private Bounds ExpandPreviewBounds(Bounds sourceBounds)
        {
            float yExtent = sourceBounds.extents.y;
            if (yExtent <= 0.0001f)
            {
                return sourceBounds;
            }

            Bounds expanded = sourceBounds;
            Vector3 top = new Vector3(sourceBounds.center.x, sourceBounds.max.y + yExtent * previewTopPadding, sourceBounds.center.z);
            Vector3 bottom = new Vector3(sourceBounds.center.x, sourceBounds.min.y - yExtent * previewBottomPadding, sourceBounds.center.z);
            expanded.Encapsulate(top);
            expanded.Encapsulate(bottom);
            return expanded;
        }

        private static float ResolveCameraAspect(Camera camera)
        {
            if (camera == null)
            {
                return 16f / 9f;
            }

            if (camera.targetTexture != null && camera.targetTexture.height > 0)
            {
                return (float)camera.targetTexture.width / camera.targetTexture.height;
            }

            return camera.aspect > 0.01f
                ? camera.aspect
                : 16f / 9f;
        }

        private bool TryGetAvatarBounds(out Bounds bounds)
        {
            bounds = default;

            if (previewAvatar == null)
            {
                return false;
            }

            Renderer[] renderers = previewAvatar.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds rendererBounds = renderer.bounds;
                if (rendererBounds.size.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasBounds;
        }

        private void CycleBodyControl(int direction)
        {
            if (suppressCallbacks || BodyControlDefinitions.Length == 0)
            {
                return;
            }

            activeBodyControlIndex = WrapIndex(activeBodyControlIndex + direction, BodyControlDefinitions.Length);
            RefreshActiveBodyControlUi();
        }

        private bool TryGetActiveBodyControlDefinition(out DnaControlDefinition definition)
        {
            if (BodyControlDefinitions == null || BodyControlDefinitions.Length == 0)
            {
                definition = default;
                return false;
            }

            activeBodyControlIndex = WrapIndex(activeBodyControlIndex, BodyControlDefinitions.Length);
            definition = BodyControlDefinitions[activeBodyControlIndex];
            return true;
        }

        private void RefreshActiveBodyControlUi()
        {
            if (!TryGetActiveBodyControlDefinition(out DnaControlDefinition definition))
            {
                return;
            }

            if (bodyControlNameLabel != null)
            {
                bodyControlNameLabel.text = definition.DisplayName;
            }

            if (activeBodyControlHeaderLabel != null)
            {
                activeBodyControlHeaderLabel.text = definition.DisplayName;
            }

            float value = GetDnaValue(definition.DnaName, 0.5f);
            if (activeBodyControlSlider != null)
            {
                activeBodyControlSlider.SetValueWithoutNotify(value);
            }

            SetActiveBodyControlValueLabel(value);
        }

        private void HandleActiveBodyControlSliderChanged(float value)
        {
            if (suppressCallbacks)
            {
                return;
            }

            if (!TryGetActiveBodyControlDefinition(out DnaControlDefinition definition))
            {
                return;
            }

            SetDnaValue(definition.DnaName, value);
            SetActiveBodyControlValueLabel(value);
            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: false, refitCamera: false);
        }

        private void SetActiveBodyControlValueLabel(float value)
        {
            if (activeBodyControlValueLabel == null)
            {
                return;
            }

            activeBodyControlValueLabel.text = value.ToString("0.00");
        }

        private void HandleSkinToneChanged(float value)
        {
            if (suppressCallbacks)
            {
                return;
            }

            currentProfile.skinColor = EvaluateTone(value, SkinToneDark, SkinToneLight);
            SetToneValueLabel(skinToneValueLabel, value);
            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: false);
        }

        private void HandleHairToneChanged(float value)
        {
            if (suppressCallbacks)
            {
                return;
            }

            currentProfile.hairColor = EvaluateTone(value, HairToneDark, HairToneLight);
            SetToneValueLabel(hairToneValueLabel, value);
            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: false);
        }

        private void HandleEyeToneChanged(float value)
        {
            if (suppressCallbacks)
            {
                return;
            }

            currentProfile.eyeColor = EvaluateTone(value, EyeToneDark, EyeToneLight);
            SetToneValueLabel(eyeToneValueLabel, value);
            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: false);
        }

        private void CycleRace(int direction)
        {
            if (suppressCallbacks)
            {
                return;
            }

            if (raceOptions.Count == 0)
            {
                return;
            }

            raceOptionIndex = WrapIndex(raceOptionIndex + direction, raceOptions.Count);
            currentProfile.raceName = raceOptions[raceOptionIndex];
            currentProfile.wardrobeSelection.hairRecipeName = string.Empty;
            currentProfile.wardrobeSelection.beardRecipeName = string.Empty;

            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: true);
            RefreshWardrobeOptions();
            SyncUiFromCurrentProfile();
        }

        private void CycleHair(int direction)
        {
            if (suppressCallbacks || hairOptions.Count == 0)
            {
                return;
            }

            hairOptionIndex = WrapIndex(hairOptionIndex + direction, hairOptions.Count);
            currentProfile.wardrobeSelection.hairRecipeName = GetOptionValue(hairOptions, hairOptionIndex);
            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: false);
            SyncUiFromCurrentProfile();
        }

        private void CycleBeard(int direction)
        {
            if (suppressCallbacks || beardOptions.Count == 0)
            {
                return;
            }

            beardOptionIndex = WrapIndex(beardOptionIndex + direction, beardOptions.Count);
            currentProfile.wardrobeSelection.beardRecipeName = GetOptionValue(beardOptions, beardOptionIndex);
            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: false);
            SyncUiFromCurrentProfile();
        }

        private void RandomizeAppearance()
        {
            if (raceOptions.Count > 0)
            {
                raceOptionIndex = UnityEngine.Random.Range(0, raceOptions.Count);
                currentProfile.raceName = raceOptions[raceOptionIndex];
            }

            Vector2 bodyRange = NormalizeRange(randomBodySliderRange, 0.48f, 0.52f);
            Vector2 heightRange = NormalizeRange(randomHeightSliderRange, 0.49f, 0.51f);
            Vector2 lockedRange = NormalizeRange(randomLockedProportionSliderRange, 0.495f, 0.505f);
            Vector2 skinToneRange = NormalizeRange(randomSkinToneRange, 0.40f, 0.65f);
            Vector2 hairToneRange = NormalizeRange(randomHairToneRange, 0.25f, 0.75f);
            Vector2 eyeToneRange = NormalizeRange(randomEyeToneRange, 0.35f, 0.75f);

            for (int index = 0; index < BodyControlDefinitions.Length; index++)
            {
                DnaControlDefinition definition = BodyControlDefinitions[index];
                Vector2 sliderRange =
                    string.Equals(definition.DnaName, HeightDnaName, StringComparison.OrdinalIgnoreCase)
                        ? heightRange
                        : (IsLockedBodyControl(definition.DnaName) ? lockedRange : bodyRange);
                float randomizedSliderValue = UnityEngine.Random.Range(sliderRange.x, sliderRange.y);
                float randomizedDnaValue = ConvertBodySliderToDnaValue(definition.DnaName, randomizedSliderValue);
                SetDnaValue(definition.DnaName, randomizedDnaValue);
            }

            currentProfile.skinColor = EvaluateTone(UnityEngine.Random.Range(skinToneRange.x, skinToneRange.y), SkinToneDark, SkinToneLight);
            currentProfile.hairColor = EvaluateTone(UnityEngine.Random.Range(hairToneRange.x, hairToneRange.y), HairToneDark, HairToneLight);
            currentProfile.eyeColor = EvaluateTone(UnityEngine.Random.Range(eyeToneRange.x, eyeToneRange.y), EyeToneDark, EyeToneLight);
            currentProfile.wardrobeSelection.hairRecipeName = string.Empty;
            currentProfile.wardrobeSelection.beardRecipeName = string.Empty;

            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: true);
            RefreshWardrobeOptions();

            if (hairOptions.Count > 1)
            {
                hairOptionIndex = UnityEngine.Random.Range(1, hairOptions.Count);
                currentProfile.wardrobeSelection.hairRecipeName = hairOptions[hairOptionIndex];
            }

            if (beardOptions.Count > 1)
            {
                beardOptionIndex = UnityEngine.Random.Range(1, beardOptions.Count);
                currentProfile.wardrobeSelection.beardRecipeName = beardOptions[beardOptionIndex];
            }

            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: false);
            SyncUiFromCurrentProfile();
            SetStatus("Randomized appearance profile (normal variation).");
        }

        private static Vector2 NormalizeRange(Vector2 range, float fallbackMin, float fallbackMax)
        {
            float min = Mathf.Clamp01(Mathf.Min(range.x, range.y));
            float max = Mathf.Clamp01(Mathf.Max(range.x, range.y));

            if (max <= min)
            {
                min = Mathf.Clamp01(Mathf.Min(fallbackMin, fallbackMax));
                max = Mathf.Clamp01(Mathf.Max(fallbackMin, fallbackMax));
            }

            return new Vector2(min, max);
        }

        private static bool IsLockedBodyControl(string dnaName)
        {
            if (string.IsNullOrWhiteSpace(dnaName))
            {
                return false;
            }

            for (int index = 0; index < LockedBodyControlDnaNames.Length; index++)
            {
                if (string.Equals(dnaName, LockedBodyControlDnaNames[index], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void ResetAppearance()
        {
            currentProfile = CharacterAppearanceProfile.CreateDefault();

            for (int index = 0; index < BodyControlDefinitions.Length; index++)
            {
                SetDnaValue(BodyControlDefinitions[index].DnaName, 0.5f);
            }

            raceOptionIndex = FindOptionIndex(raceOptions, currentProfile.raceName);
            if (raceOptionIndex < 0)
            {
                raceOptionIndex = 0;
                if (raceOptions.Count > 0)
                {
                    currentProfile.raceName = raceOptions[0];
                }
            }

            ApplyCurrentProfileToPreview(rebuildCharacter: true, recaptureAfterApply: true);
            RefreshWardrobeOptions();
            SyncUiFromCurrentProfile();
            SetStatus("Reset to default appearance.");
        }

        private void SetActiveTab(string tabName)
        {
            if (bodyTabRoot != null)
            {
                bodyTabRoot.gameObject.SetActive(string.Equals(tabName, "Body", StringComparison.Ordinal));
            }

            if (hairTabRoot != null)
            {
                hairTabRoot.gameObject.SetActive(string.Equals(tabName, "Hair", StringComparison.Ordinal));
            }

            if (skinTabRoot != null)
            {
                skinTabRoot.gameObject.SetActive(string.Equals(tabName, "Skin", StringComparison.Ordinal));
            }

            if (presetsTabRoot != null)
            {
                presetsTabRoot.gameObject.SetActive(string.Equals(tabName, "Presets", StringComparison.Ordinal));
            }

            SetTabButtonState(bodyTabButton, string.Equals(tabName, "Body", StringComparison.Ordinal));
            SetTabButtonState(hairTabButton, string.Equals(tabName, "Hair", StringComparison.Ordinal));
            SetTabButtonState(skinTabButton, string.Equals(tabName, "Skin", StringComparison.Ordinal));
            SetTabButtonState(presetsTabButton, string.Equals(tabName, "Presets", StringComparison.Ordinal));
        }

        private void SetTabButtonState(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.targetGraphic as Image;
            if (image != null)
            {
                image.color = isActive ? tabActiveTint : tabNormalTint;
            }
        }

        private RectTransform EnsureTabRoot(RectTransform parent, string name)
        {
            RectTransform tabRoot = FindChildRectTransform(parent, name);
            if (tabRoot != null)
            {
                tabRoot.anchorMin = Vector2.zero;
                tabRoot.anchorMax = Vector2.one;
                tabRoot.offsetMin = Vector2.zero;
                tabRoot.offsetMax = Vector2.zero;
                return tabRoot;
            }

            tabRoot = CreateRectTransform(name, parent);
            tabRoot.anchorMin = Vector2.zero;
            tabRoot.anchorMax = Vector2.one;
            tabRoot.offsetMin = Vector2.zero;
            tabRoot.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = tabRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 4, 0);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LayoutElement layoutElement = tabRoot.gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1f;

            return tabRoot;
        }

        private RectTransform ResolvePanelRectTransform()
        {
            RectTransform selfRect = transform as RectTransform;
            if (selfRect != null)
            {
                return selfRect;
            }

            if (customizationPanelRoot != null)
            {
                return customizationPanelRoot.parent as RectTransform;
            }

            return null;
        }

        private static RectTransform CreateRectTransform(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            return rectTransform;
        }

        private RectTransform CreateRow(Transform parent, string rowName, float preferredHeight)
        {
            RectTransform row = CreateRectTransform(rowName, parent);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = rowSpacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            LayoutElement layoutElement = row.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;
            return row;
        }

        private TMP_Text CreateLabel(
            Transform parent,
            string name,
            string value,
            float fontSize,
            FontStyles fontStyle,
            float preferredWidth = -1f)
        {
            RectTransform labelRect = CreateRectTransform(name, parent);
            TMP_Text text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = new Color(0.95f, 0.90f, 0.78f, 1f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            LayoutElement layout = labelRect.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth > 0f)
            {
                layout.preferredWidth = preferredWidth;
                // Allow labels to shrink on narrow resolutions so slider controls remain visible.
                layout.minWidth = Mathf.Min(preferredWidth, 56f);
            }
            else
            {
                layout.flexibleWidth = 1f;
            }

            return text;
        }

        private Button CreateCycleButton(Transform parent, string name, string label, Action callback)
        {
            Button button = CreateActionButton(parent, name, label, callback);
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 30f;
            layout.minWidth = 30f;
            layout.preferredHeight = 30f;
            return button;
        }

        private static void ConfigureGenderRow(RectTransform raceRow)
        {
            if (raceRow == null)
            {
                return;
            }

            HorizontalLayoutGroup layout = raceRow.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                return;
            }

            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
        }

        private void ConfigureGenderButton(Button button, string symbol)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(true);
            button.interactable = true;

            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = button.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = 54f;
            layout.minWidth = 54f;
            layout.preferredHeight = 34f;
            layout.flexibleWidth = 0f;

            Image image = button.targetGraphic as Image;
            if (image != null)
            {
                image.sprite = GetSolidSprite();
                image.type = Image.Type.Sliced;
            }

            Text legacyText = button.GetComponentInChildren<Text>(true);
            if (legacyText != null)
            {
                legacyText.gameObject.SetActive(false);
            }

            TMP_Text glyph = FindChildComponent<TMP_Text>(button.transform, "GenderGlyph");
            if (glyph == null)
            {
                RectTransform glyphRect = CreateRectTransform("GenderGlyph", button.transform);
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
        }

        private void ConfigureSelectorCycleButton(Button button, string symbol)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(true);
            button.interactable = true;

            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = button.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = 34f;
            layout.minWidth = 34f;
            layout.preferredHeight = 30f;

            Image image = button.targetGraphic as Image;
            if (image != null)
            {
                image.sprite = GetSolidSprite();
                image.type = Image.Type.Sliced;
                image.color = new Color(0.36f, 0.15f, 0.08f, 0.98f);
            }

            Text legacyText = button.GetComponentInChildren<Text>(true);
            if (legacyText != null)
            {
                legacyText.text = symbol;
                legacyText.fontStyle = FontStyle.Bold;
                legacyText.fontSize = 20;
                legacyText.alignment = TextAnchor.MiddleCenter;
                legacyText.color = new Color(0.97f, 0.92f, 0.84f, 1f);
                legacyText.resizeTextForBestFit = false;
            }

            TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                tmpText.text = symbol;
                tmpText.fontStyle = FontStyles.Bold;
                tmpText.fontSize = 18f;
                tmpText.alignment = TextAlignmentOptions.Center;
                tmpText.color = new Color(0.97f, 0.92f, 0.84f, 1f);
                tmpText.enableAutoSizing = false;
            }
        }

        private Button CreateTabButton(Transform parent, string name, string label, Action callback)
        {
            Button button = CreateActionButton(parent, name, label, callback);
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.preferredHeight = 30f;
            return button;
        }

        private Button CreateActionButton(Transform parent, string name, string label, Action callback)
        {
            GameObject buttonObject = DefaultControls.CreateButton(new DefaultControls.Resources());
            buttonObject.name = name;
            buttonObject.transform.SetParent(parent, false);

            Button button = buttonObject.GetComponent<Button>();
            Image image = buttonObject.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = GetSolidSprite();
                image.type = Image.Type.Sliced;
                image.color = tabNormalTint;
            }

            Text legacyText = buttonObject.GetComponentInChildren<Text>(true);
            if (legacyText != null)
            {
                legacyText.text = label;
                legacyText.fontStyle = FontStyle.Bold;
                legacyText.color = new Color(0.96f, 0.91f, 0.80f, 1f);
                legacyText.resizeTextForBestFit = false;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback?.Invoke());
            return button;
        }

        private Slider CreateSlider(Transform parent, string name)
        {
            GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = name;
            sliderObject.transform.SetParent(parent, false);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            LayoutElement layout = sliderObject.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = sliderObject.AddComponent<LayoutElement>();
            }

            layout.flexibleWidth = 1f;
            layout.minWidth = 90f;
            layout.preferredWidth = 160f;

            Sprite solidSprite = GetSolidSprite();

            Image background = sliderObject.transform.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = solidSprite;
                background.type = Image.Type.Sliced;
                background.color = new Color(0.13f, 0.13f, 0.13f, 0.96f);
            }

            Image fill = sliderObject.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
            if (fill != null)
            {
                fill.sprite = solidSprite;
                fill.type = Image.Type.Sliced;
                fill.color = new Color(0.83f, 0.36f, 0.19f, 0.95f);
            }

            Image handle = sliderObject.transform.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
            if (handle != null)
            {
                handle.sprite = solidSprite;
                handle.type = Image.Type.Sliced;
                handle.color = new Color(0.96f, 0.82f, 0.67f, 1f);
            }

            ConfigureSliderInteractionPresentation(slider);

            return slider;
        }

        private void ConfigureSliderInteractionPresentation(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            RectTransform sliderRect = slider.transform as RectTransform;
            if (sliderRect == null)
            {
                return;
            }

            float hitHeight = Mathf.Max(18f, sliderHitAreaHeight);
            float trackHeight = Mathf.Clamp(sliderTrackVisualHeight, 2f, hitHeight);
            float handleSize = Mathf.Clamp(sliderHandleVisualSize, 6f, hitHeight);
            float halfTrack = trackHeight * 0.5f;
            const float horizontalInset = 10f;

            Image hitAreaImage = FindChildComponent<Image>(slider.transform, "HitArea");
            if (hitAreaImage == null)
            {
                RectTransform hitAreaRect = CreateRectTransform("HitArea", slider.transform);
                hitAreaRect.SetAsFirstSibling();
                hitAreaImage = hitAreaRect.gameObject.AddComponent<Image>();
            }

            RectTransform hitRect = hitAreaImage.rectTransform;
            hitRect.anchorMin = Vector2.zero;
            hitRect.anchorMax = Vector2.one;
            hitRect.offsetMin = Vector2.zero;
            hitRect.offsetMax = Vector2.zero;

            hitAreaImage.sprite = GetSolidSprite();
            hitAreaImage.type = Image.Type.Sliced;
            hitAreaImage.color = new Color(1f, 1f, 1f, 0f);
            hitAreaImage.raycastTarget = true;

            RectTransform backgroundRect = slider.transform.Find("Background") as RectTransform;
            if (backgroundRect != null)
            {
                backgroundRect.anchorMin = new Vector2(0f, 0.5f);
                backgroundRect.anchorMax = new Vector2(1f, 0.5f);
                backgroundRect.offsetMin = new Vector2(horizontalInset, -halfTrack);
                backgroundRect.offsetMax = new Vector2(-horizontalInset, halfTrack);
            }

            RectTransform fillAreaRect = slider.transform.Find("Fill Area") as RectTransform;
            if (fillAreaRect != null)
            {
                fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
                fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
                fillAreaRect.offsetMin = new Vector2(horizontalInset, -halfTrack);
                fillAreaRect.offsetMax = new Vector2(-horizontalInset, halfTrack);
            }

            RectTransform handleSlideAreaRect = slider.transform.Find("Handle Slide Area") as RectTransform;
            if (handleSlideAreaRect != null)
            {
                handleSlideAreaRect.anchorMin = Vector2.zero;
                handleSlideAreaRect.anchorMax = Vector2.one;
                handleSlideAreaRect.offsetMin = new Vector2(horizontalInset, 0f);
                handleSlideAreaRect.offsetMax = new Vector2(-horizontalInset, 0f);
            }

            RectTransform handleRect = slider.handleRect;
            if (handleRect != null)
            {
                handleRect.anchorMin = new Vector2(0.5f, 0.5f);
                handleRect.anchorMax = new Vector2(0.5f, 0.5f);
                handleRect.sizeDelta = new Vector2(handleSize, handleSize);
            }
        }

        private static Sprite GetSolidSprite()
        {
            if (runtimeSolidSprite != null)
            {
                return runtimeSolidSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            texture.hideFlags = HideFlags.HideAndDontSave;

            runtimeSolidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            runtimeSolidSprite.hideFlags = HideFlags.HideAndDontSave;
            return runtimeSolidSprite;
        }

        private static RectTransform FindChildRectTransform(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            Transform child = parent.Find(childName);
            return child as RectTransform;
        }

        private static T FindChildComponent<T>(Transform parent, string childName) where T : Component
        {
            RectTransform child = FindChildRectTransform(parent, childName);
            if (child == null)
            {
                return null;
            }

            return child.GetComponent<T>();
        }

        private float GetDnaValue(string dnaName, float fallback)
        {
            CharacterDnaEntry entry = FindDnaEntry(dnaName);
            if (entry == null)
            {
                return fallback;
            }

            return Mathf.Clamp01(entry.dnaValue);
        }

        private void SetDnaValue(string dnaName, float value)
        {
            CharacterDnaEntry entry = FindDnaEntry(dnaName);
            if (entry == null)
            {
                entry = new CharacterDnaEntry(dnaName, value);
                currentProfile.bodyValues.Add(entry);
                return;
            }

            entry.dnaValue = Mathf.Clamp01(value);
        }

        private CharacterDnaEntry FindDnaEntry(string dnaName)
        {
            if (currentProfile.bodyValues == null)
            {
                currentProfile.bodyValues = new List<CharacterDnaEntry>();
            }

            for (int index = 0; index < currentProfile.bodyValues.Count; index++)
            {
                CharacterDnaEntry entry = currentProfile.bodyValues[index];
                if (entry == null)
                {
                    continue;
                }

                if (string.Equals(entry.dnaName, dnaName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private static void SetToneValueLabel(TMP_Text label, float value)
        {
            if (label == null)
            {
                return;
            }

            label.text = value.ToString("0.00");
        }

        private static int FindOptionIndex(List<string> options, string value)
        {
            if (options == null || options.Count == 0)
            {
                return -1;
            }

            for (int index = 0; index < options.Count; index++)
            {
                if (string.Equals(options[index], value, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static int WrapIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int wrapped = index % count;
            if (wrapped < 0)
            {
                wrapped += count;
            }

            return wrapped;
        }

        private static string GetOptionValue(List<string> options, int index)
        {
            if (options == null || options.Count == 0)
            {
                return string.Empty;
            }

            int safeIndex = Mathf.Clamp(index, 0, options.Count - 1);
            string value = options[safeIndex];
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string GetOptionDisplayValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "None"
                : value;
        }

        private static Color EvaluateTone(float value, Color dark, Color light)
        {
            Color color = Color.Lerp(dark, light, Mathf.Clamp01(value));
            color.a = 1f;
            return color;
        }

        private static float EstimateTone(Color color, Color dark, Color light)
        {
            Vector3 colorVector = new Vector3(color.r, color.g, color.b);
            Vector3 darkVector = new Vector3(dark.r, dark.g, dark.b);
            Vector3 lightVector = new Vector3(light.r, light.g, light.b);

            Vector3 axis = lightVector - darkVector;
            float denominator = Vector3.Dot(axis, axis);
            if (denominator <= 0.0001f)
            {
                return 0.5f;
            }

            float projection = Vector3.Dot(colorVector - darkVector, axis) / denominator;
            return Mathf.Clamp01(projection);
        }

        private void SetStatus(string message)
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.text = string.IsNullOrWhiteSpace(message)
                ? string.Empty
                : message.Trim();
        }

        private readonly struct DnaControlDefinition
        {
            public readonly string DnaName;
            public readonly string DisplayName;

            public DnaControlDefinition(string dnaName, string displayName)
            {
                DnaName = dnaName;
                DisplayName = displayName;
            }
        }
    }
}
