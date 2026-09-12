using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Helper for assembling the Body tab in character creator.
    /// </summary>
    public static class CharacterCreatorBodyTabHelper
    {
        public static void EnsureBodyTabUi(
            RectTransform bodyTabRoot,
            CharacterAppearanceCatalog optionCatalog,
            Dictionary<string, Slider> bodyControlSliders,
            Dictionary<string, TMP_Text> bodyControlValueLabels,
            float sliderHitAreaHeight,
            float sliderTrackVisualHeight,
            float sliderHandleVisualSize,
            float rowSpacing,
            Action refreshAllBodyControlUi,
            Action rebindBodySliderEvents)
        {
            if (bodyTabRoot == null) return;

            RemoveDeprecatedBodySelectorControls(bodyTabRoot);

            var bodySliderContent = EnsureBodySliderScrollContent(bodyTabRoot, rowSpacing, sliderHitAreaHeight, sliderTrackVisualHeight, sliderHandleVisualSize);
            if (bodySliderContent == null) return;

            RemoveLegacyBodyRowsOutsideScrollContent(bodyTabRoot, bodySliderContent, optionCatalog);

            bodyControlSliders.Clear();
            bodyControlValueLabels.Clear();

            if (optionCatalog != null)
                foreach (var definition in optionCatalog.dnaControls)
                {
                    EnsureTwoLineSliderControl(
                        bodySliderContent,
                        definition.dnaName + "Row",
                        definition.displayName,
                        56f,
                        sliderHitAreaHeight,
                        sliderTrackVisualHeight,
                        sliderHandleVisualSize,
                        rowSpacing,
                        out var slider,
                        out var valueLabel);

                    if (slider != null) bodyControlSliders[definition.dnaName] = slider;

                    if (valueLabel != null) bodyControlValueLabels[definition.dnaName] = valueLabel;
                }

            refreshAllBodyControlUi?.Invoke();
            rebindBodySliderEvents?.Invoke();
        }

        public static void RemoveDeprecatedBodySelectorControls(RectTransform bodyTabRoot)
        {
            if (bodyTabRoot == null) return;

            string[] deprecatedNames =
            {
                "BodyControlSelectorRow",
                "BodyControlSliderRow",
                "BodySelector",
                "RaceSelector",
                "GenderRow"
            };

            for (var index = 0; index < deprecatedNames.Length; index++)
            {
                var legacyRow = CharacterCreatorUIFactory.FindChildRectTransform(bodyTabRoot, deprecatedNames[index]);
                if (legacyRow != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(legacyRow.gameObject);
                    else
                        UnityEngine.Object.DestroyImmediate(legacyRow.gameObject);
                }
            }
        }

        public static RectTransform EnsureBodySliderScrollContent(
            RectTransform bodyTabRoot, 
            float rowSpacing,
            float sliderHitAreaHeight,
            float sliderTrackVisualHeight,
            float sliderHandleVisualSize)
        {
            if (bodyTabRoot == null) return null;

            var sliderScrollArea = CharacterCreatorUIFactory.FindChildRectTransform(bodyTabRoot, "SliderScrollArea");
            if (sliderScrollArea == null || sliderScrollArea.parent != bodyTabRoot)
                sliderScrollArea = CharacterCreatorUIFactory.CreateRectTransform("SliderScrollArea", bodyTabRoot);

            sliderScrollArea.anchorMin = Vector2.zero;
            sliderScrollArea.anchorMax = Vector2.one;
            sliderScrollArea.offsetMin = Vector2.zero;
            sliderScrollArea.offsetMax = Vector2.zero;

            var sliderAreaLayout = sliderScrollArea.GetComponent<LayoutElement>() ??
                                   sliderScrollArea.gameObject.AddComponent<LayoutElement>();
            sliderAreaLayout.ignoreLayout = false;
            sliderAreaLayout.minHeight = 120f;
            sliderAreaLayout.preferredHeight = 0f;
            sliderAreaLayout.flexibleHeight = 1f;
            sliderAreaLayout.flexibleWidth = 1f;

            var sliderAreaImage = sliderScrollArea.GetComponent<Image>() ??
                                  sliderScrollArea.gameObject.AddComponent<Image>();
            sliderAreaImage.sprite = CharacterCreatorUIFactory.GetSolidSprite();
            sliderAreaImage.type = Image.Type.Sliced;
            sliderAreaImage.color = new Color(1f, 1f, 1f, 0.001f);
            sliderAreaImage.raycastTarget = true;

            var bodyScrollRect = sliderScrollArea.GetComponent<ScrollRect>() ??
                                 sliderScrollArea.gameObject.AddComponent<ScrollRect>();
            bodyScrollRect.horizontal = false;
            bodyScrollRect.vertical = true;
            bodyScrollRect.inertia = true;
            bodyScrollRect.movementType = ScrollRect.MovementType.Clamped;
            bodyScrollRect.scrollSensitivity = 24f;

            var viewport = CharacterCreatorUIFactory.EnsureDirectChildRectTransform(sliderScrollArea, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(2f, 2f);
            viewport.offsetMax = new Vector2(-2f, -2f);

            var viewportImage = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>();
            viewportImage.sprite = CharacterCreatorUIFactory.GetSolidSprite();
            viewportImage.type = Image.Type.Sliced;
            viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
            viewportImage.raycastTarget = true;

            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();

            var content = CharacterCreatorUIFactory.EnsureDirectChildRectTransform(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var contentLayout = content.GetComponent<VerticalLayoutGroup>() ??
                                content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(0, 0, 2, 0);
            contentLayout.spacing = Mathf.Max(2f, rowSpacing);
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var contentSizeFitter = content.GetComponent<ContentSizeFitter>() ??
                                    content.gameObject.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            bodyScrollRect.viewport = viewport;
            bodyScrollRect.content = content;

            EnsureBodySliderVerticalScrollbar(sliderScrollArea, viewport, bodyScrollRect);

            return content;
        }

        public static void EnsureBodySliderVerticalScrollbar(
            RectTransform sliderScrollArea, 
            RectTransform viewport,
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
            trackImage.sprite = CharacterCreatorUIFactory.GetSolidSprite();
            trackImage.type = Image.Type.Sliced;
            trackImage.color = new Color(0.22f, 0.20f, 0.18f, 0.98f);
            trackImage.raycastTarget = true;

            var scrollbar = scrollbarRect.GetComponent<Scrollbar>() ??
                            scrollbarRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.size = 0.25f;

            var slidingArea = CharacterCreatorUIFactory.EnsureDirectChildRectTransform(scrollbarRect, "Sliding Area");
            slidingArea.anchorMin = Vector2.zero;
            slidingArea.anchorMax = Vector2.one;
            slidingArea.offsetMin = new Vector2(1f, 2f);
            slidingArea.offsetMax = new Vector2(-1f, -2f);

            var handleRect = CharacterCreatorUIFactory.EnsureDirectChildRectTransform(slidingArea, "Handle");
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;

            var handleImage = handleRect.GetComponent<Image>() ?? handleRect.gameObject.AddComponent<Image>();
            handleImage.sprite = CharacterCreatorUIFactory.GetSolidSprite();
            handleImage.type = Image.Type.Sliced;
            handleImage.color = new Color(0.74f, 0.68f, 0.58f, 1f);

            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;

            bodyScrollRect.verticalScrollbar = scrollbar;
            bodyScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            bodyScrollRect.verticalScrollbarSpacing = 2f;
        }

        public static void RemoveLegacyBodyRowsOutsideScrollContent(
            RectTransform bodyTabRoot, 
            RectTransform bodySliderContent,
            CharacterAppearanceCatalog optionCatalog)
        {
            if (bodyTabRoot == null || bodySliderContent == null) return;

            HashSet<string> bodyRowNames = new(StringComparer.Ordinal);

            if (optionCatalog != null)
                foreach (var definition in optionCatalog.dnaControls)
                    bodyRowNames.Add(definition.dnaName + "Row");

            var descendants = bodyTabRoot.GetComponentsInChildren<RectTransform>(true);
            for (var index = descendants.Length - 1; index >= 0; index--)
            {
                var candidate = descendants[index];
                if (candidate == null || candidate == bodyTabRoot) continue;

                if (candidate == bodySliderContent || candidate.IsChildOf(bodySliderContent)) continue;

                if (!bodyRowNames.Contains(candidate.name)) continue;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(candidate.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(candidate.gameObject);
            }
        }

        public static void CreateToneRow(
            RectTransform parent,
            string rowName,
            string displayName,
            float sliderHitAreaHeight,
            float sliderTrackVisualHeight,
            float sliderHandleVisualSize,
            float rowSpacing,
            out Slider slider,
            out TMP_Text valueLabel,
            Action<float> callback)
        {
            EnsureTwoLineSliderControl(
                parent,
                rowName,
                displayName,
                56f,
                sliderHitAreaHeight,
                sliderTrackVisualHeight,
                sliderHandleVisualSize,
                rowSpacing,
                out slider,
                out valueLabel);

            if (slider != null)
            {
                slider.onValueChanged.RemoveAllListeners();
                slider.onValueChanged.AddListener(value => callback?.Invoke(value));
            }
        }

        public static void EnsureTwoLineSliderControl(
            RectTransform parent,
            string controlRootName,
            string displayName,
            float preferredHeight,
            float sliderHitAreaHeight,
            float sliderTrackVisualHeight,
            float sliderHandleVisualSize,
            float rowSpacing,
            out Slider slider,
            out TMP_Text valueLabel)
        {
            slider = null;
            valueLabel = null;

            if (parent == null) return;

            var controlRoot = CharacterCreatorUIFactory.EnsureDirectChildRectTransform(parent, controlRootName);
            PrepareTwoLineSliderControlContainer(controlRoot, preferredHeight);
            var labelName = controlRootName + "Label";
            var sliderName = controlRootName + "Slider";
            var valueName = controlRootName + "Value";

            var headerRow = controlRoot.Find("HeaderRow") as RectTransform;
            if (headerRow == null)
            {
                ClearChildren(controlRoot);
                headerRow = CharacterCreatorUIFactory.CreateRow(controlRoot, "HeaderRow", 22f, rowSpacing);
            }

            var headerRowLayout = headerRow.GetComponent<HorizontalLayoutGroup>();
            if (headerRowLayout != null)
            {
                headerRowLayout.spacing = Mathf.Max(4f, rowSpacing * 0.5f);
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

            var headerLabel = GetDirectChildComponent<TMP_Text>(headerRow, labelName) ??
                              CharacterCreatorUIFactory.CreateLabel(headerRow, labelName, displayName, 14f, FontStyles.Normal);
            headerLabel.text = displayName;

            valueLabel = GetDirectChildComponent<TMP_Text>(headerRow, valueName) ??
                         CharacterCreatorUIFactory.CreateLabel(headerRow, valueName, "0.50", 13f, FontStyles.Bold, 44f);
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

            slider = GetDirectChildComponent<Slider>(controlRoot, sliderName) ??
                     CharacterCreatorUIFactory.CreateSlider(controlRoot, sliderName, sliderHitAreaHeight, sliderTrackVisualHeight, sliderHandleVisualSize);
            CharacterCreatorUIFactory.ConfigureSliderInteractionPresentation(slider, sliderHitAreaHeight, sliderTrackVisualHeight, sliderHandleVisualSize);

            var sliderLayout = slider.gameObject.GetComponent<LayoutElement>() ??
                               slider.gameObject.AddComponent<LayoutElement>();
            sliderLayout.flexibleWidth = 1f;
            sliderLayout.minWidth = 0f;
            sliderLayout.preferredWidth = 0f;
            var hitHeight = Mathf.Max(18f, sliderHitAreaHeight);
            sliderLayout.minHeight = hitHeight;
            sliderLayout.preferredHeight = hitHeight;
        }

        private static T GetDirectChildComponent<T>(Transform parent, string childName) where T : Component
        {
            if (parent == null || string.IsNullOrWhiteSpace(childName)) return null;

            var child = parent.Find(childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        public static void PrepareTwoLineSliderControlContainer(RectTransform controlRoot, float preferredHeight)
        {
            if (controlRoot == null) return;

            var layout = controlRoot.GetComponent<VerticalLayoutGroup>() ??
                         controlRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 0f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var layoutElement = controlRoot.GetComponent<LayoutElement>() ??
                                controlRoot.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.minHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;
        }

        public static void ClearChildren(Transform parent)
        {
            if (parent == null) return;

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(child.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
