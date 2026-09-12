using System;
using System.Collections;
using System.Collections.Generic;
using UMA;
using UnityEngine;
using Zombera.Characters;
using Zombera.UI.Menus.CharacterCreation;

namespace Zombera.UI.SquadManagement
{
    public sealed partial class PortraitStudioManager
    {
        private IEnumerator SyncRoutine(GameObject sourceRoot, string unitKey)
        {
            _isGenerating = true;

            var report = AppearanceProfileService.TryCaptureProfile(sourceRoot, out var profile);
            if (!report.Success && logGenerationWarnings)
            {
                Debug.LogWarning(
                    "[PortraitStudioManager] Failed to capture source profile for '" + sourceRoot.name + "': " +
                    report.ToMultilineString(),
                    sourceRoot);
            }

            AppearanceProfileService.TryApplyProfile(studioAvatar.gameObject, profile, true, false);
            EnsureStudioAvatarLayers();

            var waitStartedAt = Time.unscaledTime;
            while (_isGenerating && Time.unscaledTime - waitStartedAt < generationTimeoutSeconds)
            {
                if (IsStudioAvatarReadyForCapture())
                {
                    _isGenerating = false;
                    break;
                }

                yield return null;
            }

            if (_isGenerating)
            {
                if (logGenerationWarnings)
                {
                    Debug.LogWarning(
                        "[PortraitStudioManager] Portrait generation timed out for '" + sourceRoot.name + "'. Rendering best-effort frame.",
                        sourceRoot);
                }

                _isGenerating = false;
            }

            yield return WaitForStudioAvatarVisualReady();

            if (!TryRenderPortraitFrame(out var cachedSprite))
            {
                PortraitRendered?.Invoke(new PortraitStudioRenderResult
                {
                    UnitKey = unitKey,
                    Success = false,
                    Texture = portraitRT,
                    CachedSprite = null
                });
                yield break;
            }

            cachedSprite = BakeAndCachePortrait(unitKey);
            PortraitRendered?.Invoke(new PortraitStudioRenderResult
            {
                UnitKey = unitKey,
                Success = cachedSprite != null,
                Texture = portraitRT,
                CachedSprite = cachedSprite
            });
        }

        private bool IsStudioAvatarReadyForCapture()
        {
            if (studioAvatar == null) return false;
            if (studioAvatar.umaData == null || studioAvatar.umaData.dirty) return false;

            return HasVisibleStudioRenderers();
        }

        private IEnumerator WaitForStudioAvatarVisualReady()
        {
            if (studioAvatar == null) yield break;
            if (IsStudioAvatarReadyForCapture()) yield break;

            var deadline = Time.unscaledTime + 1.5f;
            while (Time.unscaledTime < deadline)
            {
                if (IsStudioAvatarReadyForCapture())
                    break;

                yield return null;
            }
        }

        private void OnCharacterGenerated(UMAData umaData)
        {
            _ = umaData;
            _isGenerating = false;
            EnsureStudioAvatarLayers();
        }

        private bool TryRenderPortraitFrame(out Sprite cachedSprite)
        {
            cachedSprite = null;
            if (studioCamera == null || portraitRT == null || studioAvatar == null) return false;

            AlignCameraToHead();
            EnsureStudioAvatarLayers();

            var wasEnabled = studioCamera.enabled;
            studioCamera.enabled = true;
            studioCamera.Render();
            studioCamera.enabled = wasEnabled;

            return HasVisibleStudioRenderers();
        }

        public void RenderOnce()
        {
            TryRenderPortraitFrame(out _);
        }

        /// <summary>
        ///     Copies the live unit appearance into the studio and captures a fresh portrait (for save-slot previews).
        ///     Uses the per-unit cache when available; otherwise applies a best-effort single-frame render.
        /// </summary>
        public string CapturePortraitFromUnitRoot(GameObject unitRoot)
        {
            if (unitRoot == null || studioAvatar == null)
                return CapturePortraitToBase64();

            var unitKey = ResolveUnitKey(unitRoot);
            if (TryGetCachedPortrait(unitKey, out var cachedSprite) && cachedSprite != null && cachedSprite.texture != null)
                return EncodeTextureToBase64(cachedSprite.texture);

            var report = AppearanceProfileService.TryCaptureProfile(unitRoot, out var profile);
            if (!report.Success && logGenerationWarnings)
            {
                Debug.LogWarning(
                    "[PortraitStudioManager] Save capture could not read profile for '" + unitRoot.name + "': " +
                    report.ToMultilineString(),
                    unitRoot);
            }

            AppearanceProfileService.TryApplyProfile(studioAvatar.gameObject, profile, true, true);
            EnsureStudioAvatarLayers();
            TryRenderPortraitFrame(out _);
            return CapturePortraitToBase64();
        }

        private static string EncodeTextureToBase64(Texture texture)
        {
            if (texture == null) return null;

            var source = texture as Texture2D;
            if (source == null) return null;

            var bytes = source.EncodeToPNG();
            return bytes == null || bytes.Length == 0 ? null : Convert.ToBase64String(bytes);
        }

