using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Core;

namespace Zombera.UI.Menus.CharacterCreation
{
/// <summary>
    ///     Helper for managing character appearance profiles and synchronizing available options.
    /// </summary>
    public static class CharacterCreatorProfileSyncHelper
    {
        public static CharacterAppearanceProfile EnsureCurrentProfileFromState(GameObject previewAvatar)
        {
            var profileJson = CharacterSelectionState.HasSelection
                ? CharacterSelectionState.SelectedAppearanceProfileJson
                : string.Empty;

            if (string.IsNullOrWhiteSpace(profileJson))
                CharacterSelectionState.GetProfileDefaults(out _, out _, out profileJson);

            if (string.IsNullOrWhiteSpace(profileJson))
                return CaptureCurrentProfileFromPreview(previewAvatar);

            var profile = CharacterAppearanceProfile.Deserialize(profileJson);
            profile.Sanitize();
            return profile;
        }

        public static CharacterAppearanceProfile CaptureCurrentProfileFromPreview(GameObject previewAvatar)
        {
            if (previewAvatar == null)
            {
                var defaultProfile = CharacterAppearanceProfile.CreateDefault();
                defaultProfile.Sanitize();
                return defaultProfile;
            }

            var report = AppearanceProfileService.TryCaptureProfile(previewAvatar, out var profile);
            if (!report.Success)
            {
                var defaultProfile = CharacterAppearanceProfile.CreateDefault();
                defaultProfile.Sanitize();
                return defaultProfile;
            }

            profile.Sanitize();
            return profile;
        }

        public static void RefreshRaceOptions(
            CharacterAppearanceCatalog optionCatalog,
            CharacterAppearanceProfile currentProfile,
            List<string> raceOptions,
            out int raceOptionIndex)
        {
            raceOptions.Clear();

            if (optionCatalog != null)
                foreach (var race in optionCatalog.races)
                    raceOptions.Add(race.name);

            if (raceOptions.Count == 0)
            {
                raceOptions.Add("HumanFemale");
                raceOptions.Add("HumanMale");
            }

            raceOptionIndex = CharacterCreatorUtility.FindOptionIndex(raceOptions, currentProfile.raceName);
            if (raceOptionIndex < 0)
            {
                var currentIsMale = CharacterCreatorUtility.IsMaleRaceName(currentProfile.raceName);
                raceOptionIndex = CharacterCreatorUtility.FindRaceIndexByGender(raceOptions, currentIsMale);
            }

            if (raceOptionIndex < 0)
            {
                raceOptionIndex = 0;
            }

            currentProfile.raceName = raceOptions[raceOptionIndex];
        }

        public static void RefreshWardrobeOptions(
            CharacterAppearanceCatalog optionCatalog,
            CharacterAppearanceProfile currentProfile,
            List<string> hairOptions,
            List<string> beardOptions,
            out int hairOptionIndex,
            out int beardOptionIndex)
        {
            hairOptions.Clear();
            beardOptions.Clear();

            hairOptions.Add(string.Empty);
            beardOptions.Add(string.Empty);

            if (optionCatalog != null)
            {
                var hairs = optionCatalog.GetWardrobeOptions(currentProfile.raceName, "Hair");
                foreach (var hair in hairs) hairOptions.Add(hair.recipeName);

                var beards = optionCatalog.GetWardrobeOptions(currentProfile.raceName, "Beard");
                foreach (var beard in beards) beardOptions.Add(beard.recipeName);
            }

            var currentHair = currentProfile.wardrobeSelection.GetRecipe("Hair");
            if (!string.IsNullOrWhiteSpace(currentHair))
                if (!hairOptions.Contains(currentHair.Trim()))
                    hairOptions.Add(currentHair.Trim());

            var currentBeard = currentProfile.wardrobeSelection.GetRecipe("Beard");
            if (!string.IsNullOrWhiteSpace(currentBeard))
                if (!beardOptions.Contains(currentBeard.Trim()))
                    beardOptions.Add(currentBeard.Trim());

            DeduplicateAndSortOptions(hairOptions);
            DeduplicateAndSortOptions(beardOptions);

            hairOptionIndex = CharacterCreatorUtility.FindOptionIndex(hairOptions, currentHair);
            beardOptionIndex = CharacterCreatorUtility.FindOptionIndex(beardOptions, currentBeard);

            if (hairOptionIndex < 0)
            {
                hairOptionIndex = 0;
                currentProfile.wardrobeSelection.SetRecipe("Hair", string.Empty);
            }

            if (beardOptionIndex < 0)
            {
                beardOptionIndex = 0;
                currentProfile.wardrobeSelection.SetRecipe("Beard", string.Empty);
            }
        }

