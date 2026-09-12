#region

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Systems;
using Random = System.Random;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed partial class ZomberaSquadManagementUI
    {
        private TextMeshProUGUI CreateText(
            RectTransform parent,
            string text,
            float size,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            var rect = CreateRect("Text", parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = _defaultFont;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Image AddImage(RectTransform rect, Color color, Sprite sprite, bool raycastTarget = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static RawImage AddRawImage(RectTransform rect, Color color, Texture texture, bool raycastTarget = false)
        {
            var image = rect.gameObject.AddComponent<RawImage>();
            image.color = color;
            image.texture = texture;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private void EnsurePortraitStudio()
        {
            if (portraitStudio != null || !Application.isPlaying) return;

            // 1. Try to find existing singleton
            portraitStudio = PortraitStudioManager.Instance;
            if (portraitStudio != null) return;

            // 2. Try to find in scene
            portraitStudio = UnityEngine.Object.FindAnyObjectByType<PortraitStudioManager>();
            if (portraitStudio != null) return;

            // 3. Instantiate from prefab as fallback
            var prefab = portraitStudioPrefab;
            if (prefab == null)
            {
        #if UNITY_EDITOR
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/01_Game/08_UI/Prefabs/PortraitStudio.prefab");
        #endif
            }

            if (prefab != null)
            {
                var instance = Instantiate(prefab);
                portraitStudio = instance.GetComponent<PortraitStudioManager>();
                instance.name = "PortraitStudio_RuntimeInstance";
            }

            WirePortraitStudio();
        }

        private void WirePortraitStudio()
        {
            if (portraitStudio == null) return;

            portraitStudio.PortraitRendered -= HandlePortraitStudioRendered;
            portraitStudio.PortraitRendered += HandlePortraitStudioRendered;

            if (portraitRT == null)
                portraitRT = portraitStudio.PortraitTexture;
        }

        private void HandlePortraitStudioRendered(PortraitStudioRenderResult result)
        {
            if (result == null || !result.Success) return;
            if (_selectedSurvivorIndex < 0 || _selectedSurvivorIndex >= _liveSurvivorContexts.Count) return;

            var unit = _liveSurvivorContexts[_selectedSurvivorIndex].Unit;
            if (unit == null) return;
            if (!string.Equals(result.UnitKey, ResolvePortraitUnitKey(unit), StringComparison.Ordinal)) return;

            ApplySelectedPortraitDisplay(true);
        }

        private static string ResolvePortraitUnitKey(Unit unit)
        {
            if (unit == null) return string.Empty;
            return string.IsNullOrWhiteSpace(unit.UnitId) ? unit.GetInstanceID().ToString() : unit.UnitId;
        }

        private void ApplySelectedPortraitDisplay(bool portraitReady)
        {
            if (selectedPortraitImage == null) return;

            if (portraitReady && portraitRT != null)
            {
                selectedPortraitImage.texture = portraitRT;
                selectedPortraitImage.color = Color.white;
            }
            else
            {
                selectedPortraitImage.color = new Color(0.24f, 0.30f, 0.24f, 1f);
            }

            if (selectedPortraitInitialText != null)
                selectedPortraitInitialText.gameObject.SetActive(!portraitReady);
        }

        private void RequestPortraitForUnit(Unit unit)
        {
            EnsurePortraitStudio();
            if (portraitStudio == null || unit == null) return;

            ApplySelectedPortraitDisplay(false);
            portraitStudio.RefreshPortraitFromUnit(unit);
        }

        private static void StretchToParent(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void DestroySafely(GameObject target)
        {
            if (target == null) return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private static string GetInitial(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "?";

            return value[..1].ToUpperInvariant();
        }

        private static Sprite CreateDistressedSprite(
            int width,
            int height,
            Color baseColor,
            Color shadowColor,
            Color rustColor,
            int seed)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var random = new Random(seed);
            var pixels = new Color[width * height];

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var noise = (float)random.NextDouble();
                var rustNoise = (float)random.NextDouble();
                var color = Color.Lerp(baseColor, shadowColor, noise * 0.40f);
                color = Color.Lerp(color, rustColor, rustNoise * 0.12f);

                if (x < 2 || y < 2 || x > width - 3 || y > height - 3) color *= 0.74f;

                if ((x * 13 + y * 7 + seed) % 41 == 0)
                    color = Color.Lerp(color, new Color(0.73f, 0.70f, 0.62f, 1f), 0.16f);

                if ((x * 5 + y * 11 + seed) % 53 == 0)
                    color = Color.Lerp(color, new Color(0.09f, 0.05f, 0.03f, 1f), 0.26f);

                pixels[y * width + x] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateWarningStripeSprite(int size, Color a, Color b, int stripeWidth)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var stripe = (x + y) / Mathf.Max(2, stripeWidth) % 2;
                var color = stripe == 0 ? a : b;
                pixels[y * size + x] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private sealed class LiveSurvivorContext
        {
            public string Id;
            public SurvivorController SurvivorController;
            public Unit Unit;
        }
    }
}
