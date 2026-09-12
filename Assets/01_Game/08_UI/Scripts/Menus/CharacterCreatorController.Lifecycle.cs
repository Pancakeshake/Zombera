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

        private void Awake()
        {
            EnsureRefs();
            // Deactivate the preview avatar before this frame's default-order Awake() calls run.
            // Legacy preview systems could bind scene-global references too early when multiple scenes
            // were loaded. Deactivating here ensures preview initialization runs later during Show(),
            // after scene-local binding is complete.
            EarlyDeactivatePreviewAvatar();
            ClearPreviewAvatarGeneratorReferences();
            Initialize();
        }


        private void OnEnable()
        {
            EnsureRefs();
            EnsureNameInputBackgroundImageVisibility();
            EnsureNameInputClickability();
            EnsureEditModeNameInputPresentation();
        }


        private void OnDisable()
        {
            // Clear generator links so preview avatars never retain references to generators from other scenes.
            ClearPreviewAvatarGeneratorReferences();
        }


        private void OnDestroy()
        {
            ClearPreviewAvatarGeneratorReferences();

            if (creatorRefs != null && creatorRefs.nameInput != null)
            {
                creatorRefs.nameInput.onValueChanged.RemoveListener(HandleCharacterNameChanged);
                creatorRefs.nameInput.onSelect.RemoveListener(HandleNameInputSelected);
                creatorRefs.nameInput.onEndEdit.RemoveListener(HandleNameInputSubmitted);
                creatorRefs.nameInput.onSubmit.RemoveListener(HandleNameInputSubmitted);
            }

            if (creatorRefs != null && creatorRefs.presetDropdown != null)
                creatorRefs.presetDropdown.onValueChanged.RemoveListener(HandleAppearancePresetChanged);

#if UNITY_EDITOR
            if (_editorFallbackPortraitCatalog != null)
            {
                if (Application.isPlaying)
                    Destroy(_editorFallbackPortraitCatalog);
                else
                    DestroyImmediate(_editorFallbackPortraitCatalog);

                _editorFallbackPortraitCatalog = null;
            }
#endif

            RestorePreviewTextureForSelector();
        }


        private void OnValidate()
        {
            EnsureRefs();
            EnsureNameInputBackgroundImageVisibility();

        #if UNITY_EDITOR
            if (menuClickSfx == null && !string.IsNullOrWhiteSpace(menuClickSfxAssetPath))
                menuClickSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(menuClickSfxAssetPath);

            if (portraitCatalog == null && !string.IsNullOrWhiteSpace(portraitCatalogAssetPath))
                portraitCatalog = AssetDatabase.LoadAssetAtPath<CharacterPortraitCatalog>(
                    portraitCatalogAssetPath);
        #endif

            if (creatorRefs == null || creatorRefs.previewAvatar == null) return;

            if (creatorRefs.previewAvatar.gameObject.scene != gameObject.scene)
            {
                creatorRefs.previewAvatar = null;
                _runtimeResolvedPreviewAvatar = null;
                return;
            }
        }


        private void EarlyDeactivatePreviewAvatar()
        {
            var previewAvatar = ResolvePreviewAvatar(includeGlobalFallback: true);
            if (previewAvatar == null) return;

            previewAvatar.SetActive(false);
        }
    }
}
