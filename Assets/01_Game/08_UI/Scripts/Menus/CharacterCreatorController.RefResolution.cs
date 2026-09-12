#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UMA.CharacterSystem;
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

        private void EnsurePreviewAvatarGeneratorBoundToCurrentScene()
        {
            if (!TryResolvePreviewDynamicAvatar(out var previewAvatar)) return;

            UmaGlobalLibraryService.TryBindAvatarLibrary(previewAvatar, allowGlobalFallback: true);
        }


        private void ClearPreviewAvatarGeneratorReferences()
        {
            if (!TryResolvePreviewDynamicAvatar(out var previewAvatar)) return;

            if (!Application.isPlaying)
            {
                UmaGlobalLibraryService.ClearCrossSceneReferences(previewAvatar);
                return;
            }

            var avatarScene = previewAvatar.gameObject.scene;
            UmaGlobalLibraryService.TryResolveLibraryForScene(
                avatarScene,
                out var sceneContext,
                out var sceneGenerator,
                allowGlobalFallback: false);

            var changed = false;

            // Keep runtime global fallback links when no same-scene replacement exists.
            if (sceneContext != null
                && previewAvatar.context != null
                && previewAvatar.context.gameObject.scene != avatarScene)
            {
                previewAvatar.context = null;
                changed = true;
            }

            if (sceneGenerator != null
                && previewAvatar.umaGenerator != null
                && previewAvatar.umaGenerator.gameObject.scene != avatarScene)
            {
                previewAvatar.umaGenerator = null;
                changed = true;
            }

            if (changed)
                UmaGlobalLibraryService.TryBindAvatarLibrary(previewAvatar, allowGlobalFallback: false);
        }


        private bool TryResolvePreviewDynamicAvatar(out DynamicCharacterAvatar previewAvatar)
        {
            previewAvatar = null;

            var previewRoot = ResolvePreviewAvatar(includeGlobalFallback: true);
            if (previewRoot == null) return false;

            previewAvatar = previewRoot.GetComponentInChildren<DynamicCharacterAvatar>(true);
            return previewAvatar != null;
        }


        private void InitializeCustomizationController()
        {
            if (creatorRefs.customizationController == null) return;

            var previewAvatar = ResolvePreviewAvatar(includeGlobalFallback: true);

            creatorRefs.customizationController.SetUiInteractionSfxCallback(PlayMenuClickSfx);
            creatorRefs.customizationController.SetSliderUiInteractionSfxCallback(PlaySliderInteractionSfx);
            creatorRefs.customizationController.SetPreviewAvatar(previewAvatar);
            creatorRefs.customizationController.Initialize(previewAvatar);
        }


        private void PlaySliderInteractionSfx()
        {
            PlayMenuClickSfx(sliderInteractionVolumeScale);
        }


        private void EnsureRefs()
        {
            if (creatorRefs != null) return;

            creatorRefs = GetComponent<CharacterCreatorRefs>();
            if (creatorRefs == null) creatorRefs = GetComponentInChildren<CharacterCreatorRefs>(true);

            if (creatorRefs == null) creatorRefs = gameObject.AddComponent<CharacterCreatorRefs>();
        }


        private void AutoResolveReferences()
        {
            EnsureRefs();

            ResolveSceneLocalPreviewAvatarReference();
            ResolveInputAndDropdownReferences();
            ResolvePortraitWidgetReferences();
            ResolveValidationMessageReference();
            ResolveActionButtonReferences();
            ResolveCustomizationControllerReference();
        }


        private void ResolveSceneLocalPreviewAvatarReference()
        {
            if (creatorRefs.previewAvatar == null || creatorRefs.previewAvatar.scene != gameObject.scene)
                creatorRefs.previewAvatar = FindPreviewAvatarInCurrentScene();

            _runtimeResolvedPreviewAvatar = creatorRefs.previewAvatar;
        }


        private void ResolveInputAndDropdownReferences()
        {
            if (creatorRefs.nameInput == null) creatorRefs.nameInput = GetComponentInChildren<TMP_InputField>(true);

            if (creatorRefs.presetDropdown == null)
                creatorRefs.presetDropdown = GetComponentInChildren<TMP_Dropdown>(true);
        }


        private void ResolvePortraitWidgetReferences()
        {
            if (creatorRefs.portraitPreview == null)
                creatorRefs.portraitPreview = FindPortraitPreviewImage();

            if (creatorRefs.previewDisplay == null)
                creatorRefs.previewDisplay = FindPreviewDisplayRawImage();
        }


        private Image FindPortraitPreviewImage()
        {
            var images = GetComponentsInChildren<Image>(true);

            for (var i = 0; i < images.Length; i++)
            {
                var image = images[i];
                if (image == null) continue;

                var lowerName = image.name.ToLowerInvariant();
                if (!lowerName.Contains("portrait") && !lowerName.Contains("preview")) continue;

                return image;
            }

            return null;
        }


        private RawImage FindPreviewDisplayRawImage()
        {
            var rawImages = GetComponentsInChildren<RawImage>(true);

            for (var i = 0; i < rawImages.Length; i++)
            {
                var raw = rawImages[i];
                if (raw == null) continue;

                var lowerName = raw.name.ToLowerInvariant();
                if (!lowerName.Contains("preview")
                    && !lowerName.Contains("character")
                    && !lowerName.Contains("display"))
                    continue;

                return raw;
            }

            return null;
        }


        private void ResolveValidationMessageReference()
        {
            if (creatorRefs.validationMessage != null) return;

            var textLabels = GetComponentsInChildren<TMP_Text>(true);

            for (var i = 0; i < textLabels.Length; i++)
            {
                var label = textLabels[i];
                if (label == null) continue;

                var lowerName = label.name.ToLowerInvariant();
                if (!lowerName.Contains("validation") && !lowerName.Contains("error")) continue;

                creatorRefs.validationMessage = label;
                return;
            }
        }


        private void ResolveActionButtonReferences()
        {
            if (HasAllActionButtons()) return;

            var buttons = GetComponentsInChildren<Button>(true);

            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null) continue;

                TryAssignActionButtonByName(button, button.name.ToLowerInvariant());
            }

            if (creatorRefs.confirmButton == null && buttons.Length > 0) creatorRefs.confirmButton = buttons[0];

            if (creatorRefs.backButton == null && buttons.Length > 1) creatorRefs.backButton = buttons[1];

            if (creatorRefs.randomNameButton == null && buttons.Length > 2)
                creatorRefs.randomNameButton = buttons[2];
        }


        private bool HasAllActionButtons()
        {
            return creatorRefs.confirmButton != null
                   && creatorRefs.backButton != null
                   && creatorRefs.randomNameButton != null
                   && creatorRefs.previousPortraitButton != null
                   && creatorRefs.nextPortraitButton != null
                   && creatorRefs.randomPortraitButton != null;
        }


        private void TryAssignActionButtonByName(Button button, string lowerName)
        {
            if (creatorRefs.previousPortraitButton == null
                && lowerName.Contains("portrait")
                && (lowerName.Contains("prev") || lowerName.Contains("previous") || lowerName.Contains("left")))
            {
                creatorRefs.previousPortraitButton = button;
                return;
            }

            if (creatorRefs.nextPortraitButton == null
                && lowerName.Contains("portrait")
                && (lowerName.Contains("next") || lowerName.Contains("right")))
            {
                creatorRefs.nextPortraitButton = button;
                return;
            }

            if (creatorRefs.randomPortraitButton == null
                && lowerName.Contains("portrait")
                && lowerName.Contains("random"))
            {
                creatorRefs.randomPortraitButton = button;
                return;
            }

            if (creatorRefs.confirmButton == null && lowerName.Contains("confirm"))
            {
                creatorRefs.confirmButton = button;
                return;
            }

            if (creatorRefs.backButton == null && (lowerName.Contains("close") || lowerName.Contains("back")))
            {
                creatorRefs.backButton = button;
                return;
            }

            if (creatorRefs.randomNameButton == null && lowerName.Contains("random") && !lowerName.Contains("portrait"))
                creatorRefs.randomNameButton = button;
        }


        private void ResolveCustomizationControllerReference()
        {
            if (creatorRefs.customizationController == null)
                creatorRefs.customizationController = GetComponent<CharacterCreatorCustomizationController>();

            if (creatorRefs.customizationController == null)
                creatorRefs.customizationController = gameObject.AddComponent<CharacterCreatorCustomizationController>();

            creatorRefs.customizationController.SetPreviewAvatar(ResolvePreviewAvatar(includeGlobalFallback: true));
        }


        private GameObject ResolvePreviewAvatar(bool includeGlobalFallback)
        {
            if (creatorRefs == null) return null;

            if (creatorRefs.previewAvatar != null)
            {
                if (creatorRefs.previewAvatar.scene == gameObject.scene)
                {
                    _runtimeResolvedPreviewAvatar = creatorRefs.previewAvatar;
                    return creatorRefs.previewAvatar;
                }

                // Never keep serialized cross-scene refs; Unity strips these and logs scene-mismatch warnings.
                creatorRefs.previewAvatar = null;
            }

            if (_runtimeResolvedPreviewAvatar != null)
            {
                if (_runtimeResolvedPreviewAvatar.scene == gameObject.scene)
                {
                    creatorRefs.previewAvatar = _runtimeResolvedPreviewAvatar;
                    return _runtimeResolvedPreviewAvatar;
                }

                if (includeGlobalFallback) return _runtimeResolvedPreviewAvatar;

                _runtimeResolvedPreviewAvatar = null;
            }

            var localPreviewAvatar = FindPreviewAvatarInCurrentScene();
            if (localPreviewAvatar != null)
            {
                creatorRefs.previewAvatar = localPreviewAvatar;
                _runtimeResolvedPreviewAvatar = localPreviewAvatar;
                return localPreviewAvatar;
            }

            if (!includeGlobalFallback) return null;

            _runtimeResolvedPreviewAvatar = FindPreviewAvatarInOtherScenes();
            return _runtimeResolvedPreviewAvatar;
        }


        private GameObject FindPreviewAvatarInOtherScenes()
        {
            var animators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject fallback = null;

            foreach (var animator in animators)
            {
                if (animator == null || animator.gameObject.scene == gameObject.scene) continue;

                var lowerName = animator.gameObject.name.ToLowerInvariant();
                if (!lowerName.Contains("preview")) continue;

                if (fallback == null) fallback = animator.gameObject;

                if (animator.gameObject.activeInHierarchy) return animator.gameObject;
            }

            return fallback;
        }


        private GameObject FindPreviewAvatarInCurrentScene()
        {
            var animators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject fallback = null;

            foreach (var animator in animators)
            {
                if (animator == null || animator.gameObject.scene != gameObject.scene) continue;

                if (fallback == null) fallback = animator.gameObject;

                var lowerName = animator.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("preview")) return animator.gameObject;
            }

            return fallback;
        }
    }
}
