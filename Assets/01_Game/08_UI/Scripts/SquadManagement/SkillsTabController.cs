#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed class SkillsTabController : MonoBehaviour
    {
        public enum SkillState
        {
            Locked,
            Unlocked,
            Passive,
            Active
        }

        private readonly List<SkillEntryData> _skills = new();
        private readonly List<SkillView> _skillViews = new();

        private TMP_Text _contextText;
        private TMP_Text _detailText;
        private TMP_FontAsset _fontAsset;

        private RectTransform _hostRoot;
        private RectTransform _listContent;
        private Sprite _panelSprite;
        private int _selectedSkillIndex = -1;
        private Sprite _slotSprite;

        public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground, Sprite slotBackground)
        {
            _hostRoot = host;
            _fontAsset = font;
            _panelSprite = panelBackground;
            _slotSprite = slotBackground;

            ClearChildren(_hostRoot);

            var header = CreateRect("Header", _hostRoot);
            Stretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -6f), new Vector2(-6f, -72f));
            AddImage(header, new Color(0.20f, 0.20f, 0.18f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            var title = CreateText(header, "SKILLS", 24f, new Color(0.95f, 0.91f, 0.78f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.30f, 1f), new Vector2(10f, 0f),
                Vector2.zero);

            _contextText = CreateText(header, "Operator: -", 14f, new Color(0.75f, 0.72f, 0.63f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            Stretch(_contextText.rectTransform, new Vector2(0.32f, 0f), new Vector2(1f, 1f), Vector2.zero,
                new Vector2(-10f, 0f));

            var listFrame = CreateRect("SkillListFrame", _hostRoot);
            Stretch(listFrame, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 84f), new Vector2(-6f, -78f));
            AddImage(listFrame, new Color(0.14f, 0.14f, 0.13f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            BuildSkillList(listFrame);

            var detailFrame = CreateRect("DetailFrame", _hostRoot);
            Stretch(detailFrame, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 6f), new Vector2(-6f, 80f));
            AddImage(detailFrame, new Color(0.18f, 0.17f, 0.15f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            _detailText = CreateText(detailFrame, "Select a skill to inspect requirements and effects.", 14f,
                new Color(0.83f, 0.79f, 0.69f, 1f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
            _detailText.textWrappingMode = TextWrappingModes.Normal;
            Stretch(_detailText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 8f), new Vector2(-10f, -8f));
        }

        public void SetSkills(IReadOnlyList<SkillEntryData> source)
        {
            _skills.Clear();
            if (source != null)
                foreach (var entry in source)
                    _skills.Add(entry);

            RebuildSkillViews();
            _selectedSkillIndex = -1;
            UpdateDetailText();
        }

        public void SetContextSurvivor(string displayName)
        {
            if (_contextText != null)
                _contextText.text = "Operator: " + (string.IsNullOrWhiteSpace(displayName) ? "-" : displayName);
        }

        private void BuildSkillList(RectTransform parent)
        {
            var scrollRoot = CreateRect("ScrollView", parent);
            Stretch(scrollRoot, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            AddImage(scrollRoot, new Color(0.11f, 0.11f, 0.10f, 1f), null);

            var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            var viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var viewportImage = AddImage(viewport, new Color(0f, 0f, 0f, 0.07f), null);
            viewportImage.maskable = true;
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _listContent = CreateRect("Content", viewport);
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.offsetMin = Vector2.zero;
            _listContent.offsetMax = Vector2.zero;

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

        private void RebuildSkillViews()
        {
            _skillViews.Clear();
            if (_listContent == null) return;

            ClearChildren(_listContent);

            var categories = BuildCategoryOrder();
            foreach (var category in categories)
            {
                BuildCategoryHeader(_listContent, category);

                for (var i = 0; i < _skills.Count; i++)
                {
                    if (!string.Equals(_skills[i].category, category, StringComparison.OrdinalIgnoreCase)) continue;

                    var capturedSkillIndex = i;
                    var view = BuildSkillView(_listContent, capturedSkillIndex, _skills[i]);
                    view.Button.onClick.AddListener(() => SelectSkill(capturedSkillIndex));

                    var relay = view.Root.gameObject.AddComponent<HoverRelay>();
                    relay.HoverChanged += hovered =>
                    {
                        view.IsHovered = hovered;
                        ApplySkillVisual(view);
                    };

                    _skillViews.Add(view);
                }
            }

            foreach (var view in _skillViews) ApplySkillVisual(view);
        }

        private List<string> BuildCategoryOrder()
        {
            return _skills
                .Select(entry => string.IsNullOrWhiteSpace(entry.category)
                    ? "Unsorted"
                    : entry.category.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void BuildCategoryHeader(RectTransform parent, string category)
        {
            var header = CreateRect(category + "Header", parent);
            var element = header.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 34f;
            AddImage(header, new Color(0.18f, 0.18f, 0.16f, 0.98f), _panelSprite).type = Image.Type.Sliced;

            var text = CreateText(header, category.ToUpperInvariant(), 14f, new Color(0.89f, 0.81f, 0.60f, 1f),
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
        }

        private SkillView BuildSkillView(RectTransform parent, int skillIndex, SkillEntryData data)
        {
            var view = new SkillView
            {
                SkillIndex = skillIndex,
                Root = CreateRect("Skill_" + data.skillName.Replace(" ", string.Empty), parent)
            };
            var element = view.Root.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 90f;

            view.Background = AddImage(view.Root, new Color(0.20f, 0.19f, 0.17f, 0.98f), _slotSprite);
            view.Background.type = Image.Type.Sliced;

            view.Button = view.Root.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = view.Background;
            view.Button.transition = Selectable.Transition.None;

            var iconFrame = CreateRect("IconFrame", view.Root);
            Stretch(iconFrame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 8f), new Vector2(76f, -8f));
            var iconFrameImage = AddImage(iconFrame, new Color(0.13f, 0.13f, 0.12f, 1f), _panelSprite);
            iconFrameImage.type = Image.Type.Sliced;
            iconFrameImage.raycastTarget = false;

            var iconRect = CreateRect("Icon", iconFrame);
            Stretch(iconRect, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            view.Icon = AddImage(iconRect, data.icon != null ? Color.white : new Color(0.24f, 0.27f, 0.24f, 1f),
                data.icon);
            view.Icon.raycastTarget = false;

            view.IconInitial = CreateText(iconRect, GetInitial(data.skillName), 20f,
                new Color(0.86f, 0.81f, 0.70f, 0.9f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(view.IconInitial.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            view.IconInitial.gameObject.SetActive(data.icon == null);

            var info = CreateRect("Info", view.Root);
            Stretch(info, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(82f, 8f), new Vector2(-8f, -8f));

            view.Name = CreateText(info, data.skillName, 19f, new Color(0.92f, 0.88f, 0.76f, 1f), FontStyles.Bold,
                TextAlignmentOptions.TopLeft);
            Stretch(view.Name.rectTransform, new Vector2(0f, 0.55f), new Vector2(0.70f, 1f), Vector2.zero,
                Vector2.zero);

            var stateChip = CreateRect("StateChip", info);
            Stretch(stateChip, new Vector2(0.72f, 0.58f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var stateChipImage = AddImage(stateChip, new Color(0.29f, 0.23f, 0.16f, 1f), _panelSprite);
            stateChipImage.type = Image.Type.Sliced;
            stateChipImage.raycastTarget = false;

            view.State = CreateText(stateChip, BuildStateLabel(data), 11f, GetStateColor(data), FontStyles.Bold,
                TextAlignmentOptions.Center);
            Stretch(view.State.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            view.Rank = CreateText(info, BuildRankText(data.rank), 13f, new Color(0.82f, 0.78f, 0.67f, 1f),
                FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            Stretch(view.Rank.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.50f), Vector2.zero, Vector2.zero);

            return view;
        }

        private void SelectSkill(int skillIndex)
        {
            _selectedSkillIndex = Mathf.Clamp(skillIndex, 0, Mathf.Max(0, _skills.Count - 1));
            foreach (var view in _skillViews) ApplySkillVisual(view);

            UpdateDetailText();
        }

        private void ApplySkillVisual(SkillView view)
        {
            if (view == null || view.SkillIndex < 0 || view.SkillIndex >= _skills.Count) return;

            var data = _skills[view.SkillIndex];
            var selected = view.SkillIndex == _selectedSkillIndex;

            var baseColor = new Color(0.21f, 0.20f, 0.18f, 0.98f);
            if (view.IsHovered) baseColor = Color.Lerp(baseColor, new Color(0.69f, 0.53f, 0.30f, 1f), 0.34f);

            if (selected) baseColor = new Color(0.48f, 0.35f, 0.18f, 1f);

            if (data.state == SkillState.Locked)
                baseColor = Color.Lerp(baseColor, new Color(0.19f, 0.16f, 0.16f, 1f), 0.38f);

            if (view.Background != null) view.Background.color = baseColor;

            if (view.Name != null)
            {
                view.Name.color = data.state switch
                {
                    SkillState.Locked => new Color(0.62f, 0.59f, 0.54f, 1f),
                    _ when selected => new Color(1f, 0.94f, 0.80f, 1f),
                    _ => new Color(0.90f, 0.86f, 0.75f, 1f)
                };
            }

            if (view.State != null)
            {
                view.State.color = GetStateColor(data);
                view.State.text = BuildStateLabel(data);
            }

            if (view.Rank != null) view.Rank.text = BuildRankText(data.rank);
        }

        private void UpdateDetailText()
        {
            if (_detailText == null) return;

            if (_selectedSkillIndex < 0 || _selectedSkillIndex >= _skills.Count)
            {
                _detailText.text = "Select a skill to inspect requirements and effects.";
                return;
            }

            var data = _skills[_selectedSkillIndex];
            var description = string.IsNullOrWhiteSpace(data.description)
                ? "No tactical notes available."
                : data.description.Trim();

            _detailText.text = data.skillName
                               + "\nState: " + BuildStateLabel(data)
                               + " | Rank: " + data.rank
                               + "\n" + description;
        }

        private static string BuildStateLabel(SkillEntryData data)
        {
            return data.state switch
            {
                SkillState.Locked => "LOCKED",
                SkillState.Active => "ACTIVE",
                SkillState.Passive => "PASSIVE",
                SkillState.Unlocked when data.isPassive => "PASSIVE",
                SkillState.Unlocked => "UNLOCKED",
                _ => "UNKNOWN"
            };
        }

        private static Color GetStateColor(SkillEntryData data)
        {
            return data.state switch
            {
                SkillState.Locked => new Color(0.76f, 0.42f, 0.36f, 1f),
                SkillState.Active => new Color(0.77f, 0.71f, 0.38f, 1f),
                SkillState.Passive => new Color(0.63f, 0.78f, 0.50f, 1f),
                SkillState.Unlocked when data.isPassive => new Color(0.63f, 0.78f, 0.50f, 1f),
                SkillState.Unlocked => new Color(0.76f, 0.72f, 0.58f, 1f),
                _ => new Color(0.72f, 0.70f, 0.62f, 1f)
            };
        }

        private static string BuildRankText(int rank)
        {
            if (rank <= 0) return "Rank: -";

            var pipCount = Mathf.Clamp(rank, 1, 5);
            return "Rank: " + new string('#', pipCount);
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
        public struct SkillEntryData
        {
            public string skillName;
            public string category;
            public SkillState state;
            public bool isPassive;
            public int rank;
            public string description;
            public Sprite icon;

            public SkillEntryData(
                string skillName,
                string category,
                SkillState state,
                bool isPassive,
                int rank,
                string description,
                Sprite icon = null)
            {
                this.skillName = skillName;
                this.category = category;
                this.state = state;
                this.isPassive = isPassive;
                this.rank = Mathf.Max(0, rank);
                this.description = description;
                this.icon = icon;
            }
        }

        private sealed class SkillView
        {
            public Image Background;
            public Button Button;
            public Image Icon;
            public TMP_Text IconInitial;
            public bool IsHovered;
            public TMP_Text Name;
            public TMP_Text Rank;
            public RectTransform Root;
            public int SkillIndex;
            public TMP_Text State;
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