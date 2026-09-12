using UnityEngine;

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Helper for resolving and framing the character preview camera.
    /// </summary>
    public static class CharacterCreatorCameraHelper
    {
        public static Camera TryResolvePreviewCamera(
            Camera currentCamera, 
            GameObject owner,
            out Camera resolvedCamera)
        {
            if (currentCamera != null && IsCameraInCurrentScene(currentCamera, owner))
            {
                resolvedCamera = currentCamera;
                return resolvedCamera;
            }

            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Camera fallbackRenderTextureCamera = null;
            Camera fallbackSceneCamera = null;

            for (var index = 0; index < cameras.Length; index++)
            {
                var candidateCamera = cameras[index];
                if (!IsCameraInCurrentScene(candidateCamera, owner)) continue;

                if (TryAssignNamedPreviewCamera(candidateCamera, out resolvedCamera)) return resolvedCamera;

                TrackPreviewCameraFallbacks(candidateCamera, ref fallbackRenderTextureCamera, ref fallbackSceneCamera);
            }

            resolvedCamera = fallbackRenderTextureCamera ?? fallbackSceneCamera;
            return resolvedCamera;
        }

        public static bool IsCameraInCurrentScene(Camera candidateCamera, GameObject owner)
        {
            return candidateCamera != null && owner != null && candidateCamera.gameObject.scene == owner.scene;
        }

        public static bool TryAssignNamedPreviewCamera(Camera candidateCamera, out Camera assignedCamera)
        {
            assignedCamera = null;
            if (candidateCamera.targetTexture == null) return false;

            var lowerName = candidateCamera.name.ToLowerInvariant();
            var isPreviewNamed = lowerName.Contains("preview") || lowerName.Contains("avatar");
            if (!isPreviewNamed) return false;

            assignedCamera = candidateCamera;
            return true;
        }

        public static void TrackPreviewCameraFallbacks(
            Camera candidateCamera,
            ref Camera fallbackRenderTextureCamera,
            ref Camera fallbackSceneCamera)
        {
            if (candidateCamera.targetTexture != null)
            {
                if (fallbackRenderTextureCamera == null) fallbackRenderTextureCamera = candidateCamera;
                return;
            }

            if (fallbackSceneCamera == null) fallbackSceneCamera = candidateCamera;
        }

        public static void TryAutoFramePreviewCamera(
            GameObject previewAvatar,
            Camera framingCamera,
            float previewVerticalCenterBias,
            float previewVerticalPadding,
            float previewHorizontalPadding,
            float minPreviewDistance,
            float maxPreviewDistance,
            float previewDistanceOffset,
            float previewTopPadding,
            float previewBottomPadding)
        {
            if (previewAvatar == null || framingCamera == null) return;

            if (!TryGetAvatarBounds(previewAvatar, out var bounds)) return;

            var expandedBounds = ExpandPreviewBounds(bounds, previewTopPadding, previewBottomPadding);
            var target = expandedBounds.center + Vector3.up * (expandedBounds.extents.y * previewVerticalCenterBias);

            if (framingCamera.orthographic)
            {
                framingCamera.orthographicSize = expandedBounds.extents.y * (1f + previewVerticalPadding);
                var orthographicDistance = Mathf.Clamp(minPreviewDistance + previewDistanceOffset,
                    minPreviewDistance,
                    maxPreviewDistance);
                framingCamera.transform.position = target - framingCamera.transform.forward * orthographicDistance;
                return;
            }

            var verticalFovRadians = Mathf.Max(1f, framingCamera.fieldOfView) * Mathf.Deg2Rad;
            var aspect = ResolveCameraAspect(framingCamera);
            var horizontalFovRadians = 2f * Mathf.Atan(Mathf.Tan(verticalFovRadians * 0.5f) * Mathf.Max(0.1f, aspect));

            var halfHeight = expandedBounds.extents.y * (1f + previewVerticalPadding);
            var halfWidth = Mathf.Max(expandedBounds.extents.x, expandedBounds.extents.z) *
                            (1f + previewHorizontalPadding);

            var distanceForHeight = halfHeight / Mathf.Tan(verticalFovRadians * 0.5f);
            var distanceForWidth = halfWidth / Mathf.Tan(horizontalFovRadians * 0.5f);
            var distance = Mathf.Clamp(
                Mathf.Max(distanceForHeight, distanceForWidth) + previewDistanceOffset,
                minPreviewDistance,
                maxPreviewDistance);

            framingCamera.transform.position = target - framingCamera.transform.forward * distance;
        }

        public static Bounds ExpandPreviewBounds(Bounds sourceBounds, float previewTopPadding, float previewBottomPadding)
        {
            var yExtent = sourceBounds.extents.y;
            if (yExtent <= 0.0001f) return sourceBounds;

            var expanded = sourceBounds;
            Vector3 top = new(sourceBounds.center.x, sourceBounds.max.y + yExtent * previewTopPadding,
                sourceBounds.center.z);
            Vector3 bottom = new(sourceBounds.center.x, sourceBounds.min.y - yExtent * previewBottomPadding,
                sourceBounds.center.z);
            expanded.Encapsulate(top);
            expanded.Encapsulate(bottom);
            return expanded;
        }

        public static float ResolveCameraAspect(Camera candidateCamera)
        {
            if (candidateCamera == null) return 16f / 9f;

            if (candidateCamera.targetTexture != null && candidateCamera.targetTexture.height > 0)
                return (float)candidateCamera.targetTexture.width / candidateCamera.targetTexture.height;

            return candidateCamera.aspect > 0.01f
                ? candidateCamera.aspect
                : 16f / 9f;
        }

        public static bool TryGetAvatarBounds(GameObject previewAvatar, out Bounds bounds)
        {
            bounds = default;

            if (previewAvatar == null) return false;

            var renderers = previewAvatar.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;

            for (var index = 0; index < renderers.Length; index++)
            {
                var candidateRenderer = renderers[index];
                if (candidateRenderer == null || !candidateRenderer.enabled) continue;

                var rendererBounds = candidateRenderer.bounds;
                if (rendererBounds.size.sqrMagnitude <= 0.0001f) continue;

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasBounds;
        }
    }
}
