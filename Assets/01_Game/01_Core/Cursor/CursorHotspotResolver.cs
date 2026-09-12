#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Zombera.Systems
{
    public readonly struct CursorHotspotResolveRequest
    {
        public readonly Texture2D Texture;
        public readonly Vector2 ConfiguredHotspot;
        public readonly bool AutoCenterWhenUnset;
        public readonly bool AutoDetectWhenUnset;
        public readonly CursorHotspotAutoAnchor AutoAnchor;
        public readonly float AlphaThreshold;

        public CursorHotspotResolveRequest(
            Texture2D texture,
            Vector2 configuredHotspot,
            bool autoCenterWhenUnset,
            bool autoDetectWhenUnset,
            CursorHotspotAutoAnchor autoAnchor,
            float alphaThreshold)
        {
            Texture = texture;
            ConfiguredHotspot = configuredHotspot;
            AutoCenterWhenUnset = autoCenterWhenUnset;
            AutoDetectWhenUnset = autoDetectWhenUnset;
            AutoAnchor = autoAnchor;
            AlphaThreshold = alphaThreshold;
        }
    }

    /// <summary>
    ///     Resolves texture hotspot pixels without applying runtime calibration offsets.
    /// </summary>
    public sealed class CursorHotspotResolver
    {
        private readonly Dictionary<int, Vector2> _autoHotspotCache = new();

        public Vector2 Resolve(in CursorHotspotResolveRequest request)
        {
            var configuredHotspot = request.ConfiguredHotspot;

            if (request.Texture == null || configuredHotspot.sqrMagnitude > 0.0001f)
                return configuredHotspot;

            if (request.AutoDetectWhenUnset &&
                TryResolveAutoHotspot(request.Texture, request.AutoAnchor, request.AlphaThreshold, out var detected))
                return detected;

            return request.AutoCenterWhenUnset
                ? new Vector2(request.Texture.width * 0.5f, request.Texture.height * 0.5f)
                : configuredHotspot;
        }

        public static Vector2 ClampHotspot(Texture2D texture, Vector2 hotspot)
        {
            return texture == null
                ? hotspot
                : new Vector2(
                    Mathf.Clamp(hotspot.x, 0f, Mathf.Max(0f, texture.width - 1f)),
                    Mathf.Clamp(hotspot.y, 0f, Mathf.Max(0f, texture.height - 1f)));
        }

        private bool TryResolveAutoHotspot(
            Texture2D texture,
            CursorHotspotAutoAnchor anchor,
            float alphaThreshold,
            out Vector2 hotspot)
        {
            hotspot = Vector2.zero;
            if (texture == null) return false;

            var thresholdByte = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(alphaThreshold) * 255f), 1, 255);
            var cacheKey = BuildHotspotCacheKey(texture, anchor, thresholdByte);

            if (_autoHotspotCache.TryGetValue(cacheKey, out hotspot)) return true;

            if (!TryResolveOpaqueAnchorHotspot(texture, anchor, thresholdByte, out hotspot)) return false;

            _autoHotspotCache[cacheKey] = hotspot;
            return true;
        }

        private static int BuildHotspotCacheKey(Texture2D texture, CursorHotspotAutoAnchor anchor, int thresholdByte)
        {
            unchecked
            {
                var key = texture.GetInstanceID();
                key = (key * 397) ^ (int)anchor;
                key = (key * 397) ^ thresholdByte;
                return key;
            }
        }

        private static bool TryResolveOpaqueAnchorHotspot(
            Texture2D texture,
            CursorHotspotAutoAnchor anchor,
            int thresholdByte,
            out Vector2 hotspot)
        {
            hotspot = Vector2.zero;

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (UnityException)
            {
                return false;
            }

            if (pixels == null || pixels.Length == 0) return false;

            var width = texture.width;
            var height = texture.height;
            var maxX = Mathf.Max(0, width - 1);
            var maxY = Mathf.Max(0, height - 1);
            var centerX = maxX * 0.5f;
            var centerY = maxY * 0.5f;

            var bestIndex = -1;
            var bestScore = float.MaxValue;

            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < thresholdByte) continue;

                var x = i % width;
                var y = i / width;
                float score;

                switch (anchor)
                {
                    case CursorHotspotAutoAnchor.TopLeft:
                        score = x + y;
                        break;
                    case CursorHotspotAutoAnchor.TopRight:
                        score = maxX - x + y;
                        break;
                    case CursorHotspotAutoAnchor.BottomLeft:
                        score = x + (maxY - y);
                        break;
                    case CursorHotspotAutoAnchor.BottomRight:
                        score = maxX - x + (maxY - y);
                        break;
                    case CursorHotspotAutoAnchor.Center:
                        var dx = x - centerX;
                        var dy = y - centerY;
                        score = dx * dx + dy * dy;
                        break;
                    default:
                        score = x + y;
                        break;
                }

                if (score >= bestScore) continue;

                bestScore = score;
                bestIndex = i;
            }

            if (bestIndex < 0) return false;

            hotspot = new Vector2(bestIndex % width, bestIndex / width);
            return true;
        }
    }
}
