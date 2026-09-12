#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.UI.Menus.CharacterCreation;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Random = UnityEngine.Random;

#endregion

namespace Zombera.UI.Menus
{
    public sealed partial class CharacterCreatorController
    {

        private string CaptureAppearanceProfileJson()
        {
            var previewAvatar = ResolvePreviewAvatar(includeGlobalFallback: true);

            if (creatorRefs.customizationController != null)
            {
                creatorRefs.customizationController.SetPreviewAvatar(previewAvatar);
                var profileJson = creatorRefs.customizationController.CaptureProfileJson();

                if (!string.IsNullOrWhiteSpace(profileJson)) return profileJson;
            }

            if (previewAvatar == null) return CharacterSelectionState.SelectedAppearanceProfileJson;

            var report = AppearanceProfileService.TryCaptureProfile(previewAvatar, out var profile);
            if (!report.Success)
            {
                Debug.LogWarning(
                    "[CharacterCreatorController] Failed to capture appearance profile. " + report.ToMultilineString(),
                    this);
                return CharacterSelectionState.SelectedAppearanceProfileJson;
            }

            if (report.HasWarnings)
                Debug.Log("[CharacterCreatorController] Appearance capture warnings: " + report.ToMultilineString(),
                    this);

            return CharacterAppearanceProfile.Serialize(profile);
        }


        private void RestoreSavedAppearanceOnPreview()
        {
            var previewAvatar = ResolvePreviewAvatar(includeGlobalFallback: true);

            if (creatorRefs.customizationController != null)
            {
                creatorRefs.customizationController.SetPreviewAvatar(previewAvatar);
                creatorRefs.customizationController.ApplySavedProfile();
                return;
            }

            if (previewAvatar == null) return;

            var appearanceProfileJson = CharacterSelectionState.HasSelection
                ? CharacterSelectionState.SelectedAppearanceProfileJson
                : string.Empty;

            if (string.IsNullOrWhiteSpace(appearanceProfileJson))
                CharacterSelectionState.GetProfileDefaults(out _, out _, out appearanceProfileJson);

            if (string.IsNullOrWhiteSpace(appearanceProfileJson)) return;

            var profile = CharacterAppearanceProfile.Deserialize(appearanceProfileJson);
            var report = AppearanceProfileService.TryApplyProfile(previewAvatar, profile);

            if (!report.Success)
            {
                Debug.LogWarning(
                    "[CharacterCreatorController] Failed to restore appearance profile on preview avatar. " +
                    report.ToMultilineString(), this);
                return;
            }

            if (report.HasWarnings)
                Debug.Log("[CharacterCreatorController] Appearance restore warnings: " + report.ToMultilineString(),
                    this);
        }


        private void ApplyRandomName()
        {
            if (creatorRefs.nameInput == null) return;

            var pool = CharacterCreatorRandomNamePools.Male;
            if (creatorRefs.customizationController != null
                && creatorRefs.customizationController.TryGetPreviewProfilePrefersMaleGender(out var male))
                pool = male ? CharacterCreatorRandomNamePools.Male : CharacterCreatorRandomNamePools.Female;
            else if (Random.value < 0.5f) pool = CharacterCreatorRandomNamePools.Female;

            var randomIndex = Random.Range(0, pool.Length);
            var randomName = NormalizeName(pool[randomIndex]);
            creatorRefs.nameInput.SetTextWithoutNotify(randomName);
            RefreshPreview();
        }


        private void SetPanelVisible(bool isVisible)
        {
            if (isVisible)
            {
                EnsurePreviewAvatarGeneratorBoundToCurrentScene();
                ApplyHighResolutionPreviewTextureForSelector();
            }
            else
            {
                RestorePreviewTextureForSelector();
                ClearPreviewAvatarGeneratorReferences();
            }

            SetPreviewAvatarVisible(isVisible);

            var wasVisible = IsVisible;

            if (creatorRefs.panelRoot != null) creatorRefs.panelRoot.SetActive(isVisible);

            if (wasVisible != isVisible) VisibilityChanged?.Invoke(isVisible);
        }


        private void SetPreviewAvatarVisible(bool isVisible)
        {
            var previewAvatar = ResolvePreviewAvatar(includeGlobalFallback: true);
            if (previewAvatar == null) return;

            if (previewAvatar.activeSelf != isVisible)
                previewAvatar.SetActive(isVisible);
        }


