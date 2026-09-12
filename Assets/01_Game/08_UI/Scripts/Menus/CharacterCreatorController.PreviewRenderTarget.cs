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

        private void ApplyHighResolutionPreviewTextureForSelector()
        {
            if (!useHighResolutionPreviewTexture) return;

            if (!TryResolvePreviewRenderTarget(out var previewCamera, out var currentTexture)) return;

            if (previewCamera == null) return;

            if (_originalPreviewTexture == null)
                _originalPreviewTexture = currentTexture != null
                    ? currentTexture
                    : previewCamera.targetTexture;

            var targetWidth = Mathf.Max(256, highResolutionPreviewWidth);
            var targetHeight = Mathf.Max(256, highResolutionPreviewHeight);

            var needsRecreate = _highResolutionPreviewTexture == null ||
                                _highResolutionPreviewTexture.width != targetWidth ||
                                _highResolutionPreviewTexture.height != targetHeight;

            if (needsRecreate)
            {
                ReleaseHighResolutionPreviewTexture();
                _highResolutionPreviewTexture =
                    CreateHighResolutionPreviewTexture(targetWidth, targetHeight, _originalPreviewTexture);
            }

            if (_highResolutionPreviewTexture == null) return;

            _previewRenderCamera = previewCamera;
            _previewRenderCamera.targetTexture = _highResolutionPreviewTexture;

            if (creatorRefs.previewDisplay != null)
                creatorRefs.previewDisplay.texture = _highResolutionPreviewTexture;
        }


        private void RestorePreviewTextureForSelector()
        {
            if (_previewRenderCamera != null) _previewRenderCamera.targetTexture = _originalPreviewTexture;

            if (creatorRefs.previewDisplay != null && _originalPreviewTexture != null)
                creatorRefs.previewDisplay.texture = _originalPreviewTexture;

            ReleaseHighResolutionPreviewTexture();
            _previewRenderCamera = null;
            _originalPreviewTexture = null;
        }


        private void ReleaseHighResolutionPreviewTexture()
        {
            if (_highResolutionPreviewTexture == null) return;

            if (_highResolutionPreviewTexture.IsCreated()) _highResolutionPreviewTexture.Release();

            if (Application.isPlaying)
                Destroy(_highResolutionPreviewTexture);
            else
                DestroyImmediate(_highResolutionPreviewTexture);

            _highResolutionPreviewTexture = null;
        }


        private RenderTexture CreateHighResolutionPreviewTexture(int width, int height, RenderTexture sourceTexture)
        {
            var descriptor = sourceTexture != null
                ? sourceTexture.descriptor
                : new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24);

            descriptor.width = width;
            descriptor.height = height;
            descriptor.msaaSamples = Mathf.Clamp(highResolutionPreviewMsaa, 1, 8);
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;

            var texture = new RenderTexture(descriptor)
            {
                name = "RT_Preview_HighResRuntime",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };

            texture.Create();
            return texture;
        }


        private bool TryResolvePreviewRenderTarget(out Camera previewCamera, out RenderTexture texture)
        {
            previewCamera = null;
            texture = creatorRefs.previewDisplay != null
                ? creatorRefs.previewDisplay.texture as RenderTexture
                : null;

            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var localSceneHandle = gameObject.scene.handle;
            if (TryResolveLocalPreviewRenderTarget(cameras, localSceneHandle, texture, out previewCamera,
                    out var localTexture))
            {
                texture = localTexture;
                return true;
            }

            if (texture != null
                && TryFindTextureMatchInOtherScenes(cameras, localSceneHandle, texture, out previewCamera))
                return true;

            if (!TryResolveGlobalPreviewFallback(cameras, localSceneHandle, out previewCamera, out var globalTexture))
                return false;

            texture = globalTexture;
            return true;
        }


        private static bool TryResolveLocalPreviewRenderTarget(
            Camera[] cameras,
            int localSceneHandle,
            RenderTexture requestedTexture,
            out Camera previewCamera,
            out RenderTexture resolvedTexture)
        {
            previewCamera = null;
            resolvedTexture = requestedTexture;

            if (requestedTexture != null
                && TryFindTextureMatchInScene(cameras, localSceneHandle, requestedTexture, out previewCamera))
                return true;

            if (!TryFindNamedOrFallbackCameraInScene(cameras, localSceneHandle, out previewCamera))
                return false;

            resolvedTexture = previewCamera.targetTexture;
            return true;
        }


        private static bool TryFindTextureMatchInScene(
            Camera[] cameras,
            int sceneHandle,
            RenderTexture texture,
            out Camera matchingCamera)
        {
            matchingCamera = null;

            for (var i = 0; i < cameras.Length; i++)
            {
                var candidate = cameras[i];
                if (candidate == null || candidate.gameObject.scene.handle != sceneHandle) continue;

                if (candidate.targetTexture != texture) continue;

                matchingCamera = candidate;
                return true;
            }

            return false;
        }


        private static bool TryFindNamedOrFallbackCameraInScene(Camera[] cameras, int sceneHandle,
            out Camera resolvedCamera)
        {
            resolvedCamera = null;

            for (var i = 0; i < cameras.Length; i++)
            {
                var candidate = cameras[i];
                if (candidate == null || candidate.gameObject.scene.handle != sceneHandle) continue;

                if (candidate.targetTexture == null) continue;

                if (IsPreviewNamedCamera(candidate))
                {
                    resolvedCamera = candidate;
                    return true;
                }

                if (resolvedCamera == null) resolvedCamera = candidate;
            }

            return resolvedCamera != null;
        }


        private static bool TryFindTextureMatchInOtherScenes(
            Camera[] cameras,
            int localSceneHandle,
            RenderTexture texture,
            out Camera matchingCamera)
        {
            matchingCamera = null;

            for (var i = 0; i < cameras.Length; i++)
            {
                var candidate = cameras[i];
                if (candidate == null || candidate.gameObject.scene.handle == localSceneHandle) continue;

                if (candidate.targetTexture != texture) continue;

                matchingCamera = candidate;
                return true;
            }

            return false;
        }


        private static bool TryResolveGlobalPreviewFallback(Camera[] cameras, int localSceneHandle,
            out Camera previewCamera, out RenderTexture texture)
        {
            previewCamera = null;
            texture = null;

            Camera previewNamedFallback = null;
            Camera activeFallback = null;
            Camera anyFallback = null;

            for (var i = 0; i < cameras.Length; i++)
            {
                var candidate = cameras[i];
                if (candidate == null || candidate.gameObject.scene.handle == localSceneHandle) continue;

                if (candidate.targetTexture == null) continue;

                var isPreviewNamed = IsPreviewNamedCamera(candidate);
                if (isPreviewNamed && candidate.gameObject.activeInHierarchy)
                {
                    previewCamera = candidate;
                    texture = candidate.targetTexture;
                    return true;
                }

                if (isPreviewNamed && previewNamedFallback == null)
                    previewNamedFallback = candidate;

                if (candidate.gameObject.activeInHierarchy && activeFallback == null)
                    activeFallback = candidate;

                if (anyFallback == null)
                    anyFallback = candidate;
            }

            var resolvedFallback = previewNamedFallback ?? activeFallback ?? anyFallback;
            if (resolvedFallback == null) return false;

            previewCamera = resolvedFallback;
            texture = resolvedFallback.targetTexture;
            return true;
        }


        private static bool IsPreviewNamedCamera(Camera camera)
        {
            var lowerName = camera.name.ToLowerInvariant();
            return lowerName.Contains("preview") || lowerName.Contains("avatar");
        }
    }
}
