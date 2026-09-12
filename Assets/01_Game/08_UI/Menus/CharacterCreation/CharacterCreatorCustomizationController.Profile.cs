using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Core;

namespace Zombera.UI.Menus.CharacterCreation
{
    public sealed partial class CharacterCreatorCustomizationController
    {
        private void EnsureCurrentProfileFromState()
        {
            _currentProfile = CharacterCreatorProfileSyncHelper.EnsureCurrentProfileFromState(_previewAvatar);
        }

        private void CaptureCurrentProfileFromPreview()
        {
            _currentProfile = CharacterCreatorProfileSyncHelper.CaptureCurrentProfileFromPreview(_previewAvatar);
        }

        private void RefreshRaceOptions()
        {
            TryResolveCatalog();
            CharacterCreatorProfileSyncHelper.RefreshRaceOptions(optionCatalog, _currentProfile, _raceOptions, out _raceOptionIndex);
        }

        private void RefreshWardrobeOptions()
        {
            TryResolveCatalog();
            CharacterCreatorProfileSyncHelper.RefreshWardrobeOptions(
                optionCatalog,
                _currentProfile,
                _hairOptions,
                _beardOptions,
                out _hairOptionIndex,
                out _beardOptionIndex);
        }

        private void SyncUiFromCurrentProfile()
        {
            CharacterCreatorProfileSyncHelper.SyncUiFromCurrentProfile(
                _currentProfile,
                EnableRuntimeCustomizationUi,
                customizationPanelRoot,
                _hairValueLabel,
                _hairOptions,
                _hairOptionIndex,
                _beardValueLabel,
                _beardOptions,
                _beardOptionIndex,
                _skinToneSlider,
                _skinToneValueLabel,
                _hairToneSlider,
                _hairToneValueLabel,
                _eyeToneSlider,
                _eyeToneValueLabel,
                suppress => _suppressCallbacks = suppress,
                UpdateGenderButtonState,
                UpdateRaceSummaryLabels,
                RefreshAllBodyControlUi,
                SetToneValueLabel);
        }

        private void RefreshAllBodyControlUi()
        {
            CharacterCreatorProfileSyncHelper.RefreshAllBodyControlUi(
                optionCatalog,
                _bodyControlSliders,
                GetDnaValue,
                ConvertBodyDnaToSliderValue,
                SetBodyControlValueLabel);
        }

        private void HandleBodyControlSliderChanged(string dnaName, float value)
        {
            if (_suppressCallbacks || string.IsNullOrWhiteSpace(dnaName)) return;

            var sliderValue = Mathf.Clamp01(value);
            var dnaValue = ConvertBodySliderToDnaValue(dnaName, sliderValue);

            SetDnaValue(dnaName, dnaValue);
            SetBodyControlValueLabel(dnaName, sliderValue);
            ApplyCurrentProfileToPreview(true, false, false);
        }

        private float ConvertBodySliderToDnaValue(string dnaName, float sliderValue)
        {
            var definition = FindDnaControlDefinition(dnaName);
            return CharacterCreatorDnaHelper.ConvertBodySliderToDnaValue(
                dnaName,
                sliderValue,
                definition,
                bodyDnaOutputRange,
                heightDnaOutputRangeNormalized,
                BodySliderOutputMax);
        }

        private float ConvertBodyDnaToSliderValue(string dnaName, float dnaValue)
        {
            var v = Mathf.Clamp01(dnaValue);
            var definition = FindDnaControlDefinition(dnaName);

            if (!string.Equals(dnaName, HeightDnaName, StringComparison.OrdinalIgnoreCase))
            {
                var range = definition?.outputRange ?? bodyDnaOutputRange;
                if (definition is { isLocked: true }) range = definition.lockedOutputRange;

                var denom = Mathf.Max(0.0001f, range.y - range.x);
                return Mathf.Clamp01((v - range.x) / denom);
            }

            var maxOutput = Mathf.Clamp(BodySliderOutputMax, 0.1f, 1f);
            var heightN = CharacterCreatorUtility.NormalizeRange(heightDnaOutputRangeNormalized, 0.46f, 0.54f);
            var min = Mathf.Clamp01(heightN.x) * maxOutput;
            var max = Mathf.Clamp01(heightN.y) * maxOutput;
            var heightDenom = Mathf.Max(0.0001f, max - min);
            return Mathf.Clamp01((v - min) / heightDenom);
        }