        public static void DeduplicateAndSortOptions(List<string> options)
        {
            if (options == null) return;

            var nonEmpty = options
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            options.Clear();
            options.Add(string.Empty);
            options.AddRange(nonEmpty);
        }

        public static void SyncUiFromCurrentProfile(
            CharacterAppearanceProfile currentProfile,
            bool enableRuntimeCustomizationUi,
            RectTransform customizationPanelRoot,
            TMP_Text hairValueLabel,
            List<string> hairOptions,
            int hairOptionIndex,
            TMP_Text beardValueLabel,
            List<string> beardOptions,
            int beardOptionIndex,
            Slider skinToneSlider,
            TMP_Text skinToneValueLabel,
            Slider hairToneSlider,
            TMP_Text hairToneValueLabel,
            Slider eyeToneSlider,
            TMP_Text eyeToneValueLabel,
            Action<bool> setSuppressCallbacks,
            Action updateGenderButtonState,
            Action updateRaceSummaryLabels,
            Action refreshAllBodyControlUi,
            Action<TMP_Text, float> setToneValueLabel)
        {
            if (!enableRuntimeCustomizationUi || customizationPanelRoot == null) return;

            setSuppressCallbacks?.Invoke(true);

            updateGenderButtonState?.Invoke();
            updateRaceSummaryLabels?.Invoke();

            if (hairValueLabel != null)
                hairValueLabel.text = CharacterCreatorUtility.GetOptionDisplayValue(CharacterCreatorUtility.GetOptionValue(hairOptions, hairOptionIndex));

            if (beardValueLabel != null)
                beardValueLabel.text = CharacterCreatorUtility.GetOptionDisplayValue(CharacterCreatorUtility.GetOptionValue(beardOptions, beardOptionIndex));

            refreshAllBodyControlUi?.Invoke();

            if (skinToneSlider != null)
            {
                var tone = CharacterCreatorUtility.EstimateTone(currentProfile.skinColor, CharacterCreatorStyle.SkinToneDark,
                    CharacterCreatorStyle.SkinToneLight);
                skinToneSlider.SetValueWithoutNotify(tone);
                setToneValueLabel?.Invoke(skinToneValueLabel, tone);
            }

            if (hairToneSlider != null)
            {
                var tone = CharacterCreatorUtility.EstimateTone(currentProfile.hairColor, CharacterCreatorStyle.HairToneDark,
                    CharacterCreatorStyle.HairToneLight);
                hairToneSlider.SetValueWithoutNotify(tone);
                setToneValueLabel?.Invoke(hairToneValueLabel, tone);
            }

            if (eyeToneSlider != null)
            {
                var tone = CharacterCreatorUtility.EstimateTone(currentProfile.eyeColor, CharacterCreatorStyle.EyeToneDark,
                    CharacterCreatorStyle.EyeToneLight);
                eyeToneSlider.SetValueWithoutNotify(tone);
                setToneValueLabel?.Invoke(eyeToneValueLabel, tone);
            }

            setSuppressCallbacks?.Invoke(false);
        }

        public static void RefreshAllBodyControlUi(
            CharacterAppearanceCatalog optionCatalog,
            Dictionary<string, Slider> bodyControlSliders,
            Func<string, float, float> getDnaValue,
            Func<string, float, float> convertBodyDnaToSliderValue,
            Action<string, float> setBodyControlValueLabel)
        {
            if (optionCatalog == null) return;

            foreach (var definition in optionCatalog.dnaControls)
            {
                var dnaValue = getDnaValue?.Invoke(definition.dnaName, definition.defaultValue) ?? definition.defaultValue;
                var sliderValue = convertBodyDnaToSliderValue?.Invoke(definition.dnaName, dnaValue) ?? 0.5f;

                if (bodyControlSliders.TryGetValue(definition.dnaName, out var slider) && slider != null)
                    slider.SetValueWithoutNotify(sliderValue);

                setBodyControlValueLabel?.Invoke(definition.dnaName, sliderValue);
                }
                }

                public static void SetStatus(TMP_Text statusLabel, string message)
                {
                if (statusLabel == null) return;

                statusLabel.text = string.IsNullOrWhiteSpace(message)
                ? string.Empty
                : message.Trim();
                }

                public static void SetToneValueLabel(TMP_Text label, float value)
                {
                if (label == null) return;

                label.text = value.ToString("0.00");
                }
                }
                }
