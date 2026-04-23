using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zombera.UI.SquadManagement
{
    public sealed class SquadListPanelController : MonoBehaviour
    {
        public enum SurvivorCondition
        {
            Stable,
            Wounded,
            Exhausted,
            Critical
        }

        [Serializable]
        public struct SurvivorEntryData
        {
            public string Id;
            public string DisplayName;
            public Sprite Portrait;
            public float Health01;
            public float Stamina01;
            public SurvivorCondition Condition;

            public SurvivorEntryData(
                string id,
                string displayName,
                Sprite portrait,
                float health01,
                float stamina01,
                SurvivorCondition condition)
            {
                Id = id;
                DisplayName = displayName;
                Portrait = portrait;
                Health01 = Mathf.Clamp01(health01);
                Stamina01 = Mathf.Clamp01(stamina01);
                Condition = condition;
            }
        }

        private sealed class EntryView
        {
            public RectTransform Root;
            public Button Button;
            public Image Background;
            public Image Portrait;
            public TMP_Text PortraitInitial;
            public TMP_Text Name;
            public TMP_Text Condition;
            public Image HealthFill;
            public Image StaminaFill;
            public TMP_Text HealthValue;
            public TMP_Text StaminaValue;
            public bool IsHovered;
        }

        private sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action<bool> HoverChanged;

            public void OnPointerEnter(PointerEventData eventData)
            {
                HoverChanged?.Invoke(true);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                HoverChanged?.Invoke(false);
            }
        }

        public event Action<int, SurvivorEntryData> SelectionChanged;

        private readonly List<SurvivorEntryData> entries = new List<SurvivorEntryData>();
        private readonly List<EntryView> entryViews = new List<EntryView>();

        private RectTransform hostRoot;
        private RectTransform listContent;
        private TMP_FontAsset fontAsset;
        private Sprite panelSprite;
        private Sprite entrySprite;
        private int selectedIndex = -1;

        public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground, Sprite entryBackground)
        {
            hostRoot = host;
            fontAsset = font;
            panelSprite = panelBackground;
            entrySprite = entryBackground;

            ClearChildren(hostRoot);

            RectTransform titleBar = CreateRect("TitleBar", hostRoot);
            Stretch(titleBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -6f), new Vector2(-6f, -56f));
            AddImage(titleBar, new Color(0.19f, 0.19f, 0.17f, 0.98f), panelSprite).type = Image.Type.Sliced;

            TMP_Text title = CreateText(titleBar, "SURVIVORS", 22f, new Color(0.94f, 0.90f, 0.76f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-10f, 0f));

            RectTransform subtitleBand = CreateRect("SubtitleBand", hostRoot);
            Stretch(subtitleBand, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -60f), new Vector2(-6f, -94f));
            AddImage(subtitleBand, new Color(0.15f, 0.16f, 0.15f, 0.95f), panelSprite).type = Image.Type.Sliced;

            TMP_Text subtitle = CreateText(subtitleBand, "Roster health and readiness overview", 13f, new Color(0.68f, 0.67f, 0.61f, 1f), FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            Stretch(subtitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-10f, 0f));

            RectTransform listFrame = CreateRect("ListFrame", hostRoot);
            Stretch(listFrame, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(-6f, -98f));
            AddImage(listFrame, new Color(0.13f, 0.13f, 0.12f, 0.96f), panelSprite).type = Image.Type.Sliced;

            BuildScrollList(listFrame);
        }

        public void SetEntries(IReadOnlyList<SurvivorEntryData> source)
        {
            entries.Clear();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    entries.Add(source[i]);
                }
            }

            RebuildEntryViews();

            if (entries.Count == 0)
            {
                selectedIndex = -1;
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= entries.Count)
            {
                SelectIndex(0);
            }
            else
            {
                UpdateEntryVisuals();
                SelectionChanged?.Invoke(selectedIndex, entries[selectedIndex]);
            }
        }

        public void SelectIndex(int index)
        {
            if (entries.Count == 0)
            {
                selectedIndex = -1;
                return;
            }

            int clamped = Mathf.Clamp(index, 0, entries.Count - 1);
            if (selectedIndex == clamped)
            {
                UpdateEntryVisuals();
                return;
            }

            selectedIndex = clamped;
            UpdateEntryVisuals();
            SelectionChanged?.Invoke(selectedIndex, entries[selectedIndex]);
        }

        private void BuildScrollList(RectTransform parent)
        {
            RectTransform scrollRoot = CreateRect("ScrollView", parent);
            Stretch(scrollRoot, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            AddImage(scrollRoot, new Color(0.12f, 0.12f, 0.11f, 0.9f), null);

            ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            RectTransform viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Image viewportImage = AddImage(viewport, new Color(0f, 0f, 0f, 0.08f), null);
            viewportImage.maskable = true;

            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            listContent = CreateRect("Content", viewport);
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.offsetMin = new Vector2(0f, 0f);
            listContent.offsetMax = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = listContent;
        }

        private void RebuildEntryViews()
        {
            entryViews.Clear();
            if (listContent == null)
            {
                return;
            }

            ClearChildren(listContent);

            for (int i = 0; i < entries.Count; i++)
            {
                int capturedIndex = i;
                EntryView view = BuildEntry(listContent, entries[i]);
                view.Button.onClick.AddListener(() => SelectIndex(capturedIndex));

                HoverRelay hoverRelay = view.Root.gameObject.AddComponent<HoverRelay>();
                hoverRelay.HoverChanged += hovered =>
                {
                    view.IsHovered = hovered;
                    ApplyEntryVisual(capturedIndex, view);
                };

                entryViews.Add(view);
            }

            UpdateEntryVisuals();
        }

        private EntryView BuildEntry(RectTransform parent, SurvivorEntryData data)
        {
            EntryView view = new EntryView();

            view.Root = CreateRect("Entry_" + data.DisplayName.Replace(" ", string.Empty), parent);
            LayoutElement element = view.Root.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 110f;
            element.flexibleHeight = 0f;

            view.Background = AddImage(view.Root, new Color(0.18f, 0.18f, 0.17f, 0.97f), entrySprite);
            view.Background.type = Image.Type.Sliced;

            view.Button = view.Root.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = view.Background;

            RectTransform portraitFrame = CreateRect("PortraitFrame", view.Root);
            Stretch(portraitFrame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 8f), new Vector2(86f, -8f));
            AddImage(portraitFrame, new Color(0.26f, 0.25f, 0.22f, 1f), panelSprite).type = Image.Type.Sliced;

            RectTransform portrait = CreateRect("Portrait", portraitFrame);
            Stretch(portrait, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            view.Portrait = AddImage(portrait, data.Portrait != null ? Color.white : new Color(0.24f, 0.28f, 0.24f, 1f), data.Portrait);

            view.PortraitInitial = CreateText(portrait, GetInitial(data.DisplayName), 27f, new Color(0.88f, 0.84f, 0.72f, 0.9f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(view.PortraitInitial.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            view.PortraitInitial.gameObject.SetActive(data.Portrait == null);

            RectTransform details = CreateRect("Details", view.Root);
            Stretch(details, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(92f, 8f), new Vector2(-8f, -8f));

            view.Name = CreateText(details, data.DisplayName, 20f, new Color(0.93f, 0.90f, 0.78f, 1f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(view.Name.rectTransform, new Vector2(0f, 0.62f), new Vector2(0.64f, 1f), Vector2.zero, Vector2.zero);

            RectTransform conditionPill = CreateRect("ConditionPill", details);
            Stretch(conditionPill, new Vector2(0.66f, 0.64f), new Vector2(1f, 1f), new Vector2(4f, 2f), Vector2.zero);
            AddImage(conditionPill, new Color(0.30f, 0.18f, 0.14f, 1f), entrySprite).type = Image.Type.Sliced;

            view.Condition = CreateText(conditionPill, data.Condition.ToString().ToUpperInvariant(), 12f, new Color(0.94f, 0.84f, 0.70f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(view.Condition.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform bars = CreateRect("Bars", details);
            Stretch(bars, new Vector2(0f, 0f), new Vector2(1f, 0.60f), Vector2.zero, Vector2.zero);

            BuildStatBar(bars, "HP", new Color(0.58f, 0.17f, 0.14f, 1f), data.Health01, out view.HealthFill, out view.HealthValue, 0);
            BuildStatBar(bars, "STA", new Color(0.44f, 0.39f, 0.15f, 1f), data.Stamina01, out view.StaminaFill, out view.StaminaValue, 1);

            return view;
        }

        private void BuildStatBar(
            RectTransform parent,
            string label,
            Color fillColor,
            float value,
            out Image fill,
            out TMP_Text valueText,
            int rowIndex)
        {
            RectTransform row = CreateRect(label + "Row", parent);
            float rowHeight = 0.31f;
            float top = 1f - (rowIndex * 0.33f);
            float bottom = top - rowHeight;
            Stretch(row, new Vector2(0f, bottom), new Vector2(1f, top), Vector2.zero, Vector2.zero);

            TMP_Text labelText = CreateText(row, label, 11f, new Color(0.69f, 0.66f, 0.58f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(labelText.rectTransform, new Vector2(0f, 0f), new Vector2(0.16f, 1f), Vector2.zero, Vector2.zero);

            RectTransform trackRect = CreateRect(label + "Track", row);
            Stretch(trackRect, new Vector2(0.17f, 0.15f), new Vector2(0.84f, 0.85f), Vector2.zero, Vector2.zero);
            AddImage(trackRect, new Color(0.10f, 0.10f, 0.10f, 1f), panelSprite).type = Image.Type.Sliced;

            RectTransform fillRect = CreateRect(label + "Fill", trackRect);
            Stretch(fillRect, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            fill = AddImage(fillRect, fillColor, null);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = Mathf.Clamp01(value);

            valueText = CreateText(row, Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%", 11f, new Color(0.84f, 0.80f, 0.70f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            Stretch(valueText.rectTransform, new Vector2(0.86f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        }

        private void UpdateEntryVisuals()
        {
            for (int i = 0; i < entryViews.Count; i++)
            {
                ApplyEntryVisual(i, entryViews[i]);
            }
        }

        private void ApplyEntryVisual(int index, EntryView view)
        {
            if (index < 0 || index >= entries.Count)
            {
                return;
            }

            SurvivorEntryData data = entries[index];
            bool selected = index == selectedIndex;

            Color baseColor = new Color(0.20f, 0.19f, 0.18f, 0.96f);
            Color hoverColor = new Color(0.28f, 0.25f, 0.20f, 0.98f);
            Color selectedColor = new Color(0.43f, 0.30f, 0.16f, 0.98f);

            if (view.Background != null)
            {
                if (selected)
                {
                    view.Background.color = selectedColor;
                }
                else if (view.IsHovered)
                {
                    view.Background.color = hoverColor;
                }
                else
                {
                    view.Background.color = baseColor;
                }
            }

            if (view.Name != null)
            {
                view.Name.color = selected
                    ? new Color(1f, 0.94f, 0.79f, 1f)
                    : new Color(0.90f, 0.86f, 0.75f, 1f);
            }

            if (view.Condition != null)
            {
                view.Condition.color = GetConditionColor(data.Condition);
            }
        }

        private static Color GetConditionColor(SurvivorCondition condition)
        {
            switch (condition)
            {
                case SurvivorCondition.Stable:
                    return new Color(0.64f, 0.78f, 0.48f, 1f);
                case SurvivorCondition.Wounded:
                    return new Color(0.84f, 0.70f, 0.33f, 1f);
                case SurvivorCondition.Exhausted:
                    return new Color(0.90f, 0.56f, 0.28f, 1f);
                case SurvivorCondition.Critical:
                    return new Color(0.93f, 0.33f, 0.29f, 1f);
                default:
                    return new Color(0.70f, 0.70f, 0.70f, 1f);
            }
        }

        private TMP_Text CreateText(
            RectTransform parent,
            string value,
            float size,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect("Text", parent);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = fontAsset;
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
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Image AddImage(RectTransform rect, Color color, Sprite sprite)
        {
            Image image = rect.gameObject.AddComponent<Image>();
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

        private static string GetInitial(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "?";
            }

            return value.Substring(0, 1).ToUpperInvariant();
        }

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
