using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Helper for building Hair, Skin, and Presets tabs in the character creator.
    /// </summary>
    public static class CharacterCreatorTabBuilderHelper
    {
        public static void EnsureHairTabUi(
            RectTransform hairTabRoot,
            Action<int> cycleHair,
            Action<int> cycleBeard,
            out Button hairPrevButton,
            out TMP_Text hairValueLabel,
            out Button hairNextButton,
            out Button beardPrevButton,
            out TMP_Text beardValueLabel,
            out Button beardNextButton,
            float rowSpacing,
            Color tabNormalTint,
            Action playUiInteractionSfx)
        {
            hairPrevButton = null;
            hairValueLabel = null;
            hairNextButton = null;
            beardPrevButton = null;
            beardValueLabel = null;
            beardNextButton = null;

            if (hairTabRoot == null) return;

            var hairRow = CharacterCreatorUIFactory.FindChildRectTransform(hairTabRoot, "HairRow");
            if (hairRow == null)
            {
                hairRow = CharacterCreatorUIFactory.CreateRow(hairTabRoot, "HairRow", 34f, rowSpacing);
                CharacterCreatorUIFactory.CreateLabel(hairRow, "HairLabel", "Hair", 16f, FontStyles.Bold, 76f);
                hairPrevButton = CharacterCreatorUIFactory.CreateCycleButton(hairRow, "HairPrevButton", "<", () => cycleHair?.Invoke(-1), tabNormalTint, playUiInteractionSfx);
                hairValueLabel = CharacterCreatorUIFactory.CreateLabel(hairRow, "HairValue", "None", 15f, FontStyles.Bold);
                hairNextButton = CharacterCreatorUIFactory.CreateCycleButton(hairRow, "HairNextButton", ">", () => cycleHair?.Invoke(1), tabNormalTint, playUiInteractionSfx);
            }
            else
            {
                hairPrevButton = CharacterCreatorUIFactory.FindChildComponent<Button>(hairRow, "HairPrevButton");
                hairValueLabel = CharacterCreatorUIFactory.FindChildComponent<TMP_Text>(hairRow, "HairValue");
                hairNextButton = CharacterCreatorUIFactory.FindChildComponent<Button>(hairRow, "HairNextButton");
            }

            var beardRow = CharacterCreatorUIFactory.FindChildRectTransform(hairTabRoot, "BeardRow");
            if (beardRow == null)
            {
                beardRow = CharacterCreatorUIFactory.CreateRow(hairTabRoot, "BeardRow", 34f, rowSpacing);
                CharacterCreatorUIFactory.CreateLabel(beardRow, "BeardLabel", "Beard", 16f, FontStyles.Bold, 76f);
                beardPrevButton = CharacterCreatorUIFactory.CreateCycleButton(beardRow, "BeardPrevButton", "<", () => cycleBeard?.Invoke(-1), tabNormalTint, playUiInteractionSfx);
                beardValueLabel = CharacterCreatorUIFactory.CreateLabel(beardRow, "BeardValue", "None", 15f, FontStyles.Bold);
                beardNextButton = CharacterCreatorUIFactory.CreateCycleButton(beardRow, "BeardNextButton", ">", () => cycleBeard?.Invoke(1), tabNormalTint, playUiInteractionSfx);
            }
            else
            {
                beardPrevButton = CharacterCreatorUIFactory.FindChildComponent<Button>(beardRow, "BeardPrevButton");
                beardValueLabel = CharacterCreatorUIFactory.FindChildComponent<TMP_Text>(beardRow, "BeardValue");
                beardNextButton = CharacterCreatorUIFactory.FindChildComponent<Button>(beardRow, "BeardNextButton");
            }

            var helpText = CharacterCreatorUIFactory.FindChildComponent<TMP_Text>(hairTabRoot, "HairHelp");
            if (helpText == null)
            {
                helpText = CharacterCreatorUIFactory.CreateLabel(
                    hairTabRoot,
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

        public static void EnsureSkinTabUi(
            RectTransform skinTabRoot,
            float sliderHitAreaHeight,
            float sliderTrackVisualHeight,
            float sliderHandleVisualSize,
            float rowSpacing,
            Action<float> handleSkinToneChanged,
            Action<float> handleHairToneChanged,
            Action<float> handleEyeToneChanged,
            out Slider skinToneSlider,
            out TMP_Text skinToneValueLabel,
            out Slider hairToneSlider,
            out TMP_Text hairToneValueLabel,
            out Slider eyeToneSlider,
            out TMP_Text eyeToneValueLabel)
        {
            skinToneSlider = null;
            skinToneValueLabel = null;
            hairToneSlider = null;
            hairToneValueLabel = null;
            eyeToneSlider = null;
            eyeToneValueLabel = null;

            if (skinTabRoot == null) return;

            CharacterCreatorBodyTabHelper.CreateToneRow(
                skinTabRoot,
                "SkinToneRow",
                "Skin Tone",
                sliderHitAreaHeight,
                sliderTrackVisualHeight,
                sliderHandleVisualSize,
                rowSpacing,
                out skinToneSlider,
                out skinToneValueLabel,
                handleSkinToneChanged);

            CharacterCreatorBodyTabHelper.CreateToneRow(
                skinTabRoot,
                "HairToneRow",
                "Hair Tone",
                sliderHitAreaHeight,
                sliderTrackVisualHeight,
                sliderHandleVisualSize,
                rowSpacing,
                out hairToneSlider,
                out hairToneValueLabel,
                handleHairToneChanged);

            CharacterCreatorBodyTabHelper.CreateToneRow(
                skinTabRoot,
                "EyeToneRow",
                "Eye Tone",
                sliderHitAreaHeight,
                sliderTrackVisualHeight,
                sliderHandleVisualSize,
                rowSpacing,
                out eyeToneSlider,
                out eyeToneValueLabel,
                handleEyeToneChanged);
        }

        public static void EnsurePresetsTabUi(
            RectTransform presetsTabRoot,
            Action randomizeAppearance,
            Action resetAppearance,
            out Button randomAppearanceButton,
            out Button resetAppearanceButton,
            out TMP_Text statusLabel,
            Color tabNormalTint,
            Action playUiInteractionSfx)
        {
            randomAppearanceButton = null;
            resetAppearanceButton = null;
            statusLabel = null;

            if (presetsTabRoot == null) return;

            randomAppearanceButton = CharacterCreatorUIFactory.FindChildComponent<Button>(presetsTabRoot, "RandomAppearanceButton");
            if (randomAppearanceButton == null)
            {
                randomAppearanceButton = CharacterCreatorUIFactory.CreateActionButton(presetsTabRoot, "RandomAppearanceButton", "RANDOMIZE",
                    randomizeAppearance, tabNormalTint, playUiInteractionSfx);
                randomAppearanceButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            }

            resetAppearanceButton = CharacterCreatorUIFactory.FindChildComponent<Button>(presetsTabRoot, "ResetAppearanceButton");
            if (resetAppearanceButton == null)
            {
                resetAppearanceButton =
                    CharacterCreatorUIFactory.CreateActionButton(presetsTabRoot, "ResetAppearanceButton", "RESET", resetAppearance, tabNormalTint, playUiInteractionSfx);
                resetAppearanceButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            }

            statusLabel = CharacterCreatorUIFactory.FindChildComponent<TMP_Text>(presetsTabRoot, "StatusLabel");
            if (statusLabel == null)
            {
                statusLabel = CharacterCreatorUIFactory.CreateLabel(
                    presetsTabRoot,
                    "StatusLabel",
                    "Use Randomize for variety, then fine tune in other tabs.",
                    12f,
                    FontStyles.Italic);
                statusLabel.textWrappingMode = TextWrappingModes.Normal;
                statusLabel.color = new Color(0.91f, 0.87f, 0.79f, 0.88f);

                var layout = statusLabel.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 60f;
            }
        }
    }
}