        private CharacterDnaControlDefinition FindDnaControlDefinition(string dnaName)
        {
            TryResolveCatalog();
            return optionCatalog?.dnaControls.Find(d =>
                string.Equals(d.dnaName, dnaName, StringComparison.OrdinalIgnoreCase));
        }

        private void SetBodyControlValueLabel(string dnaName, float value)
        {
            if (string.IsNullOrWhiteSpace(dnaName)) return;

            if (!_bodyControlValueLabels.TryGetValue(dnaName, out var label) || label == null) return;

            label.text = value.ToString("0.00");
        }

        private void UpdateGenderButtonState()
        {
            if (_raceOptions.Count == 0)
            {
                SetGenderButtonState(_racePrevButton, false, true);
                SetGenderButtonState(_raceNextButton, false, false);
                return;
            }

            var selectedRace = CharacterCreatorUtility.GetOptionValue(_raceOptions, _raceOptionIndex);
            var isMale = CharacterCreatorUtility.IsMaleRaceName(selectedRace);
            SetGenderButtonState(_racePrevButton, isMale, true);
            SetGenderButtonState(_raceNextButton, !isMale, false);
        }

        private void SetGenderButtonState(Button button, bool isActive, bool isMaleButton)
        {
            if (button == null) return;

            var image = button.targetGraphic as Image;
            if (image != null)
            {
                var activeTint = isMaleButton
                    ? new Color(0.20f, 0.41f, 0.66f, 0.98f)
                    : new Color(0.74f, 0.32f, 0.54f, 0.98f);

                image.color = isActive
                    ? activeTint
                    : tabNormalTint;
            }

            var glyph = FindChildComponent<TMP_Text>(button.transform, "GenderGlyph");
            if (glyph != null)
                glyph.color = isActive
                    ? new Color(0.98f, 0.95f, 0.90f, 1f)
                    : new Color(0.90f, 0.84f, 0.74f, 0.94f);
        }

        private void HideRaceSummaryChrome(Transform raceRow)
        {
            if (raceRow == null) return;

            var header = FindChildComponent<TMP_Text>(raceRow, "RaceLabel");
            header?.gameObject.SetActive(false);

            var value = FindChildComponent<TMP_Text>(raceRow, "RaceValue");
            value?.gameObject.SetActive(false);

            _raceHeaderLabel = null;
            _raceValueLabel = null;
        }

        private void UpdateRaceSummaryLabels()
        {
            if (_raceHeaderLabel == null && _raceValueLabel == null) return;

            if (_raceHeaderLabel != null) _raceHeaderLabel.text = "RACE";

            if (_raceValueLabel == null) return;

            if (_raceOptions.Count == 0)
            {
                _raceValueLabel.text = string.Empty;
                return;
            }

            var selectedRace = CharacterCreatorUtility.GetOptionValue(_raceOptions, _raceOptionIndex);
            _raceValueLabel.text = CharacterCreatorUtility.GetOptionDisplayValue(selectedRace);
        }

        private void SetRaceByGender(bool preferMale)
        {
            if (_suppressCallbacks || _raceOptions.Count == 0) return;

            var targetIndex = CharacterCreatorUtility.FindRaceIndexByGender(_raceOptions, preferMale);
            if (targetIndex < 0) return;

            if (targetIndex == _raceOptionIndex)
            {
                UpdateGenderButtonState();
                return;
            }

            _raceOptionIndex = targetIndex;
            _currentProfile.raceName = _raceOptions[_raceOptionIndex];
            _currentProfile.wardrobeSelection.SetRecipe("Hair", string.Empty);
            _currentProfile.wardrobeSelection.SetRecipe("Beard", string.Empty);

            ApplyCurrentProfileToPreview(true, true);
            RefreshWardrobeOptions();
            SyncUiFromCurrentProfile();
        }

