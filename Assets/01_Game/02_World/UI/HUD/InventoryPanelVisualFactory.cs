using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI
{
    internal static class InventoryPanelVisualFactory
    {
        public static void BuildContextMenu(
            InventoryPanelController owner,
            Transform parent,
            out RectTransform root,
            out TMP_Text title,
            out Button equipButton,
            out TMP_Text equipButtonLabel,
            out Button dropButton)
        {
            _ = owner;

            var rootGo = new GameObject("ItemContextMenu", typeof(RectTransform), typeof(Image));
            rootGo.transform.SetParent(parent, false);

            root = rootGo.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(196f, 118f);

            var background = rootGo.GetComponent<Image>();
            background.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

            var outline = rootGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.26f, 0.30f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            title = CreateContextLabel(root, "Title", new Vector2(0f, 0.66f), new Vector2(1f, 1f), 14f,
                FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(0.90f, 0.86f, 0.74f, 1f);
            title.text = "Item";

            equipButton = CreateContextButton(root, "EquipButton", "Equip", new Vector2(0.08f, 0.34f),
                new Vector2(0.92f, 0.60f));
            equipButtonLabel = equipButton != null
                ? equipButton.GetComponentInChildren<TMP_Text>(true)
                : null;

            dropButton = CreateContextButton(root, "DropButton", "Drop", new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.30f));
        }

        public static void BuildDragGhost(
            Transform parent,
            out RectTransform root,
            out Image icon,
            out TMP_Text initial)
        {
            var rootGo = new GameObject("InventoryDragGhost", typeof(RectTransform), typeof(Image),
                typeof(CanvasGroup));
            rootGo.transform.SetParent(parent, false);

            root = rootGo.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(78f, 78f);

            var background = rootGo.GetComponent<Image>();
            background.color = new Color(0.08f, 0.08f, 0.10f, 0.78f);
            background.raycastTarget = false;

            var canvasGroup = rootGo.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(rootGo.transform, false);

            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(4f, 4f);
            iconRect.offsetMax = new Vector2(-4f, -4f);

            icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var initialGo = new GameObject("Initial", typeof(RectTransform), typeof(TextMeshProUGUI));
            initialGo.transform.SetParent(rootGo.transform, false);

            var initialRect = initialGo.GetComponent<RectTransform>();
            initialRect.anchorMin = Vector2.zero;
            initialRect.anchorMax = Vector2.one;
            initialRect.offsetMin = Vector2.zero;
            initialRect.offsetMax = Vector2.zero;

            initial = initialGo.GetComponent<TMP_Text>();
            initial.font = TMP_Settings.defaultFontAsset;
            initial.fontSize = 24f;
            initial.fontStyle = FontStyles.Bold;
            initial.alignment = TextAlignmentOptions.Center;
            initial.color = new Color(0.93f, 0.88f, 0.76f, 1f);
            initial.raycastTarget = false;
        }

        private static TMP_Text CreateContextLabel(Transform parent, string objectName, Vector2 anchorMin,
            Vector2 anchorMax, float fontSize, FontStyles fontStyle)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);

            var label = go.GetComponent<TMP_Text>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateContextButton(Transform parent, string objectName, string text, Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);

            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = anchorMin;
            buttonRect.anchorMax = anchorMax;
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            var buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = new Color(0.16f, 0.19f, 0.23f, 1f);

            var button = buttonGo.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.94f, 0.88f, 1f);
            colors.pressedColor = new Color(0.72f, 0.84f, 0.77f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.52f, 0.52f, 0.52f, 0.75f);
            button.colors = colors;

            var label = CreateContextLabel(buttonGo.transform, "Label", Vector2.zero, Vector2.one, 13f,
                FontStyles.Bold);
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.90f, 0.90f, 0.90f, 1f);

            return button;
        }
    }
}
