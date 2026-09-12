#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;

#endregion

namespace Zombera.UI
{
    public sealed partial class SquadPortraitStrip
    {

        private static Vector3 ResolvePortraitForward(Transform head, Transform unitRoot)
        {
            return SquadPortraitStripCaptureHelpers.ResolvePortraitForward(head, unitRoot);
        }


        private Sprite CreateFaceCropSprite(Sprite sourceSprite)
        {
            if (sourceSprite == null || sourceSprite.texture == null)
                return null;

            var textureRect = sourceSprite.textureRect;
            var sourceRect = new RectInt(
                Mathf.RoundToInt(textureRect.x),
                Mathf.RoundToInt(textureRect.y),
                Mathf.RoundToInt(textureRect.width),
                Mathf.RoundToInt(textureRect.height));

            if (sourceRect.width <= 0 || sourceRect.height <= 0)
                return null;

            var subjectRect = sourceRect;
            if (useAlphaBoundsForFaceCrop)
                subjectRect = TryFindOpaqueBounds(sourceSprite.texture, sourceRect, alphaDetectionThreshold,
                    out var detected)
                    ? detected
                    : sourceRect;

            var cropRect = ComputeFaceCropRect(sourceRect, subjectRect);

            return Sprite.Create(
                sourceSprite.texture,
                new Rect(cropRect.x, cropRect.y, cropRect.width, cropRect.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }


        private Sprite CreateFaceCropSprite(Texture texture)
        {
            if (!TryGetTexture2D(
                    texture,
                    allowRuntimeTextureReadbackFallback,
                    out var texture2D,
                    out var createdRuntimeTexture))
                return null;

            var sourceRect = new RectInt(0, 0, texture2D.width, texture2D.height);
            var subjectRect = sourceRect;
            if (useAlphaBoundsForFaceCrop)
                subjectRect = TryFindOpaqueBounds(texture2D, sourceRect, alphaDetectionThreshold, out var detected)
                    ? detected
                    : sourceRect;

            var cropRect = ComputeFaceCropRect(sourceRect, subjectRect);

            var sprite = Sprite.Create(
                texture2D,
                new Rect(cropRect.x, cropRect.y, cropRect.width, cropRect.height),
                new Vector2(0.5f, 0.5f),
                100f);

            if (sprite != null && createdRuntimeTexture) _capturedPortraitTextures.Add(texture2D);

            return sprite;
        }


        private static bool TryResolvePortraitTexture(Transform unitRoot, Transform head, out Texture texture)
        {
            return SquadPortraitStripCaptureHelpers.TryResolvePortraitTexture(unitRoot, head, out texture);
        }


        private static bool TryGetTextureFromRenderer(Renderer renderer, out Texture texture)
        {
            return SquadPortraitStripCaptureHelpers.TryGetTextureFromRenderer(renderer, out texture);
        }


        private static bool TryGetTextureFromMaterial(Material mat, out Texture texture)
        {
            return SquadPortraitStripCaptureHelpers.TryGetTextureFromMaterial(mat, out texture);
        }


        private bool TryCreateSpriteFromTexture(Texture texture, out Sprite sprite)
        {
            sprite = null;
            if (!TryGetTexture2D(
                    texture,
                    allowRuntimeTextureReadbackFallback,
                    out var texture2D,
                    out var createdRuntimeTexture)) return false;

            sprite = Sprite.Create(
                texture2D,
                new Rect(0f, 0f, texture2D.width, texture2D.height),
                new Vector2(0.5f, 0.5f),
                100f);

            if (sprite != null && createdRuntimeTexture) _capturedPortraitTextures.Add(texture2D);

            return sprite != null;
        }


        private static bool TryGetTexture2D(
            Texture sourceTexture,
            bool allowReadbackConversions,
            out Texture2D texture2D,
            out bool createdRuntimeTexture)
        {
            texture2D = null;
            createdRuntimeTexture = false;

            if (sourceTexture == null || sourceTexture.width <= 0 || sourceTexture.height <= 0) return false;

            if (sourceTexture is Texture2D directTexture)
            {
                texture2D = directTexture;
                return true;
            }

            if (!allowReadbackConversions) return false;

            RenderTexture readbackSource;
            var temporarySource = false;

            if (sourceTexture is RenderTexture renderTexture)
            {
                readbackSource = renderTexture;
            }
            else
            {
                readbackSource = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                temporarySource = true;
                Graphics.Blit(sourceTexture, readbackSource);
            }

            var copied = TryCopyRenderTextureToTexture2D(readbackSource, out texture2D);

            if (temporarySource) RenderTexture.ReleaseTemporary(readbackSource);

            if (!copied || texture2D == null) return false;

            createdRuntimeTexture = true;
            return true;
        }


        private static bool TryCopyRenderTextureToTexture2D(RenderTexture source, out Texture2D texture2D)
        {
            texture2D = null;
            if (source == null || source.width <= 0 || source.height <= 0) return false;

            var previousActive = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                texture2D = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
                texture2D.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                texture2D.Apply(false, false);
                return true;
            }
            catch (Exception)
            {
                if (texture2D == null) return false;

                Destroy(texture2D);
                texture2D = null;

                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }


        private RectInt ComputeFaceCropRect(RectInt sourceRect, RectInt subjectRect)
        {
            var baseSize = Mathf.Min(subjectRect.width, subjectRect.height);
            var cropSize = Mathf.Clamp(Mathf.RoundToInt(baseSize * Mathf.Clamp(faceCropScale, 0.10f, 1f)), 8, baseSize);

            var centerX = subjectRect.xMin + Mathf.RoundToInt(subjectRect.width * Mathf.Clamp01(faceCropCenterX));
            var centerY = subjectRect.yMin + Mathf.RoundToInt(subjectRect.height * Mathf.Clamp01(faceCropCenterY));

            var minX = sourceRect.xMin;
            var maxX = sourceRect.xMax - cropSize;
            var minY = sourceRect.yMin;
            var maxY = sourceRect.yMax - cropSize;

            var cropX = Mathf.Clamp(centerX - cropSize / 2, minX, maxX);
            var cropY = Mathf.Clamp(centerY - cropSize / 2, minY, maxY);

            return new RectInt(cropX, cropY, cropSize, cropSize);
        }


        private bool TryFindOpaqueBounds(Texture2D texture, RectInt sourceRect, float alphaThreshold,
            out RectInt bounds)
        {
            bounds = sourceRect;
            if (texture == null || sourceRect.width <= 0 || sourceRect.height <= 0)
                return false;

            if (!TryGetReadablePixels(texture, out var pixels)) return false;

            var width = texture.width;
            var alphaCutoff = (byte)Mathf.Clamp(Mathf.RoundToInt(alphaThreshold * 255f), 0, 255);

            return TryFindOpaquePixelBounds(pixels, width, sourceRect, alphaCutoff, out bounds);
        }


        private bool TryGetReadablePixels(Texture2D texture, out Color32[] pixels)
        {
            pixels = null;
            if (texture == null) return false;

            var cacheKey = texture.GetInstanceID();
            if (_readablePixelCacheByTextureId.TryGetValue(cacheKey, out pixels) && pixels is { Length: > 0 })
                return true;

            try
            {
                pixels = texture.GetPixels32();
                if (pixels is { Length: > 0 }) _readablePixelCacheByTextureId[cacheKey] = pixels;
                return true;
            }
            catch (UnityException)
            {
                // Non-readable textures cannot be scanned for alpha; fallback to source rect.
                return false;
            }
        }


        private static bool TryFindOpaquePixelBounds(
            Color32[] pixels,
            int textureWidth,
            RectInt sourceRect,
            byte alphaCutoff,
            out RectInt bounds)
        {
            return SquadPortraitStripCaptureHelpers.TryFindOpaquePixelBounds(
                pixels,
                textureWidth,
                sourceRect,
                alphaCutoff,
                out bounds);
        }


        private static Transform FindHeadTransform(Transform root)
        {
            return SquadPortraitStripCaptureHelpers.FindHeadTransform(root);
        }
    }
}
