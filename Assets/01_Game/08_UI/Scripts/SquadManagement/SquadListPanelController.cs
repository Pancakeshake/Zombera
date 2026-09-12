#region

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#endregion

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

        private readonly List<SurvivorEntryData> _entries = new();
        private readonly List<EntryView> _entryViews = new();
        private Sprite _entrySprite;
        private TMP_FontAsset _fontAsset;

        private RectTransform _hostRoot;
        private RectTransform _listContent;
        private Sprite _panelSprite;
        private int _selectedIndex = -1;

        public event Action<int, SurvivorEntryData> SelectionChanged;

        public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground, Sprite entryBackground)
        {
            _hostRoot = host;
            _fontAsset = font;
            _panelSprite = panelBackground;
            _entrySprite = entryBackground;

            ClearChildren(_hostRoot);

            var titleBar = CreateRect("TitleBar", _hostRoot);
            Stretch(titleBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -6f), new Vector2(-6f, -56f));
            AddImage(titleBar, new Color(0.19f, 0.19f, 0.17f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            var title = CreateText(titleBar, "SURVIVORS", 22f, new Color(0.94f, 0.90f, 0.76f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 0f),
                new Vector2(-10f, 0f));

            var subtitleBand = CreateRect("SubtitleBand", _hostRoot);
            Stretch(subtitleBand, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -60f),
                new Vector2(-6f, -94f));
            AddImage(subtitleBand, new Color(0.15f, 0.16f, 0.15f, 0.95f), _panelSprite).type = Image.Type.Sliced;

            var subtitle = CreateText(subtitleBand, "Roster health and readiness overview", 13f,
                new Color(0.68f, 0.67f, 0.61f, 1f), FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            Stretch(subtitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 0f),
                new Vector2(-10f, 0f));

            var listFrame = CreateRect("ListFrame", _hostRoot);
            Stretch(listFrame, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(-6f, -98f));
            AddImage(listFrame, new Color(0.13f, 0.13f, 0.12f, 0.96f), _panelSprite).type = Image.Type.Sliced;

            BuildScrollList(listFrame);
        }

        public void SetEntries(IReadOnlyList<SurvivorEntryData> source)
        {
            _entries.Clear();
            if (source != null)
                foreach (var entry in source)
                    _entries.Add(entry);

            RebuildEntryViews();

            if (_entries.Count == 0)
            {
                _selectedIndex = -1;
                return;
            }

            if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
            {
                SelectIndex(0);
                return;
            }

            UpdateEntryVisuals();
            SelectionChanged?.Invoke(_selectedIndex, _entries[_selectedIndex]);
        }

        public void SelectIndex(int index)
        {
            if (_entries.Count == 0)
            {
                _selectedIndex = -1;
                return;
            }

            var clamped = Mathf.Clamp(index, 0, _entries.Count - 1);
            if (_selectedIndex == clamped)
            {
                UpdateEntryVisuals();
                return;
            }

            _selectedIndex = clamped;
            UpdateEntryVisuals();
            SelectionChanged?.Invoke(_selectedIndex, _entries[_selectedIndex]);
        }

        private void BuildScrollList(RectTransform parent)
        {
            var scrollRoot = CreateRect("ScrollView", parent);
            Stretch(scrollRoot, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            AddImage(scrollRoot, new Color(0.12f, 0.12f, 0.11f, 0.9f), null);

            var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            var viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var viewportImage = AddImage(viewport, new Color(0f, 0f, 0f, 0.08f), null);
            viewportImage.maskable = true;

            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _listContent = CreateRect("Content", viewport);
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.offsetMin = new Vector2(0f, 0f);
            _listContent.offsetMax = new Vector2(0f, 0f);

            var layout = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _listContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = _listContent;
        }

        private void RebuildEntryViews()
        {
            _entryViews.Clear();
            if (_listContent == null) return;

            ClearChildren(_listContent);

            for (var i = 0; i < _entries.Count; i++)
            {
                var capturedIndex = i;
                var view = BuildEntry(_listContent, _entries[i]);
                view.Button.onClick.AddListener(() => SelectIndex(capturedIndex));

                var hoverRelay = view.Root.gameObject.AddComponent<HoverRelay>();
                hoverRelay.HoverChanged += hovered =>
                {
                    view.IsHovered = hovered;
                    ApplyEntryVisual(capturedIndex, view);
                };

                _entryViews.Add(view);
            }

            UpdateEntryVisuals();
        }

        private EntryView BuildEntry(RectTransform parent, SurvivorEntryData data)
        {
            var view = new EntryView
            {
                Root = CreateRect("Entry_" + data.displayName.Replace(" ", string.Empty), parent)
            };
            var element = view.Root.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 110f;
            element.flexibleHeight = 0f;

            view.Background = AddImage(view.Root, new Color(0.18f, 0.18f, 0.17f, 0.97f), _entrySprite);
            view.Background.type = Image.Type.Sliced;

            view.Button = view.Root.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = view.Background;

            var portraitFrame = CreateRect("PortraitFrame", view.Root);
            Stretch(portraitFrame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 8f),
                new Vector2(86f, -8f));
            AddImage(portraitFrame, new Color(0.26f, 0.25f, 0.22f, 1f), _panelSprite).type = Image.Type.Sliced;

            var portrait = CreateRect("Portrait", portraitFrame);
            Stretch(portrait, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            AddImage(portrait, data.portrait != null ? Color.white : new Color(0.24f, 0.28f, 0.24f, 1f), data.portrait);

            view.PortraitInitial = CreateText(portrait, GetInitial(data.displayName), 27f,
                new Color(0.88f, 0.84f, 0.72f, 0.9f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(view.PortraitInitial.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            view.PortraitInitial.gameObject.SetActive(data.portrait == null);

            var details = CreateRect("Details", view.Root);
            Stretch(details, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(92f, 8f), new Vector2(-8f, -8f));

            view.Name = CreateText(details, data.displayName, 20f, new Color(0.93f, 0.90f, 0.78f, 1f), FontStyles.Bold,
                TextAlignmentOptions.TopLeft);
            Stretch(view.Name.rectTransform, new Vector2(0f, 0.62f), new Vector2(0.64f, 1f), Vector2.zero,
                Vector2.zero);

            var conditionPill = CreateRect("ConditionPill", details);
            Stretch(conditionPill, new Vector2(0.66f, 0.64f), new Vector2(1f, 1f), new Vector2(4f, 2f), Vector2.zero);
            AddImage(conditionPill, new Color(0.30f, 0.18f, 0.14f, 1f), _entrySprite).type = Image.Type.Sliced;

            view.Condition = CreateText(conditionPill, data.condition.ToString().ToUpperInvariant(), 12f,
                new Color(0.94f, 0.84f, 0.70f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(view.Condition.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var bars = CreateRect("Bars", details);
            Stretch(bars, new Vector2(0f, 0f), new Vector2(1f, 0.60f), Vector2.zero, Vector2.zero);

            BuildStatBar(bars, "HP", new Color(0.58f, 0.17f, 0.14f, 1f), data.health01, 0);
            BuildStatBar(bars, "STA", new Color(0.44f, 0.39f, 0.15f, 1f), data.stamina01, 1);

            return view;
        }

        private void BuildStatBar(
            RectTransform parent,
            string label,
            Color fillColor,
            float value,
            int rowIndex)
        {
            var row = CreateRect(label + "Row", parent);
            const float rowHeight = 0.31f;
            var top = 1f - rowIndex * 0.33f;
            var bottom = top - rowHeight;
            Stretch(row, new Vector2(0f, bottom), new Vector2(1f, top), Vector2.zero, Vector2.zero);

            var labelText = CreateText(row, label, 11f, new Color(0.69f, 0.66f, 0.58f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            Stretch(labelText.rectTransform, new Vector2(0f, 0f), new Vector2(0.16f, 1f), Vector2.zero, Vector2.zero);

            var trackRect = CreateRect(label + "Track", row);
            Stretch(trackRect, new Vector2(0.17f, 0.15f), new Vector2(0.84f, 0.85f), Vector2.zero, Vector2.zero);
            AddImage(trackRect, new Color(0.10f, 0.10f, 0.10f, 1f), _panelSprite).type = Image.Type.Sliced;

            var fillRect = CreateRect(label + "Fill", trackRect);
            Stretch(fillRect, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var fill = AddImage(fillRect, fillColor, null);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = Mathf.Clamp01(value);

            var valueText = CreateText(row, Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%", 11f,
                new Color(0.84f, 0.80f, 0.70f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            Stretch(valueText.rectTransform, new Vector2(0.86f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        }

        private void UpdateEntryVisuals()
        {
            for (var i = 0; i < _entryViews.Count; i++) ApplyEntryVisual(i, _entryViews[i]);
        }

        private void ApplyEntryVisual(int index, EntryView view)
        {
            if (index < 0 || index >= _entries.Count) return;

            var data = _entries[index];
            var selected = index == _selectedIndex;

            var baseColor = new Color(0.20f, 0.19f, 0.18f, 0.96f);
            var hoverColor = new Color(0.28f, 0.25f, 0.20f, 0.98f);
            var selectedColor = new Color(0.43f, 0.30f, 0.16f, 0.98f);

            if (view.Background != null)
            {
                if (selected)
                    view.Background.color = selectedColor;
                else if (view.IsHovered)
                    view.Background.color = hoverColor;
                else
                    view.Background.color = baseColor;
            }

            if (view.Name != null)
                view.Name.color = selected
                    ? new Color(1f, 0.94f, 0.79f, 1f)
                    : new Color(0.90f, 0.86f, 0.75f, 1f);

            if (view.Condition != null) view.Condition.color = GetConditionColor(data.condition);
        }

        private static Color GetConditionColor(SurvivorCondition condition)
        {
            return condition switch
            {
                SurvivorCondition.Stable => new Color(0.64f, 0.78f, 0.48f, 1f),
                SurvivorCondition.Wounded => new Color(0.84f, 0.70f, 0.33f, 1f),
                SurvivorCondition.Exhausted => new Color(0.90f, 0.56f, 0.28f, 1f),
                SurvivorCondition.Critical => new Color(0.93f, 0.33f, 0.29f, 1f),
                _ => new Color(0.70f, 0.70f, 0.70f, 1f)
            };
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

        private static string GetInitial(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "?"
                : value[..1].ToUpperInvariant();
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

        [Serializable]
        public struct SurvivorEntryData
        {
            public string id;
            public string displayName;
            public Sprite portrait;
            public float health01;
            public float stamina01;
            public SurvivorCondition condition;

            public SurvivorEntryData(
                string id,
                string displayName,
                Sprite portrait,
                float health01,
                float stamina01,
                SurvivorCondition condition)
            {
                this.id = id;
                this.displayName = displayName;
                this.portrait = portrait;
                this.health01 = Mathf.Clamp01(health01);
                this.stamina01 = Mathf.Clamp01(stamina01);
                this.condition = condition;
            }
        }

        private sealed class EntryView
        {
            public Image Background;
            public Button Button;
            public TMP_Text Condition;
            public bool IsHovered;
            public TMP_Text Name;
            public TMP_Text PortraitInitial;
            public RectTransform Root;
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
    }
}