        private void ApplyCurrentProfileToPreview(bool rebuildCharacter, bool recaptureAfterApply,
            bool refitCamera = true, bool forceRebuildWhenUnchanged = false)
        {
            if (_previewAvatar == null) return;

            _currentProfile.Sanitize();

            var report = AppearanceProfileService.TryApplyProfile(
                _previewAvatar,
                _currentProfile,
                rebuildCharacter,
                forceRebuildWhenUnchanged);
            if (!report.Success)
            {
                Debug.LogWarning(
                    "[CharacterCreatorCustomizationController] Appearance apply failed. " + report.ToMultilineString(),
                    this);
                return;
            }

            if (recaptureAfterApply)
            {
                var captureReport = AppearanceProfileService.TryCaptureProfile(_previewAvatar, out var profile);
                if (captureReport.Success)
                {
                    _currentProfile = profile;
                    _currentProfile.Sanitize();
                }
            }

            if (refitCamera && autoFramePreviewCamera)
            {
                TryAutoFramePreviewCamera();
                RequestAutoFramePasses();
            }
        }

        private void RequestAutoFramePasses()
        {
            if (!autoFramePreviewCamera)
            {
                _remainingAutoFramePasses = 0;
                return;
            }

            _remainingAutoFramePasses = Mathf.Max(_remainingAutoFramePasses, Mathf.Max(1, PostApplyRefitFrames));
        }

        private void HandleSkinToneChanged(float value)
        {
            if (_suppressCallbacks) return;

            _currentProfile.skinColor = CharacterCreatorUtility.EvaluateTone(value, CharacterCreatorStyle.SkinToneDark,
                CharacterCreatorStyle.SkinToneLight);
            SetToneValueLabel(_skinToneValueLabel, value);
            ApplyCurrentProfileToPreview(true, false);
        }

        private void HandleHairToneChanged(float value)
        {
            if (_suppressCallbacks) return;

            _currentProfile.hairColor = CharacterCreatorUtility.EvaluateTone(value, CharacterCreatorStyle.HairToneDark,
                CharacterCreatorStyle.HairToneLight);
            SetToneValueLabel(_hairToneValueLabel, value);
            ApplyCurrentProfileToPreview(true, false);
        }

        private void HandleEyeToneChanged(float value)
        {
            if (_suppressCallbacks) return;

            _currentProfile.eyeColor =
                CharacterCreatorUtility.EvaluateTone(value, CharacterCreatorStyle.EyeToneDark, CharacterCreatorStyle.EyeToneLight);
            SetToneValueLabel(_eyeToneValueLabel, value);
            ApplyCurrentProfileToPreview(true, false);
        }

        private void CycleHair(int direction)
        {
            if (_suppressCallbacks || _hairOptions.Count == 0) return;

            _hairOptionIndex = CharacterCreatorUtility.WrapIndex(_hairOptionIndex + direction, _hairOptions.Count);
            _currentProfile.wardrobeSelection.SetRecipe("Hair", CharacterCreatorUtility.GetOptionValue(_hairOptions, _hairOptionIndex));
            ApplyCurrentProfileToPreview(true, false);
            SyncUiFromCurrentProfile();
        }

        private void CycleBeard(int direction)
        {
            if (_suppressCallbacks || _beardOptions.Count == 0) return;

            _beardOptionIndex = CharacterCreatorUtility.WrapIndex(_beardOptionIndex + direction, _beardOptions.Count);
            _currentProfile.wardrobeSelection.SetRecipe("Beard", CharacterCreatorUtility.GetOptionValue(_beardOptions, _beardOptionIndex));
            ApplyCurrentProfileToPreview(true, false);
            SyncUiFromCurrentProfile();
        }

