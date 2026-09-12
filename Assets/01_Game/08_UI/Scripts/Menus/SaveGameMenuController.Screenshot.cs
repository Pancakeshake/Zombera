using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.UI.Menus
{
    public partial class SaveGameMenuController
    {
        // ── Screenshot Caching & Decoding ─────────────────────────────

        private void ClearScreenshotCache()
        {
            foreach (var sprite in _screenshotCache.Values)
            {
                if (sprite != null && sprite.texture != null)
                {
                    if (Application.isPlaying) Destroy(sprite.texture);
                    else DestroyImmediate(sprite.texture);
                }
            }
            _screenshotCache.Clear();
        }

        private Sprite GetCachedScreenshot(string base64, string slotId)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;

            var cacheKey = BuildScreenshotCacheKey(slotId, base64);
            if (_screenshotCache.TryGetValue(cacheKey, out var cachedSprite) && cachedSprite != null)
                return cachedSprite;

            var sprite = DecodeBase64ToSprite(base64);
            if (sprite != null)
                _screenshotCache[cacheKey] = sprite;

            return sprite;
        }

        private static string BuildScreenshotCacheKey(string slotId, string base64)
        {
            var hash = base64.GetHashCode(StringComparison.Ordinal);
            return slotId + ":" + base64.Length + ":" + hash;
        }

        private void InvalidateScreenshotCacheForSlot(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId)) return;

            var prefix = slotId + ":";
            var staleKeys = new List<string>();
            foreach (var key in _screenshotCache.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    staleKeys.Add(key);
            }

            for (var i = 0; i < staleKeys.Count; i++)
            {
                if (_screenshotCache.TryGetValue(staleKeys[i], out var sprite) && sprite != null && sprite.texture != null)
                {
                    if (Application.isPlaying) Destroy(sprite.texture);
                    else DestroyImmediate(sprite.texture);
                }

                _screenshotCache.Remove(staleKeys[i]);
            }
        }

        private static Sprite DecodeBase64ToSprite(string base64)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(bytes))
                {
                    return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveGameMenu] Failed to decode screenshot: {e.Message}");
            }
            return null;
        }
    }
}
