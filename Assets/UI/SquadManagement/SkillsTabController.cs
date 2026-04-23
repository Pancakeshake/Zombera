using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        [Serializable]
        public struct SkillEntryData
        {
            public string SkillName;
            public string Category;
            public SkillState State;
            public bool IsPassive;
            public int Rank;
            public string Description;
            public Sprite Icon;

            public SkillEntryData(
                string skillName,
                string category,
                SkillState state,
                bool isPassive,
                int rank,
                string description,
                Sprite icon = null)
            {
                SkillName = skillName;
                Category = category;
                State = state;
                IsPassive = isPassive;
                Rank = Mathf.Max(0, rank);
                Description = description;
                Icon = icon;
            }
        }

        private sealed class SkillView
        {
            public int SkillIndex;
            public RectTransform Root;
            public Button Button;
            public Image Background;
            public Image Icon;
            public TMP_Text IconInitial;
            public TMP_Text Name;
            public TMP_Text State;
            public TMP_Text Rank;
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

        private readonly List<SkillEntryData> skills = new List<SkillEntryData>();
        private readonly List<SkillView> skillViews = new List<SkillView>();

        private RectTransform hostRoot;
        private RectTransform listContent;
        private TMP_FontAsset fontAsset;
        private Sprite panelSprite;
        private Sprite slotSprite;

        private TMP_Text contextText;
        private TMP_Text detailText;
        private int selectedSkillIndex = -1;

        public void Build(RectTransform host, TMP_FontAsset font, Sprite panelBackground, Sprite slotBackground)
        {
            hostRoot = host;
            fontAsset = font;
            panelSprite = panelBackground;
            slotSprite = slotBackground;

            ClearChildren(hostRoot);

            RectTransform header = CreateRect("Header", hostRoot);
            Stretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -6f), new Vector2(-6f, -72f));
            AddImage(header, new Color(0.20f, 0.20f, 0.18f, 0.98f), panelSprite).type = Image.Type.Sliced;

            TMP_Text title = CreateText(header, "SKILLS", 24f, new Color(0.95f, 0.91f, 0.78f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.30f, 1f), new Vector2(10f, 0f), Vector2.zero);

            contextText = CreateText(header, "Operator: -", 14f, new Color(0.75f, 0.72f, 0.63f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(contextText.rectTransform, new Vector2(0.32f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-10f, 0f));

            RectTransform listFrame = CreateRect("SkillListFrame", hostRoot);
            Stretch(listFrame, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 84f), new Vector2(-6f, -78f));
            AddImage(listFrame, new Color(0.14f, 0.14f, 0.13f, 0.98f), panelSprite).type = Image.Type.Sliced;

            BuildSkillList(listFrame);

            RectTransform detailFrame = CreateRect("DetailFrame", hostRoot);
            Stretch(detailFrame, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 6f), new Vector2(-6f, 80f));
            AddImage(detailFrame, new Color(0.18f, 0.17f, 0.15f, 0.98f), panelSprite).type = Image.Type.Sliced;

            detailText = CreateText(detailFrame, "Select a skill to inspect requirements and effects.", 14f, new Color(0.83f, 0.79f, 0.69f, 1f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
            detailText.textWrappingMode = TextWrappingModes.Normal;
            Stretch(detailText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 8f), new Vector2(-10f, -8f));
        }

        public void SetSkills(IReadOnlyList<SkillEntryData> source)
        {
            skills.Clear();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    skills.Add(source[i]);
                }
            }

            RebuildSkillViews();
            selectedSkillIndex = -1;
            UpdateDetailText();
        }

        public void SetContextSurvivor(string displayName)
        {
            if (contextText != null)
            {
                contextText.text = "Operator: " + (string.IsNullOrWhiteSpace(displayName) ? "-" : displayName);
            }
        }

        private void BuildSkillList(RectTransform parent)
        {
            RectTransform scrollRoot = CreateRect("ScrollView", parent);
            Stretch(scrollRoot, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            AddImage(scrollRoot, new Color(0.11f, 0.11f, 0.10f, 1f), null);

            ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            RectTransform viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Image viewportImage = AddImage(viewport, new Color(0f, 0f, 0f, 0.07f), null);
            viewportImage.maskable = true;
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            listContent = CreateRect("Content", viewport);
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.offsetMin = Vector2.zero;
            listContent.offsetMax = Vector2.zero;

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

        private void RebuildSkillViews()
        {
            skillViews.Clear();
            if (listContent == null)
            {
                return;
            }

            ClearChildren(listContent);

            List<string> categories = BuildCategoryOrder();
            for (int c = 0; c < categories.Count; c++)
            {
                string category = categories[c];
                BuildCategoryHeader(listContent, category);

                for (int i = 0; i < skills.Count; i++)
                {
                    if (!string.Equals(skills[i].Category, category, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int capturedSkillIndex = i;
                    SkillView view = BuildSkillView(listContent, capturedSkillIndex, skills[i]);
                    view.Button.onClick.AddListener(() => SelectSkill(capturedSkillIndex));

                    HoverRelay relay = view.Root.gameObject.AddComponent<HoverRelay>();
                    relay.HoverChanged += hovered =>
                    {
                        view.IsHovered = hovered;
                        ApplySkillVisual(view);
                    };

                    skillViews.Add(view);
                }
            }

            for (int i = 0; i < skillViews.Count; i++)
            {
                ApplySkillVisual(skillViews[i]);
            }
        }

        private List<string> BuildCategoryOrder()
        {
            List<string> order = new List<string>();
            for (int i = 0; i < skills.Count; i++)
            {
                string category = string.IsNullOrWhiteSpace(skills[i].Category)
                    ? "Unsorted"
                    : skills[i].Category.Trim();

                bool exists = false;
                for (int j = 0; j < order.Count; j++)
                {
                    if (string.Equals(order[j], category, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    order.Add(category);
                }
            }

            return order;
        }

        private void BuildCategoryHeader(RectTransform parent, string category)
        {
            RectTransform header = CreateRect(category + "Header", parent);
            LayoutElement element = header.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 34f;
            AddImage(header, new Color(0.18f, 0.18f, 0.16f, 0.98f), panelSprite).type = Image.Type.Sliced;

            TMP_Text text = CreateText(header, category.ToUpperInvariant(), 14f, new Color(0.89f, 0.81f, 0.60f, 1f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
        }

        private SkillView BuildSkillView(RectTransform parent, int skillIndex, SkillEntryData data)
        {
            SkillView view = new SkillView();
            view.SkillIndex = skillIndex;
            view.Root = CreateRect("Skill_" + data.SkillName.Replace(" ", string.Empty), parent);
            LayoutElement element = view.Root.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 90f;

            view.Background = AddImage(view.Root, new Color(0.20f, 0.19f, 0.17f, 0.98f), slotSprite);
            view.Background.type = Image.Type.Sliced;

            view.Button = view.Root.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = view.Background;
            view.Button.transition = Selectable.Transition.None;

            RectTransform iconFrame = CreateRect("IconFrame", view.Root);
            Stretch(iconFrame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 8f), new Vector2(76f, -8f));
            Image iconFrameImage = AddImage(iconFrame, new Color(0.13f, 0.13f, 0.12f, 1f), panelSprite);
            iconFrameImage.type = Image.Type.Sliced;
            iconFrameImage.raycastTarget = false;

            RectTransform iconRect = CreateRect("Icon", iconFrame);
            Stretch(iconRect, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            view.Icon = AddImage(iconRect, data.Icon != null ? Color.white : new Color(0.24f, 0.27f, 0.24f, 1f), data.Icon);
            view.Icon.raycastTarget = false;

            view.IconInitial = CreateText(iconRect, GetInitial(data.SkillName), 20f, new Color(0.86f, 0.81f, 0.70f, 0.9f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(view.IconInitial.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            view.IconInitial.gameObject.SetActive(data.Icon == null);

            RectTransform info = CreateRect("Info", view.Root);
            Stretch(info, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(82f, 8f), new Vector2(-8f, -8f));

            view.Name = CreateText(info, data.SkillName, 19f, new Color(0.92f, 0.88f, 0.76f, 1f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(view.Name.rectTransform, new Vector2(0f, 0.55f), new Vector2(0.70f, 1f), Vector2.zero, Vector2.zero);

            RectTransform stateChip = CreateRect("StateChip", info);
            Stretch(stateChip, new Vector2(0.72f, 0.58f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            Image stateChipImage = AddImage(stateChip, new Color(0.29f, 0.23f, 0.16f, 1f), panelSprite);
            stateChipImage.type = Image.Type.Sliced;
            stateChipImage.raycastTarget = false;

            view.State = CreateText(stateChip, BuildStateLabel(data), 11f, GetStateColor(data), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(view.State.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            view.Rank = CreateText(info, BuildRankText(data.Rank), 13f, new Color(0.82f, 0.78f, 0.67f, 1f), FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            Stretch(view.Rank.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.50f), Vector2.zero, Vector2.zero);

            return view;
        }

        private void SelectSkill(int skillIndex)
        {
            selectedSkillIndex = Mathf.Clamp(skillIndex, 0, Mathf.Max(0, skills.Count - 1));
            for (int i = 0; i < skillViews.Count; i++)
            {
                ApplySkillVisual(skillViews[i]);
            }

            UpdateDetailText();
        }

        private void ApplySkillVisual(SkillView view)
        {
            if (view == null || view.SkillIndex < 0 || view.SkillIndex >= skills.Count)
            {
                return;
            }

            SkillEntryData data = skills[view.SkillIndex];
            bool selected = view.SkillIndex == selectedSkillIndex;

            Color baseColor = new Color(0.21f, 0.20f, 0.18f, 0.98f);
            if (view.IsHovered)
            {
                baseColor = Color.Lerp(baseColor, new Color(0.69f, 0.53f, 0.30f, 1f), 0.34f);
            }

            if (selected)
            {
                baseColor = new Color(0.48f, 0.35f, 0.18f, 1f);
            }

            if (data.State == SkillState.Locked)
            {
                baseColor = Color.Lerp(baseColor, new Color(0.19f, 0.16f, 0.16f, 1f), 0.38f);
            }

            if (view.Background != null)
            {
                view.Background.color = baseColor;
            }

            if (view.Name != null)
            {
                view.Name.color = data.State == SkillState.Locked
                    ? new Color(0.62f, 0.59f, 0.54f, 1f)
                    : selected
                        ? new Color(1f, 0.94f, 0.80f, 1f)
                        : new Color(0.90f, 0.86f, 0.75f, 1f);
            }

            if (view.State != null)
            {
                view.State.color = GetStateColor(data);
                view.State.text = BuildStateLabel(data);
            }

            if (view.Rank != null)
            {
                view.Rank.text = BuildRankText(data.Rank);
            }
        }

        private void UpdateDetailText()
        {
            if (detailText == null)
            {
                return;
            }

            if (selectedSkillIndex < 0 || selectedSkillIndex >= skills.Count)
            {
                detailText.text = "Select a skill to inspect requirements and effects.";
                return;
            }

            SkillEntryData data = skills[selectedSkillIndex];
            string description = string.IsNullOrWhiteSpace(data.Description)
                ? "No tactical notes available."
                : data.Description.Trim();

            detailText.text = data.SkillName
                + "\nState: " + BuildStateLabel(data)
                + " | Rank: " + data.Rank
                + "\n" + description;
        }

        private static string BuildStateLabel(SkillEntryData data)
        {
            if (data.State == SkillState.Locked)
            {
                return "LOCKED";
            }

            if (data.State == SkillState.Active)
            {
                return "ACTIVE";
            }

            if (data.State == SkillState.Passive || data.IsPassive)
            {
                return "PASSIVE";
            }

            return "UNLOCKED";
        }

        private static Color GetStateColor(SkillEntryData data)
        {
            switch (data.State)
            {
                case SkillState.Locked:
                    return new Color(0.76f, 0.42f, 0.36f, 1f);
                case SkillState.Active:
                    return new Color(0.77f, 0.71f, 0.38f, 1f);
                case SkillState.Passive:
                    return new Color(0.63f, 0.78f, 0.50f, 1f);
                case SkillState.Unlocked:
                    return data.IsPassive
                        ? new Color(0.63f, 0.78f, 0.50f, 1f)
                        : new Color(0.76f, 0.72f, 0.58f, 1f);
                default:
                    return new Color(0.72f, 0.70f, 0.62f, 1f);
            }
        }

        private static string BuildRankText(int rank)
        {
            if (rank <= 0)
            {
                return "Rank: -";
            }

            string pips = string.Empty;
            int pipCount = Mathf.Clamp(rank, 1, 5);
            for (int i = 0; i < pipCount; i++)
            {
                pips += "#";
            }

            return "Rank: " + pips;
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
