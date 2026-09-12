using System;
using UnityEngine;
using UMA;
using UMA.CharacterSystem;

namespace Zombera.UI.Menus.CharacterCreation
{
    public sealed partial class CharacterCreatorCustomizationController
    {
        private void LateUpdate()
        {
            if (!Application.isPlaying || !autoFramePreviewCamera || _remainingAutoFramePasses <= 0) return;

            _remainingAutoFramePasses--;
            TryAutoFramePreviewCamera();
        }

        public void Initialize(GameObject avatar)
        {
            SetPreviewAvatar(avatar);

            if (EnableRuntimeCustomizationUi)
            {
                BuildOrResolveRuntimeUi();
                BindUiEvents();
            }

            if (!_isInitialized) _isInitialized = true;

            ApplySavedProfile();
        }

        public void SetPreviewAvatar(GameObject avatar)
        {
            if (_previewAvatar != null)
            {
                var oldDca = _previewAvatar.GetComponentInChildren<DynamicCharacterAvatar>();
                if (oldDca != null) oldDca.CharacterUpdated.RemoveListener(HandleUmaCharacterUpdated);
            }

            _previewAvatar = avatar;

            if (_previewAvatar != null)
            {
                var dca = _previewAvatar.GetComponentInChildren<DynamicCharacterAvatar>();
                if (dca != null) dca.CharacterUpdated.AddListener(HandleUmaCharacterUpdated);
            }

            TryResolvePreviewCamera();
            RequestAutoFramePasses();
        }

        private void HandleUmaCharacterUpdated(UMAData umaData)
        {
            if (!Application.isPlaying) return;

            if (autoFramePreviewCamera)
            {
                RequestAutoFramePasses();
            }
        }

        public void SetUiInteractionSfxCallback(Action callback)
        {
            _uiInteractionSfxCallback = callback;
            if (_sliderUiInteractionSfxCallback == null) _sliderUiInteractionSfxCallback = callback;
        }

        public void SetSliderUiInteractionSfxCallback(Action callback)
        {
            _sliderUiInteractionSfxCallback = callback ?? _uiInteractionSfxCallback;
        }
    }
}
