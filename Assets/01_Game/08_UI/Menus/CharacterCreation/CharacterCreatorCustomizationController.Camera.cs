using UnityEngine;

namespace Zombera.UI.Menus.CharacterCreation
{
    public sealed partial class CharacterCreatorCustomizationController
    {
        private Camera TryResolvePreviewCamera()
        {
            return CharacterCreatorCameraHelper.TryResolvePreviewCamera(previewCamera, gameObject, out previewCamera);
        }

        private bool IsCameraInCurrentScene(Camera candidateCamera)
        {
            return CharacterCreatorCameraHelper.IsCameraInCurrentScene(candidateCamera, gameObject);
        }

        private bool TryAssignNamedPreviewCamera(Camera candidateCamera)
        {
            var success = CharacterCreatorCameraHelper.TryAssignNamedPreviewCamera(candidateCamera, out var assignedCamera);
            if (success) previewCamera = assignedCamera;
            return success;
        }

        private static void TrackPreviewCameraFallbacks(
            Camera candidateCamera,
            ref Camera fallbackRenderTextureCamera,
            ref Camera fallbackSceneCamera)
        {
            CharacterCreatorCameraHelper.TrackPreviewCameraFallbacks(candidateCamera, ref fallbackRenderTextureCamera, ref fallbackSceneCamera);
        }

        private void TryAutoFramePreviewCamera()
        {
            CharacterCreatorCameraHelper.TryAutoFramePreviewCamera(
                _previewAvatar,
                TryResolvePreviewCamera(),
                PreviewVerticalCenterBias,
                PreviewVerticalPadding,
                PreviewHorizontalPadding,
                MinPreviewDistance,
                MaxPreviewDistance,
                PreviewDistanceOffset,
                PreviewTopPadding,
                PreviewBottomPadding);
        }

        private static Bounds ExpandPreviewBounds(Bounds sourceBounds)
        {
            return CharacterCreatorCameraHelper.ExpandPreviewBounds(sourceBounds, PreviewTopPadding, PreviewBottomPadding);
        }

        private static float ResolveCameraAspect(Camera candidateCamera)
        {
            return CharacterCreatorCameraHelper.ResolveCameraAspect(candidateCamera);
        }

        private bool TryGetAvatarBounds(out Bounds bounds)
        {
            return CharacterCreatorCameraHelper.TryGetAvatarBounds(_previewAvatar, out bounds);
        }
    }
}
