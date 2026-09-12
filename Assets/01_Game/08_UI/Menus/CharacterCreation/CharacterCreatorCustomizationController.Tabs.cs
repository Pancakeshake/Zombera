using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI.Menus.CharacterCreation
{
    public sealed partial class CharacterCreatorCustomizationController
    {
        private void EnsureBodyTabUi()
        {
            CharacterCreatorBodyTabHelper.EnsureBodyTabUi(
                _bodyTabRoot,
                optionCatalog,
                _bodyControlSliders,
                _bodyControlValueLabels,
                SliderHitAreaHeight,
                SliderTrackVisualHeight,
                SliderHandleVisualSize,
                RowSpacing,
                RefreshAllBodyControlUi,
                RebindBodySliderEvents);
        }

        private void RemoveDeprecatedBodySelectorControls()
        {
            CharacterCreatorBodyTabHelper.RemoveDeprecatedBodySelectorControls(_bodyTabRoot);
        }

        private RectTransform EnsureBodySliderScrollContent()
        {
            return CharacterCreatorBodyTabHelper.EnsureBodySliderScrollContent(
                _bodyTabRoot,
                RowSpacing,
                SliderHitAreaHeight,
                SliderTrackVisualHeight,
                SliderHandleVisualSize);
        }

        public static void EnsureBodySliderVerticalScrollbar(RectTransform sliderScrollArea, RectTransform viewport,
            ScrollRect bodyScrollRect)
        {
            if (sliderScrollArea == null || viewport == null || bodyScrollRect == null) return;

            const float scrollbarWidth = 14f;
            const float scrollbarInsetFromEdge = 2f;
            const float viewportEdge = 2f;
            const float viewportRightInset = scrollbarWidth + scrollbarInsetFromEdge + viewportEdge;

            viewport.offsetMin = new Vector2(viewportEdge, viewportEdge);
            viewport.offsetMax = new Vector2(-viewportRightInset, -viewportEdge);

            var scrollbarRect = sliderScrollArea.Find("ScrollbarVertical") as RectTransform;
            if (scrollbarRect == null)
            {
                GameObject scrollbarGo = new("ScrollbarVertical", typeof(RectTransform));
                scrollbarRect = scrollbarGo.GetComponent<RectTransform>();
                scrollbarRect.SetParent(sliderScrollArea, false);
            }

            scrollbarRect.SetAsLastSibling();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.sizeDelta = new Vector2(scrollbarWidth, 0f);
            scrollbarRect.anchoredPosition = Vector2.zero;
            scrollbarRect.offsetMin = new Vector2(-scrollbarWidth - scrollbarInsetFromEdge, scrollbarInsetFromEdge);
            scrollbarRect.offsetMax = new Vector2(-scrollbarInsetFromEdge, -scrollbarInsetFromEdge);

            var trackImage = scrollbarRect.GetComponent<Image>() ?? scrollbarRect.gameObject.AddComponent<Image>();
            trackImage.sprite = GetSolidSprite();
            trackImage.type = Image.Type.Sliced;
            trackImage.color = new Color(0.22f, 0.20f, 0.18f, 0.98f);
            trackImage.raycastTarget = true;

            var scrollbar = scrollbarRect.GetComponent<Scrollbar>() ??
                            scrollbarRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.size = 0.25f;

            var slidingArea = EnsureDirectChildRectTransform(scrollbarRect, "Sliding Area");
            slidingArea.anchorMin = Vector2.zero;
            slidingArea.anchorMax = Vector2.one;
            slidingArea.offsetMin = new Vector2(1f, 2f);
            slidingArea.offsetMax = new Vector2(-1f, -2f);

            var handleRect = EnsureDirectChildRectTransform(slidingArea, "Handle");
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;

            var handleImage = handleRect.GetComponent<Image>() ?? handleRect.gameObject.AddComponent<Image>();
            handleImage.sprite = GetSolidSprite();
            handleImage.type = Image.Type.Sliced;
            handleImage.color = new Color(0.74f, 0.68f, 0.58f, 1f);

            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;

            bodyScrollRect.verticalScrollbar = scrollbar;
            bodyScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            bodyScrollRect.verticalScrollbarSpacing = 2f;
        }

        private void RemoveLegacyBodyRowsOutsideScrollContent(RectTransform bodySliderContent)
        {
            CharacterCreatorBodyTabHelper.RemoveLegacyBodyRowsOutsideScrollContent(_bodyTabRoot, bodySliderContent, optionCatalog);
        }

        private void EnsureHairTabUi()
        {
            if (_hairTabRoot == null) return;

            var hairRow = FindChildRectTransform(_hairTabRoot, "HairRow");
            if (hairRow == null)
            {
                hairRow = CreateRow(_hairTabRoot, "HairRow", 34f);
                CreateLabel(hairRow, "HairLabel", "Hair", 16f, FontStyles.Bold, 76f);
                _hairPrevButton = CreateCycleButton(hairRow, "HairPrevButton", "<", () => CycleHair(-1));
                _hairValueLabel = CreateLabel(hairRow, "HairValue", "None", 15f, FontStyles.Bold);
                _hairNextButton = CreateCycleButton(hairRow, "HairNextButton", ">", () => CycleHair(1));
            }
            else
            {
                _hairPrevButton = FindChildComponent<Button>(hairRow, "HairPrevButton");
                _hairValueLabel = FindChildComponent<TMP_Text>(hairRow, "HairValue");
                _hairNextButton = FindChildComponent<Button>(hairRow, "HairNextButton");
            }

            var beardRow = FindChildRectTransform(_hairTabRoot, "BeardRow");
            if (beardRow == null)
            {
                beardRow = CreateRow(_hairTabRoot, "BeardRow", 34f);
                CreateLabel(beardRow, "BeardLabel", "Beard", 16f, FontStyles.Bold, 76f);
                _beardPrevButton = CreateCycleButton(beardRow, "BeardPrevButton", "<", () => CycleBeard(-1));
                _beardValueLabel = CreateLabel(beardRow, "BeardValue", "None", 15f, FontStyles.Bold);
                _beardNextButton = CreateCycleButton(beardRow, "BeardNextButton", ">", () => CycleBeard(1));
            }
            else
            {
                _beardPrevButton = FindChildComponent<Button>(beardRow, "BeardPrevButton");
                _beardValueLabel = FindChildComponent<TMP_Text>(beardRow, "BeardValue");
                _beardNextButton = FindChildComponent<Button>(beardRow, "BeardNextButton");
            }

            var helpText = FindChildComponent<TMP_Text>(_hairTabRoot, "HairHelp");
            if (helpText == null)
            {
                helpText = CreateLabel(
                    _hairTabRoot,
                    "HairHelp",
                    "Hair and beard options are filtered by the selected gender.",
                    12f,
                    FontStyles.Italic);
                helpText.color = new Color(0.90f, 0.86f, 0.78f, 0.86f);
                helpText.textWrappingMode = TextWrappingModes.Normal;
                var layout = helpText.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 40f;
            }
        }

        private void EnsureSkinTabUi()
        {
            if (_skinTabRoot == null) return;

            CreateToneRow(
                _skinTabRoot,
                "SkinToneRow",
                "Skin Tone",
                out _skinToneSlider,
                out _skinToneValueLabel,
                HandleSkinToneChanged);

            CreateToneRow(
                _skinTabRoot,
                "HairToneRow",
                "Hair Tone",
                out _hairToneSlider,
                out _hairToneValueLabel,
                HandleHairToneChanged);

            CreateToneRow(
                _skinTabRoot,
                "EyeToneRow",
                "Eye Tone",
                out _eyeToneSlider,
                out _eyeToneValueLabel,
                HandleEyeToneChanged);
        }

        private void EnsurePresetsTabUi()
        {
            if (_presetsTabRoot == null) return;

            _randomAppearanceButton = FindChildComponent<Button>(_presetsTabRoot, "RandomAppearanceButton");
            if (_randomAppearanceButton == null)
            {
                _randomAppearanceButton = CreateActionButton(_presetsTabRoot, "RandomAppearanceButton", "RANDOMIZE",
                    RandomizeAppearance);
                _randomAppearanceButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            }

            _resetAppearanceButton = FindChildComponent<Button>(_presetsTabRoot, "ResetAppearanceButton");
            if (_resetAppearanceButton == null)
            {
                _resetAppearanceButton =
                    CreateActionButton(_presetsTabRoot, "ResetAppearanceButton", "RESET", ResetAppearance);
                _resetAppearanceButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            }

            _statusLabel = FindChildComponent<TMP_Text>(_presetsTabRoot, "StatusLabel");
            if (_statusLabel == null)
            {
                _statusLabel = CreateLabel(
                    _presetsTabRoot,
                    "StatusLabel",
                    "Use Randomize for variety, then fine tune in other tabs.",
                    12f,
                    FontStyles.Italic);
                _statusLabel.textWrappingMode = TextWrappingModes.Normal;
                _statusLabel.color = new Color(0.91f, 0.87f, 0.79f, 0.88f);

                const float statusLabelPreferredHeight = 60f;
                var layout = _statusLabel.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = statusLabelPreferredHeight;
            }
        }

        private static void CreateToneRow(
            RectTransform parent,
            string rowName,
            string displayName,
            out Slider slider,
            out TMP_Text valueLabel,
            Action<float> callback)
        {
            CharacterCreatorBodyTabHelper.CreateToneRow(
                parent,
                rowName,
                displayName,
                SliderHitAreaHeight,
                SliderTrackVisualHeight,
                SliderHandleVisualSize,
                RowSpacing,
                out slider,
                out valueLabel,
                callback);
        }

        private static void EnsureTwoLineSliderControl(
            RectTransform parent,
            string controlRootName,
            string displayName,
            float preferredHeight,
            out Slider slider,
            out TMP_Text valueLabel)
        {
            slider = null;
            valueLabel = null;

            if (parent == null) return;

            var controlRoot = FindChildRectTransform(parent, controlRootName) ??
                               CreateRectTransform(controlRootName, parent);
            PrepareTwoLineSliderControlContainer(controlRoot, preferredHeight);
            var labelName = controlRootName + "Label";
            var sliderName = controlRootName + "Slider";
            var valueName = controlRootName + "Value";

            var headerRow = FindChildRectTransform(controlRoot, "HeaderRow");
            if (headerRow == null)
            {
                ClearChildren(controlRoot);
                headerRow = CreateRow(controlRoot, "HeaderRow", 22f);
            }

            var headerRowLayout = headerRow.GetComponent<HorizontalLayoutGroup>();
            if (headerRowLayout != null)
            {
                headerRowLayout.spacing = Mathf.Max(4f, RowSpacing * 0.5f);
                headerRowLayout.childAlignment = TextAnchor.MiddleLeft;
                headerRowLayout.childControlWidth = true;
                headerRowLayout.childControlHeight = true;
                headerRowLayout.childForceExpandWidth = false;
                headerRowLayout.childForceExpandHeight = false;
            }

            var headerLayoutElement = headerRow.GetComponent<LayoutElement>();
            if (headerLayoutElement != null)
            {
                headerLayoutElement.minWidth = 0f;
                headerLayoutElement.preferredWidth = 0f;
                headerLayoutElement.flexibleWidth = 1f;
            }

            var headerLabel = FindChildComponent<TMP_Text>(headerRow, labelName) ??
                               CreateLabel(headerRow, labelName, displayName, 14f, FontStyles.Normal);
            headerLabel.text = displayName;

            valueLabel = FindChildComponent<TMP_Text>(headerRow, valueName) ??
                         CreateLabel(headerRow, valueName, "0.50", 13f, FontStyles.Bold, 44f);
            valueLabel.alignment = TextAlignmentOptions.MidlineRight;
            valueLabel.textWrappingMode = TextWrappingModes.NoWrap;
            valueLabel.overflowMode = TextOverflowModes.Truncate;

            var valueLabelLayout = valueLabel.GetComponent<LayoutElement>();
            if (valueLabelLayout != null)
            {
                valueLabelLayout.minWidth = 40f;
                valueLabelLayout.preferredWidth = 44f;
                valueLabelLayout.flexibleWidth = 0f;
            }

            slider = FindChildComponent<Slider>(controlRoot, sliderName) ?? CreateSlider(controlRoot, sliderName);
            ConfigureSliderInteractionPresentation(slider);

            var sliderLayout = slider.gameObject.GetComponent<LayoutElement>() ??
                                slider.gameObject.AddComponent<LayoutElement>();
            sliderLayout.flexibleWidth = 1f;
            sliderLayout.minWidth = 0f;
            sliderLayout.preferredWidth = 0f;
            var hitHeight = Mathf.Max(18f, SliderHitAreaHeight);
            sliderLayout.minHeight = hitHeight;
            sliderLayout.preferredHeight = hitHeight;

            var controlLayout = controlRoot.GetComponent<LayoutElement>();
            if (controlLayout != null)
            {
                // Keep the row tall enough for header + spacing + expanded slider hit area.
                var requiredHeight = 22f + 2f + hitHeight;
                controlLayout.minHeight = Mathf.Max(controlLayout.minHeight, requiredHeight);
                controlLayout.preferredHeight = Mathf.Max(controlLayout.preferredHeight, requiredHeight);
            }
        }

        private static void PrepareTwoLineSliderControlContainer(RectTransform controlRoot, float preferredHeight)
        {
            if (controlRoot == null) return;

            var horizontalLayout = controlRoot.GetComponent<HorizontalLayoutGroup>();
            if (horizontalLayout != null) DestroyComponent(horizontalLayout);

            var verticalLayout = controlRoot.GetComponent<VerticalLayoutGroup>() ??
                                 controlRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayout.padding = new RectOffset(0, 0, 0, 0);
            verticalLayout.spacing = 2f;
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            var layoutElement = controlRoot.GetComponent<LayoutElement>() ??
                                controlRoot.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = preferredHeight;
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.minWidth = 0f;
            layoutElement.preferredWidth = 0f;
            layoutElement.flexibleWidth = 1f;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;

            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                var child = parent.GetChild(index);
                if (child == null) continue;

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
            if (component == null) return;

            if (Application.isPlaying)
                Destroy(component);
            else
                DestroyImmediate(component);
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null) return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