        private void RandomizeAppearance()
        {
            TryResolveCatalog();
            CharacterCreatorRandomizationHelper.RandomizeAppearance(
                _currentProfile,
                optionCatalog,
                _raceOptions,
                ref _raceOptionIndex,
                randomBodySliderRange,
                randomHeightSliderRange,
                randomLockedProportionSliderRange,
                randomSkinToneRange,
                randomHairToneRange,
                randomEyeToneRange,
                ConvertBodySliderToDnaValue,
                SetDnaValue);

            ApplyCurrentProfileToPreview(true, true);
            RefreshWardrobeOptions();

            if (_hairOptions.Count > 1)
            {
                _hairOptionIndex = UnityEngine.Random.Range(1, _hairOptions.Count);
                _currentProfile.wardrobeSelection.SetRecipe("Hair", _hairOptions[_hairOptionIndex]);
            }

            if (_beardOptions.Count > 1)
            {
                _beardOptionIndex = UnityEngine.Random.Range(1, _beardOptions.Count);
                _currentProfile.wardrobeSelection.SetRecipe("Beard", _beardOptions[_beardOptionIndex]);
            }

            ApplyCurrentProfileToPreview(true, false);
            SyncUiFromCurrentProfile();
            SetStatus("Randomized appearance profile (normal variation).");
        }

        private void ResetAppearance()
        {
            TryResolveCatalog();
            _currentProfile = CharacterAppearanceProfile.CreateDefault();

            if (optionCatalog != null)
                foreach (var definition in optionCatalog.dnaControls)
                    SetDnaValue(definition.dnaName, definition.defaultValue);

            _raceOptionIndex = CharacterCreatorUtility.FindOptionIndex(_raceOptions, _currentProfile.raceName);
            if (_raceOptionIndex < 0)
            {
                _raceOptionIndex = 0;
                if (_raceOptions.Count > 0) _currentProfile.raceName = _raceOptions[0];
            }

            ApplyCurrentProfileToPreview(true, true);
            RefreshWardrobeOptions();
            SyncUiFromCurrentProfile();
            SetStatus("Reset to default appearance.");
        }

        private void SetActiveTab(string tabName)
        {
            _bodyTabRoot?.gameObject.SetActive(string.Equals(tabName, "Body", StringComparison.Ordinal));

            _hairTabRoot?.gameObject.SetActive(string.Equals(tabName, "Hair", StringComparison.Ordinal));

            _skinTabRoot?.gameObject.SetActive(string.Equals(tabName, "Skin", StringComparison.Ordinal));

            _presetsTabRoot?.gameObject.SetActive(string.Equals(tabName, "Presets", StringComparison.Ordinal));

            SetTabButtonState(_bodyTabButton, string.Equals(tabName, "Body", StringComparison.Ordinal));
            SetTabButtonState(_hairTabButton, string.Equals(tabName, "Hair", StringComparison.Ordinal));
            SetTabButtonState(_skinTabButton, string.Equals(tabName, "Skin", StringComparison.Ordinal));
            SetTabButtonState(_presetsTabButton, string.Equals(tabName, "Presets", StringComparison.Ordinal));
        }

        private void SetTabButtonState(Button button, bool isActive)
        {
            if (button == null) return;

            var image = button.targetGraphic as Image;
            if (image != null) image.color = isActive ? tabActiveTint : tabNormalTint;
        }

        private float GetDnaValue(string dnaName, float fallback)
        {
            var entry = FindDnaEntry(dnaName);
            if (entry == null) return fallback;

            return Mathf.Clamp01(entry.dnaValue);
        }

        private void SetDnaValue(string dnaName, float value)
        {
            var entry = FindDnaEntry(dnaName);
            if (entry == null)
            {
                entry = new CharacterDnaEntry(dnaName, value);
                _currentProfile.bodyValues.Add(entry);
                return;
            }

            entry.dnaValue = Mathf.Clamp01(value);
        }

        private CharacterDnaEntry FindDnaEntry(string dnaName)
        {
            _currentProfile.bodyValues ??= new List<CharacterDnaEntry>();

            return _currentProfile.bodyValues.FirstOrDefault(entry =>
                entry != null && string.Equals(entry.dnaName, dnaName, StringComparison.OrdinalIgnoreCase));
        }

        private static void SetToneValueLabel(TMP_Text label, float value)
        {
            CharacterCreatorProfileSyncHelper.SetToneValueLabel(label, value);
        }

        private void SetStatus(string message)
        {
            CharacterCreatorProfileSyncHelper.SetStatus(_statusLabel, message);
        }
    }
}