        private void BindInputEvents()
        {
            if (creatorRefs.nameInput != null)
            {
                creatorRefs.nameInput.characterLimit = Mathf.Max(minimumNameLength, maximumNameLength);
                creatorRefs.nameInput.onValueChanged.RemoveListener(HandleCharacterNameChanged);
                creatorRefs.nameInput.onSelect.RemoveListener(HandleNameInputSelected);
                creatorRefs.nameInput.onEndEdit.RemoveListener(HandleNameInputSubmitted);
                creatorRefs.nameInput.onSubmit.RemoveListener(HandleNameInputSubmitted);

                creatorRefs.nameInput.onValueChanged.AddListener(HandleCharacterNameChanged);
                creatorRefs.nameInput.onSelect.AddListener(HandleNameInputSelected);
                creatorRefs.nameInput.onEndEdit.AddListener(HandleNameInputSubmitted);
                creatorRefs.nameInput.onSubmit.AddListener(HandleNameInputSubmitted);
            }

            if (creatorRefs.presetDropdown != null)
            {
                creatorRefs.presetDropdown.onValueChanged.RemoveListener(HandleAppearancePresetChanged);
                creatorRefs.presetDropdown.onValueChanged.AddListener(HandleAppearancePresetChanged);
            }
        }


        private void HandleCharacterNameChanged(string _)
        {
            PlayMenuClickSfx();
            RefreshPreview();
        }


        private void HandleNameInputSelected(string _)
        {
            PlayMenuClickSfx();
        }


        private void HandleNameInputSubmitted(string _)
        {
            PlayMenuClickSfx();
        }


        private void HandleAppearancePresetChanged(int _)
        {
            PlayMenuClickSfx();
            RefreshPreview();
        }


        private void ApplySavedSelectionToFields()
        {
            CharacterSelectionState.GetProfileDefaults(out _, out var savedPresetIndex);

            if (CharacterSelectionState.HasSelection)
                savedPresetIndex = CharacterSelectionState.SelectedAppearancePresetIndex;

            // Keep preset selection, but force the user to enter a fresh name each time.
            SelectedCharacterName = string.Empty;
            SelectedAppearancePresetIndex = ClampPresetIndex(savedPresetIndex);

            if (creatorRefs.nameInput != null) creatorRefs.nameInput.SetTextWithoutNotify(string.Empty);

            if (creatorRefs.presetDropdown != null && creatorRefs.presetDropdown.options.Count > 0)
                creatorRefs.presetDropdown.SetValueWithoutNotify(SelectedAppearancePresetIndex);

            ApplySavedPortraitSelection();
        }


        private void RefreshPreview()
        {
            var preset = GetCurrentPreset();
            var normalizedName =
                NormalizeName(creatorRefs.nameInput != null ? creatorRefs.nameInput.text : SelectedCharacterName);

            var isNameValid = TryValidateCharacterName(normalizedName, out var validationMessage);

            if (creatorRefs.confirmButton != null)
                creatorRefs.confirmButton.interactable = isNameValid && preset != null;

            SetValidationMessage(validationMessage);

            if (creatorRefs.portraitPreview == null) return;

            var portraitSprite = ResolveSelectedPortraitSprite(out _);
            creatorRefs.portraitPreview.sprite = portraitSprite;
            creatorRefs.portraitPreview.enabled = portraitSprite != null;
        }


        private void SetValidationMessage(string message)
        {
            if (creatorRefs.validationMessage == null) return;

            creatorRefs.validationMessage.text = string.IsNullOrWhiteSpace(message)
                ? string.Empty
                : message;
        }


