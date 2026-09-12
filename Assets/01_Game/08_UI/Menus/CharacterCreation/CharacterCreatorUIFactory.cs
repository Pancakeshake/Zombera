using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI.Menus.CharacterCreation
{
    /// <summary>
    ///     Factory for creating character creation UI primitives (rows, labels, sliders, buttons).
    /// </summary>
    public static class CharacterCreatorUIFactory
    {
        private static Sprite _solidSprite;

        public static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            texture.hideFlags = HideFlags.HideAndDontSave;

            _solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _solidSprite.hideFlags = HideFlags.HideAndDontSave;
            return _solidSprite;
        }

        public static RectTransform CreateRectTransform(string objectName, Transform parent)
        {
            GameObject gameObject = new(objectName, typeof(RectTransform));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            return rectTransform;
        }

        public static RectTransform EnsureDirectChildRectTransform(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(childName)) return null;

            var directChild = parent.Find(childName);
            if (directChild is RectTransform directRect) return directRect;

            return CreateRectTransform(childName, parent);
        }

        public static RectTransform FindChildRectTransform(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(childName)) return null;

            var direct = parent.Find(childName);
            if (direct != null) return direct as RectTransform;

            var descendants = parent.GetComponentsInChildren<Transform>(true);
            return descendants
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate != parent &&
                    string.Equals(candidate.name, childName, StringComparison.Ordinal)) as RectTransform;
        }

        public static T FindChildComponent<T>(Transform parent, string childName) where T : Component
        {
            var child = FindChildRectTransform(parent, childName);
            if (child == null) return null;

            var onSelf = child.GetComponent<T>();
            if (onSelf != null) return onSelf;

            return child.GetComponentInChildren<T>(true);
        }

        public static RectTransform EnsureTabRoot(RectTransform parent, string objectName)
        {
            var tabRoot = FindChildRectTransform(parent, objectName);
            if (tabRoot != null)
            {
                tabRoot.anchorMin = Vector2.zero;
                tabRoot.anchorMax = Vector2.one;
                tabRoot.offsetMin = Vector2.zero;
                tabRoot.offsetMax = Vector2.zero;
                StripTabRootLayoutElement(tabRoot);
                return tabRoot;
            }

            tabRoot = CreateRectTransform(objectName, parent);
            tabRoot.anchorMin = Vector2.zero;
            tabRoot.anchorMax = Vector2.one;
            tabRoot.offsetMin = Vector2.zero;
            tabRoot.offsetMax = Vector2.zero;

            var layout = tabRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 4, 0);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            return tabRoot;
        }

        public static void StripTabRootLayoutElement(RectTransform tabRoot)
        {
            if (tabRoot == null) return;

            var layoutElement = tabRoot.GetComponent<LayoutElement>();
            if (layoutElement == null) return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(layoutElement);
            else
                UnityEngine.Object.DestroyImmediate(layoutElement);
        }

        public static RectTransform CreateRow(Transform parent, string rowName, float preferredHeight, float spacing)
        {
            var row = CreateRectTransform(rowName, parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var layoutElement = row.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;
            return row;
        }

        public static TMP_Text CreateLabel(
            Transform parent,
            string objectName,
            string value,
            float fontSize,
            FontStyles fontStyle,
            float preferredWidth = -1f)
        {
            var labelRect = CreateRectTransform(objectName, parent);
            TMP_Text text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = new Color(0.95f, 0.90f, 0.78f, 1f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;

            var layout = labelRect.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth > 0f)
            {
                layout.preferredWidth = preferredWidth;
                layout.minWidth = Mathf.Min(preferredWidth, 56f);
            }
            else
            {
                layout.minWidth = 0f;
                layout.preferredWidth = 0f;
                layout.flexibleWidth = 1f;
            }

            return text;
        }

        public static Slider CreateSlider(Transform parent, string objectName, float hitAreaHeight, float trackHeight, float handleSize)
        {
            var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = objectName;
            sliderObject.transform.SetParent(parent, false);

            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            var layout = sliderObject.GetComponent<LayoutElement>() ?? sliderObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.minWidth = 0f;
            layout.preferredWidth = 0f;

            var sliderRect = sliderObject.transform as RectTransform;
            if (sliderRect != null)
            {
                sliderRect.anchorMin = new Vector2(0f, 0.5f);
                sliderRect.anchorMax = new Vector2(1f, 0.5f);
                sliderRect.pivot = new Vector2(0.5f, 0.5f);
                sliderRect.sizeDelta = new Vector2(0f, Mathf.Max(18f, hitAreaHeight));
            }

            var solidSprite = GetSolidSprite();

            var background = sliderObject.transform.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = solidSprite;
                background.type = Image.Type.Sliced;
                background.color = new Color(0.13f, 0.13f, 0.13f, 0.96f);
            }

            var fill = sliderObject.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
            if (fill != null)
            {
                fill.sprite = solidSprite;
                fill.type = Image.Type.Sliced;
                fill.color = new Color(0.83f, 0.36f, 0.19f, 0.95f);
            }

            var handle = sliderObject.transform.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
            if (handle != null)
            {
                handle.sprite = solidSprite;
                handle.type = Image.Type.Sliced;
                handle.color = new Color(0.96f, 0.82f, 0.67f, 1f);
            }

            ConfigureSliderInteractionPresentation(slider, hitAreaHeight, trackHeight, handleSize);

            return slider;
        }

        public static void ConfigureSliderInteractionPresentation(Slider slider, float hitAreaHeight, float trackHeight, float handleSize)
        {
            if (slider == null) return;

            var sliderRect = slider.transform as RectTransform;
            if (sliderRect == null) return;

            var hitHeight = Mathf.Max(18f, hitAreaHeight);
            var actualTrackHeight = Mathf.Clamp(trackHeight, 2f, hitHeight);
            var actualHandleSize = Mathf.Clamp(handleSize, 6f, hitHeight);
            var halfTrack = actualTrackHeight * 0.5f;
            const float horizontalInset = 10f;

            var hitAreaImage = FindChildComponent<Image>(slider.transform, "HitArea");
            if (hitAreaImage == null)
            {
                var hitAreaRect = CreateRectTransform("HitArea", slider.transform);
                hitAreaRect.SetAsFirstSibling();
                hitAreaImage = hitAreaRect.gameObject.AddComponent<Image>();
            }

            var hitRect = hitAreaImage.rectTransform;
            hitRect.anchorMin = Vector2.zero;
            hitRect.anchorMax = Vector2.one;
            hitRect.offsetMin = Vector2.zero;
            hitRect.offsetMax = Vector2.zero;

            hitAreaImage.sprite = GetSolidSprite();
            hitAreaImage.type = Image.Type.Sliced;
            hitAreaImage.color = new Color(1f, 1f, 1f, 0f);
            hitAreaImage.raycastTarget = true;

            var backgroundRect = slider.transform.Find("Background") as RectTransform;
            if (backgroundRect != null)
            {
                backgroundRect.anchorMin = new Vector2(0f, 0.5f);
                backgroundRect.anchorMax = new Vector2(1f, 0.5f);
                backgroundRect.offsetMin = new Vector2(horizontalInset, -halfTrack);
                backgroundRect.offsetMax = new Vector2(-horizontalInset, halfTrack);
            }

            var fillAreaRect = slider.transform.Find("Fill Area") as RectTransform;
            if (fillAreaRect != null)
            {
                fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
                fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
                fillAreaRect.offsetMin = new Vector2(horizontalInset, -halfTrack);
                fillAreaRect.offsetMax = new Vector2(-horizontalInset, halfTrack);
            }

            var handleSlideAreaRect = slider.transform.Find("Handle Slide Area") as RectTransform;
            if (handleSlideAreaRect != null)
            {
                handleSlideAreaRect.anchorMin = Vector2.zero;
                handleSlideAreaRect.anchorMax = Vector2.one;
                handleSlideAreaRect.offsetMin = new Vector2(horizontalInset, 0f);
                handleSlideAreaRect.offsetMax = new Vector2(-horizontalInset, 0f);
            }

            var handleRect = slider.handleRect;
            if (handleRect != null)
            {
                handleRect.anchorMin = new Vector2(0.5f, 0.5f);
                handleRect.anchorMax = new Vector2(0.5f, 0.5f);
                handleRect.sizeDelta = new Vector2(actualHandleSize, actualHandleSize);
            }

            slider.interactable = true;
            slider.enabled = true;
        }

        public static Button CreateActionButton(Transform parent, string objectName, string label, Action callback, Color normalTint, Action sfxCallback)
        {
            var buttonObject = DefaultControls.CreateButton(new DefaultControls.Resources());
            buttonObject.name = objectName;
            buttonObject.transform.SetParent(parent, false);

            var button = buttonObject.GetComponent<Button>();
            var image = buttonObject.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = GetSolidSprite();
                image.type = Image.Type.Sliced;
                image.color = normalTint;
            }

            var legacyText = buttonObject.GetComponentInChildren<Text>(true);
            if (legacyText != null)
            {
                legacyText.raycastTarget = false;
                legacyText.text = label;
                legacyText.fontStyle = FontStyle.Bold;
                legacyText.color = new Color(0.96f, 0.91f, 0.80f, 1f);
                legacyText.resizeTextForBestFit = false;
            }

            var tmpLabel = buttonObject.GetComponentInChildren<TMP_Text>(true);
            if (tmpLabel != null) tmpLabel.raycastTarget = false;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                sfxCallback?.Invoke();
                callback?.Invoke();
            });
            return button;
        }

        public static Button CreateTabButton(Transform parent, string objectName, string label, Action callback, Color normalTint, Action sfxCallback)
        {
            var button = CreateActionButton(parent, objectName, label, callback, normalTint, sfxCallback);
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.preferredHeight = 30f;
            return button;
        }

        public static Button CreateCycleButton(Transform parent, string objectName, string label, Action callback, Color normalTint, Action sfxCallback)
        {
            var button = CreateActionButton(parent, objectName, label, callback, normalTint, sfxCallback);
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 30f;
            layout.minWidth = 30f;
            layout.preferredHeight = 30f;
            return button;
        }
    }
}
