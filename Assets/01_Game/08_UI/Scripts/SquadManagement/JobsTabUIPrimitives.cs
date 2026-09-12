using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI.SquadManagement
{
    /// <summary>
    ///     Reusable UI primitives for squad management tab controllers.
    ///     Extracted from <see cref="JobsTabController"/>.
    /// </summary>
    internal static class JobsTabUIPrimitives
    {
        public static void ClearChildren(RectTransform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
                Object.Destroy(root.GetChild(i).gameObject);
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static Image AddImage(RectTransform rect, Color color, Sprite sprite)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        public static TMP_Text CreateText(
            RectTransform parent,
            string text,
            float size,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment,
            TMP_FontAsset font)
        {
            var rect = CreateRect("Text", parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            if (font != null) tmp.font = font;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static RectTransform CreatePanelColumn(RectTransform parent, float width, Color color, Sprite panelSprite, bool flexibleWidth = false)
        {
            var column = CreateRect("Column", parent);
            var layout = column.gameObject.AddComponent<LayoutElement>();
            if (flexibleWidth)
                layout.flexibleWidth = 1f;
            else
            {
                layout.minWidth = width;
                layout.preferredWidth = width;
                layout.flexibleWidth = 0f;
            }

            AddImage(column, color, panelSprite).type = Image.Type.Sliced;
            return column;
        }

        public static void CreateActionButton(RectTransform parent, string label, bool interactive, Color color, Sprite panelSprite, System.Action onClick)
        {
            var image = AddImage(parent, color, panelSprite);
            image.type = Image.Type.Sliced;

            if (interactive && onClick != null)
            {
                var button = parent.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => onClick());
            }

            var text = CreateText(parent, label, 13f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center, null);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.raycastTarget = false;
        }

        public static RectTransform BuildVerticalScroll(
            RectTransform parent,
            Vector2 offsetMin,
            Vector2 offsetMax,
            out RectTransform content)
        {
            var scrollRoot = CreateRect("ScrollView", parent);
            Stretch(scrollRoot, Vector2.zero, Vector2.one, offsetMin, offsetMax);
            AddImage(scrollRoot, new Color(0.11f, 0.11f, 0.10f, 0.5f), null);

            var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            var viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var viewportImage = AddImage(viewport, Color.clear, null);
            viewportImage.maskable = true;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return scrollRoot;
        }
    }
}