        private bool TryValidateCharacterName(string candidateName, out string validationMessage)
        {
            var trimmed = string.IsNullOrWhiteSpace(candidateName)
                ? string.Empty
                : candidateName.Trim();

            if (trimmed.Length == 0)
            {
                validationMessage = "Name is required.";
                return false;
            }

            if (trimmed.Length < minimumNameLength)
            {
                validationMessage = $"Name must be at least {minimumNameLength} characters.";
                return false;
            }

            if (trimmed.Length > maximumNameLength)
            {
                validationMessage = $"Name must be {maximumNameLength} characters or fewer.";
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }


        private void InitializePresetDropdown()
        {
            if (creatorRefs.presetDropdown == null) return;

            creatorRefs.presetDropdown.ClearOptions();

            var options = appearancePresets
                .Select((preset, index) =>
                {
                    var displayName = preset != null && !string.IsNullOrWhiteSpace(preset.displayName)
                        ? preset.displayName.Trim()
                        : $"Preset {index + 1}";
                    return new TMP_Dropdown.OptionData(displayName);
                })
                .ToList();

            creatorRefs.presetDropdown.AddOptions(options);
            creatorRefs.presetDropdown.SetValueWithoutNotify(ClampPresetIndex(SelectedAppearancePresetIndex));
        }


        private CharacterAppearancePreset GetCurrentPreset()
        {
            if (appearancePresets == null || appearancePresets.Count == 0) return null;

            var selectedIndex = ResolveSelectedPresetIndex();

            if (selectedIndex < 0 || selectedIndex >= appearancePresets.Count) return null;

            return appearancePresets[selectedIndex];
        }


        private int ResolveSelectedPresetIndex()
        {
            if (appearancePresets == null || appearancePresets.Count == 0) return 0;

            var dropdownIndex = creatorRefs.presetDropdown != null
                ? creatorRefs.presetDropdown.value
                : SelectedAppearancePresetIndex;

            return ClampPresetIndex(dropdownIndex);
        }


        private int ClampPresetIndex(int index)
        {
            if (appearancePresets == null || appearancePresets.Count == 0) return 0;

            return Mathf.Clamp(index, 0, appearancePresets.Count - 1);
        }


        private void EnsureDefaults()
        {
            minimumNameLength = Mathf.Max(1, minimumNameLength);
            maximumNameLength = Mathf.Max(minimumNameLength, maximumNameLength);

            // Keep UI click sounds audible even if legacy prefab values were saved as zero.
            if (menuClickVolume <= 0f) menuClickVolume = 0.05f;

            if (string.IsNullOrWhiteSpace(defaultCharacterName)) defaultCharacterName = "Survivor";

            appearancePresets ??= new List<CharacterAppearancePreset>();

            if (appearancePresets.Count == 0)
            {
                appearancePresets.Add(CharacterAppearancePreset.Create(
                    "Drifter",
                    "A steady survivor who can adapt to most situations.",
                    100000f,
                    10f,
                    4f,
                    100f,
                    35f,
                    new CharacterLoadoutEntry("Pistol", 1),
                    new CharacterLoadoutEntry("9mm Ammo", 10),
                    new CharacterLoadoutEntry("Bandage", 1),
                    new CharacterLoadoutEntry("Food", 1)));

                appearancePresets.Add(CharacterAppearancePreset.Create(
                    "Runner",
                    "Moves quickly and keeps stamina high while carrying less.",
                    100000f,
                    9f,
                    4.8f,
                    120f,
                    28f,
                    new CharacterLoadoutEntry("Knife", 1),
                    new CharacterLoadoutEntry("Bandage", 2),
                    new CharacterLoadoutEntry("Food", 1)));

                appearancePresets.Add(CharacterAppearancePreset.Create(
                    "Bruiser",
                    "Hits harder and carries more, but moves slower.",
                    100000f,
                    13f,
                    3.5f,
                    90f,
                    42f,
                    new CharacterLoadoutEntry("Bat", 1),
                    new CharacterLoadoutEntry("Pistol", 1),
                    new CharacterLoadoutEntry("9mm Ammo", 8)));
            }

            var presetNumber = 1;
            foreach (var preset in appearancePresets)
            {
                if (preset == null)
                {
                    presetNumber++;
                    continue;
                }

                preset.Sanitize(presetNumber);
                preset.maxHealth = 100000f;
                presetNumber++;
            }
        }


        private string NormalizeName(string candidateName)
        {
            var trimmed = string.IsNullOrWhiteSpace(candidateName)
                ? string.Empty
                : candidateName.Trim();

            if (trimmed.Length > maximumNameLength) trimmed = trimmed[..maximumNameLength];

            return trimmed;
        }


        private void BindButton(Button button, UnityAction callback)
        {
            if (button == null) return;

            if (_boundClickHandlers.TryGetValue(button, out var existingHandler))
                button.onClick.RemoveListener(existingHandler);

            button.onClick.RemoveListener(callback);

            UnityAction wrappedHandler = () =>
            {
                PlayMenuClickSfx();
                callback?.Invoke();
            };

            _boundClickHandlers[button] = wrappedHandler;
            button.onClick.AddListener(wrappedHandler);
        }


        private void EnsureUiClickAudioSource()
        {
            if (_uiClickAudioSource != null) return;

            _uiClickAudioSource = GetComponent<AudioSource>();
            if (_uiClickAudioSource == null)
                _uiClickAudioSource = gameObject.AddComponent<AudioSource>();

            _uiClickAudioSource.playOnAwake = false;
            _uiClickAudioSource.loop = false;
            _uiClickAudioSource.spatialBlend = 0f;
            _uiClickAudioSource.volume = 1f;
        }
    }
}