        public string CapturePortraitToBase64()
        {
            if (portraitRT == null) return null;

            TryRenderPortraitFrame(out _);

            RenderTexture.active = portraitRT;
            var texture = new Texture2D(portraitRT.width, portraitRT.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, portraitRT.width, portraitRT.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            var bytes = texture.EncodeToPNG();

            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);

            return bytes == null || bytes.Length == 0 ? null : Convert.ToBase64String(bytes);
        }

        private Sprite BakeAndCachePortrait(string unitKey)
        {
            if (portraitRT == null || string.IsNullOrWhiteSpace(unitKey)) return null;

            if (_textureCacheByUnitKey.TryGetValue(unitKey, out var existingTexture) && existingTexture != null)
            {
                if (Application.isPlaying) Destroy(existingTexture);
                else DestroyImmediate(existingTexture);
            }

            if (_spriteCacheByUnitKey.TryGetValue(unitKey, out var existingSprite) && existingSprite != null)
                _spriteCacheByUnitKey.Remove(unitKey);

            RenderTexture.active = portraitRT;
            var bakedTexture = new Texture2D(portraitRT.width, portraitRT.height, TextureFormat.RGBA32, false);
            bakedTexture.ReadPixels(new Rect(0, 0, portraitRT.width, portraitRT.height), 0, 0);
            bakedTexture.Apply();
            RenderTexture.active = null;

            var sprite = Sprite.Create(
                bakedTexture,
                new Rect(0, 0, bakedTexture.width, bakedTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);

            _textureCacheByUnitKey[unitKey] = bakedTexture;
            _spriteCacheByUnitKey[unitKey] = sprite;
            return sprite;
        }

        private static string ResolveUnitKey(GameObject unitRoot)
        {
            if (unitRoot == null) return string.Empty;

            var unit = unitRoot.GetComponent<Unit>() ?? unitRoot.GetComponentInChildren<Unit>();
            if (unit != null && !string.IsNullOrWhiteSpace(unit.UnitId))
                return unit.UnitId;

            return unitRoot.GetInstanceID().ToString();
        }

        private void ConfigureStudioCameraCullingMask()
        {
            if (studioCamera == null) return;

            var mask = 0;
            if (studioAvatar != null)
                mask |= 1 << studioAvatar.gameObject.layer;

            mask |= 1 << LayerMask.NameToLayer("Default");
            if (mask == 0)
                mask = ~0;

            studioCamera.cullingMask = mask;
        }

        private void EnsureStudioAvatarLayers()
        {
            if (studioAvatar == null) return;

            var targetLayer = studioAvatar.gameObject.layer;
            var transforms = studioAvatar.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var child = transforms[i];
                if (child != null)
                    child.gameObject.layer = targetLayer;
            }

            ConfigureStudioCameraCullingMask();
        }

        private bool HasVisibleStudioRenderers()
        {
            if (studioAvatar == null) return false;

            var renderers = studioAvatar.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;
                if (renderer.bounds.size.sqrMagnitude > 0.0001f)
                    return true;
            }

            return false;
        }

        private void AlignCameraToHead()
        {
            if (studioCamera == null || studioAvatar == null) return;

            var avatarRoot = studioAvatar.transform;
            var head = PortraitStudioFramingUtility.FindHeadTransform(avatarRoot);
            var headPosition = head != null
                ? head.position
                : portraitAnchor != null
                    ? portraitAnchor.position
                    : avatarRoot.position + Vector3.up * 1.6f;

            var forward = ResolvePortraitForward(head, avatarRoot);

            var lookPosition = headPosition + Vector3.up * headshotLookOffset;
            var cameraPosition = headPosition + Vector3.up * headshotVerticalOffset + forward * headshotDistance;

            var camTransform = studioCamera.transform;
            camTransform.position = cameraPosition;
            camTransform.rotation = Quaternion.LookRotation(lookPosition - cameraPosition, Vector3.up);
            studioCamera.fieldOfView = headshotFieldOfView;
        }

        private static Vector3 ResolvePortraitForward(Transform head, Transform unitRoot)
        {
            if (unitRoot != null)
            {
                var rootForward = Vector3.ProjectOnPlane(unitRoot.forward, Vector3.up);
                if (rootForward.sqrMagnitude > 0.0001f)
                    return rootForward.normalized;
            }

            if (head != null)
            {
                var headForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (headForward.sqrMagnitude > 0.0001f)
                {
                    // Humanoid head bones often point out the back of the skull; negate when root is unavailable.
                    return -headForward.normalized;
                }
            }

            return Vector3.forward;
        }
    }

    internal static class PortraitStudioFramingUtility
    {
        public static Transform FindHeadTransform(Transform root)
        {
            if (root == null) return null;

            var animator = root.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null) return headBone;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var child = all[i];
                var childName = child.name;
                if (string.IsNullOrEmpty(childName)) continue;

                var lowerName = childName.ToLowerInvariant();
                if (lowerName.Contains("head") || lowerName.Contains("face"))
                    return child;
            }

            return portraitAnchorFallback(root);
        }

        private static Transform portraitAnchorFallback(Transform root)
        {
            return root;
        }
    }
}
