using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI.Menus.CharacterCreation
{
    public sealed partial class CharacterCreatorCustomizationController
    {
        private void BuildOrResolveRuntimeUi()
        {
            var panelRect = ResolvePanelRectTransform();
            if (panelRect == null) return;

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

            if (customizationPanelRoot != null && string.Equals(customizationPanelRoot.name, "CustomizationPanelRoot",
                    StringComparison.Ordinal)) existingRoot = customizationPanelRoot;

            if (existingRoot == null)
            {
                if (string.Equals(panelRect.name, "CustomizationPanelRoot", StringComparison.Ordinal))
                    existingRoot = panelRect;
                else
                    existingRoot = FindChildRectTransform(panelRect, "CustomizationPanelRoot");
            }

            return RemoveDuplicateCustomizationPanelRoots(panelRect, existingRoot);
        }

        private static RectTransform RemoveDuplicateCustomizationPanelRoots(RectTransform panelRect,
            RectTransform keepRoot)
        {
            if (panelRect == null) return keepRoot;

            if (keepRoot != null && keepRoot != panelRect && !keepRoot.IsChildOf(panelRect)) keepRoot = null;

            var candidates = panelRect.GetComponentsInChildren<RectTransform>(true);
            for (var index = candidates.Length - 1; index >= 0; index--)
            {
                var candidate = candidates[index];
                if (candidate == null ||
                    !string.Equals(candidate.name, "CustomizationPanelRoot", StringComparison.Ordinal)) continue;

                if (keepRoot == null)
                {
                    keepRoot = candidate;
                    continue;
                }

                if (candidate == keepRoot) continue;

                if (Application.isPlaying)
                    Destroy(candidate.gameObject);
                else
                    DestroyImmediate(candidate.gameObject);
            }

            return keepRoot;
        }

        private void EnsureCustomizationPanelRootComponents()
        {
            if (customizationPanelRoot == null) return;

            var rootBackground = customizationPanelRoot.GetComponent<Image>() ??
                                 customizationPanelRoot.gameObject.AddComponent<Image>();
            var existingRootSprite = rootBackground.sprite;

            rootBackground.sprite = GetSolidSprite();
            rootBackground.type = Image.Type.Sliced;
            rootBackground.color = panelTint;
            rootBackground.raycastTarget = false;

            EnsurePanelBackgroundImage(existingRootSprite);

            var rootLayout = customizationPanelRoot.GetComponent<VerticalLayoutGroup>() ??
                             customizationPanelRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            var padding = Mathf.RoundToInt(PanelInnerPadding);
            rootLayout.padding = new RectOffset(padding, padding, padding, padding);
            rootLayout.spacing = PanelSpacing;
            rootLayout.childAlignment = TextAnchor.UpperCenter;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;
        }

        private void EnsurePanelBackgroundImage(Sprite fallbackSprite)
        {
            if (customizationPanelRoot == null) return;

            var backgroundRect = FindChildRectTransform(customizationPanelRoot, "PanelBackgroundImage") ??
                                 CreateRectTransform("PanelBackgroundImage", customizationPanelRoot);
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            backgroundRect.SetAsFirstSibling();

            var layout = backgroundRect.GetComponent<LayoutElement>() ??
                         backgroundRect.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            var backgroundImage =
                backgroundRect.GetComponent<Image>() ?? backgroundRect.gameObject.AddComponent<Image>();
            var existingBackgroundSprite = backgroundImage.sprite;
            var effectiveSprite = existingBackgroundSprite;

            if (effectiveSprite == null && fallbackSprite != null && fallbackSprite != GetSolidSprite())
                effectiveSprite = fallbackSprite;

            backgroundImage.sprite = effectiveSprite;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = PanelBackgroundPreserveAspect;
            backgroundImage.color = effectiveSprite != null ? panelBackgroundTint : Color.clear;
            backgroundImage.raycastTarget = false;
        }

        private void DestroyAllCustomizationPanelRoots()
        {
            var panelRect = transform as RectTransform;
            if (panelRect == null)
            {
                if (customizationPanelRoot != null)
                {
                    if (Application.isPlaying)
                        Destroy(customizationPanelRoot.gameObject);
                    else
                        DestroyImmediate(customizationPanelRoot.gameObject);
                }

                return;
            }

            var candidates = panelRect.GetComponentsInChildren<RectTransform>(true);
            for (var index = candidates.Length - 1; index >= 0; index--)
            {
                var candidate = candidates[index];
                if (candidate == null ||
                    !string.Equals(candidate.name, "CustomizationPanelRoot", StringComparison.Ordinal)) continue;

                if (Application.isPlaying)
                    Destroy(candidate.gameObject);
                else
                    DestroyImmediate(candidate.gameObject);
            }
        }

        private void EnsureRuntimeUiElements()
        {
            var raceRow = FindChildRectTransform(customizationPanelRoot, "RaceRow");
            var topBarGenderRow = FindTopBarGenderRow();

            if (raceRow == null && topBarGenderRow == null) raceRow = CreateRow(customizationPanelRoot, "RaceRow", 34f);

            if (topBarGenderRow != null && raceRow != null && raceRow != topBarGenderRow)
                CollapseAndHideLegacyRaceRow(raceRow);

            var activeGenderRow = topBarGenderRow ?? raceRow;

            ConfigureGenderRow(activeGenderRow);

            HideRaceSummaryChrome(activeGenderRow);

            _racePrevButton = FindChildComponent<Button>(activeGenderRow, "RacePrevButton") ??
                              CreateActionButton(activeGenderRow, "RacePrevButton", MaleSymbol,
                                  () => SetRaceByGender(true));
            ConfigureGenderButton(_racePrevButton, MaleSymbol);

            _raceNextButton = FindChildComponent<Button>(activeGenderRow, "RaceNextButton") ??
                              CreateActionButton(activeGenderRow, "RaceNextButton", FemaleSymbol,
                                  () => SetRaceByGender(false));
            ConfigureGenderButton(_raceNextButton, FemaleSymbol);

            var tabRow = FindChildRectTransform(customizationPanelRoot, "TabRow");
            if (tabRow == null)
            {
                tabRow = CreateRow(customizationPanelRoot, "TabRow", 32f);
                var layout = tabRow.gameObject.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 4f;

                _bodyTabButton = CreateTabButton(tabRow, "BodyTabButton", "Body", () => SetActiveTab("Body"));
                _hairTabButton = CreateTabButton(tabRow, "HairTabButton", "Hair", () => SetActiveTab("Hair"));
                _skinTabButton = CreateTabButton(tabRow, "SkinTabButton", "Skin", () => SetActiveTab("Skin"));
                _presetsTabButton =
                    CreateTabButton(tabRow, "PresetsTabButton", "Presets", () => SetActiveTab("Presets"));
            }
            else
            {
                _bodyTabButton = FindChildComponent<Button>(tabRow, "BodyTabButton");
                _hairTabButton = FindChildComponent<Button>(tabRow, "HairTabButton");
                _skinTabButton = FindChildComponent<Button>(tabRow, "SkinTabButton");
                _presetsTabButton = FindChildComponent<Button>(tabRow, "PresetsTabButton");
            }

            var tabContent = FindChildRectTransform(customizationPanelRoot, "TabContentRoot") ??
                             CreateRectTransform("TabContentRoot", customizationPanelRoot);
            tabContent.anchorMin = Vector2.zero;
            tabContent.anchorMax = Vector2.one;
            tabContent.offsetMin = Vector2.zero;
            tabContent.offsetMax = Vector2.zero;

            var tabContentLayoutElement = tabContent.GetComponent<LayoutElement>() ??
                                          tabContent.gameObject.AddComponent<LayoutElement>();
            tabContentLayoutElement.minHeight = 200f;
            tabContentLayoutElement.preferredHeight = 0f;
            tabContentLayoutElement.flexibleHeight = 1f;
            tabContentLayoutElement.flexibleWidth = 1f;

            _bodyTabRoot = EnsureTabRoot(tabContent, "BodyTabRoot");
            _hairTabRoot = EnsureTabRoot(tabContent, "HairTabRoot");
            _skinTabRoot = EnsureTabRoot(tabContent, "SkinTabRoot");
            _presetsTabRoot = EnsureTabRoot(tabContent, "PresetsTabRoot");

            EnsureBodyTabUi();
            EnsureHairTabUi();
            EnsureSkinTabUi();
            EnsurePresetsTabUi();

            SetActiveTab("Body");
        }

        private RectTransform FindTopBarGenderRow()
        {
            var panelRect = ResolvePanelRectTransform();
            if (panelRect == null) return null;

            var topBar = FindChildRectTransform(panelRect, "TopBar");
            if (topBar == null) return null;

            var directGenderRow = FindChildRectTransform(topBar, "GenderMetaRow");
            if (directGenderRow != null) return directGenderRow;

            var legacyMetaTabs = FindChildRectTransform(topBar, "MetaTabs");
            if (legacyMetaTabs == null) return null;

            var hasGenderButtons =
                FindChildRectTransform(legacyMetaTabs, "RacePrevButton") != null ||
                FindChildRectTransform(legacyMetaTabs, "RaceNextButton") != null;

            return hasGenderButtons ? legacyMetaTabs : null;
        }

        private static void CollapseAndHideLegacyRaceRow(RectTransform raceRow)
        {
            if (raceRow == null) return;

            var layout = raceRow.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minHeight = 0f;
                layout.preferredHeight = 0f;
                layout.flexibleHeight = 0f;
                layout.ignoreLayout = true;
            }

            raceRow.gameObject.SetActive(false);
        }
    }
}
