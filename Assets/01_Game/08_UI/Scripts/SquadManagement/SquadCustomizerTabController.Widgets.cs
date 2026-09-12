#region

using TMPro;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed partial class SquadCustomizerTabController
    {
        private TMP_InputField CreateInputField(RectTransform parent, string defaultValue)
        {
            AddImage(parent, new Color(0.11f, 0.11f, 0.10f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            var input = parent.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = parent;

            var textRect = CreateRect("Text", parent);
            Stretch(textRect, Vector2.zero, Vector2.one, new Vector2(8f, 6f), new Vector2(-8f, -6f));

            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _fontAsset;
            text.fontSize = 14f;
            text.color = new Color(0.94f, 0.89f, 0.77f, 1f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            var placeholderRect = CreateRect("Placeholder", parent);
            Stretch(placeholderRect, Vector2.zero, Vector2.one, new Vector2(8f, 6f), new Vector2(-8f, -6f));

            var placeholder = placeholderRect.gameObject.AddComponent<TextMeshProUGUI>();
            placeholder.font = _fontAsset;
            placeholder.fontSize = 14f;
            placeholder.color = new Color(0.58f, 0.56f, 0.50f, 1f);
            placeholder.text = "Enter squad name";
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;

            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = defaultValue;
            input.caretColor = new Color(0.96f, 0.90f, 0.74f, 1f);
            input.selectionColor = new Color(0.46f, 0.34f, 0.18f, 0.5f);

            return input;
        }

        private Button CreateButton(RectTransform rect, string label, float fontSize)
        {
            var image = AddImage(rect, new Color(0.24f, 0.21f, 0.17f, 1f), _slotSprite);
            image.type = Image.Type.Sliced;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.89f, 0.81f, 0.60f, 1f);
            colors.pressedColor = new Color(0.76f, 0.59f, 0.31f, 1f);
            colors.selectedColor = new Color(0.81f, 0.62f, 0.33f, 1f);
            colors.disabledColor = new Color(0.46f, 0.43f, 0.38f, 0.7f);
            button.colors = colors;

            var text = CreateText(rect, label, fontSize, new Color(0.92f, 0.87f, 0.74f, 1f), FontStyles.Bold,
                TextAlignmentOptions.Center);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return button;
        }

        private TMP_Text CreateText(
            RectTransform parent,
            string value,
            float size,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            var rect = CreateRect("Text", parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = _fontAsset;
            tmp.text = value;
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

        private static Image AddImage(RectTransform rect, Color color, Sprite sprite)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            return image;
        }

        private static void Stretch(
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

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null) return;

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
    }
}
