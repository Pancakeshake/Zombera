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

        private void ApplyGameLikePresentation()
        {
            if (!applyGameLikePresentation || creatorRefs.panelRoot == null) return;

            var panelRect = creatorRefs.panelRoot.transform as RectTransform;

            if (panelRect == null) return;

            RemoveDecorativeLayers(panelRect);

            var headerRect = FindOrCreateRectChild(panelRect, "CreatorHeaderContainer");
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(0f, 24f);
            headerRect.anchoredPosition = new Vector2(0f, -2f);

            EnsureHeaderText(headerRect);
            LayoutActionButtons();
            StyleNameInput(true);
            StyleButton(creatorRefs.confirmButton, confirmButtonLabel, CharacterCreatorStyle.ConfirmTint, true);
            StyleButton(creatorRefs.backButton, closeButtonLabel, CharacterCreatorStyle.CloseTint, false);

            if (creatorRefs.randomNameButton != null)
                StyleButton(creatorRefs.randomNameButton, "RANDOM", CharacterCreatorStyle.RandomButtonTint, false);

            if (creatorRefs.validationMessage != null)
                creatorRefs.validationMessage.color = CharacterCreatorStyle.ValidationErrorTint;
        }


        private static void RemoveDecorativeLayers(RectTransform panelRect)
        {
            RemoveDecorativeLayer(panelRect, "CreatorOverlay");
            RemoveDecorativeLayer(panelRect, "CreatorTopStrip");
            RemoveDecorativeLayer(panelRect, "CreatorBottomStrip");
            RemoveDecorativeLayer(panelRect, "CreatorFrameTop");
            RemoveDecorativeLayer(panelRect, "CreatorFrameBottom");
            RemoveDecorativeLayer(panelRect, "CreatorFrameLeft");
            RemoveDecorativeLayer(panelRect, "CreatorFrameRight");
            RemoveDecorativeLayer(panelRect, "CreatorFrameTopAccent");
            RemoveDecorativeLayer(panelRect, "CreatorFrameBottomAccent");
            RemoveDecorativeLayer(panelRect, "CreatorVignetteTop");
            RemoveDecorativeLayer(panelRect, "CreatorVignetteBottom");
            RemoveDecorativeLayer(panelRect, "CreatorVignetteLeft");
            RemoveDecorativeLayer(panelRect, "CreatorVignetteRight");
            RemoveDecorativeLayer(panelRect, "CreatorSubtitlePlate");
        }


        private static void RemoveDecorativeLayer(RectTransform panelRect, string objectName)
        {
            var layer = panelRect.Find(objectName);
            if (layer == null) return;

            layer.gameObject.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(layer.gameObject);
                return;
            }

            DestroyImmediate(layer.gameObject);
        }


        private void LayoutActionButtons()
        {
            PlaceActionButton(creatorRefs.confirmButton, true);
            PlaceActionButton(creatorRefs.backButton, false);

            var randomRect = creatorRefs.randomNameButton != null
                ? creatorRefs.randomNameButton.transform as RectTransform
                : null;
            if (ParentUsesLayout(randomRect) || randomRect == null) return;

            randomRect.anchorMin = new Vector2(0.79f, 0.72f);
            randomRect.anchorMax = new Vector2(0.94f, 0.80f);
            randomRect.offsetMin = Vector2.zero;
            randomRect.offsetMax = Vector2.zero;
        }


        private static void PlaceActionButton(Button button, bool placeLeft)
        {
            var rect = button != null ? button.transform as RectTransform : null;

            if (ParentUsesLayout(rect) || rect == null) return;

            rect.anchorMin = placeLeft ? new Vector2(0.16f, 0.04f) : new Vector2(0.52f, 0.04f);
            rect.anchorMax = placeLeft ? new Vector2(0.46f, 0.12f) : new Vector2(0.82f, 0.12f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }


        private static bool ParentUsesLayout(RectTransform rect)
        {
            if (rect == null || rect.parent == null) return false;

            return rect.parent.GetComponent<LayoutGroup>() != null;
        }


        private void EnsureHeaderText(RectTransform headerRect)
        {
            var title = FindOrCreateText(headerRect, "CreatorHeaderTitle", 70f, FontStyles.Bold);
            var resolvedTitle = string.IsNullOrWhiteSpace(creatorHeaderTitle)
                ? "CREATE SURVIVOR"
                : creatorHeaderTitle.Trim();
            title.text = resolvedTitle.ToUpperInvariant();
            title.color = CharacterCreatorStyle.TextTint;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.gameObject.SetActive(true);

            var subtitle = FindOrCreateText(headerRect, "CreatorHeaderSubtitle", 23f, FontStyles.Bold);
            if (string.IsNullOrWhiteSpace(creatorHeaderSubtitle))
            {
                subtitle.gameObject.SetActive(false);
                return;
            }

            subtitle.text = creatorHeaderSubtitle.Trim();
            subtitle.color = Color.Lerp(CharacterCreatorStyle.TextTint, CharacterCreatorStyle.AccentTint, 0.35f);
            subtitle.fontStyle = FontStyles.Italic;
            subtitle.alignment = TextAlignmentOptions.Center;
            subtitle.gameObject.SetActive(true);
        }


        private Image ResolveNameInputImage()
        {
            EnsureRefs();
            if (creatorRefs == null) return null;

            if (creatorRefs.nameInput == null) creatorRefs.nameInput = GetComponentInChildren<TMP_InputField>(true);

            if (creatorRefs.nameInput == null) return null;

            var inputImage = creatorRefs.nameInput.targetGraphic as Image;
            if (inputImage == null) inputImage = creatorRefs.nameInput.GetComponent<Image>();

            return inputImage;
        }


        private void EnsureNameInputBackgroundImageVisibility()
        {
            var inputImage = ResolveNameInputImage();
            if (inputImage == null) return;

            var hasCustomBackgroundSprite = inputImage.sprite != null && inputImage.sprite != _runtimeSolidSprite;
            if (!hasCustomBackgroundSprite)
            {
                if (!applyGameLikePresentation) return;

                inputImage.sprite = GetSolidSprite();
                inputImage.type = Image.Type.Sliced;
                inputImage.color = CharacterCreatorStyle.InputTint;
                return;
            }

            inputImage.type = Image.Type.Simple;
            inputImage.material = null;

            if (inputImage.color.maxColorComponent < 0.25f) inputImage.color = Color.white;
        }


        private void StyleNameInput(bool applyLayout)
        {
            if (creatorRefs.nameInput == null) return;

            EnsureNameInputClickability();

            var inputImage = ResolveNameInputImage();

            if (inputImage != null)
            {
                EnsureNameInputBackgroundImageVisibility();

                var outline = GetOrAddComponent<Outline>(inputImage.gameObject);
                outline.effectColor = new Color(CharacterCreatorStyle.ButtonBorderTint.r,
                    CharacterCreatorStyle.ButtonBorderTint.g, CharacterCreatorStyle.ButtonBorderTint.b, 0.60f);
                outline.effectDistance = new Vector2(2f, -2f);
                outline.useGraphicAlpha = true;

                var shadow = GetOrAddComponent<Shadow>(inputImage.gameObject);
                shadow.effectColor = new Color(0f, 0f, 0f, 0.52f);
                shadow.effectDistance = new Vector2(0f, -4f);
                shadow.useGraphicAlpha = true;
            }

            if (creatorRefs.nameInput.textComponent != null)
            {
                creatorRefs.nameInput.textComponent.color = CharacterCreatorStyle.TextTint;
                creatorRefs.nameInput.textComponent.fontStyle = FontStyles.Bold;
                creatorRefs.nameInput.textComponent.characterSpacing = 1.6f;
                creatorRefs.nameInput.textComponent.fontSize =
                    Mathf.Max(28f, creatorRefs.nameInput.textComponent.fontSize);
            }

            if (creatorRefs.nameInput.placeholder is TMP_Text placeholder)
            {
                placeholder.color = new Color(CharacterCreatorStyle.TextTint.r, CharacterCreatorStyle.TextTint.g,
                    CharacterCreatorStyle.TextTint.b, 0.45f);
                placeholder.fontStyle = FontStyles.Italic;
            }

            var inputRect = creatorRefs.nameInput.transform as RectTransform;
            if (!applyLayout || inputRect == null) return;

            if (ParentUsesLayout(inputRect))
            {
                inputRect.sizeDelta = new Vector2(Mathf.Max(460f, inputRect.sizeDelta.x),
                    Mathf.Max(56f, inputRect.sizeDelta.y));
                return;
            }

            inputRect.anchorMin = new Vector2(0.23f, 0.75f);
            inputRect.anchorMax = new Vector2(0.77f, 0.75f);
            inputRect.sizeDelta = new Vector2(0f, 56f);
            inputRect.anchoredPosition = Vector2.zero;
        }


        private void EnsureNameInputClickability()
        {
            EnsureRefs();
            if (creatorRefs == null) return;

            var inputImage = ResolveNameInputImage();
            if (inputImage != null)
            {
                inputImage.raycastTarget = true;
                inputImage.raycastPadding = new Vector4(
                    Mathf.Max(0f, nameInputRaycastPadding.x),
                    Mathf.Max(0f, nameInputRaycastPadding.y),
                    Mathf.Max(0f, nameInputRaycastPadding.z),
                    Mathf.Max(0f, nameInputRaycastPadding.w));
            }

            if (creatorRefs.previewDisplay != null) creatorRefs.previewDisplay.raycastTarget = false;
        }


        private void EnsureEditModeNameInputPresentation()
        {
            if (Application.isPlaying) return;

            if (!applyGameLikePresentation) return;

            EnsureRefs();
            if (creatorRefs == null) return;

            if (creatorRefs.nameInput == null) creatorRefs.nameInput = GetComponentInChildren<TMP_InputField>(true);

            if (creatorRefs.nameInput == null) return;

            StyleNameInput(false);
        }


        private static void StyleButton(Button button, string labelText, Color baseColor, bool emphasizePrimary)
        {
            if (button == null) return;

            var buttonImage = button.targetGraphic as Image;
            if (buttonImage == null) buttonImage = button.GetComponent<Image>();

            if (buttonImage != null) ApplyButtonImageChrome(buttonImage, baseColor, emphasizePrimary);

            ApplySelectableColors(button, baseColor);
            ApplyButtonLayoutSizeHint(button.transform as RectTransform, emphasizePrimary);

            var tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpLabel != null)
            {
                ApplyTmpLabelStyling(tmpLabel, labelText, emphasizePrimary);
                return;
            }

            var legacyLabel = button.GetComponentInChildren<Text>(true);
            if (legacyLabel == null) return;

            ApplyLegacyTextFallbackStyling(legacyLabel, labelText, emphasizePrimary);
        }


        private static void ApplyButtonImageChrome(Image buttonImage, Color baseColor, bool emphasizePrimary)
        {
            buttonImage.sprite = GetSolidSprite();
            buttonImage.type = Image.Type.Sliced;
            buttonImage.color = baseColor;

            var border = GetOrAddComponent<Outline>(buttonImage.gameObject);
            border.effectColor = new Color(
                CharacterCreatorStyle.ButtonBorderTint.r,
                CharacterCreatorStyle.ButtonBorderTint.g,
                CharacterCreatorStyle.ButtonBorderTint.b,
                emphasizePrimary ? 0.76f : 0.58f);
            border.effectDistance = emphasizePrimary ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
            border.useGraphicAlpha = true;

            var shadow = GetOrAddComponent<Shadow>(buttonImage.gameObject);
            shadow.effectColor = new Color(0f, 0f, 0f, emphasizePrimary ? 0.68f : 0.54f);
            shadow.effectDistance = emphasizePrimary ? new Vector2(0f, -6f) : new Vector2(0f, -4f);
            shadow.useGraphicAlpha = true;
        }


        private static void ApplySelectableColors(Button button, Color baseColor)
        {
            var colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, CharacterCreatorStyle.AccentTint, 0.22f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.28f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(baseColor.r * 0.5f, baseColor.g * 0.5f, baseColor.b * 0.5f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
        }


        private static void ApplyButtonLayoutSizeHint(RectTransform buttonRect, bool emphasizePrimary)
        {
            if (buttonRect == null || !ParentUsesLayout(buttonRect)) return;

            var minWidth = emphasizePrimary ? 300f : 280f;
            var minHeight = emphasizePrimary ? 74f : 70f;
            buttonRect.sizeDelta = new Vector2(
                Mathf.Max(minWidth, buttonRect.sizeDelta.x),
                Mathf.Max(minHeight, buttonRect.sizeDelta.y));
        }


        private static void ApplyTmpLabelStyling(TMP_Text tmpLabel, string labelText, bool emphasizePrimary)
        {
            if (!string.IsNullOrWhiteSpace(labelText)) tmpLabel.text = labelText.ToUpperInvariant();

            tmpLabel.color = CharacterCreatorStyle.TextTint;
            tmpLabel.fontStyle = FontStyles.Bold;
            tmpLabel.characterSpacing = emphasizePrimary ? 2.6f : 2.0f;
            tmpLabel.fontSize = Mathf.Max(emphasizePrimary ? 36f : 34f, tmpLabel.fontSize);
            tmpLabel.alignment = TextAlignmentOptions.Center;
        }


        private static void ApplyLegacyTextFallbackStyling(Text legacyLabel, string labelText, bool emphasizePrimary)
        {
            if (!string.IsNullOrWhiteSpace(labelText)) legacyLabel.text = labelText.ToUpperInvariant();

            legacyLabel.color = CharacterCreatorStyle.TextTint;
            legacyLabel.fontStyle = FontStyle.Bold;
            legacyLabel.fontSize = Mathf.Max(emphasizePrimary ? 30 : 26, legacyLabel.fontSize);
            legacyLabel.alignment = TextAnchor.MiddleCenter;
        }


        private static TMP_Text FindOrCreateText(RectTransform parent, string objectName, float fontSize,
            FontStyles fontStyle)
        {
            var existing = parent.Find(objectName);
            var text = existing != null ? existing.GetComponent<TMP_Text>() : null;

            if (text == null)
            {
                var textObject = new GameObject(objectName);
                textObject.transform.SetParent(parent, false);
                text = textObject.AddComponent<TextMeshProUGUI>();
            }

            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            return text;
        }


        private static RectTransform FindOrCreateRectChild(RectTransform parent, string objectName)
        {
            var existing = parent.Find(objectName);

            if (existing is RectTransform existingRect) return existingRect;

            var child = new GameObject(objectName, typeof(RectTransform));
            var childRect = child.GetComponent<RectTransform>();
            childRect.SetParent(parent, false);
            return childRect;
        }


        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component ?? target.AddComponent<T>();
        }


        private static Sprite GetSolidSprite()
        {
            if (_runtimeSolidSprite != null) return _runtimeSolidSprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            texture.hideFlags = HideFlags.HideAndDontSave;

            _runtimeSolidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _runtimeSolidSprite.hideFlags = HideFlags.HideAndDontSave;
            return _runtimeSolidSprite;
        }
    }
}
